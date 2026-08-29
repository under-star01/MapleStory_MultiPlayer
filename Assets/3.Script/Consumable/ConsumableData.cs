using UnityEngine;

[CreateAssetMenu(
    menuName = "Game/Consumable Data",
    fileName = "ConsumableData")]
public class ConsumableData : ScriptableObject
{
    [SerializeField]
    private ConsumableId consumableId;

    [SerializeField]
    private string displayName;

    [SerializeField]
    private Sprite icon;

    [SerializeField, Min(1)]
    private int healAmount = 50;

    [SerializeField, Min(1)]
    private int maxStack = 100;

    public ConsumableId Id => consumableId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public int HealAmount => healAmount;
    public int MaxStack => maxStack;
}