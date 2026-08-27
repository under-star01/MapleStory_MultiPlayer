using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerAnimController))]
public class PlayerSkillController : MonoBehaviour
{
    [Serializable]
    private class DefaultSkillEntry
    {
        public SkillId skillId;
        public Sprite icon;
    }

    [Header("Default Skills")]
    [SerializeField]
    private List<DefaultSkillEntry> defaultSkills = new();

    private readonly Dictionary<SkillId, PlayerSkillBase>
        learnedSkills = new();

    private PlayerMove playerMove;
    private PlayerAnimController playerAnim;

    private bool isAttackExecuting;

    public bool IsAttackExecuting =>
        isAttackExecuting;

    private void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
        playerAnim = GetComponent<PlayerAnimController>();

        LearnDefaultSkills();
    }

    public bool TryExecute(
        IPlayerSkill skill,
        Vector2 inputDirection = default)
    {
        if (IsNull(skill))
            return false;

        SkillContext context = new SkillContext(
            gameObject,
            playerMove,
            playerAnim,
            this,
            inputDirection
        );

        if (!skill.CanExecute(context))
            return false;

        skill.Execute(context);
        return true;
    }

    /// <summary>
    /// 아이콘 정보 없이 스킬을 획득합니다.
    /// 기존 코드와의 호환을 위해 유지합니다.
    /// </summary>
    public bool LearnSkill(
        SkillId skillId,
        out PlayerSkillBase learnedSkill)
    {
        return LearnSkill(
            skillId,
            null,
            out learnedSkill
        );
    }

    /// <summary>
    /// 스킬 ID에 해당하는 스킬을 생성하거나 재사용하고,
    /// 전달받은 아이콘으로 초기화합니다.
    /// </summary>
    public bool LearnSkill(
        SkillId skillId,
        Sprite icon,
        out PlayerSkillBase learnedSkill)
    {
        if (skillId == SkillId.None)
        {
            learnedSkill = null;
            return false;
        }

        // 이미 배운 스킬이면 기존 인스턴스를 반환합니다.
        if (learnedSkills.TryGetValue(
                skillId,
                out learnedSkill))
        {
            if (icon != null)
            {
                learnedSkill.Initialize(icon);
            }

            return true;
        }

        if (!SkillRegistry.TryGetSkillType(
                skillId,
                out Type skillType))
        {
            learnedSkill = null;
            return false;
        }

        if (!typeof(PlayerSkillBase)
            .IsAssignableFrom(skillType))
        {
            Debug.LogError(
                $"{skillType.Name}은 " +
                $"{nameof(PlayerSkillBase)}를 상속하지 않습니다.",
                this
            );

            learnedSkill = null;
            return false;
        }

        // 이미 플레이어에게 붙어 있다면 재사용합니다.
        learnedSkill =
            GetComponent(skillType) as PlayerSkillBase;

        if (learnedSkill == null)
        {
            learnedSkill =
                gameObject.AddComponent(skillType)
                as PlayerSkillBase;
        }

        if (learnedSkill == null)
            return false;

        learnedSkill.Initialize(icon);

        if (icon == null)
        {
            Debug.LogWarning(
                $"{skillId} 스킬의 아이콘이 설정되지 않았습니다.",
                this
            );
        }

        learnedSkills.Add(
            skillId,
            learnedSkill
        );

        return true;
    }

    public bool HasSkill(SkillId skillId)
    {
        return learnedSkills.ContainsKey(skillId);
    }

    public bool TryGetSkill(
        SkillId skillId,
        out PlayerSkillBase skill)
    {
        return learnedSkills.TryGetValue(
            skillId,
            out skill
        );
    }

    private void LearnDefaultSkills()
    {
        foreach (DefaultSkillEntry entry in defaultSkills)
        {
            if (entry == null ||
                entry.skillId == SkillId.None)
            {
                continue;
            }

            if (!LearnSkill(
                    entry.skillId,
                    entry.icon,
                    out _))
            {
                Debug.LogWarning(
                    $"기본 스킬 획득 실패: {entry.skillId}",
                    this
                );
            }
        }
    }

    private bool IsNull(IPlayerSkill skill)
    {
        if (skill == null)
            return true;

        if (skill is UnityEngine.Object unityObject &&
            unityObject == null)
        {
            return true;
        }

        return false;
    }

    public bool TryBeginAttack()
    {
        if (isAttackExecuting)
            return false;

        isAttackExecuting = true;
        return true;
    }

    public void EndAttack()
    {
        isAttackExecuting = false;
    }
}