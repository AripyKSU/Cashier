using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>스테이지 1~3 가판·카운터·상자·설비 원본에 픽셀 크기와 색 보정을 슬라이더로 다시 굽는 창입니다.</summary>
public sealed class StageArtStyleWindow : EditorWindow
{
    /// <summary>원본 PNG와 마지막 설정을 두는 저장소 루트 기준 폴더입니다. Assets 밖이라 임포트되지 않습니다.</summary>
    private const string SourceRoot = "ArtStyleSource";
    private const string SettingsPath = SourceRoot + "/style.json";
    private const double AutoApplyDelay = .5;

    private enum Group { Stall, Counter, Crate, Facility, Soldier }

    /// <summary>구울 텍스처와 1280×720 캔버스를 1920 화면에 그릴 때의 텍스처 1픽셀당 화면 픽셀 배율입니다.</summary>
    private readonly struct Target
    {
        public readonly string path;
        public readonly float screenScale;
        public readonly Group group;
        public Target(string path, float screenScale, Group group) { this.path = path; this.screenScale = screenScale; this.group = group; }
    }

    private const string Env = "Assets/Textures/Environment/Dystopia/";
    private const string Ui = "Assets/Textures/UI/Dystopia/";
    private static readonly Target[] Targets =
    {
        new Target(Env + "BoothCanopy.png", 1.177f, Group.Stall),
        new Target(Env + "Stage2Shop.png", 1.194f, Group.Stall),
        new Target(Env + "Stage2RustedFrame.png", 1.194f, Group.Stall),
        new Target(Env + "Stage3Shop.png", 1.194f, Group.Stall),
        new Target(Env + "Stage1CounterTop.png", 1.205f, Group.Counter),
        new Target(Env + "Stage2CounterTop.png", 1.092f, Group.Counter),
        new Target(Env + "Stage3CounterTop.png", 1.093f, Group.Counter),
        new Target(Ui + "Stage1CrateClosed.png", 2.375f, Group.Crate),
        new Target(Ui + "Stage1CrateOpen.png", 2.375f, Group.Crate),
        new Target(Ui + "Stage2RustedCrateClosed.png", 2.576f, Group.Crate),
        new Target(Ui + "Stage2RustedCrateOpen.png", 2.576f, Group.Crate),
        new Target(Ui + "Stage3CrateClosed.png", 2.576f, Group.Crate),
        new Target(Ui + "Stage3CrateOpen.png", 2.576f, Group.Crate),
        new Target(Env + "Stage1FoodShelf.png", .331f, Group.Facility),
        new Target(Env + "Stage1MedicineCabinet.png", .375f, Group.Facility),
        new Target(Env + "Stage2FoodShelf.png", .318f, Group.Facility),
        new Target(Env + "Stage2MedicineCabinet.png", .346f, Group.Facility),
        new Target(Env + "Stage2PowerCommunications.png", .378f, Group.Facility),
        new Target(Env + "Stage2ToolBench.png", .404f, Group.Facility),
        new Target(Env + "Stage3FoodShelf.png", .325f, Group.Facility),
        new Target(Env + "Stage3MedicineCabinet.png", .333f, Group.Facility),
        new Target(Env + "Stage3NuclearProtection.png", .514f, Group.Facility),
        new Target(Env + "Stage3PowerCommunications.png", .308f, Group.Facility),
        new Target(Env + "Stage3PrecisionElectronics.png", .396f, Group.Facility),
        new Target(Env + "Stage3ToolBench.png", .363f, Group.Facility),
        // 손으로 찍은 26×17 픽셀아트라 칸으로 묶지 않고 색 보정만 합니다(블록 1).
        new Target(Env + "WatchGuard.png", 2.54f, Group.Soldier),
        // 감독관 대사창은 손으로 찍은 픽셀아트로 바꿔서 굽지 않습니다. 적용해도 덮어쓰지 않도록 목록에서 뺐습니다.
    };

    /// <summary>배경·손님에서 뽑은 40색, 녹슨 빨강·바랜 황동 4색, 1단계 나무 12색입니다.</summary>
    private static readonly Color32[] Palette =
    {
        new Color32(242, 200, 145, 255), new Color32(244, 179, 122, 255), new Color32(229, 167, 119, 255), new Color32(207, 149, 115, 255),
        new Color32(189, 142, 114, 255), new Color32(180, 134, 110, 255), new Color32(166, 130, 114, 255), new Color32(158, 123, 107, 255),
        new Color32(148, 121, 110, 255), new Color32(142, 119, 112, 255), new Color32(140, 116, 108, 255), new Color32(139, 112, 101, 255),
        new Color32(133, 111, 105, 255), new Color32(126, 110, 106, 255), new Color32(123, 106, 101, 255), new Color32(120, 101, 95, 255),
        new Color32(109, 99, 100, 255), new Color32(102, 93, 94, 255), new Color32(95, 88, 90, 255), new Color32(87, 84, 88, 255),
        new Color32(84, 80, 81, 255), new Color32(78, 78, 82, 255), new Color32(76, 71, 71, 255), new Color32(68, 68, 72, 255),
        new Color32(62, 59, 58, 255), new Color32(57, 50, 48, 255), new Color32(50, 49, 48, 255), new Color32(48, 42, 41, 255),
        new Color32(41, 39, 38, 255), new Color32(38, 33, 32, 255), new Color32(36, 30, 29, 255), new Color32(32, 28, 28, 255),
        new Color32(30, 24, 22, 255), new Color32(25, 23, 22, 255), new Color32(24, 22, 22, 255), new Color32(24, 20, 20, 255),
        new Color32(23, 16, 15, 255), new Color32(17, 13, 14, 255), new Color32(11, 8, 8, 255), new Color32(3, 3, 3, 255),
        new Color32(140, 48, 38, 255), new Color32(112, 40, 32, 255), new Color32(168, 128, 64, 255), new Color32(128, 98, 52, 255),
        new Color32(101, 86, 72, 255), new Color32(91, 78, 66, 255), new Color32(86, 74, 63, 255), new Color32(82, 71, 60, 255),
        new Color32(79, 68, 58, 255), new Color32(76, 66, 56, 255), new Color32(74, 63, 53, 255), new Color32(71, 61, 51, 255),
        new Color32(67, 58, 47, 255), new Color32(64, 54, 44, 255), new Color32(56, 46, 37, 255), new Color32(30, 25, 21, 255),
    };

    /// <summary>색 보정 값입니다.</summary>
    private readonly struct Look
    {
        public readonly float saturation, contrast, warmth;
        public Look(float saturation, float contrast, float warmth) { this.saturation = saturation; this.contrast = contrast; this.warmth = warmth; }
    }


    /// <summary>창에서 조절하는 값이며 style.json에 저장해 다음에도 같은 값으로 엽니다.</summary>
    [Serializable] private sealed class Settings
    {
        public float pixelSize = 4f;
        public float saturation = .7f;
        public float contrast = .85f;
        public float warmth = 1f;
        public float paletteLock = 1f;
        public float outlineSoften = 1f;
        public float soldierPixelSize = 3f;
        public bool crispDetails = true;
        public bool stall = true, counter = true, crate = true, facility = true, soldier = true;
    }

    private Settings settings;
    private bool autoApply = true;
    private double pendingApplyAt = -1;
    private string status = "";

    [MenuItem("Cashier/스테이지 아트 스타일")]
    private static void Open() => GetWindow<StageArtStyleWindow>("스테이지 아트 스타일");

    private void OnEnable()
    {
        // 예전 style.json에 없는 값은 기본값을 유지하도록 덮어쓰기로 읽습니다.
        settings = new Settings();
        if (File.Exists(SettingsPath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(SettingsPath), settings);
        EditorApplication.update += Tick;
    }

    private void OnDisable() => EditorApplication.update -= Tick;

    private void Tick()
    {
        if (pendingApplyAt < 0 || EditorApplication.timeSinceStartup < pendingApplyAt) return;
        pendingApplyAt = -1;
        Apply();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("원본(ArtStyleSource)에서 매번 새로 굽습니다. 값을 바꿔도 원본은 그대로입니다.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        settings.pixelSize = EditorGUILayout.Slider("픽셀 크기 (화면 px)", settings.pixelSize, 1f, 8f);
        settings.saturation = EditorGUILayout.Slider("채도", settings.saturation, 0f, 1.2f);
        settings.contrast = EditorGUILayout.Slider("대비", settings.contrast, .5f, 1.2f);
        settings.warmth = EditorGUILayout.Slider("노을 톤", settings.warmth, 0f, 2f);
        settings.paletteLock = EditorGUILayout.Slider("배경 팔레트 고정", settings.paletteLock, 0f, 1f);
        settings.outlineSoften = EditorGUILayout.Slider("카운터 검은 선 정리", settings.outlineSoften, 0f, 1f);
        settings.crispDetails = EditorGUILayout.Toggle("선명하게 (칸마다 대표색)", settings.crispDetails);
        EditorGUILayout.Space();
        settings.stall = EditorGUILayout.Toggle("가판", settings.stall);
        settings.counter = EditorGUILayout.Toggle("카운터", settings.counter);
        settings.crate = EditorGUILayout.Toggle("상자", settings.crate);
        settings.facility = EditorGUILayout.Toggle("설비", settings.facility);
        settings.soldier = EditorGUILayout.Toggle("망루 군인", settings.soldier);
        settings.soldierPixelSize = EditorGUILayout.Slider("망루 군인 픽셀 크기", settings.soldierPixelSize, 1f, 8f);
        bool changed = EditorGUI.EndChangeCheck();
        autoApply = EditorGUILayout.Toggle("값 바꾸면 자동 적용", autoApply);
        if (changed && autoApply) pendingApplyAt = EditorApplication.timeSinceStartup + AutoApplyDelay;

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("적용")) Apply();
            if (GUILayout.Button("기본값")) { settings = new Settings(); Apply(); }
            if (GUILayout.Button("전부 원본으로")) RestoreAll();
        }
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);
    }

    private bool Enabled(Group group) => group switch
    {
        Group.Stall => settings.stall,
        Group.Counter => settings.counter,
        Group.Crate => settings.crate,
        Group.Soldier => settings.soldier,
        _ => settings.facility,
    };

    /// <summary>켜진 묶음은 현재 값으로 굽고, 꺼진 묶음은 원본으로 되돌립니다.</summary>
    private void Apply()
    {
        File.WriteAllText(SettingsPath, JsonUtility.ToJson(settings, true));
        var missing = new List<string>();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var target in Targets)
            {
                string source = Path.Combine(SourceRoot, target.path);
                if (!File.Exists(source)) { missing.Add(target.path); continue; }
                byte[] bytes = Enabled(target.group) ? Bake(File.ReadAllBytes(source), target) : File.ReadAllBytes(source);
                File.WriteAllBytes(target.path, bytes);
                AssetDatabase.ImportAsset(target.path);
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        status = missing.Count == 0 ? $"적용 완료: 픽셀 {settings.pixelSize:0.0}px" : "원본 없음: " + string.Join(", ", missing);
        Repaint();
    }

    private void RestoreAll()
    {
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var target in Targets)
            {
                string source = Path.Combine(SourceRoot, target.path);
                if (!File.Exists(source)) continue;
                File.Copy(source, target.path, true);
                AssetDatabase.ImportAsset(target.path);
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        status = "전부 원본으로 되돌렸습니다.";
    }

    private byte[] Bake(byte[] png, Target target)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            texture.LoadImage(png);
            int width = texture.width, height = texture.height;
            Color32[] src = texture.GetPixels32();
            var look = new Look(settings.saturation, settings.contrast, settings.warmth);
            float pixelSize = target.group == Group.Soldier ? settings.soldierPixelSize : settings.pixelSize;
            // 상자는 이미 원본 픽셀아트라 다시 묶으면 격자가 어긋나 선이 겹칩니다. 색 보정과 팔레트만 적용합니다.
            int block = target.group == Group.Crate ? 1 : Mathf.Max(1, Mathf.RoundToInt(pixelSize / target.screenScale));
            int cols = (width + block - 1) / block, rows = (height + block - 1) / block;
            var cells = new Color[cols * rows];
            var solid = new bool[cols * rows];
            // 군인은 한 칸이 원본 70px 안팎이라 가는 총신이 반을 못 채워 끊깁니다. 1/5만 차도 칸으로 남깁니다.
            float minCoverage = target.group == Group.Soldier ? .2f : .5f;
            var counts = new int[Palette.Length];
            var sums = new Color[Palette.Length];
            var nearestCache = new Dictionary<int, int>();

            // 선명하게 켜면 칸마다 가장 많은 팔레트 색을, 끄면 알파 가중 평균을 씁니다.
            for (int cy = 0; cy < rows; cy++)
            for (int cx = 0; cx < cols; cx++)
            {
                int i = cy * cols + cx;
                cells[i] = settings.crispDetails && block > 1
                    ? DominantColor(src, width, height, cx * block, cy * block, block, minCoverage, look, counts, sums, nearestCache, out solid[i])
                    : AverageColor(src, width, height, cx * block, cy * block, block, minCoverage, look, out solid[i]);
            }

            if (target.group == Group.Counter) SoftenOutlines(cells, solid, cols, rows);

            var dst = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = (y / block) * cols + x / block;
                if (!solid[i]) continue;
                Color c = cells[i];
                if (settings.paletteLock > 0) c = Color.Lerp(c, Nearest(c), settings.paletteLock);
                dst[y * width + x] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 255);
            }
            texture.SetPixels32(dst);
            return texture.EncodeToPNG();
        }
        finally { DestroyImmediate(texture); }
    }

    /// <summary>블록의 알파 가중 평균을 보정하고, minCoverage 이상 차 있으면 불투명 칸으로 둡니다.</summary>
    private static Color AverageColor(Color32[] src, int width, int height, int x0, int y0, int block, float minCoverage, Look look, out bool solid)
    {
        float r = 0, g = 0, b = 0, a = 0;
        for (int y = y0; y < y0 + block; y++)
        for (int x = x0; x < x0 + block; x++)
        {
            if (x >= width || y >= height) continue;
            Color32 p = src[y * width + x];
            float pa = p.a / 255f;
            r += p.r / 255f * pa; g += p.g / 255f * pa; b += p.b / 255f * pa; a += pa;
        }
        solid = a / (block * block) >= minCoverage;
        return a > 0 ? Grade(new Color(r / a, g / a, b / a), look) : Color.clear;
    }

    /// <summary>불투명 픽셀을 보정 후 가까운 팔레트 색으로 세어 가장 많은 색 묶음의 평균을 돌려줍니다. 계기판·표식 같은 작은 무늬가 뭉개지지 않습니다.</summary>
    private static Color DominantColor(Color32[] src, int width, int height, int x0, int y0, int block, float minCoverage, Look look,
        int[] counts, Color[] sums, Dictionary<int, int> nearestCache, out bool solid)
    {
        Array.Clear(counts, 0, counts.Length);
        Array.Clear(sums, 0, sums.Length);
        int opaque = 0;
        for (int y = y0; y < y0 + block; y++)
        for (int x = x0; x < x0 + block; x++)
        {
            if (x >= width || y >= height) continue;
            Color32 p = src[y * width + x];
            if (p.a < 128) continue;
            Color graded = Grade(p, look);
            int key = (int)(graded.r * 255) << 16 | (int)(graded.g * 255) << 8 | (int)(graded.b * 255);
            if (!nearestCache.TryGetValue(key, out int index)) nearestCache[key] = index = NearestIndex(graded);
            counts[index]++;
            sums[index] += graded;
            opaque++;
        }
        solid = opaque >= minCoverage * block * block;
        if (opaque == 0) return Color.clear;
        int best = 0;
        for (int k = 1; k < counts.Length; k++) if (counts[k] > counts[best]) best = k;
        Color mean = sums[best] / counts[best];
        return new Color(mean.r, mean.g, mean.b);
    }

    private static Color Grade(Color c, Look look)
    {
        float grey = (c.r + c.g + c.b) / 3f;
        c = new Color(grey + (c.r - grey) * look.saturation, grey + (c.g - grey) * look.saturation, grey + (c.b - grey) * look.saturation);
        c = new Color(.5f + (c.r - .5f) * look.contrast, .5f + (c.g - .5f) * look.contrast, .5f + (c.b - .5f) * look.contrast);
        var tint = Color.Lerp(Color.white, new Color(1.06f, .97f, .86f), look.warmth);
        return new Color(Mathf.Clamp01(c.r * tint.r), Mathf.Clamp01(c.g * tint.g), Mathf.Clamp01(c.b * tint.b));
    }

    /// <summary>안쪽에 고립된 아주 어두운 칸을 주변 색으로 덮어 촘촘한 검은 선을 줄입니다. 실루엣 가장자리는 유지합니다.</summary>
    private void SoftenOutlines(Color[] cells, bool[] solid, int cols, int rows)
    {
        if (settings.outlineSoften <= 0) return;
        var source = (Color[])cells.Clone();
        var neighbours = new Color[4];
        for (int y = 1; y < rows - 1; y++)
        for (int x = 1; x < cols - 1; x++)
        {
            int i = y * cols + x;
            if (!solid[i] || Luma(source[i]) >= .16f) continue;
            int up = i + cols, down = i - cols, left = i - 1, right = i + 1;
            if (!solid[up] || !solid[down] || !solid[left] || !solid[right]) continue;
            neighbours[0] = source[up]; neighbours[1] = source[down]; neighbours[2] = source[left]; neighbours[3] = source[right];
            Array.Sort(neighbours, (p, q) => Luma(p).CompareTo(Luma(q)));
            Color median = Color.Lerp(neighbours[1], neighbours[2], .5f);
            if (Luma(median) <= .24f) continue;
            cells[i] = Color.Lerp(source[i], median * .78f, settings.outlineSoften);
        }
    }

    private static float Luma(Color c) => (c.r + c.g + c.b) / 3f;

    private static Color Nearest(Color c) => Palette[NearestIndex(c)];

    private static int NearestIndex(Color c)
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int k = 0; k < Palette.Length; k++)
        {
            Color32 p = Palette[k];
            float dr = c.r * 255 - p.r, dg = c.g * 255 - p.g, db = c.b * 255 - p.b;
            float distance = dr * dr + dg * dg + db * db;
            if (distance < bestDistance) { bestDistance = distance; best = k; }
        }
        return best;
    }
}
