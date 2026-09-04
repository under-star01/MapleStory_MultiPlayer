using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class DefaultActionSlotUI :
    MonoBehaviour,
    IPointerClickHandler
{
    private Image iconImage;
    private QuickSlotSettingUI settingUI;

    private BasicActionId actionId =
        BasicActionId.None;

    public bool IsEmpty =>
        actionId == BasicActionId.None;

    public BasicActionId ActionId =>
        actionId;

    private void Awake()
    {
        iconImage =
            GetComponent<Image>();

        Clear();
    }

    // BasicAction 정보와 아이콘 설정
    public void SetBasicAction(
        BasicActionId newActionId,
        Sprite icon,
        QuickSlotSettingUI owner)
    {
        if (newActionId ==
                BasicActionId.None ||
            icon == null ||
            owner == null)
        {
            Clear();
            return;
        }

        actionId =
            newActionId;

        settingUI =
            owner;

        SetIcon(
            icon,
            true
        );
    }

    public void Clear()
    {
        actionId =
            BasicActionId.None;

        settingUI = null;

        if (iconImage == null)
        {
            iconImage =
                GetComponent<Image>();
        }

        SetIcon(
            null,
            false
        );
    }

    // 아이콘 선택 중 원본 팔레트 아이콘 숨김 처리
    public void SetIconVisible(
        bool visible)
    {
        if (iconImage == null)
            return;

        Color color =
            iconImage.color;

        color.a =
            visible &&
            iconImage.sprite != null
                ? 1f
                : 0f;

        iconImage.color =
            color;
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (eventData.button !=
                PointerEventData.InputButton.Left ||
            IsEmpty ||
            settingUI == null ||
            iconImage == null ||
            iconImage.sprite == null)
        {
            return;
        }

        settingUI.PickDefaultAction(
            actionId,
            iconImage.sprite,
            this
        );
    }

    private void SetIcon(
        Sprite icon,
        bool visible)
    {
        if (iconImage == null)
            return;

        iconImage.sprite =
            icon;

        SetIconVisible(
            visible
        );
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (iconImage == null)
        {
            iconImage =
                GetComponent<Image>();
        }
    }
#endif
}