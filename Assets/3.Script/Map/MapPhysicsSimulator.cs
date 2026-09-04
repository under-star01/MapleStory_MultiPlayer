using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapPhysicsSimulator : MonoBehaviour
{
    private PhysicsScene2D physicsScene;

    private void Awake()
    {
        physicsScene =
            gameObject.scene.GetPhysicsScene2D();

        if (!physicsScene.IsValid())
        {
            Debug.LogError(
                $"유효한 PhysicsScene2D를 찾지 못했습니다: " +
                $"{gameObject.scene.name}",
                this
            );
        }
    }

    // 서버의 맵별 독립 PhysicsScene2D를 직접 시뮬레이션
    private void FixedUpdate()
    {
        if (!NetworkServer.active ||
            !physicsScene.IsValid())
        {
            return;
        }

        physicsScene.Simulate(
            Time.fixedDeltaTime
        );
    }
}