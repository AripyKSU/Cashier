using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 영업 중 명패 아래 "손님 성향" 버튼으로 여닫는 성향 아이콘 설명표.
/// 정확한 수치 없이 아이콘별 특징만 보여 주며, 열려 있어도 영업 시간은 그대로 흐릅니다.
/// </summary>
public sealed class CustomerTraitGuidePresenter : MonoBehaviour
{
    [Tooltip("설명표를 여닫는 버튼")]
    [SerializeField] private Button toggleButton;

    [Tooltip("버튼 글자")]
    [SerializeField] private TextMeshProUGUI toggleLabel;

    [Tooltip("설명표 패널")]
    [SerializeField] private GameObject panel;

    [Tooltip("성향 이름 텍스트 (Normal, Hasty, PriceSensitive, Wealthy, Poor 순서)")]
    [SerializeField] private TextMeshProUGUI[] nameTexts = new TextMeshProUGUI[0];

    [Tooltip("성향 특징 텍스트 (이름과 같은 순서)")]
    [SerializeField] private TextMeshProUGUI[] descriptionTexts = new TextMeshProUGUI[0];

    [Tooltip("이름 TextData 키 (이름과 같은 순서)")]
    [SerializeField] private uint[] nameTextIdxs = { 8564, 8566, 8568, 8570, 8572 };

    [Tooltip("특징 TextData 키 (이름과 같은 순서)")]
    [SerializeField] private uint[] descriptionTextIdxs = { 8565, 8567, 8569, 8571, 8573 };

    [Tooltip("버튼 글자 TextData 키")]
    [SerializeField] private uint toggleTextIdx = 8574;

    private bool hasText;

    private void Awake()
    {
        if (toggleButton != null) toggleButton.onClick.AddListener(toggle);
        if (panel != null) panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (toggleButton != null) toggleButton.onClick.RemoveListener(toggle);
    }

    /// <summary>설명표를 닫습니다. 영업 화면이 숨겨질 때 호출합니다.</summary>
    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>설명표를 열거나 닫습니다. 처음 열 때 TextData에서 문구를 채웁니다.</summary>
    private void toggle()
    {
        if (panel == null) return;
        fillTexts();
        panel.SetActive(!panel.activeSelf);
    }

    /// <summary>TextData의 이름·특징 문구를 한 번만 채웁니다.</summary>
    private void fillTexts()
    {
        if (hasText) return;
        var texts = DataTableManager.Instance?.GetDB<TextDataTable>(DataTableType.Text);
        if (texts == null) return;
        if (toggleLabel != null && texts.Rows.TryGetValue(toggleTextIdx, out var toggleRow)) toggleLabel.text = toggleRow.Text;
        for (int index = 0; index < nameTexts.Length && index < nameTextIdxs.Length; index++)
            if (nameTexts[index] != null && texts.Rows.TryGetValue(nameTextIdxs[index], out var row)) nameTexts[index].text = row.Text;
        for (int index = 0; index < descriptionTexts.Length && index < descriptionTextIdxs.Length; index++)
            if (descriptionTexts[index] != null && texts.Rows.TryGetValue(descriptionTextIdxs[index], out var row)) descriptionTexts[index].text = row.Text;
        hasText = true;
    }
}
