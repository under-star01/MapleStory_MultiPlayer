using UnityEngine;

public class ParallaxLayer : MonoBehaviour
{
    [SerializeField] private Transform targetCamera;

    [Range(0f, 1f)]
    [SerializeField] private float parallaxX = 0.5f;

    [Range(0f, 1f)]
    [SerializeField] private float parallaxY = 0f;

    private Vector3 startPosition;
    private Vector3 startCameraPosition;

    private void Awake()
    {
        if (targetCamera == null && Camera.main != null)
            targetCamera = Camera.main.transform;

        startPosition = transform.position;

        if (targetCamera != null)
            startCameraPosition = targetCamera.position;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            return;

        Vector3 cameraDelta = targetCamera.position - startCameraPosition;

        transform.position = new Vector3(
            startPosition.x + cameraDelta.x * parallaxX,
            startPosition.y + cameraDelta.y * parallaxY,
            startPosition.z
        );
    }
}