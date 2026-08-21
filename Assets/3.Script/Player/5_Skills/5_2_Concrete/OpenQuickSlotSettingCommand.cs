using UnityEngine;

public class OpenQuickSlotSettingCommand :
    IQuickSlotCommand
{
    private readonly QuickSlotSettingUI settingUI;

    public OpenQuickSlotSettingCommand(
        QuickSlotSettingUI settingUI)
    {
        this.settingUI = settingUI;
    }

    public bool Execute(Vector2 inputDirection)
    {
        if (settingUI == null)
            return false;

        settingUI.Toggle();
        return true;
    }
}