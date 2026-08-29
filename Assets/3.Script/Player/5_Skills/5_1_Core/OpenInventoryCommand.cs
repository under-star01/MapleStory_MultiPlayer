using UnityEngine;

public class OpenInventoryCommand :
    IQuickSlotCommand
{
    private readonly InventoryUI inventoryUI;

    public OpenInventoryCommand(
        InventoryUI inventoryUI)
    {
        this.inventoryUI =
            inventoryUI;
    }

    public bool Execute(
        Vector2 inputDirection)
    {
        if (inventoryUI == null)
            return false;

        inventoryUI.Toggle();

        return true;
    }
}