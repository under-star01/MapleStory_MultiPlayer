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

    private void FixedUpdate()
    {
        /*
         * 별도 클라이언트는 기본 PhysicsScene2D를
         * Unity가 자동으로 처리합니다.
         *
         * 서버에서 로드한 맵별 로컬 PhysicsScene2D만
         * 직접 시뮬레이션합니다.
         */
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