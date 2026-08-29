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

    private QuickSlotBinding binding =
        QuickSlotBinding.Empty();

    private BasicActionData basicActionData;

    public bool IsEmpty =>
        binding.IsEmpty;

    public QuickSlotBinding Binding =>
        binding;

    public BasicActionData BasicActionData =>
        basicActionData;

    private void Awake()
    {
        iconImage =
            GetComponent<Image>();

        Clear();
    }

    /// <summary>
    /// 기본 제공 스킬을 슬롯에 표시합니다.
    /// </summary>
    public void SetSkill(
        SkillId skillId,
        Sprite icon,
        QuickSlotSettingUI owner)
    {
        if (skillId == SkillId.None ||
            icon == null ||
            owner == null)
        {
            Clear();
            return;
        }

        binding =
            QuickSlotBinding.FromSkill(
                skillId
            );

        basicActionData = null;
        settingUI = owner;

        SetIcon(
            icon,
            true
        );
    }

    /// <summary>
    /// 기본 기능을 슬롯에 표시합니다.
    /// </summary>
    public void SetBasicAction(
        BasicActionData actionData,
        QuickSlotSettingUI owner)
    {
        if (actionData == null ||
            actionData.ActionId ==
                BasicActionId.None ||
            actionData.Icon == null ||
            actionData.Command == null ||
            owner == null)
        {
            Clear();
            return;
        }

        binding =
            QuickSlotBinding.FromBasicAction(
                actionData.ActionId
            );

        basicActionData =
            actionData;

        settingUI =
            owner;

        SetIcon(
            actionData.Icon,
            true
        );
    }

    public void Clear()
    {
        binding =
            QuickSlotBinding.Empty();

        basicActionData = null;
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

    /// <summary>
    /// 아이콘을 집고 있는 동안
    /// 원래 팔레트 슬롯의 표시를 숨깁니다.
    /// </summary>
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
            settingUI == null)
        {
            return;
        }

        settingUI.PickDefaultAction(
            binding,
            basicActionData,
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