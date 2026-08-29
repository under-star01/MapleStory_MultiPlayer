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
    private DefaultActionPaletteUI defaultActionPaletteUI;

    [SerializeField]
    private Transform keyboard;

    [SerializeField]
    private Image pickedIcon;

    [Header("Window")]
    [SerializeField]
    private bool startOpened;

    private readonly Dictionary<QuickKey, QuickKeySlotUI>
        keySlots = new();

    private PlayerQuickSlotController quickSlotController;
    private CanvasGroup canvasGroup;

    private QuickSlotBinding pickedBinding =
        QuickSlotBinding.Empty();

    private BasicActionData pickedBasicActionData;
    private DefaultActionSlotUI pickedPaletteSourceSlot;
    private QuickKey? pickedSourceKey;

    private bool isBindingsSubscribed;

    public bool IsOpened { get; private set; }

    private bool IsPicking =>
        !pickedBinding.IsEmpty;

    private void Awake()
    {
        canvasGroup =
            GetComponent<CanvasGroup>();

        CollectKeySlots();
        SetPickedIcon(null);
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

    public void Bind(
        LocalPlayerContext context)
    {
        if (context?.QuickSlot == null)
        {
            Debug.LogError(
                $"{nameof(PlayerQuickSlotController)}를 " +
                "연결할 수 없습니다.",
                this
            );

            return;
        }

        if (quickSlotController !=
            context.QuickSlot)
        {
            Unbind();

            quickSlotController =
                context.QuickSlot;
        }

        defaultActionPaletteUI?.Bind(
            quickSlotController
        );

        SubscribeBindingsChanged();
        RefreshAllSlots();
    }

    public void Unbind()
    {
        UnsubscribeBindingsChanged();
        ClearPickedState(true);

        defaultActionPaletteUI?.Unbind();

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
    /// 일반 스킬창 등에서 스킬을 집습니다.
    /// </summary>
    public void PickSkill(
        SkillId skillId)
    {
        if (!IsOpened ||
            quickSlotController == null ||
            !quickSlotController.TryGetSkillIcon(
                skillId,
                out Sprite icon))
        {
            return;
        }

        BeginPick(
            QuickSlotBinding.FromSkill(
                skillId
            ),
            icon
        );
    }

    /// <summary>
    /// 기본 제공 팔레트에서 항목을 집습니다.
    /// </summary>
    public void PickDefaultAction(
        QuickSlotBinding binding,
        BasicActionData actionData,
        DefaultActionSlotUI sourceSlot)
    {
        if (!IsOpened ||
            sourceSlot == null ||
            !TryGetPickedIcon(
                binding,
                actionData,
                out Sprite icon) ||
            !BeginPick(
                binding,
                icon))
        {
            return;
        }

        pickedBasicActionData =
            actionData;

        pickedPaletteSourceSlot =
            sourceSlot;

        sourceSlot.SetIconVisible(false);
    }

    public void PickConsumable(
        ConsumableId consumableId)
    {
        if (!IsOpened ||
            quickSlotController == null ||
            !quickSlotController.TryGetConsumableIcon(
                consumableId,
                out Sprite icon))
        {
            return;
        }

        BeginPick(
            QuickSlotBinding.FromConsumable(
                consumableId
            ),
            icon
        );
    }

    public void OnEmptyAreaClicked()
    {
        if (!IsPicking ||
            quickSlotController == null)
        {
            return;
        }

        if (!pickedSourceKey.HasValue)
        {
            CancelPick();
            return;
        }

        QuickKey sourceKey =
            pickedSourceKey.Value;

        if (!quickSlotController.TryGetBinding(
                sourceKey,
                out QuickSlotBinding removedBinding))
        {
            return;
        }

        quickSlotController
            .TryGetBoundBasicActionData(
                sourceKey,
                out BasicActionData removedActionData
            );

        if (!quickSlotController.ClearSlot(
                sourceKey))
        {
            return;
        }

        RestoreDefaultAction(
            removedBinding,
            removedActionData
        );

        FinishPick();
    }

    public void CancelPick()
    {
        if (!IsPicking)
            return;

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
            if (!quickSlotController.TryGetBinding(
                    pair.Key,
                    out QuickSlotBinding binding) ||
                !TryGetBindingIcon(
                    pair.Key,
                    binding,
                    out Sprite icon))
            {
                pair.Value.Clear();
                continue;
            }

            pair.Value.Refresh(
                icon,
                pickedSourceKey == pair.Key
            );
        }
    }

    private void PickFromKey(
        QuickKey key)
    {
        if (!quickSlotController.TryGetBinding(
                key,
                out QuickSlotBinding binding) ||
            !TryGetBindingIcon(
                key,
                binding,
                out Sprite icon) ||
            !BeginPick(
                binding,
                icon))
        {
            return;
        }

        pickedSourceKey =
            key;

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

        RefreshAllSlots();
    }

    private void PlaceOnKey(
        QuickKey targetKey)
    {
        if (pickedSourceKey.HasValue)
        {
            MoveOrSwap(
                targetKey
            );

            return;
        }

        BindFromPalette(
            targetKey
        );
    }

    private void MoveOrSwap(
        QuickKey targetKey)
    {
        QuickKey sourceKey =
            pickedSourceKey.Value;

        if (sourceKey == targetKey)
        {
            CancelPick();
            return;
        }

        if (!quickSlotController.MoveOrSwap(
                sourceKey,
                targetKey))
        {
            return;
        }

        FinishPick();
    }

    private void BindFromPalette(
        QuickKey targetKey)
    {
        quickSlotController.TryGetBinding(
            targetKey,
            out QuickSlotBinding displacedBinding
        );

        quickSlotController
            .TryGetBoundBasicActionData(
                targetKey,
                out BasicActionData displacedActionData
            );

        if (!BindPickedToKey(
                targetKey))
        {
            return;
        }

        pickedPaletteSourceSlot?.Clear();

        RestoreDefaultAction(
            displacedBinding,
            displacedActionData
        );

        FinishPick();
    }

    private bool BindPickedToKey(
        QuickKey targetKey)
    {
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

            case QuickSlotBindingType.Consumable:
                return quickSlotController.BindConsumable(
                    targetKey,
                    pickedBinding.ConsumableId
                );

            default:
                return false;
        }
    }

    private bool TryGetBindingIcon(
        QuickKey key,
        QuickSlotBinding binding,
        out Sprite icon)
    {
        icon = null;

        switch (binding.Type)
        {
            case QuickSlotBindingType.Skill:
                return quickSlotController.TryGetSkillIcon(
                    binding.SkillId,
                    out icon
                );

            case QuickSlotBindingType.BasicAction:
                if (!quickSlotController
                        .TryGetBoundBasicActionData(
                            key,
                            out BasicActionData actionData))
                {
                    return false;
                }

                icon =
                    actionData.Icon;

                return icon != null;

            case QuickSlotBindingType.Consumable:
                return quickSlotController
                    .TryGetConsumableIcon(
                        binding.ConsumableId,
                        out icon
                    );

            default:
                return false;
        }
    }

    private bool TryGetPickedIcon(
        QuickSlotBinding binding,
        BasicActionData actionData,
        out Sprite icon)
    {
        icon = null;

        switch (binding.Type)
        {
            case QuickSlotBindingType.Skill:
                return quickSlotController != null &&
                       quickSlotController.TryGetSkillIcon(
                           binding.SkillId,
                           out icon
                       );

            case QuickSlotBindingType.BasicAction:
                icon =
                    actionData?.Icon;

                return icon != null;

            default:
                return false;
        }
    }

    private bool BeginPick(
        QuickSlotBinding binding,
        Sprite icon)
    {
        if (binding.IsEmpty ||
            icon == null)
        {
            return false;
        }

        ClearPickedState(true);

        pickedBinding =
            binding;

        SetPickedIcon(icon);

        return true;
    }

    private void RestoreDefaultAction(
        QuickSlotBinding binding,
        BasicActionData actionData)
    {
        if (binding.Type !=
                QuickSlotBindingType.Skill &&
            binding.Type !=
                QuickSlotBindingType.BasicAction)
        {
            return;
        }

        defaultActionPaletteUI?.RestoreAction(
            binding,
            actionData
        );
    }

    private void FinishPick()
    {
        ClearPickedState(false);
        RefreshAllSlots();
    }

    private void ClearPickedState(
        bool restorePaletteSlot)
    {
        if (restorePaletteSlot)
        {
            pickedPaletteSourceSlot
                ?.SetIconVisible(true);
        }

        pickedBinding =
            QuickSlotBinding.Empty();

        pickedBasicActionData = null;
        pickedPaletteSourceSlot = null;
        pickedSourceKey = null;

        SetPickedIcon(null);
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

        foreach (QuickKeySlotUI slot in
                 keyboard.GetComponentsInChildren
                     <QuickKeySlotUI>(true))
        {
            if (!keySlots.TryAdd(
                    slot.Key,
                    slot))
            {
                Debug.LogWarning(
                    $"중복된 키 슬롯입니다: {slot.Key}",
                    slot
                );

                continue;
            }

            slot.Initialize(this);
        }
    }

    private void ClearAllSlots()
    {
        foreach (QuickKeySlotUI slot
                 in keySlots.Values)
        {
            slot.Clear();
        }
    }

    private void SetWindowVisible(
        bool visible)
    {
        IsOpened = visible;

        canvasGroup.alpha =
            visible ? 1f : 0f;

        canvasGroup.interactable =
            visible;

        canvasGroup.blocksRaycasts =
            visible;
    }

    private void SetPickedIcon(
        Sprite icon)
    {
        if (pickedIcon == null)
            return;

        pickedIcon.sprite =
            icon;

        pickedIcon.gameObject.SetActive(
            icon != null
        );
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
}