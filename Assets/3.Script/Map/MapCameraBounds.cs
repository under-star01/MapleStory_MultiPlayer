using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class MapCameraBounds : MonoBehaviour
{
    private BoxCollider2D boundsCollider;

    public BoxCollider2D BoundsCollider
    {
        get
        {
            if (boundsCollider == null)
            {
                boundsCollider =
                    GetComponent<BoxCollider2D>();
            }

            return boundsCollider;
        }
    }

    private void Awake()
    {
        boundsCollider =
            GetComponent<BoxCollider2D>();
    }
}