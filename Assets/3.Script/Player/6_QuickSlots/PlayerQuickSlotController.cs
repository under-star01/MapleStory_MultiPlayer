using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(PlayerSkillController))]
[RequireComponent(typeof(PlayerInventory))]
public class PlayerQuickSlotController : NetworkBehaviour
{
    private readonly Dictionary<QuickKey, QuickSlot>
        quickSlots = new();

    // 클라이언트가 DB 바인딩을 실제 퀵슬롯에 복원할 때 사용합니다.
    private PlayerQuickSlotLoadData[] loadedBindings =
        Array.Empty<PlayerQuickSlotLoadData>();

    // 서버가 DB 저장 시 사용하는 최신 퀵슬롯 상태입니다.
    private PlayerQuickSlotLoadData[] serverBindings =
        Array.Empty<PlayerQuickSlotLoadData>();

    private PlayerSkillController skillController;
    private PlayerInventory inventory;

    // DB 데이터를 적용하는 동안 발생한 Bind 호출이
    // 다시 서버로 전송되는 것을 막습니다.
    private bool isApplyingLoadedBindings;
    private bool hasUnsavedChanges;

    public IReadOnlyList<PlayerQuickSlotLoadData>
        LoadedBindings => loadedBindings;

    public bool HasReceivedLoadedBindings
    {
        get;
        private set;
    }

    public bool HasUnsavedChanges =>
        hasUnsavedChanges;

    public event Action BindingsChanged;
    public event Action LoadedBindingsReceived;

    private void Awake()
    {
        skillController =
            GetComponent<PlayerSkillController>();

        inventory =
            GetComponent<PlayerInventory>();

        foreach (QuickKey key in
                 Enum.GetValues(typeof(QuickKey)))
        {
            quickSlots.Add(
                key,
                new QuickSlot()
            );
        }
    }

    public bool ExecuteBasicAction(
        QuickKey key,
        Vector2 inputDirection)
    {
        return TryGetSlot(
                   key,
                   out QuickSlot slot) &&
               slot.ExecuteBasicAction(
                   inputDirection
               );
    }

    public bool ExecuteSkill(
        SkillId skillId,
        Vector2 inputDirection)
    {
        return skillController.TryGetSkill(
                   skillId,
                   out PlayerSkillBase skill) &&
               skillController.TryExecute(
                   skill,
                   inputDirection
               );
    }

    public bool ExecuteConsumable(
        ConsumableId consumableId)
    {
        return inventory.TryUseConsumable(
            consumableId
        );
    }

    public bool BindSkill(
        QuickKey key,
        SkillId skillId,
        QuickSlotBindingSource source)
    {
        if ((source != QuickSlotBindingSource.SkillUI &&
             source != QuickSlotBindingSource.DefaultActionPalette) ||
            !TryGetSlot(
                key,
                out QuickSlot slot) ||
            !skillController.TryGetSkill(
                skillId,
                out _))
        {
            return false;
        }

        slot.BindSkill(
            skillId,
            source
        );

        NotifyBindingsChanged();
        return true;
    }

    public bool BindConsumable(
        QuickKey key,
        ConsumableId consumableId)
    {
        if (!TryGetSlot(
                key,
                out QuickSlot slot) ||
            !inventory.TryGetConsumableData(
                consumableId,
                out _))
        {
            return false;
        }

        slot.BindConsumable(
            consumableId
        );

        NotifyBindingsChanged();
        return true;
    }

    public bool BindBasicAction(
        QuickKey key,
        BasicActionData actionData)
    {
        if (actionData == null ||
            actionData.ActionId == BasicActionId.None ||
            actionData.Icon == null ||
            actionData.Command == null ||
            !TryGetSlot(
                key,
                out QuickSlot slot))
        {
            return false;
        }

        slot.BindBasicAction(
            actionData
        );

        NotifyBindingsChanged();
        return true;
    }

    public bool MoveOrSwap(
        QuickKey sourceKey,
        QuickKey targetKey)
    {
        if (sourceKey == targetKey)
            return true;

        if (!TryGetSlot(
                sourceKey,
                out QuickSlot sourceSlot) ||
            !TryGetSlot(
                targetKey,
                out QuickSlot targetSlot) ||
            sourceSlot.IsEmpty)
        {
            return false;
        }

        sourceSlot.SwapWith(
            targetSlot
        );

        NotifyBindingsChanged();
        return true;
    }

    public bool ClearSlot(
        QuickKey key,
        out BasicActionData removedActionData)
    {
        removedActionData = null;

        if (!TryGetSlot(
                key,
                out QuickSlot slot) ||
            slot.IsEmpty)
        {
            return false;
        }

        removedActionData =
            slot.BasicActionData;

        slot.Clear();

        NotifyBindingsChanged();
        return true;
    }

    public bool ClearSlot(
        QuickKey key)
    {
        return ClearSlot(
            key,
            out _
        );
    }

    public bool TryGetBinding(
        QuickKey key,
        out QuickSlotBinding binding)
    {
        binding =
            QuickSlotBinding.Empty();

        if (!TryGetSlot(
                key,
                out QuickSlot slot) ||
            slot.IsEmpty)
        {
            return false;
        }

        binding =
            slot.Binding;

        return true;
    }

    public bool TryGetBoundSkillId(
        QuickKey key,
        out SkillId skillId)
    {
        skillId =
            SkillId.None;

        if (!TryGetBinding(
                key,
                out QuickSlotBinding binding) ||
            binding.Type != QuickSlotBindingType.Skill)
        {
            return false;
        }

        skillId =
            binding.SkillId;

        return true;
    }

    public bool TryGetBoundConsumableId(
        QuickKey key,
        out ConsumableId consumableId)
    {
        consumableId =
            ConsumableId.None;

        if (!TryGetBinding(
                key,
                out QuickSlotBinding binding) ||
            binding.Type != QuickSlotBindingType.Consumable)
        {
            return false;
        }

        consumableId =
            binding.ConsumableId;

        return true;
    }

    public bool TryGetBoundBasicActionData(
        QuickKey key,
        out BasicActionData actionData)
    {
        actionData = null;

        if (!TryGetSlot(
                key,
                out QuickSlot slot) ||
            slot.Binding.Type !=
                QuickSlotBindingType.BasicAction ||
            slot.BasicActionData == null)
        {
            return false;
        }

        actionData =
            slot.BasicActionData;

        return true;
    }

    public bool TryGetSkillIcon(
        SkillId skillId,
        out Sprite icon)
    {
        icon = null;

        if (!skillController.TryGetSkill(
                skillId,
                out PlayerSkillBase skill))
        {
            return false;
        }

        icon =
            skill.Icon;

        return icon != null;
    }

    public bool TryGetConsumableIcon(
        ConsumableId consumableId,
        out Sprite icon)
    {
        icon = null;

        if (!inventory.TryGetConsumableData(
                consumableId,
                out ConsumableData data))
        {
            return false;
        }

        icon =
            data.Icon;

        return icon != null;
    }

    public bool IsSlotEmpty(
        QuickKey key)
    {
        return TryGetSlot(
                   key,
                   out QuickSlot slot) &&
               slot.IsEmpty;
    }

    [Server]
    public void InitializeServerBindings(
        IReadOnlyCollection<PlayerQuickSlotRecord>
            records)
    {
        if (records == null ||
            records.Count == 0)
        {
            serverBindings =
                Array.Empty<PlayerQuickSlotLoadData>();

            hasUnsavedChanges = false;
            return;
        }

        serverBindings =
            new PlayerQuickSlotLoadData[
                records.Count
            ];

        int index = 0;

        foreach (PlayerQuickSlotRecord record
                 in records)
        {
            serverBindings[index++] =
                new PlayerQuickSlotLoadData(
                    record
                );
        }

        hasUnsavedChanges = false;
    }

    // DB에서 불러온 퀵슬롯 데이터를 소유 클라이언트에 전달합니다.
    [TargetRpc]
    public void TargetLoadBindings(
        NetworkConnection target,
        PlayerQuickSlotLoadData[] records)
    {
        loadedBindings =
            records ??
            Array.Empty<PlayerQuickSlotLoadData>();

        HasReceivedLoadedBindings = true;
        isApplyingLoadedBindings = true;

        LoadedBindingsReceived?.Invoke();
    }

    public void CompleteLoadedBindings()
    {
        isApplyingLoadedBindings = false;
        hasUnsavedChanges = false;
    }

    // 서버가 보관 중인 최신 상태를 DB 저장용 Record로 변환합니다.
    [Server]
    public List<PlayerQuickSlotRecord>
        CreateSaveSnapshot()
    {
        List<PlayerQuickSlotRecord> records =
            new(serverBindings.Length);

        foreach (PlayerQuickSlotLoadData binding
                 in serverBindings)
        {
            records.Add(
                new PlayerQuickSlotRecord(
                    binding.QuickKey,
                    binding.BindingType,
                    binding.BindingSource,
                    binding.TargetId
                )
            );
        }

        return records;
    }

    [Server]
    public void MarkSaved()
    {
        hasUnsavedChanges = false;
    }

    private void NotifyBindingsChanged()
    {
        hasUnsavedChanges = true;

        BindingsChanged?.Invoke();

        if (isOwned &&
            !isApplyingLoadedBindings)
        {
            CmdUpdateBindings(
                CreateBindingSnapshot()
            );
        }
    }

    private PlayerQuickSlotLoadData[]
        CreateBindingSnapshot()
    {
        List<PlayerQuickSlotLoadData> bindings =
            new(quickSlots.Count);

        foreach (KeyValuePair<QuickKey, QuickSlot>
                 pair in quickSlots)
        {
            QuickSlotBinding binding =
                pair.Value.Binding;

            if (binding.IsEmpty)
                continue;

            int targetId =
                GetTargetId(binding);

            if (targetId <= 0)
                continue;

            bindings.Add(
                new PlayerQuickSlotLoadData
                {
                    QuickKey =
                        (int)pair.Key,

                    BindingType =
                        (int)binding.Type,

                    BindingSource =
                        (int)binding.Source,

                    TargetId =
                        targetId
                }
            );
        }

        return bindings.ToArray();
    }

    // 클라이언트의 현재 퀵슬롯 전체 상태를 서버에 전달합니다.
    [Command]
    private void CmdUpdateBindings(
        PlayerQuickSlotLoadData[] bindings)
    {
        serverBindings =
            bindings ??
            Array.Empty<PlayerQuickSlotLoadData>();

        hasUnsavedChanges = true;
    }

    private bool TryGetSlot(
        QuickKey key,
        out QuickSlot slot)
    {
        return quickSlots.TryGetValue(
            key,
            out slot
        );
    }

    private static int GetTargetId(
        QuickSlotBinding binding)
    {
        return binding.Type switch
        {
            QuickSlotBindingType.Skill =>
                (int)binding.SkillId,

            QuickSlotBindingType.BasicAction =>
                (int)binding.BasicActionId,

            QuickSlotBindingType.Consumable =>
                (int)binding.ConsumableId,

            _ => 0
        };
    }
}