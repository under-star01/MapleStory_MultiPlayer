using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class QuickKeySlotUI :
    MonoBehaviour,
    IPointerClickHandler
{
    [Header("Key")]
    [SerializeField]
    private QuickKey quickKey;

    [Header("UI")]
    [SerializeField]
    private Image iconImage;

    [SerializeField]
    private GameObject keyLabel;

    private QuickSlotSettingUI settingUI;

    public QuickKey Key => quickKey;

    private void Awake()
    {
        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }
    }

    // 클릭 이벤트를 전달할 퀵슬롯 설정 UI 연결
    public void Initialize(
        QuickSlotSettingUI quickSlotSettingUI)
    {
        settingUI = quickSlotSettingUI;
    }

    // 현재 바인딩된 아이콘 표시
    public void Refresh(
        Sprite icon,
        bool hideIcon = false)
    {
        bool shouldShow =
            icon != null &&
            !hideIcon;

        iconImage.sprite = icon;
        SetIconAlpha(shouldShow ? 1f : 0f);

        if (keyLabel != null)
        {
            keyLabel.SetActive(shouldShow);
        }
    }

    public void Clear()
    {
        iconImage.sprite = null;
        SetIconAlpha(0f);

        if (keyLabel != null)
        {
            keyLabel.SetActive(false);
        }
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (eventData.button !=
            PointerEventData.InputButton.Left)
        {
            return;
        }

        if (settingUI == null)
        {
            Debug.LogWarning(
                $"{name} 슬롯에 " +
                $"{nameof(QuickSlotSettingUI)}가 " +
                $"연결되지 않았습니다.",
                this
            );

            return;
        }

        settingUI.OnKeySlotClicked(quickKey);
    }

    private void SetIconAlpha(
        float alpha)
    {
        Color color =
            iconImage.color;

        color.a =
            alpha;

        iconImage.color =
            color;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }
    }
#endif
}