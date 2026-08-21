using UnityEngine;

public abstract class PlayerSkillBase : MonoBehaviour, IPlayerSkill
{
    public abstract bool CanExecute(SkillContext context);

    public abstract void Execute(SkillContext context);
}