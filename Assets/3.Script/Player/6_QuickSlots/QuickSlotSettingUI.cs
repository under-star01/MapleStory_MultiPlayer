using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class QuickSlotSettingUI :
    MonoBehaviour,
    ILocalPlayerUI
{
    [Header("References")]
    [SerializeField]
    private BasicActionPaletteUI basicActionPaletteUI;

    [SerializeField]
    private Transform keyboard;

    [SerializeField]
    private Image pickedIcon;

    [Header("Window")]
    [SerializeField]
    private bool startOpened;

    private PlayerQuickSlotController quickSlotController;
    private CanvasGroup canvasGroup;

    private bool isBindingsSubscribed;

    private readonly Dictionary<QuickKey, QuickKeySlotUI>
        keySlots = new();

    private QuickSlotBinding pickedBinding =
        QuickSlotBinding.Empty();

    private BasicActionData pickedBasicActionData;

    // BasicActionPalette에서 집은 경우
    private BasicActionSlotUI pickedPaletteSourceSlot;

    // 키보드 슬롯에서 집은 경우
    private QuickKey? pickedSourceKey;

    public bool IsOpened { get; private set; }

    private bool IsPicking =>
        !pickedBinding.IsEmpty;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        CollectKeySlots();
        HidePickedIcon();
        SetWindowVisible(startOpened);
    }

    private void OnEnable()
    {
        SubscribeBindingsChanged();
        RefreshAllSlots();
    }

    private void OnDisable()
    {
        UnsubscribeBindingsChanged();
    }

    private void Update()
    {
        FollowMouse();
    }

    /// <summary>
    /// 현재 클라이언트의 로컬 플레이어를 연결합니다.
    /// </summary>
    public void Bind(LocalPlayerContext context)
    {
        if (context == null)
        {
            Debug.LogError(
                $"{nameof(LocalPlayerContext)}가 null입니다.",
                this
            );

            return;
        }

        if (context.QuickSlot == null)
        {
            Debug.LogError(
                $"{nameof(PlayerQuickSlotController)}를 " +
                "찾지 못했습니다.",
                context.Player
            );

            return;
        }

        if (quickSlotController == context.QuickSlot)
        {
            /*
             * 동일한 플레이어가 다시 연결되더라도
             * 하위 팔레트가 연결되지 않았을 수 있으므로
             * Bind를 다시 요청합니다.
             */
            if (basicActionPaletteUI != null)
            {
                basicActionPaletteUI.Bind(
                    quickSlotController
                );
            }

            SubscribeBindingsChanged();
            RefreshAllSlots();
            return;
        }

        Unbind();

        quickSlotController =
            context.QuickSlot;

        /*
         * 로컬 플레이어의 퀵슬롯 컨트롤러를
         * BasicActionPaletteUI에도 전달합니다.
         */
        if (basicActionPaletteUI != null)
        {
            basicActionPaletteUI.Bind(
                quickSlotController
            );
        }
        else
        {
            Debug.LogError(
                $"{nameof(BasicActionPaletteUI)}가 " +
                "연결되지 않았습니다.",
                this
            );
        }

        SubscribeBindingsChanged();
        RefreshAllSlots();
    }

    /// <summary>
    /// 현재 플레이어와 연결된 이벤트와 참조를 정리합니다.
    /// </summary>
    public void Unbind()
    {
        UnsubscribeBindingsChanged();

        ClearPickedState(true);

        if (basicActionPaletteUI != null)
        {
            basicActionPaletteUI.Unbind();
        }

        quickSlotController = null;

        ClearAllSlots();
    }

    public void OnKeySlotClicked(
        QuickKey clickedKey)
    {
        if (quickSlotController == null)
            return;

        if (IsPicking)
        {
            PlaceOnKey(clickedKey);
        }
        else
        {
            PickFromKey(clickedKey);
        }
    }

    /// <summary>
    /// 스킬 팔레트에서 스킬을 집습니다.
    /// </summary>
    public void PickSkill(SkillId skillId)
    {
        if (quickSlotController == null)
            return;

        QuickSlotBinding binding =
            QuickSlotBinding.FromSkill(skillId);

        if (binding.IsEmpty)
            return;

        if (!quickSlotController.TryGetSkillIcon(
                skillId,
                out Sprite icon))
        {
            return;
        }

        CancelCurrentPick();

        pickedBinding = binding;
        pickedBasicActionData = null;
        pickedPaletteSourceSlot = null;
        pickedSourceKey = null;

        ShowPickedIcon(icon);
    }

    /// <summary>
    /// 기본 기능 팔레트에서 기능을 집습니다.
    /// </summary>
    public void PickBasicAction(
        BasicActionData actionData,
        BasicActionSlotUI sourceSlot)
    {
        if (actionData == null ||
            actionData.ActionId == BasicActionId.None ||
            actionData.Icon == null ||
            actionData.Command == null ||
            sourceSlot == null)
        {
            return;
        }

        CancelCurrentPick();

        pickedBinding =
            QuickSlotBinding.FromBasicAction(
                actionData.ActionId
            );

        pickedBasicActionData = actionData;
        pickedPaletteSourceSlot = sourceSlot;
        pickedSourceKey = null;

        sourceSlot.SetIconVisible(false);
        ShowPickedIcon(actionData.Icon);
    }

    /// <summary>
    /// 빈 공간을 클릭했을 때 선택한 기능을 해제합니다.
    /// 키에서 집은 기본 기능은 팔레트로 되돌립니다.
    /// </summary>
    public void OnEmptyAreaClicked()
    {
        if (!IsPicking ||
            quickSlotController == null)
        {
            return;
        }

        if (pickedSourceKey.HasValue)
        {
            bool success =
                quickSlotController.ClearSlot(
                    pickedSourceKey.Value,
                    out BasicActionData removedActionData
                );

            if (!success)
                return;

            if (removedActionData != null)
            {
                RestoreBasicAction(
                    removedActionData
                );
            }

            ClearPickedState(false);
            RefreshAllSlots();
            return;
        }

        CancelPick();
    }

    public void CancelPick()
    {
        ClearPickedState(true);
        RefreshAllSlots();
    }

    public void Open()
    {
        SetWindowVisible(true);
        RefreshAllSlots();
    }

    public void Close()
    {
        CancelPick();
        SetWindowVisible(false);
    }

    public void Toggle()
    {
        if (IsOpened)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void RefreshAllSlots()
    {
        if (quickSlotController == null)
        {
            ClearAllSlots();
            return;
        }

        foreach (KeyValuePair<QuickKey, QuickKeySlotUI>
                 pair in keySlots)
        {
            QuickKey key = pair.Key;
            QuickKeySlotUI slotUI = pair.Value;

            if (!quickSlotController.TryGetBinding(
                    key,
                    out QuickSlotBinding binding))
            {
                slotUI.Clear();
                continue;
            }

            Sprite icon =
                GetBindingIcon(
                    key,
                    binding
                );

            if (icon == null)
            {
                slotUI.Clear();
                continue;
            }

            bool hideIcon =
                pickedSourceKey.HasValue &&
                pickedSourceKey.Value == key;

            slotUI.Refresh(
                icon,
                hideIcon
            );
        }
    }

    private void SubscribeBindingsChanged()
    {
        if (!isActiveAndEnabled ||
            isBindingsSubscribed ||
            quickSlotController == null)
        {
            return;
        }

        quickSlotController.BindingsChanged +=
            RefreshAllSlots;

        isBindingsSubscribed = true;
    }

    private void UnsubscribeBindingsChanged()
    {
        if (!isBindingsSubscribed)
            return;

        if (quickSlotController != null)
        {
            quickSlotController.BindingsChanged -=
                RefreshAllSlots;
        }

        isBindingsSubscribed = false;
    }

    private void PickFromKey(QuickKey key)
    {
        if (quickSlotController == null)
            return;

        if (!quickSlotController.TryGetBinding(
                key,
                out QuickSlotBinding binding))
        {
            return;
        }

        Sprite icon =
            GetBindingIcon(
                key,
                binding
            );

        if (icon == null)
            return;

        CancelCurrentPick();

        pickedBinding = binding;
        pickedSourceKey = key;
        pickedPaletteSourceSlot = null;

        if (binding.Type ==
            QuickSlotBindingType.BasicAction)
        {
            if (!quickSlotController
                    .TryGetBoundBasicActionData(
                        key,
                        out pickedBasicActionData))
            {
                ClearPickedState(false);
                return;
            }
        }
        else
        {
            pickedBasicActionData = null;
        }

        ShowPickedIcon(icon);
        RefreshAllSlots();
    }

    private void PlaceOnKey(QuickKey targetKey)
    {
        if (quickSlotController == null)
            return;

        bool success;
        BasicActionData displacedActionData = null;

        if (pickedSourceKey.HasValue)
        {
            QuickKey sourceKey =
                pickedSourceKey.Value;

            if (sourceKey == targetKey)
            {
                CancelPick();
                return;
            }

            success =
                quickSlotController.MoveOrSwap(
                    sourceKey,
                    targetKey
                );
        }
        else
        {
            success =
                BindPickedToKey(
                    targetKey,
                    out displacedActionData
                );
        }

        if (!success)
            return;

        if (pickedPaletteSourceSlot != null)
        {
            pickedPaletteSourceSlot.Clear();
        }

        if (displacedActionData != null)
        {
            RestoreBasicAction(
                displacedActionData
            );
        }

        ClearPickedState(false);
        RefreshAllSlots();
    }

    private bool BindPickedToKey(
        QuickKey targetKey,
        out BasicActionData displacedActionData)
    {
        displacedActionData = null;

        if (quickSlotController == null)
            return false;

        quickSlotController
            .TryGetBoundBasicActionData(
                targetKey,
                out displacedActionData
            );

        switch (pickedBinding.Type)
        {
            case QuickSlotBindingType.Skill:
                return quickSlotController.BindSkill(
                    targetKey,
                    pickedBinding.SkillId
                );

            case QuickSlotBindingType.BasicAction:
                return quickSlotController.BindBasicAction(
                    targetKey,
                    pickedBasicActionData
                );

            default:
                displacedActionData = null;
                return false;
        }
    }

    private Sprite GetBindingIcon(
        QuickKey key,
        QuickSlotBinding binding)
    {
        if (quickSlotController == null)
            return null;

        switch (binding.Type)
        {
            case QuickSlotBindingType.Skill:
                if (quickSlotController.TryGetSkillIcon(
                        binding.SkillId,
                        out Sprite skillIcon))
                {
                    return skillIcon;
                }

                return null;

            case QuickSlotBindingType.BasicAction:
                if (quickSlotController
                    .TryGetBoundBasicActionData(
                        key,
                        out BasicActionData actionData))
                {
                    return actionData.Icon;
                }

                return null;

            default:
                return null;
        }
    }

    private void RestoreBasicAction(
        BasicActionData actionData)
    {
        if (actionData == null ||
            basicActionPaletteUI == null)
        {
            return;
        }

        basicActionPaletteUI.RestoreAction(
            actionData
        );
    }

    private void CancelCurrentPick()
    {
        if (IsPicking)
        {
            ClearPickedState(true);
        }
    }

    private void ClearPickedState(
        bool restorePaletteSlot)
    {
        if (restorePaletteSlot &&
            pickedPaletteSourceSlot != null)
        {
            pickedPaletteSourceSlot
                .SetIconVisible(true);
        }

        pickedBinding =
            QuickSlotBinding.Empty();

        pickedBasicActionData = null;
        pickedPaletteSourceSlot = null;
        pickedSourceKey = null;

        HidePickedIcon();
    }

    private void ClearAllSlots()
    {
        foreach (QuickKeySlotUI slotUI
                 in keySlots.Values)
        {
            slotUI.Clear();
        }
    }

    private void SetWindowVisible(bool visible)
    {
        IsOpened = visible;

        canvasGroup.alpha =
            visible ? 1f : 0f;

        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void CollectKeySlots()
    {
        keySlots.Clear();

        if (keyboard == null)
        {
            Debug.LogError(
                "Keyboard가 연결되지 않았습니다.",
                this
            );

            return;
        }

        QuickKeySlotUI[] slots =
            keyboard.GetComponentsInChildren
                <QuickKeySlotUI>(true);

        foreach (QuickKeySlotUI slot in slots)
        {
            if (keySlots.ContainsKey(slot.Key))
            {
                Debug.LogWarning(
                    $"중복된 키 슬롯입니다: {slot.Key}",
                    slot
                );

                continue;
            }

            keySlots.Add(
                slot.Key,
                slot
            );

            slot.Initialize(this);
        }
    }

    private void ShowPickedIcon(Sprite icon)
    {
        if (pickedIcon == null)
            return;

        pickedIcon.sprite = icon;
        SetImageAlpha(pickedIcon, 1f);
        pickedIcon.gameObject.SetActive(true);
    }

    private void HidePickedIcon()
    {
        if (pickedIcon == null)
            return;

        pickedIcon.sprite = null;
        SetImageAlpha(pickedIcon, 0f);
        pickedIcon.gameObject.SetActive(false);
    }

    private void FollowMouse()
    {
        if (!IsPicking ||
            pickedIcon == null ||
            Mouse.current == null)
        {
            return;
        }

        pickedIcon.transform.position =
            Mouse.current.position.ReadValue();
    }

    private void SetImageAlpha(
        Image image,
        float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}