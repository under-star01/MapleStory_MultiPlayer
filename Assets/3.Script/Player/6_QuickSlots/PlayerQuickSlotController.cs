using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(PlayerSkillController))]
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerBasicActionController))]
public class PlayerQuickSlotController : NetworkBehaviour
{
    private readonly Dictionary<QuickKey, QuickSlot>
        quickSlots = new();

    private PlayerQuickSlotLoadData[] loadedBindings =
        Array.Empty<PlayerQuickSlotLoadData>();

    private PlayerQuickSlotLoadData[] serverBindings =
        Array.Empty<PlayerQuickSlotLoadData>();

    private PlayerSkillController skillController;
    private PlayerInventory inventory;
    private PlayerBasicActionController basicActionController;

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

        basicActionController =
            GetComponent<PlayerBasicActionController>();

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
        QuickKey key)
    {
        if (!TryGetBinding(
                key,
                out QuickSlotBinding binding) ||
            binding.Type !=
                QuickSlotBindingType.BasicAction)
        {
            return false;
        }

        return basicActionController.Execute(
            binding.BasicActionId
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
        SkillId skillId)
    {
        if (!TryGetSlot(
                key,
                out QuickSlot slot) ||
            !skillController.TryGetSkill(
                skillId,
                out _))
        {
            return false;
        }

        slot.BindSkill(
            skillId
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
        BasicActionId actionId)
    {
        if (actionId == BasicActionId.None ||
            !TryGetSlot(
                key,
                out QuickSlot slot) ||
            !basicActionController.TryGetAction(
                actionId,
                out _))
        {
            return false;
        }

        slot.BindBasicAction(
            actionId
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
        QuickKey key)
    {
        if (!TryGetSlot(
                key,
                out QuickSlot slot) ||
            slot.IsEmpty)
        {
            return false;
        }

        slot.Clear();

        NotifyBindingsChanged();

        return true;
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

    public bool TryGetBasicActionIcon(
        BasicActionId actionId,
        out Sprite icon)
    {
        icon = null;

        if (!basicActionController.TryGetAction(
                actionId,
                out BasicActionData actionData))
        {
            return false;
        }

        icon =
            actionData.Icon;

        return icon != null;
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

    // 서버의 DB 바인딩 상태 초기화
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

    // DB 퀵슬롯 데이터를 소유 클라이언트에 전달
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

    // 서버 퀵슬롯 상태를 DB 저장용 Record로 변환
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

    // 바인딩 변경을 알리고 소유 클라이언트의 상태를 서버에 전달
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

                    TargetId =
                        targetId
                }
            );
        }

        return bindings.ToArray();
    }

    // 클라이언트의 현재 퀵슬롯 상태를 서버에 반영
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

            _ => 0 // null이면 0으로 반환
        };
    }
}