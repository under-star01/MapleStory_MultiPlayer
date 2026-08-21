using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerSkillController))]
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

    public event Action BindingsChanged;

    private void Awake()
    {
        skillController =
            GetComponent<PlayerSkillController>();

        CreateSlots();
    }

    private void Start()
    {
        BindDefaultSkills();
    }

    public bool Execute(
        QuickKey key,
        Vector2 inputDirection)
    {
        if (!TryGetSlot(
                key,
                out QuickSlot slot))
        {
            return false;
        }

        return slot.Execute(inputDirection);
    }

    public bool BindSkill(
        QuickKey key,
        SkillId skillId)
    {
        if (!TryGetSlot(
                key,
                out QuickSlot slot))
        {
            return false;
        }

        if (!TryCreateSkillCommand(
                skillId,
                out IQuickSlotCommand command))
        {
            return false;
        }

        slot.BindSkill(
            skillId,
            command
        );

        NotifyBindingsChanged();
        return true;
    }

    /// <summary>
    /// 기본 기능 데이터 전체를 지정된 키에 연결합니다.
    /// </summary>
    public bool BindBasicAction(
        QuickKey key,
        BasicActionData actionData)
    {
        if (actionData == null ||
            actionData.ActionId == BasicActionId.None ||
            actionData.Icon == null ||
            actionData.Command == null)
        {
            return false;
        }

        if (!TryGetSlot(
                key,
                out QuickSlot slot))
        {
            return false;
        }

        slot.BindBasicAction(actionData);

        NotifyBindingsChanged();
        return true;
    }

    /// <summary>
    /// 지정된 키의 바인딩 식별 정보를 조회합니다.
    /// </summary>
    public bool TryGetBinding(
        QuickKey key,
        out QuickSlotBinding binding)
    {
        binding = QuickSlotBinding.Empty();

        if (!TryGetSlot(
                key,
                out QuickSlot slot))
        {
            return false;
        }

        if (slot.IsEmpty)
            return false;

        binding = slot.Binding;
        return true;
    }

    /// <summary>
    /// 지정된 키에 연결된 스킬 ID를 조회합니다.
    /// </summary>
    public bool TryGetBoundSkillId(
        QuickKey key,
        out SkillId skillId)
    {
        skillId = SkillId.None;

        if (!TryGetBinding(
                key,
                out QuickSlotBinding binding))
        {
            return false;
        }

        if (binding.Type !=
            QuickSlotBindingType.Skill)
        {
            return false;
        }

        skillId = binding.SkillId;
        return true;
    }

    /// <summary>
    /// 플레이어가 보유한 스킬의 아이콘을 조회합니다.
    /// </summary>
    public bool TryGetSkillIcon(
        SkillId skillId,
        out Sprite icon)
    {
        icon = null;

        if (skillId == SkillId.None)
            return false;

        if (!skillController.TryGetSkill(
                skillId,
                out PlayerSkillBase skill))
        {
            return false;
        }

        icon = skill.Icon;
        return icon != null;
    }

    /// <summary>
    /// 지정된 키에 연결된 기본 기능 데이터를 조회합니다.
    /// </summary>
    public bool TryGetBoundBasicActionData(
        QuickKey key,
        out BasicActionData actionData)
    {
        actionData = null;

        if (!TryGetSlot(
                key,
                out QuickSlot slot))
        {
            return false;
        }

        if (slot.Binding.Type !=
            QuickSlotBindingType.BasicAction)
        {
            return false;
        }

        if (slot.BasicActionData == null)
            return false;

        actionData = slot.BasicActionData;
        return true;
    }

    /// <summary>
    /// 두 키 슬롯의 내용을 교환합니다.
    /// 대상이 비어 있으면 이동처럼 동작합니다.
    /// </summary>
    public bool MoveOrSwap(
        QuickKey sourceKey,
        QuickKey targetKey)
    {
        if (sourceKey == targetKey)
            return true;

        if (!TryGetSlot(
                sourceKey,
                out QuickSlot sourceSlot))
        {
            return false;
        }

        if (!TryGetSlot(
                targetKey,
                out QuickSlot targetSlot))
        {
            return false;
        }

        if (sourceSlot.IsEmpty)
            return false;

        sourceSlot.SwapWith(targetSlot);

        NotifyBindingsChanged();
        return true;
    }

    /// <summary>
    /// 바인딩을 제거합니다.
    /// 기본 기능이었다면 복구할 BasicActionData를 반환합니다.
    /// 스킬이었다면 removedActionData는 null입니다.
    /// </summary>
    public bool ClearSlot(
        QuickKey key,
        out BasicActionData removedActionData)
    {
        removedActionData = null;

        if (!TryGetSlot(
                key,
                out QuickSlot slot))
        {
            return false;
        }

        if (slot.IsEmpty)
            return false;

        removedActionData =
            slot.BasicActionData;

        slot.Clear();

        NotifyBindingsChanged();
        return true;
    }

    /// <summary>
    /// 반환 데이터가 필요 없는 경우 사용하는 간단한 버전입니다.
    /// </summary>
    public bool ClearSlot(QuickKey key)
    {
        return ClearSlot(
            key,
            out _
        );
    }

    public bool IsSlotEmpty(QuickKey key)
    {
        if (!TryGetSlot(
                key,
                out QuickSlot slot))
        {
            return true;
        }

        return slot.IsEmpty;
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

            if (!TryGetSlot(
                    binding.key,
                    out QuickSlot slot))
            {
                Debug.LogWarning(
                    $"존재하지 않는 단축키입니다: " +
                    $"{binding.key}",
                    this
                );

                continue;
            }

            if (!TryCreateSkillCommand(
                    binding.skillId,
                    out IQuickSlotCommand command))
            {
                Debug.LogWarning(
                    $"기본 단축키 등록 실패: " +
                    $"{binding.key} → {binding.skillId}",
                    this
                );

                continue;
            }

            slot.BindSkill(
                binding.skillId,
                command
            );
        }

        NotifyBindingsChanged();
    }

    private bool TryCreateSkillCommand(
        SkillId skillId,
        out IQuickSlotCommand command)
    {
        command = null;

        if (skillId == SkillId.None)
            return false;

        if (!skillController.TryGetSkill(
                skillId,
                out PlayerSkillBase skill))
        {
            return false;
        }

        command = new UseSkillCommand(
            skillController,
            skill
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