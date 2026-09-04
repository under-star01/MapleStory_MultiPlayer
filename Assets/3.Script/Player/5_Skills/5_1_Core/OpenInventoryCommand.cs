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

    public bool Execute()
    {
        if (inventoryUI == null)
            return false;

        inventoryUI.Toggle();

        return true;
    }
}