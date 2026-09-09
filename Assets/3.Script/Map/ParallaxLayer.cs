using UnityEngine;

[DefaultExecutionOrder(1000)]
public class ParallaxLayer : MonoBehaviour
{
    [SerializeField] private Transform targetCamera;

    [Range(0f, 1f)]
    [SerializeField] private float parallaxX = 0.5f;

    [Range(0f, 1f)]
    [SerializeField] private float parallaxY = 0f;

    private Vector3 parallaxOrigin;

    private void Awake()
    {
        if (targetCamera == null && Camera.main != null)
            targetCamera = Camera.main.transform;

        parallaxOrigin = transform.position;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            return;

        transform.position = new Vector3(
            parallaxOrigin.x + targetCamera.position.x * parallaxX,
            parallaxOrigin.y + targetCamera.position.y * parallaxY,
            parallaxOrigin.z
        );
    }
}