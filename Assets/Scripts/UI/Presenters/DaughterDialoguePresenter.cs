using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>정산 화면의 딸 대사와 이미지만 표시한다.</summary>
public sealed class DaughterDialoguePresenter : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private TextMeshProUGUI dialogue;

    /// <summary>직렬화된 필수 참조를 확인한다.</summary>
    /// <exception cref="InvalidOperationException">이미지 또는 텍스트 연결 누락.</exception>
    public void ValidateReferences()
    {
        if (portrait == null || dialogue == null)
            throw new InvalidOperationException("DaughterDialoguePanel: 이미지와 대사 참조가 필요합니다.");
    }

    /// <summary>이미 확정된 표시값을 다시 선택하지 않고 반영한다.</summary>
    /// <param name="viewData">딸 대사 표시 스냅샷.</param>
    public void UpdateView(DaughterDialogueViewData viewData)
    {
        ValidateReferences();
        portrait.sprite = viewData.Sprite;
        dialogue.text = viewData.Text;
    }
}
