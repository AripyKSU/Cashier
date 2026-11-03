using UnityEngine;

/// <summary>
/// 씬에 놓여 커서 텍스처를 <see cref="GameCursor"/>에 등록하는 연결 컴포넌트.
/// Hub와 Gameplay UI에 하나씩 두며, 먼저 열린 쪽이 등록하고 이후에는 현재 설정을 다시 적용만 한다.
/// </summary>
public sealed class GameCursorTextureSet : MonoBehaviour
{
    [Tooltip("모양×크기 순서 9장: 종이 작게/보통/크게, 나무 작게/보통/크게, 녹슨 쇠 작게/보통/크게")]
    [SerializeField] private Texture2D[] arrowTextures = new Texture2D[9];

    [Tooltip("정산 화면 연필 3장: 작게/보통/크게")]
    [SerializeField] private Texture2D[] pencilTextures = new Texture2D[3];

    /// <summary>씬이 열릴 때 커서 텍스처를 등록하고 현재 설정을 적용한다.</summary>
    private void Awake()
    {
        GameCursor.RegisterTextures(arrowTextures, pencilTextures);
    }
}
