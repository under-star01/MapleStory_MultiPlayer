using UnityEngine;

public class OpenSkillUICommand :
    IQuickSlotCommand
{
    private readonly SkillUI skillUI;

    public OpenSkillUICommand(
        SkillUI skillUI)
    {
        this.skillUI =
            skillUI;
    }

    public bool Execute(
        Vector2 inputDirection)
    {
        if (skillUI == null)
            return false;

        skillUI.Toggle();

        return true;
    }
}