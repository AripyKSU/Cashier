using UnityEngine;

/// <summary>
/// 게임 전체 마우스 커서의 모양·크기 설정과 적용을 소유하는 정적 진입점.
/// 기본은 종이 화살표, 정산(가계부) 화면에서는 몽당연필을 쓴다. 물건을 끄는 손 커서는
/// <see cref="SaleSortingHandCursor"/>가 OS 커서를 숨기고 따로 그리므로 여기서 다루지 않는다.
/// 설정값은 PlayerPrefs에 저장해 다음 실행에도 유지한다.
/// </summary>
public static class GameCursor
{
    /// <summary>설정 창에서 고르는 기본 화살표 모양.</summary>
    public enum CursorStyle
    {
        Paper,
        Wood,
        Iron,
        CursorStyle_End
    }

    /// <summary>설정 창에서 고르는 커서 크기. 도트 배율 1·2·3배에 대응한다.</summary>
    public enum CursorSize
    {
        Small,
        Medium,
        Large,
        CursorSize_End
    }

    private const string StylePrefsKey = "Cashier.Cursor.Style";
    private const string SizePrefsKey = "Cashier.Cursor.Size";
    // 원본 도트 기준 연필 끝(흑연) 위치. 크기 배율을 곱해 hotspot으로 쓴다.
    private static readonly Vector2 PencilTipPixels = new Vector2(1f, 1f);

    // GameCursorTextureSet이 처음 등록한 텍스처. 정적 참조라 씬이 바뀌어도 언로드되지 않는다.
    private static Texture2D[] arrowTextures;
    private static Texture2D[] pencilTextures;
    private static bool isLedgerMode;
    private static bool hasLoadedPrefs;
    private static CursorStyle style = CursorStyle.Paper;
    private static CursorSize size = CursorSize.Medium;

    /// <summary>현재 기본 화살표 모양.</summary>
    public static CursorStyle Style
    {
        get { loadPrefs(); return style; }
    }

    /// <summary>현재 커서 크기.</summary>
    public static CursorSize Size
    {
        get { loadPrefs(); return size; }
    }

    /// <summary>
    /// 커서 텍스처를 한 번 등록하고 현재 설정을 적용한다. 이미 등록돼 있으면 다시 적용만 한다.
    /// </summary>
    /// <param name="arrows">모양×크기 순서(Paper Small, Paper Medium, Paper Large, Wood …)의 9장.</param>
    /// <param name="pencils">크기 순서(Small, Medium, Large)의 연필 3장.</param>
    public static void RegisterTextures(Texture2D[] arrows, Texture2D[] pencils)
    {
        int styleCount = (int)CursorStyle.CursorStyle_End;
        int sizeCount = (int)CursorSize.CursorSize_End;
        if (arrowTextures == null)
        {
            if (arrows == null || arrows.Length != styleCount * sizeCount || pencils == null || pencils.Length != sizeCount)
            {
                Debug.LogError("[GameCursor] 커서 텍스처 개수가 맞지 않습니다. 화살표 9장, 연필 3장이 필요합니다.");
                return;
            }

            arrowTextures = arrows;
            pencilTextures = pencils;
        }

        apply();
    }

    /// <summary>기본 화살표 모양을 바꾸고 저장한다.</summary>
    /// <param name="newStyle">새 모양.</param>
    public static void SetStyle(CursorStyle newStyle)
    {
        if (newStyle < 0 || newStyle >= CursorStyle.CursorStyle_End) return;
        loadPrefs();
        style = newStyle;
        PlayerPrefs.SetInt(StylePrefsKey, (int)style);
        PlayerPrefs.Save();
        apply();
    }

    /// <summary>커서 크기를 바꾸고 저장한다.</summary>
    /// <param name="newSize">새 크기.</param>
    public static void SetSize(CursorSize newSize)
    {
        if (newSize < 0 || newSize >= CursorSize.CursorSize_End) return;
        loadPrefs();
        size = newSize;
        PlayerPrefs.SetInt(SizePrefsKey, (int)size);
        PlayerPrefs.Save();
        apply();
    }

    /// <summary>정산(가계부) 화면이 열리고 닫힐 때 연필 커서로 바꾸거나 되돌린다.</summary>
    /// <param name="enabled">true면 연필, false면 기본 화살표.</param>
    public static void SetLedgerMode(bool enabled)
    {
        if (isLedgerMode == enabled) return;
        isLedgerMode = enabled;
        apply();
    }

    /// <summary>저장된 설정을 처음 한 번만 읽는다. 범위를 벗어난 값은 기본값으로 둔다.</summary>
    private static void loadPrefs()
    {
        if (hasLoadedPrefs) return;
        hasLoadedPrefs = true;
        int savedStyle = PlayerPrefs.GetInt(StylePrefsKey, (int)CursorStyle.Paper);
        int savedSize = PlayerPrefs.GetInt(SizePrefsKey, (int)CursorSize.Medium);
        if (savedStyle >= 0 && savedStyle < (int)CursorStyle.CursorStyle_End) style = (CursorStyle)savedStyle;
        if (savedSize >= 0 && savedSize < (int)CursorSize.CursorSize_End) size = (CursorSize)savedSize;
    }

    /// <summary>현재 모드·모양·크기에 맞는 텍스처를 OS 커서로 적용한다.</summary>
    private static void apply()
    {
        if (arrowTextures == null) return;
        loadPrefs();
        int sizeIndex = (int)size;
        if (isLedgerMode)
        {
            // 연필은 끝이 클릭 지점이라 hotspot을 끝 픽셀에 맞춘다.
            Cursor.SetCursor(pencilTextures[sizeIndex], PencilTipPixels * (sizeIndex + 1), CursorMode.Auto);
            return;
        }

        Texture2D arrow = arrowTextures[(int)style * (int)CursorSize.CursorSize_End + sizeIndex];
        Cursor.SetCursor(arrow, Vector2.zero, CursorMode.Auto);
    }
}
