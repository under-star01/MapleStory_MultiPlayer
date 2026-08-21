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

    /// <summary>
    /// 단축키 바인딩이 변경됐을 때 발생합니다.
    /// UI는 이 이벤트를 구독해 표시를 갱신할 수 있습니다.
    /// </summary>
    public event Action BindingsChanged;

    private void Awake()
    {
        skillController =
            GetComponent<PlayerSkillController>();

        CreateSlots();
    }

    /*
     * PlayerSkillController의 Awake에서
     * 기본 스킬 획득이 끝난 뒤 바인딩하기 위해
     * Start에서 실행합니다.
     */
    private void Start()
    {
        BindDefaultSkills();
    }

    /// <summary>
    /// 지정된 키에 연결된 Command를 실행합니다.
    /// </summary>
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

    /// <summary>
    /// 일반 Command를 지정된 키에 연결합니다.
    /// 스킬이 아닌 상호작용이나 UI 열기 등에 사용합니다.
    /// </summary>
    public bool BindCommand(
        QuickKey key,
        IQuickSlotCommand command)
    {
        if (command == null)
            return false;

        if (!TryGetSlot(
                key,
                out QuickSlot slot))
        {
            return false;
        }

        slot.BindCommand(command);

        NotifyBindingsChanged();
        return true;
    }

    /// <summary>
    /// 플레이어가 보유한 스킬을 찾아
    /// 지정된 키에 연결합니다.
    /// </summary>
    public bool BindSkill(
        QuickKey key,
        SkillId skillId)
    {
        if (!TryCreateSkillCommand(
                skillId,
                out IQuickSlotCommand command))
        {
            return false;
        }

        if (!TryGetSlot(
                key,
                out QuickSlot slot))
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
    /// 지정된 키에 연결된 스킬 ID를 조회합니다.
    /// 스킬이 아닌 일반 Command이거나 빈 슬롯이면 false입니다.
    /// </summary>
    public bool TryGetBoundSkillId(
        QuickKey key,
        out SkillId skillId)
    {
        skillId = SkillId.None;

        if (!TryGetSlot(
                key,
                out QuickSlot slot))
        {
            return false;
        }

        if (slot.SkillId == SkillId.None)
            return false;

        skillId = slot.SkillId;
        return true;
    }

    /// <summary>
    /// 두 키 슬롯의 내용을 서로 교환합니다.
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
    /// 지정된 키의 바인딩을 제거합니다.
    /// </summary>
    public bool ClearSlot(QuickKey key)
    {
        if (!TryGetSlot(
                key,
                out QuickSlot slot))
        {
            return false;
        }

        if (slot.IsEmpty)
            return false;

        slot.Clear();

        NotifyBindingsChanged();
        return true;
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

            /*
             * BindSkill()을 호출하지 않는 이유:
             * 기본 스킬을 여러 개 등록하는 동안
             * BindingsChanged 이벤트가 반복되는 것을 막기 위해서입니다.
             */
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