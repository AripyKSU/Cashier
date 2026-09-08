using System;
using System.Collections.Generic;

/// <summary>FIFO 대기열과 5초 입장 시계를 소유한다. 계산 중인 방문은 이 목록에 포함하지 않는다.</summary>
public sealed class CustomerQueue
{
    /// <summary>계산 중 손님을 제외한 논리·표시 정원.</summary>
    public const int Capacity = 10;
    /// <summary>초기 테스트용 입장 간격.</summary>
    public const double ArrivalSeconds = 5;
    /// <summary>재촉·불만 표시 유지 초.</summary>
    public const double SpeechSeconds = 3;
    private readonly Func<CustomerVisit> createVisit;
    private readonly IReadOnlyDictionary<uint, CustomerDispositionData> dispositions;
    private readonly List<Entry> waiting = new List<Entry>();
    private readonly List<Entry> leaving = new List<Entry>();
    private double now, nextArrival;
    private bool running;
    /// <summary>논리적 대기열. 외부는 방문 상태를 직접 변경하지 않는다.</summary>
    public IReadOnlyList<Entry> Waiting { get; }
    /// <summary>이미 이탈했지만 불만 대사를 3초 표시하는 기록.</summary>
    public IReadOnlyList<Entry> Leaving { get; }

    /// <summary>기존 생성기와 성향 catalog를 연결한다.</summary>
    /// <param name="createVisit">입장 시점 현재가로 방문을 생성한다. 상품 후보가 없으면 null.</param>
    /// <param name="dispositions">검증된 성향 원본.</param>
    /// <exception cref="ArgumentNullException">필수 의존성 누락.</exception>
    public CustomerQueue(Func<CustomerVisit> createVisit, IReadOnlyDictionary<uint, CustomerDispositionData> dispositions)
    {
        this.createVisit = createVisit ?? throw new ArgumentNullException(nameof(createVisit));
        this.dispositions = dispositions ?? throw new ArgumentNullException(nameof(dispositions));
        Waiting = waiting.AsReadOnly();
        Leaving = leaving.AsReadOnly();
    }

    /// <summary>새 영업일 시계를 시작한다. 첫 자동 입장은 5초 후다.</summary>
    /// <exception cref="InvalidOperationException">중복 영업 시작.</exception>
    public void Start()
    {
        if (running) throw new InvalidOperationException("대기열 영업 중입니다.");
        now = 0;
        nextArrival = ArrivalSeconds;
        running = true;
    }

    /// <summary>새 방문을 생성하여 줄 끝에 등록한다. 정원 초과 입장은 누적하지 않는다.</summary>
    /// <returns>등록 성공 여부.</returns>
    /// <exception cref="ArgumentException">성향 데이터 오류.</exception>
    public bool TryAdd()
    {
        if (!running || waiting.Count >= Capacity) return false;
        var visit = createVisit();
        if (visit == null) return false;
        var data = dispositions[visit.DispositionIdx];
        data.ValidateQueueSettings();
        visit.JoinQueue();
        waiting.Add(new Entry(visit, now + data.QueuePatienceSeconds, data.QueueWarningTextIdx, data.QueueLeaveTextIdx));
        return true;
    }

    /// <summary>만료를 먼저 처리한 대기열 맨 앞을 계산대에 넘긴다.</summary>
    /// <returns>대기 손님이 없으면 null.</returns>
    public CustomerVisit TakeNext()
    {
        if (!running || waiting.Count == 0) return null;
        var entry = waiting[0];
        waiting.RemoveAt(0);
        entry.Visit.LeaveQueue(false, true);
        return entry.Visit;
    }

    /// <summary>일시정지를 제외한 경과 시간을 처리한다. 프레임 내 입장은 5초 경계 순서로 처리한다.</summary>
    /// <param name="deltaSeconds">프레임 경과 초.</param>
    /// <param name="paused">일시정지 여부.</param>
    /// <exception cref="ArgumentOutOfRangeException">음수·비유한 시간.</exception>
    public void Advance(double deltaSeconds, bool paused)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (!running || paused) return;
        double end = now + deltaSeconds;
        while (nextArrival <= end)
        {
            now = nextArrival;
            updateWaiting(end);
            TryAdd();
            nextArrival += ArrivalSeconds;
        }
        now = end;
        updateWaiting(end);
        leaving.RemoveAll(x => x.SpeechUntil <= now);
    }

    /// <summary>영업 종료 시 불만 없이 모든 대기·말풍선을 정리한다.</summary>
    public void Stop()
    {
        foreach (var entry in waiting) entry.Visit.LeaveQueue(false);
        waiting.Clear();
        leaving.Clear();
        running = false;
    }

    /// <summary>말풍선의 표시 여부를 조회한다.</summary>
    /// <param name="entry">대기 또는 이탈 기록.</param>
    /// <returns>아직 표시할 대사 PK. 없으면 0.</returns>
    public uint GetSpeech(Entry entry) => entry.SpeechUntil > now ? entry.SpeechIdx : 0;

    /// <summary>만료 우선. 긴 프레임에서 재촉·만료를 함께 넘으면 불만만 표시한다.</summary>
    /// <param name="frameEnd">현재 프레임 종료 시각.</param>
    private void updateWaiting(double frameEnd)
    {
        for (int i = waiting.Count - 1; i >= 0; i--)
        {
            var entry = waiting[i];
            if (entry.Deadline <= now)
            {
                entry.Visit.LeaveQueue(true);
                entry.SpeechIdx = entry.LeaveTextIdx;
                entry.SpeechUntil = frameEnd + SpeechSeconds;
                leaving.Add(entry);
                waiting.RemoveAt(i);
            }
            else if (!entry.Warned && entry.Deadline - 6 <= now && entry.Deadline > frameEnd)
            {
                entry.Warned = true;
                entry.SpeechIdx = entry.WarningTextIdx;
                entry.SpeechUntil = now + SpeechSeconds;
            }
        }
    }

    /// <summary>줄 합류 시 확정한 방문·대기 한도·대사. 소유 대기열만 상태를 갱신한다.</summary>
    public sealed class Entry
    {
        /// <summary>합류 시 생성한 불변 구매 목록을 가진 방문.</summary>
        public CustomerVisit Visit { get; }
        /// <summary>영업 시계 기준 만료 시각.</summary>
        internal double Deadline { get; }
        /// <summary>재촉 대사 snapshot.</summary>
        internal uint WarningTextIdx { get; }
        /// <summary>불만 대사 snapshot.</summary>
        internal uint LeaveTextIdx { get; }
        /// <summary>중복 재촉 방지 표식.</summary>
        internal bool Warned { get; set; }
        /// <summary>현재 말풍선 PK.</summary>
        internal uint SpeechIdx { get; set; }
        /// <summary>말풍선 제거 시각.</summary>
        internal double SpeechUntil { get; set; }
        /// <summary>검증된 성향을 복사해 방문 중 데이터 변경을 차단한다.</summary>
        /// <param name="visit">방문.</param>
        /// <param name="deadline">만료 시각.</param>
        /// <param name="warning">재촉 FK.</param>
        /// <param name="leave">불만 FK.</param>
        internal Entry(CustomerVisit visit, double deadline, uint warning, uint leave)
        {
            Visit = visit;
            Deadline = deadline;
            WarningTextIdx = warning;
            LeaveTextIdx = leave;
        }
    }
}
