using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerAnimController))]
[RequireComponent(typeof(PlayerEffectController))]
[RequireComponent(typeof(PlayerInventory))]
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
    private PlayerEffectController playerEffect;
    private PlayerInventory inventory;

    private bool isActionExecuting;

    public bool IsActionExecuting =>
        isActionExecuting;

    private void Awake()
    {
        playerMove =
            GetComponent<PlayerMove>();

        playerAnim =
            GetComponent<PlayerAnimController>();

        playerEffect =
            GetComponent<PlayerEffectController>();

        inventory =
            GetComponent<PlayerInventory>();

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
            playerEffect,
            this,
            inventory,
            inputDirection
        );

        if (!skill.CanExecute(context))
            return false;

        skill.Execute(context);

        return true;
    }

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

    // 스킬 ID에 맞는 스킬을 생성하거나 기존 인스턴스를 재사용
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

    public bool HasSkill(
        SkillId skillId)
    {
        return learnedSkills.ContainsKey(
            skillId
        );
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

    // 인스펙터에 등록된 기본 스킬 획득
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

    private bool IsNull(
        IPlayerSkill skill)
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

    // 중복 액션 실행 방지
    public bool TryBeginAction()
    {
        if (isActionExecuting)
            return false;

        isActionExecuting = true;

        return true;
    }

    public void EndAction()
    {
        isActionExecuting = false;
    }
}