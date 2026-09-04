using Mirror;

public class PickupSkill : PlayerSkillBase
{
    public override bool CanExecute(
        SkillContext context)
    {
        return context != null &&
               context.Inventory != null &&
               NetworkServer.active;
    }

    public override void Execute(
        SkillContext context)
    {
        context.Inventory.TryPickupNearestItem();
    }
}