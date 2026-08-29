using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerSkillController))]
[RequireComponent(typeof(PlayerInventory))]
public class PlayerQuickSlotController : MonoBehaviour
{
    [Serializable]
    private class SkillQuickSlotBinding
    {
        public QuickKey key;
        public SkillId skillId;
    }

    [Header("Default Skill Bindings")]
    [SerializeField]
    private List<SkillQuickSlotBinding>
        defaultSkillBindings = new();

    private readonly Dictionary<QuickKey, QuickSlot>
        quickSlots = new();

    private PlayerSkillController skillController;
    private PlayerInventory inventory;

    public event Action BindingsChanged;

    private void Awake()
    {
        skillController =
            GetComponent<PlayerSkillController>();

        inventory =
            GetComponent<PlayerInventory>();

        CreateSlots();
    }

    private void Start()
    {
        BindDefaultSkills();
    }

    /// <summary>
    /// 슬롯에 등록된 기본 기능을 실행합니다.
    /// 스킬과 소비 아이템은 서버 실행 경로를 따로 사용합니다.
    /// </summary>
    public bool ExecuteBasicAction(
        QuickKey key,
        Vector2 inputDirection)
    {
        if (!TryGetSlot(
                key,
                out QuickSlot slot))
        {
            return false;
        }

        return slot.ExecuteBasicAction(
            inputDirection
        );
    }

    public bool ExecuteSkill(
        SkillId skillId,
        Vector2 inputDirection)
    {
        if (!skillController.TryGetSkill(
                skillId,
                out PlayerSkillBase skill))
        {
            return false;
        }

        return skillController.TryExecute(
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
        if (!TryBindSkill(
                key,
                skillId))
        {
            return false;
        }

        NotifyBindingsChanged();
        return true;
    }

    public bool BindConsumable(
        QuickKey key,
        ConsumableId consumableId)
    {
        if (!TryBindConsumable(
                key,
                consumableId))
        {
            return false;
        }

        NotifyBindingsChanged();
        return true;
    }

    /// <summary>
    /// 기본 기능 데이터 전체를 지정한 키에 연결합니다.
    /// </summary>
    public bool BindBasicAction(
        QuickKey key,
        BasicActionData actionData)
    {
        if (actionData == null ||
            actionData.ActionId ==
                BasicActionId.None ||
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

    /// <summary>
    /// 지정한 키의 바인딩 정보를 조회합니다.
    /// </summary>
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

        binding = slot.Binding;
        return true;
    }

    public bool TryGetBoundSkillId(
        QuickKey key,
        out SkillId skillId)
    {
        skillId = SkillId.None;

        if (!TryGetBinding(
                key,
                out QuickSlotBinding binding) ||
            binding.Type !=
                QuickSlotBindingType.Skill)
        {
            return false;
        }

        skillId = binding.SkillId;
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
            binding.Type !=
                QuickSlotBindingType.Consumable)
        {
            return false;
        }

        consumableId =
            binding.ConsumableId;

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

        icon = skill.Icon;
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

        icon = data.Icon;
        return icon != null;
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

    /// <summary>
    /// 두 키 슬롯의 내용을 교환합니다.
    /// 대상 슬롯이 비어 있으면 이동처럼 동작합니다.
    /// </summary>
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

    /// <summary>
    /// 바인딩을 제거합니다.
    /// 기본 기능이면 팔레트로 복구할 데이터를 반환합니다.
    /// </summary>
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

    public bool IsSlotEmpty(
        QuickKey key)
    {
        return TryGetSlot(
                   key,
                   out QuickSlot slot) &&
               slot.IsEmpty;
    }

    private void CreateSlots()
    {
        quickSlots.Clear();

        foreach (QuickKey key in
                 Enum.GetValues(typeof(QuickKey)))
        {
            quickSlots.Add(
                key,
                new QuickSlot()
            );
        }
    }

    private void BindDefaultSkills()
    {
        foreach (SkillQuickSlotBinding binding
                 in defaultSkillBindings)
        {
            if (binding == null ||
                binding.skillId == SkillId.None)
            {
                continue;
            }

            if (!TryBindSkill(
                    binding.key,
                    binding.skillId))
            {
                Debug.LogWarning(
                    $"기본 단축키 등록 실패: " +
                    $"{binding.key} → {binding.skillId}",
                    this
                );
            }
        }

        NotifyBindingsChanged();
    }

    private bool TryBindSkill(
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

        return true;
    }

    private bool TryBindConsumable(
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

        return true;
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

    private void NotifyBindingsChanged()
    {
        BindingsChanged?.Invoke();
    }
}