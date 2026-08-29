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

    private readonly Dictionary<QuickKey, QuickKeySlotUI>
        keySlots = new();

    private PlayerQuickSlotController quickSlotController;
    private CanvasGroup canvasGroup;

    private QuickSlotBinding pickedBinding =
        QuickSlotBinding.Empty();

    private BasicActionData pickedBasicActionData;
    private BasicActionSlotUI pickedPaletteSourceSlot;
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
        if (context == null ||
            context.QuickSlot == null)
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

        BindBasicActionPalette();
        SubscribeBindingsChanged();
        RefreshAllSlots();
    }

    public void Unbind()
    {
        UnsubscribeBindingsChanged();
        ClearPickedState(true);

        basicActionPaletteUI?.Unbind();

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
            return;
        }

        PickFromKey(clickedKey);
    }

    /// <summary>
    /// 스킬 팔레트에서 스킬을 집습니다.
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
    /// 인벤토리에서 소비 아이템을 집습니다.
    /// 퀵슬롯 설정 창이 열려 있을 때만 가능합니다.
    /// </summary>
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

    /// <summary>
    /// 기본 기능 팔레트에서 기능을 집습니다.
    /// </summary>
    public void PickBasicAction(
        BasicActionData actionData,
        BasicActionSlotUI sourceSlot)
    {
        if (!IsOpened ||
            actionData == null ||
            actionData.ActionId ==
                BasicActionId.None ||
            actionData.Icon == null ||
            actionData.Command == null ||
            sourceSlot == null)
        {
            return;
        }

        if (!BeginPick(
                QuickSlotBinding.FromBasicAction(
                    actionData.ActionId
                ),
                actionData.Icon))
        {
            return;
        }

        pickedBasicActionData =
            actionData;

        pickedPaletteSourceSlot =
            sourceSlot;

        sourceSlot.SetIconVisible(false);
    }

    /// <summary>
    /// 빈 공간을 클릭하면 현재 선택을 취소합니다.
    /// 키 슬롯에서 집은 경우 해당 바인딩을 제거합니다.
    /// </summary>
    public void OnEmptyAreaClicked()
    {
        if (!IsPicking ||
            quickSlotController == null)
        {
            return;
        }

        /*
         * 팔레트나 인벤토리에서 집은 경우에는
         * 원래 퀵슬롯 바인딩이 없으므로 선택만 취소합니다.
         */
        if (!pickedSourceKey.HasValue)
        {
            CancelPick();
            return;
        }

        if (!quickSlotController.ClearSlot(
                pickedSourceKey.Value,
                out BasicActionData removedActionData))
        {
            return;
        }

        RestoreBasicAction(
            removedActionData
        );

        ClearPickedState(false);
        RefreshAllSlots();
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
            return;
        }

        Open();
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
                out Sprite icon))
        {
            return;
        }

        if (!BeginPick(
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
        bool success;

        BasicActionData displacedActionData =
            null;

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

        pickedPaletteSourceSlot?.Clear();

        RestoreBasicAction(
            displacedActionData
        );

        ClearPickedState(false);
        RefreshAllSlots();
    }

    private bool BindPickedToKey(
        QuickKey targetKey,
        out BasicActionData displacedActionData)
    {
        displacedActionData = null;

        /*
         * 대상 키에 기본 기능이 있다면,
         * 덮어쓴 뒤 팔레트로 복구하기 위해 미리 가져옵니다.
         */
        quickSlotController
            .TryGetBoundBasicActionData(
                targetKey,
                out displacedActionData
            );

        bool success;

        switch (pickedBinding.Type)
        {
            case QuickSlotBindingType.Skill:
                success =
                    quickSlotController.BindSkill(
                        targetKey,
                        pickedBinding.SkillId
                    );
                break;

            case QuickSlotBindingType.BasicAction:
                success =
                    quickSlotController.BindBasicAction(
                        targetKey,
                        pickedBasicActionData
                    );
                break;

            case QuickSlotBindingType.Consumable:
                success =
                    quickSlotController.BindConsumable(
                        targetKey,
                        pickedBinding.ConsumableId
                    );
                break;

            default:
                success = false;
                break;
        }

        if (!success)
        {
            displacedActionData = null;
        }

        return success;
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
                return quickSlotController
                    .TryGetSkillIcon(
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

    /// <summary>
    /// 현재 선택을 정리한 뒤 새로운 바인딩을 집습니다.
    /// </summary>
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

    private void RestoreBasicAction(
        BasicActionData actionData)
    {
        if (actionData == null)
            return;

        basicActionPaletteUI
            ?.RestoreAction(actionData);
    }

    private void BindBasicActionPalette()
    {
        if (basicActionPaletteUI == null)
        {
            Debug.LogError(
                $"{nameof(BasicActionPaletteUI)}가 " +
                "연결되지 않았습니다.",
                this
            );

            return;
        }

        basicActionPaletteUI.Bind(
            quickSlotController
        );
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

        QuickKeySlotUI[] foundSlots =
            keyboard.GetComponentsInChildren
                <QuickKeySlotUI>(true);

        foreach (QuickKeySlotUI slot
                 in foundSlots)
        {
            if (!keySlots.TryAdd(
                    slot.Key,
                    slot))
            {
                Debug.LogWarning(
                    $"중복된 키 슬롯입니다: " +
                    $"{slot.Key}",
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
        IsOpened =
            visible;

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

        bool visible =
            icon != null;

        pickedIcon.sprite =
            icon;

        pickedIcon.gameObject.SetActive(
            visible
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