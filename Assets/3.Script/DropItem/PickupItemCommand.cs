using UnityEngine;

public class PickupItemCommand :
    IQuickSlotCommand
{
    private readonly PlayerInventory inventory;

    public PickupItemCommand(
        PlayerInventory inventory)
    {
        this.inventory =
            inventory;
    }

    public bool Execute(
        Vector2 inputDirection)
    {
        if (inventory == null)
            return false;

        return inventory.RequestPickup();
    }
}