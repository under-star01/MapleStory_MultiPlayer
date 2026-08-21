using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class BasicActionSlotUI :
    MonoBehaviour,
    IPointerClickHandler
{
    private Image iconImage;

    private QuickSlotSettingUI quickSlotSettingUI;
    private BasicActionData actionData;

    public bool IsEmpty =>
        actionData == null;

    public BasicActionData ActionData =>
        actionData;

    private void Awake()
    {
        iconImage = GetComponent<Image>();
        Clear();
    }

    /// <summary>
    /// 이 슬롯에 기본 기능 데이터를 표시합니다.
    /// </summary>
    public void SetAction(
        BasicActionData newActionData,
        QuickSlotSettingUI settingUI)
    {
        if (newActionData == null ||
            newActionData.ActionId == BasicActionId.None ||
            newActionData.Icon == null ||
            newActionData.Command == null ||
            settingUI == null)
        {
            Clear();
            return;
        }

        actionData = newActionData;
        quickSlotSettingUI = settingUI;

        iconImage.sprite = actionData.Icon;
        SetIconAlpha(1f);
    }

    /// <summary>
    /// 슬롯을 빈 상태로 표시합니다.
    /// Image는 클릭 영역으로 사용하므로 비활성화하지 않습니다.
    /// </summary>
    public void Clear()
    {
        actionData = null;
        quickSlotSettingUI = null;

        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }

        iconImage.sprite = null;
        SetIconAlpha(0f);
    }

    /// <summary>
    /// 아이콘을 집는 동안 원래 슬롯의 표시만 숨깁니다.
    /// 실제 데이터는 성공적으로 배치될 때까지 유지합니다.
    /// </summary>
    public void SetIconVisible(bool visible)
    {
        if (IsEmpty)
            return;

        SetIconAlpha(visible ? 1f : 0f);
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (eventData.button !=
            PointerEventData.InputButton.Left)
        {
            return;
        }

        if (IsEmpty ||
            quickSlotSettingUI == null)
        {
            return;
        }

        quickSlotSettingUI.PickBasicAction(
            actionData,
            this
        );
    }

    private void SetIconAlpha(float alpha)
    {
        Color color = iconImage.color;
        color.a = alpha;
        iconImage.color = color;
    }
}