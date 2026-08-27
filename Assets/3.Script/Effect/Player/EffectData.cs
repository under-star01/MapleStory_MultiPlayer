using UnityEngine;

[CreateAssetMenu(
    fileName = "EffectData",
    menuName = "Game/Effect Data"
)]
public class EffectData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private EffectId effectId;

    [Header("Animation")]
    [SerializeField]
    private AnimationClip animationClip;

    [Header("Transform")]
    [SerializeField]
    private Vector2 positionOffset;

    [SerializeField]
    private Vector2 scale =
        Vector2.one;

    [Header("Rendering")]
    [SerializeField]
    private int sortingOrder;

    public EffectId EffectId =>
        effectId;

    public AnimationClip AnimationClip =>
        animationClip;

    public Vector2 PositionOffset =>
        positionOffset;

    public Vector2 Scale =>
        scale;

    public int SortingOrder =>
        sortingOrder;
}