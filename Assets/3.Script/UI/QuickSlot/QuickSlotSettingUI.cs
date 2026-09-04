using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class QuickSlotSettingUI : MonoBehaviour, ILocalPlayerUI
{
    [Header("References")]
    [SerializeField] private DefaultActionPaletteUI defaultActionPaletteUI;
    [SerializeField] private Transform keyboard;
    [SerializeField] private Image pickedIcon;

    [Header("Window")]
    [SerializeField] private bool startOpened;

    private readonly Dictionary<QuickKey, QuickKeySlotUI>
        keySlots = new();

    private PlayerQuickSlotController quickSlotController;
    private CanvasGroup canvasGroup;

    private QuickSlotBinding pickedBinding =
        QuickSlotBinding.Empty();

    private DefaultActionSlotUI pickedPaletteSourceSlot;
    private QuickKey? pickedSourceKey;

    private bool isBindingsSubscribed;

    public bool IsOpened { get; private set; }

    private bool IsPicking =>
        !pickedBinding.IsEmpty;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

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

    public void Bind(LocalPlayerContext context)
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

        if (quickSlotController != context.QuickSlot)
        {
            Unbind();
            quickSlotController = context.QuickSlot;
        }

        defaultActionPaletteUI?.Bind(
            quickSlotController
        );

        // UI 연결 순서와 관계없이 DB 바인딩 수신 처리
        quickSlotController.LoadedBindingsReceived -=
            OnLoadedBindingsReceived;

        quickSlotController.LoadedBindingsReceived +=
            OnLoadedBindingsReceived;

        if (quickSlotController.HasReceivedLoadedBindings)
            ApplyLoadedBindings();

        SubscribeBindingsChanged();
        RefreshAllSlots();
    }

    public void Unbind()
    {
        UnsubscribeBindingsChanged();
        ClearPickedState(true);

        defaultActionPaletteUI?.Unbind();

        if (quickSlotController != null)
        {
            quickSlotController.LoadedBindingsReceived -=
                OnLoadedBindingsReceived;
        }

        quickSlotController = null;

        ClearAllSlots();
    }

    private void OnLoadedBindingsReceived()
    {
        ApplyLoadedBindings();
        RefreshAllSlots();
    }

    // DB 바인딩 데이터를 실제 퀵슬롯에 복원
    private void ApplyLoadedBindings()
    {
        if (quickSlotController == null)
            return;

        bool hasSavedBindings =
            quickSlotController.LoadedBindings.Count > 0;

        foreach (PlayerQuickSlotLoadData data
                 in quickSlotController.LoadedBindings)
        {
            if (!Enum.IsDefined(
                    typeof(QuickKey),
                    data.QuickKey) ||
                !Enum.IsDefined(
                    typeof(QuickSlotBindingType),
                    data.BindingType))
            {
                continue;
            }

            QuickKey key =
                (QuickKey)data.QuickKey;

            QuickSlotBindingType type =
                (QuickSlotBindingType)data.BindingType;

            switch (type)
            {
                case QuickSlotBindingType.Skill:
                    quickSlotController.BindSkill(
                        key,
                        (SkillId)data.TargetId
                    );
                    break;

                case QuickSlotBindingType.Consumable:
                    quickSlotController.BindConsumable(
                        key,
                        (ConsumableId)data.TargetId
                    );
                    break;

                case QuickSlotBindingType.BasicAction:
                    quickSlotController.BindBasicAction(
                        key,
                        (BasicActionId)data.TargetId
                    );
                    break;
            }
        }

        quickSlotController.CompleteLoadedBindings();

        // 저장 데이터가 없을 때만 기본 키 배치
        defaultActionPaletteUI?.InitializePalette(
            !hasSavedBindings
        );
    }

    public void OnKeySlotClicked(
        QuickKey clickedKey)
    {
        if (quickSlotController == null)
            return;

        if (IsPicking)
            PlaceOnKey(clickedKey);
        else
            PickFromKey(clickedKey);
    }

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

    public void PickDefaultAction(
        BasicActionId actionId,
        Sprite icon,
        DefaultActionSlotUI sourceSlot)
    {
        if (!IsOpened ||
            actionId == BasicActionId.None ||
            icon == null ||
            sourceSlot == null ||
            !BeginPick(
                QuickSlotBinding.FromBasicAction(
                    actionId
                ),
                icon))
        {
            return;
        }

        pickedPaletteSourceSlot =
            sourceSlot;

        sourceSlot.SetIconVisible(
            false
        );
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

    // 키에서 집은 항목을 빈 공간에 놓으면 바인딩 제거
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

        if (!quickSlotController.ClearSlot(
                sourceKey))
        {
            return;
        }

        RestoreDefaultAction(
            removedBinding
        );

        FinishPick();
    }

    public void CancelPick()
    {
        if (!IsPicking)
            return;

        AudioManager.Instance?.PlayEffect(
            EffectSoundId.DragEnd
        );

        ClearPickedState(true);
        RefreshAllSlots();
    }

    public void Open()
    {
        AudioManager.Instance?.PlayEffect(
            EffectSoundId.UIOpen
        );

        SetWindowVisible(true);
        RefreshAllSlots();
    }

    public void Close()
    {
        AudioManager.Instance?.PlayEffect(
            EffectSoundId.UIClose
        );

        CancelPick();
        SetWindowVisible(false);
    }

    public void Toggle()
    {
        if (IsOpened)
            Close();
        else
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
                    binding,
                    out Sprite icon
                ))
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
                binding,
                out Sprite icon
            ) ||
            !BeginPick(
                binding,
                icon))
        {
            return;
        }

        pickedSourceKey = key;

        RefreshAllSlots();
    }

    private void PlaceOnKey(
        QuickKey targetKey)
    {
        if (pickedSourceKey.HasValue)
            MoveOrSwap(targetKey);
        else
            BindFromPalette(targetKey);
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

        if (quickSlotController.MoveOrSwap(
                sourceKey,
                targetKey))
        {
            FinishPick();
        }
    }

    private void BindFromPalette(
        QuickKey targetKey)
    {
        quickSlotController.TryGetBinding(
            targetKey,
            out QuickSlotBinding displacedBinding
        );

        if (!BindPickedToKey(
                targetKey))
        {
            return;
        }

        pickedPaletteSourceSlot?.Clear();

        RestoreDefaultAction(
            displacedBinding
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
                    pickedBinding.BasicActionId
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
                return quickSlotController.TryGetBasicActionIcon(
                    binding.BasicActionId,
                    out icon
                );

            case QuickSlotBindingType.Consumable:
                return quickSlotController.TryGetConsumableIcon(
                    binding.ConsumableId,
                    out icon
                );

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

        pickedBinding = binding;
        SetPickedIcon(icon);

        AudioManager.Instance?.PlayEffect(
            EffectSoundId.DragStart
        );

        return true;
    }

    // 제거된 기본 행동을 팔레트에 복원
    private void RestoreDefaultAction(
        QuickSlotBinding binding)
    {
        if (binding.Type !=
            QuickSlotBindingType.BasicAction)
        {
            return;
        }

        defaultActionPaletteUI?.RestoreAction(
            binding
        );
    }

    private void FinishPick()
    {
        AudioManager.Instance?.PlayEffect(
            EffectSoundId.DragEnd
        );

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

        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void SetPickedIcon(
        Sprite icon)
    {
        if (pickedIcon == null)
            return;

        pickedIcon.sprite = icon;
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