using UnityEngine;

public abstract class PlayerSkillBase :
    MonoBehaviour,
    IPlayerSkill
{
    private Sprite icon;

    public Sprite Icon => icon;

    public void Initialize(Sprite skillIcon)
    {
        icon = skillIcon;
    }

    public abstract bool CanExecute(
        SkillContext context
    );

    public abstract void Execute(
        SkillContext context
    );
}