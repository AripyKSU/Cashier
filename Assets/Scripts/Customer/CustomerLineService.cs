using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>손님 입장 대사를 고를 때 필요한 진행 상황 snapshot.</summary>
public readonly struct CustomerLineContext
{
    /// <summary>1부터 시작하는 표시 일차.</summary>
    public readonly int Day;
    /// <summary>마지막 영업일 여부.</summary>
    public readonly bool IsFinalDay;
    /// <summary>현재 가게 단계(1~3).</summary>
    public readonly uint StoreStage;
    /// <summary>계산대 뒤에서 기다리는 손님 수.</summary>
    public readonly int WaitingCount;
    /// <summary>영업 시간 경과 비율. 0은 개점, 1은 마감.</summary>
    public readonly float BusinessProgress01;

    /// <summary>진행 상황 snapshot을 만든다.</summary>
    /// <param name="day">표시 일차.</param><param name="isFinalDay">마지막 영업일 여부.</param>
    /// <param name="storeStage">가게 단계.</param><param name="waitingCount">대기 손님 수.</param>
    /// <param name="businessProgress01">영업 시간 경과 비율.</param>
    public CustomerLineContext(int day, bool isFinalDay, uint storeStage, int waitingCount, float businessProgress01)
    {
        Day = day;
        IsFinalDay = isFinalDay;
        StoreStage = storeStage;
        WaitingCount = waitingCount;
        BusinessProgress01 = businessProgress01;
    }
}

/// <summary>
/// 손님 입장 대사를 Claude API로 만들고, 응답이 늦거나 실패하면 미리 만든 풀에서 같은 주제의 대사를 고른다.
/// 손님이 줄에 서는 순간 요청을 시작해 계산대 도착 시 대부분 응답이 준비되어 있게 한다.
/// API 키 파일 또는 ANTHROPIC_API_KEY 환경 변수가 없으면 API 요청 없이 풀 대사만 쓰는 풀 전용 모드로 동작한다.
/// </summary>
public sealed class CustomerLineService : IDisposable
{
    /// <summary>계산대 도착 후 API 응답을 기다리는 최대 시간(초). 넘으면 풀 대사를 쓴다.</summary>
    public const float MaxWaitSeconds = 2f;
    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2026-10-21";
    private const string Model = "claude-haiku-5-5";
    private const int MaxOutputTokens = 120;
    // 줄에서 오래 기다린 손님도 계산대 도착 전에 응답을 받을 수 있게 넉넉히 잡되, 무한 대기는 막는다.
    private const int RequestTimeoutSeconds = 10;
    private const int MaxLineLength = 60;
    private const int MaxPendingVisits = 32;
    // 이웃 아줌마 손님이 하루 안부 주제를 후보로 가질 확률. 다른 손님은 하루를 언급하지 않는다.
    private const double NeighborTopicChance = 0.35;
    private const string KeyFolderName = ".cashier";
    private const string KeyFileName = "anthropic_api_key.txt";
    // 세계관과 말투 규칙. 손님마다 바뀌는 정보는 사용자 메시지로 보낸다.
    private const string SystemPrompt =
        "너는 디스토피아 게임 'Cashier'의 손님 대사 작가다. 담장 밖 빈민 구역, 배급과 물이 부족하고 방사능과 검문이 일상인 세계다. " +
        "플레이어는 작은 가게를 하는 아빠이고, 딸 하루는 아파서 집에 누워 있다. 담장 안 병원에 가려면 시민권이 필요하다.\n" +
        "규칙:\n" +
        "- 손님이 계산대에서 하는 말 한 줄만 출력한다. 설명, 따옴표, 이름표, 이모지 금지.\n" +
        "- 35자 안팎, 길어도 두 문장. 담담하고 지친 말투. 밝거나 과장된 감탄은 쓰지 않는다.\n" +
        "- 어른은 존댓말. 아이는 가게 주인을 '아저씨'라 부르고 아이다운 짧은 말투.\n" +
        "- 가격, 할인, 흥정 이야기는 하지 않는다. 주어진 주제에 집중한다.\n" +
        "- 하루는 이웃 아줌마만 안부를 묻는다. 주제가 이웃 안부가 아니면 하루나 가게 주인의 가족을 절대 언급하지 않는다.";

    private readonly string apiKey;
    private readonly Func<uint, string> getProductName;
    private readonly System.Random random = new System.Random();
    // 방문별 진행 중인 요청과 미리 골라 둔 풀 대사. 계산대에서 꺼내 쓰면 제거한다.
    private readonly Dictionary<CustomerVisit, PendingLine> pending = new Dictionary<CustomerVisit, PendingLine>();
    // 같은 세션에서 이미 쓴 풀 대사. 풀이 바닥나기 전까지 반복을 피한다.
    private readonly HashSet<string> usedPoolLines = new HashSet<string>();
    private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

    /// <summary>API 키가 없어 풀 대사만 쓰는 모드인지 여부. 이때는 기다림 없이 바로 대사를 고른다.</summary>
    public bool IsPoolOnly => this.apiKey == null;

    /// <summary>API 키와 상품 이름 조회 함수로 서비스를 만든다.</summary>
    /// <param name="apiKey">Anthropic API 키. null이면 풀 전용 모드. 로그에 남기지 않는다.</param>
    /// <param name="getProductName">상품 PK를 표시 이름으로 바꾸는 함수.</param>
    private CustomerLineService(string apiKey, Func<uint, string> getProductName)
    {
        this.apiKey = apiKey;
        this.getProductName = getProductName;
    }

    /// <summary>
    /// 이 PC에 API 키가 설정되어 있으면 서비스를 만든다. 키 위치는 사용자 폴더의 .cashier/anthropic_api_key.txt
    /// 또는 ANTHROPIC_API_KEY 환경 변수이며, 저장소와 빌드에는 키를 넣지 않는다.
    /// </summary>
    /// <param name="getProductName">상품 PK를 표시 이름으로 바꾸는 함수.</param>
    /// <returns>키가 있으면 API 서비스, 없으면 풀 전용 서비스.</returns>
    public static CustomerLineService TryCreate(Func<uint, string> getProductName)
    {
        string key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), KeyFolderName, KeyFileName);
            if (File.Exists(path)) key = File.ReadAllText(path);
        }
        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.Log("[CustomerLine] API 키 없음, 미리 만든 풀 대사만 사용");
            return new CustomerLineService(null, getProductName);
        }
        Debug.Log($"[CustomerLine] Claude API 손님 대사 사용 ({Model}, 최대 대기 {MaxWaitSeconds}초)");
        return new CustomerLineService(key.Trim(), getProductName);
    }

    /// <summary>줄에 선 손님의 대사 요청을 미리 시작한다. 같은 방문에 여러 번 호출해도 한 번만 요청한다.</summary>
    /// <param name="visit">대사를 준비할 방문.</param>
    /// <param name="context">요청 시점의 진행 상황.</param>
    public void Prefetch(CustomerVisit visit, CustomerLineContext context)
    {
        if (visit == null || this.pending.ContainsKey(visit)) return;
        // 이탈한 손님의 요청이 쌓이지 않게 오래된 항목을 비운다.
        if (this.pending.Count >= MaxPendingVisits) this.pending.Clear();

        List<Topic> topics = this.collectTopics(visit, context);
        Topic topic = topics.Count > 0 ? topics[this.random.Next(topics.Count)] : null;
        string fallback = topic != null ? this.takePoolLine(topic.Lines) : null;
        UniTask<string> request = this.IsPoolOnly
            ? UniTask.FromResult<string>(null)
            : this.requestLineAsync(this.buildPrompt(visit, context, topic), this.lifetime.Token).Preserve();
        this.pending[visit] = new PendingLine(request, fallback, Time.realtimeSinceStartup);
    }

    /// <summary>미리 골라 둔 풀 대사를 기다림 없이 꺼낸다. 풀 전용 모드에서 쓴다.</summary>
    /// <param name="visit">계산대에 선 방문.</param>
    /// <param name="context">현재 진행 상황. 미리 고르지 않은 방문이면 이 값으로 고른다.</param>
    /// <returns>풀 대사, 맞는 주제가 없으면 null(기존 CSV 대사 사용).</returns>
    public string TakePoolEntryLine(CustomerVisit visit, CustomerLineContext context)
    {
        this.Prefetch(visit, context);
        string line = this.pending[visit].Fallback;
        this.pending.Remove(visit);
        return line;
    }

    /// <summary>계산대에 선 손님의 입장 대사를 최대 2초 기다려 반환한다.</summary>
    /// <param name="visit">계산대에 선 방문.</param>
    /// <param name="context">현재 진행 상황. 미리 요청하지 않은 방문이면 이 값으로 요청한다.</param>
    /// <returns>API 대사, 늦으면 풀 대사, 둘 다 없으면 null(기존 CSV 대사 사용).</returns>
    public async UniTask<string> GetEntryLineAsync(CustomerVisit visit, CustomerLineContext context)
    {
        if (this.IsPoolOnly) return this.TakePoolEntryLine(visit, context);
        this.Prefetch(visit, context);
        PendingLine line = this.pending[visit];
        this.pending.Remove(visit);

        float arrivedAt = Time.realtimeSinceStartup;
        (bool hasApiResult, string apiLine) = await UniTask.WhenAny(line.Request,
            UniTask.Delay(TimeSpan.FromSeconds(MaxWaitSeconds), DelayType.Realtime, cancellationToken: this.lifetime.Token));
        float waited = Time.realtimeSinceStartup - arrivedAt;
        if (hasApiResult && !string.IsNullOrEmpty(apiLine))
        {
            Debug.Log($"[CustomerLine] API 대사 (요청 후 {Time.realtimeSinceStartup - line.RequestedAt:0.00}초, 계산대 대기 {waited:0.00}초): {apiLine}");
            return apiLine;
        }
        Debug.Log($"[CustomerLine] 풀 대사 사용 ({(hasApiResult ? "API 실패" : "2초 초과")}): {line.Fallback ?? "(CSV 기본 대사)"}");
        return line.Fallback;
    }

    /// <summary>진행 중인 모든 요청을 취소한다.</summary>
    public void Dispose()
    {
        this.lifetime.Cancel();
        this.lifetime.Dispose();
        this.pending.Clear();
    }

    /// <summary>이번 손님에게 어울리는 대사 주제와 그 주제의 풀 대사를 모은다.</summary>
    /// <param name="visit">대상 방문.</param><param name="context">진행 상황.</param>
    /// <returns>후보 주제 목록. 해당 풀 대사가 없는 주제는 제외한다.</returns>
    private List<Topic> collectTopics(CustomerVisit visit, CustomerLineContext context)
    {
        string speaker = (visit.Attributes & CustomerAttributes.Child) != 0 ? "child" : "adult";
        IEnumerable<CustomerLinePool.Line> entryLines = CustomerLinePool.Lines.Where(x => x.Timing == "입장" && x.Speaker == speaker);
        var topics = new List<Topic>();
        void add(string description, Func<CustomerLinePool.Line, bool> match)
        {
            var lines = entryLines.Where(match).ToList();
            if (lines.Count > 0) topics.Add(new Topic(description, lines));
        }

        foreach (CustomerOrderItem item in visit.Items)
        {
            string prefix = item.ProductIdx + " ";
            add($"{this.getProductName(item.ProductIdx)}을(를) 사는 이유나 쓸 곳", x => x.Axis == "품목 사연" && x.Key.StartsWith(prefix));
        }
        string dayKey = context.Day + "일차";
        add($"오늘 바깥 소식: {getDayNews(context)}", x => x.Axis == "바깥 소식" && x.Key == dayKey);
        string disposition = getDispositionName(visit.DispositionType);
        add($"손님 자신의 사정 (손님 성향: {disposition})", x => x.Axis == "손님 사연" && x.Disposition == disposition);
        bool isAunt = (visit.Attributes & CustomerAttributes.Female) != 0
            && (visit.Attributes & (CustomerAttributes.Child | CustomerAttributes.Elderly)) == 0;
        if (isAunt && this.random.NextDouble() < NeighborTopicChance) add("가게 주인의 딸 하루(아파서 집에 누워 있음)의 안부를 묻는 이웃 아줌마", x => x.Axis == "이웃");
        if (context.StoreStage >= 2)
        {
            string stageKey = context.StoreStage + "단계 확장";
            add($"가게가 {context.StoreStage}단계로 커진 것에 대한 반응", x => x.Axis == "가게 변화" && x.Key == stageKey);
        }
        if (context.WaitingCount >= 3) add("줄이 길어서 오래 기다린 것", x => x.Axis == "줄" && x.Key == "줄이 길 때");
        else if (context.WaitingCount == 0) add("가게가 한산한 것", x => x.Axis == "줄" && x.Key == "손님이 없을 때");
        if (context.BusinessProgress01 < 0.15f) add("문 연 직후에 온 것", x => x.Axis == "시간대" && x.Key == "개점 직후");
        else if (context.BusinessProgress01 > 0.8f) add("문 닫기 직전, 통금 전에 온 것", x => x.Axis == "시간대" && x.Key == "마감 직전");
        if (context.IsFinalDay) add("오늘이 시민권을 살 수 있는 마지막 날이라는 소문", x => x.Axis == "마지막 날");
        return topics;
    }

    /// <summary>안 쓴 풀 대사를 우선 고르고, 모두 썼으면 다시 섞어 쓴다.</summary>
    /// <param name="lines">같은 주제의 풀 대사.</param><returns>고른 대사.</returns>
    private string takePoolLine(List<CustomerLinePool.Line> lines)
    {
        var unused = lines.Where(x => !this.usedPoolLines.Contains(x.Text)).ToList();
        List<CustomerLinePool.Line> source = unused.Count > 0 ? unused : lines;
        string text = source[this.random.Next(source.Count)].Text;
        this.usedPoolLines.Add(text);
        return text;
    }

    /// <summary>손님 정보와 주제로 사용자 메시지를 만든다.</summary>
    /// <param name="visit">대상 방문.</param><param name="context">진행 상황.</param><param name="topic">이번 대사 주제. null이면 자유.</param>
    /// <returns>요청 본문의 사용자 메시지.</returns>
    private string buildPrompt(CustomerVisit visit, CustomerLineContext context, Topic topic)
    {
        bool isChild = (visit.Attributes & CustomerAttributes.Child) != 0;
        string gender = (visit.Attributes & CustomerAttributes.Female) != 0 ? "여성" : "남성";
        string items = string.Join(", ", visit.Items.Select(x => $"{this.getProductName(x.ProductIdx)} {x.Quantity}개"));
        var prompt = new StringBuilder();
        prompt.AppendLine($"손님: {(isChild ? "아이" : "어른")} {gender}, 성향 {getDispositionName(visit.DispositionType)}");
        prompt.AppendLine($"고른 물건: {items}");
        prompt.AppendLine($"{context.Day}일차{(context.IsFinalDay ? " (마지막 영업일)" : string.Empty)}, 가게 {context.StoreStage}단계, 뒤에 기다리는 손님 {context.WaitingCount}명");
        prompt.AppendLine($"오늘 바깥 분위기: {getDayNews(context)}");
        prompt.AppendLine($"이번 대사의 주제: {topic?.Description ?? "자유"}");
        if (topic != null) prompt.AppendLine($"같은 주제의 예시(그대로 쓰지 말 것): {string.Join(" / ", topic.Lines.Take(3).Select(x => x.Text))}");
        prompt.Append("이 손님이 계산대에 물건을 올려놓으며 하는 첫마디 한 줄만 써.");
        return prompt.ToString();
    }

    /// <summary>Claude API에 한 번 요청하고 대사 한 줄을 돌려준다. 실패와 시간 초과는 null로 바꾼다.</summary>
    /// <param name="userPrompt">사용자 메시지.</param><param name="cancellationToken">서비스 수명 취소.</param>
    /// <returns>정리된 대사 또는 null.</returns>
    private async UniTask<string> requestLineAsync(string userPrompt, CancellationToken cancellationToken)
    {
        var body = new MessageRequest
        {
            model = Model,
            max_tokens = MaxOutputTokens,
            thinking = new ThinkingConfig { type = "disabled" },
            output_config = new OutputConfig { effort = "low" },
            system = SystemPrompt,
            messages = new[] { new RequestMessage { role = "user", content = userPrompt } }
        };
        using var request = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body))),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = RequestTimeoutSeconds
        };
        request.SetRequestHeader("content-type", "application/json");
        request.SetRequestHeader("x-api-key", this.apiKey);
        request.SetRequestHeader("anthropic-version", ApiVersion);
        try
        {
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone) await UniTask.Yield(cancellationToken);
            if (request.result != UnityWebRequest.Result.Success)
            {
                // 응답 본문에는 키가 들어 있지 않으므로 원인 확인용으로 남긴다.
                Debug.LogWarning($"[CustomerLine] API 요청 실패 {request.responseCode} {request.error}: {request.downloadHandler?.text}");
                return null;
            }
            MessageResponse response = JsonUtility.FromJson<MessageResponse>(request.downloadHandler.text);
            if (response == null || response.stop_reason == "refusal" || response.content == null) return null;
            return cleanLine(string.Concat(response.content.Where(x => x.type == "text").Select(x => x.text)));
        }
        catch (OperationCanceledException)
        {
            request.Abort();
            return null;
        }
        catch (Exception exception)
        {
            // 대사 생성 실패는 풀 대사로 대체하므로 게임 진행을 막지 않고 원인만 남긴다.
            Debug.LogWarning($"[CustomerLine] API 응답 처리 실패: {exception.Message}");
            return null;
        }
    }

    /// <summary>모델 출력에서 첫 줄만 남기고 따옴표와 과도한 길이를 정리한다.</summary>
    /// <param name="text">모델 출력.</param><returns>말풍선에 넣을 대사 또는 null.</returns>
    private static string cleanLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string line = text.Trim().Split('\n')[0].Trim().Trim('"', '\'', '“', '”', '「', '」');
        if (line.Length == 0 || line.Length > MaxLineLength) return null;
        return line;
    }

    /// <summary>시연판 1~4일차의 바깥 분위기. 풀의 바깥 소식 축과 같은 설정이다.</summary>
    /// <param name="context">진행 상황.</param><returns>그날 분위기 설명.</returns>
    private static string getDayNews(CustomerLineContext context)
    {
        if (context.IsFinalDay) return "담장 안 시민권 발표 소문, 위병소 앞 줄이 골목까지 이어짐";
        return context.Day switch
        {
            1 => "배급 차가 오지 않아 정수장 앞이 소란함",
            2 => "밤새 정전, 담장 검문 강화, 집행인들이 돌아다님",
            3 => "검은 비 경보, 남쪽 구역 봉쇄",
            _ => "별다른 소식 없이 무거운 하루"
        };
    }

    /// <summary>성향 타입을 풀의 성향 이름으로 바꾼다.</summary>
    /// <param name="type">성향 타입.</param><returns>풀에서 쓰는 성향 이름.</returns>
    private static string getDispositionName(CustomerDispositionType type) => type switch
    {
        CustomerDispositionType.Hasty => "절박",
        CustomerDispositionType.PriceSensitive => "가격 민감",
        CustomerDispositionType.Wealthy => "부유한 손님",
        CustomerDispositionType.Poor => "가난",
        _ => "평범"
    };

    /// <summary>방문 하나의 진행 중인 요청과 대체 풀 대사.</summary>
    private sealed class PendingLine
    {
        /// <summary>여러 번 기다릴 수 있게 Preserve한 API 요청.</summary>
        public readonly UniTask<string> Request;
        /// <summary>같은 주제의 풀 대사. null이면 기존 CSV 대사를 쓴다.</summary>
        public readonly string Fallback;
        /// <summary>요청 시작 시각(realtimeSinceStartup 초).</summary>
        public readonly float RequestedAt;

        /// <summary>진행 중인 요청을 기록한다.</summary>
        /// <param name="request">API 요청.</param><param name="fallback">풀 대사.</param><param name="requestedAt">요청 시각.</param>
        public PendingLine(UniTask<string> request, string fallback, float requestedAt)
        {
            Request = request;
            Fallback = fallback;
            RequestedAt = requestedAt;
        }
    }

    /// <summary>대사 주제와 그 주제의 풀 대사.</summary>
    private sealed class Topic
    {
        /// <summary>프롬프트에 넣는 주제 설명.</summary>
        public readonly string Description;
        /// <summary>같은 주제의 풀 대사.</summary>
        public readonly List<CustomerLinePool.Line> Lines;

        /// <summary>주제를 만든다.</summary>
        /// <param name="description">주제 설명.</param><param name="lines">풀 대사.</param>
        public Topic(string description, List<CustomerLinePool.Line> lines)
        {
            Description = description;
            Lines = lines;
        }
    }

    // JsonUtility 직렬화용 요청·응답 형식. 필드 이름이 API JSON 키와 같아야 한다.
    [Serializable] private sealed class MessageRequest { public string model; public int max_tokens; public ThinkingConfig thinking; public OutputConfig output_config; public string system; public RequestMessage[] messages; }
    [Serializable] private sealed class ThinkingConfig { public string type; }
    [Serializable] private sealed class OutputConfig { public string effort; }
    [Serializable] private sealed class RequestMessage { public string role; public string content; }
    [Serializable] private sealed class MessageResponse { public ResponseBlock[] content; public string stop_reason; }
    [Serializable] private sealed class ResponseBlock { public string type; public string text; }
}
