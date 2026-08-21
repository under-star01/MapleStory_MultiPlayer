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

    /// <summary>
    /// 이 슬롯이 클릭 결과를 전달할
    /// 단축키 설정 UI를 연결합니다.
    /// </summary>
    public void Initialize(
        QuickSlotSettingUI quickSlotSettingUI)
    {
        settingUI = quickSlotSettingUI;
    }

    /// <summary>
    /// 현재 키에 등록된 기능의 아이콘을 표시합니다.
    ///
    /// hideIcon이 true이면 아이콘을 집고 있는 동안
    /// 원래 슬롯의 표시만 잠시 숨깁니다.
    /// </summary>
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

    /// <summary>
    /// 슬롯을 빈 상태로 표시합니다.
    /// </summary>
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

    private void SetIconAlpha(float alpha)
    {
        Color color = iconImage.color;
        color.a = alpha;
        iconImage.color = color;
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