using UnityEngine;

public class RainLoop : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float fallSpeed = 8f;

    [Header("Loop Range")]
    [SerializeField] private float topY = 15f;
    [SerializeField] private float bottomY = -10f;

    private Transform[] rainDrops;

    private void Awake()
    {
        rainDrops = new Transform[transform.childCount];

        for (int i = 0; i < transform.childCount; i++)
        {
            rainDrops[i] = transform.GetChild(i);
        }
    }

    private void Update()
    {
        float moveDistance = fallSpeed * Time.deltaTime;
        float loopHeight = topY - bottomY;

        foreach (Transform rain in rainDrops)
        {
            Vector3 position = rain.position;
            position.y -= moveDistance;

            if (position.y < bottomY)
            {
                // 초과해서 내려간 거리까지 유지해서 이동
                position.y += loopHeight;
            }

            rain.position = position;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawLine(
            new Vector3(-100f, topY, 0f),
            new Vector3(100f, topY, 0f)
        );

        Gizmos.DrawLine(
            new Vector3(-100f, bottomY, 0f),
            new Vector3(100f, bottomY, 0f)
        );
    }
}