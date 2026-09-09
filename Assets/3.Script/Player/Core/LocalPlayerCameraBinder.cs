using Mirror;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlayerMapController))]
public class LocalPlayerCameraBinder : NetworkBehaviour
{
    private CinemachineCamera cinemachineCamera;
    private CinemachineConfiner2D cinemachineConfiner;

    private PlayerMapController mapController;
    private MapSceneManager mapSceneManager;

    private void Awake()
    {
        mapController =
            GetComponent<PlayerMapController>();
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();

        BindCamera();
    }

    public override void OnStopAuthority()
    {
        UnbindCamera();

        base.OnStopAuthority();
    }

    private void BindCamera()
    {
        cinemachineCamera =
            FindFirstObjectByType<CinemachineCamera>();

        if (cinemachineCamera == null)
        {
            Debug.LogError(
                "CinemachineCamera를 찾지 못했습니다.",
                this
            );

            return;
        }

        cinemachineConfiner =
            cinemachineCamera
                .GetComponent<CinemachineConfiner2D>();

        if (cinemachineConfiner == null)
        {
            Debug.LogError(
                "CinemachineConfiner2D를 찾지 못했습니다.",
                cinemachineCamera
            );

            return;
        }

        mapSceneManager =
            FindFirstObjectByType<MapSceneManager>();

        if (mapSceneManager == null)
        {
            Debug.LogError(
                "MapSceneManager를 찾지 못했습니다.",
                this
            );

            return;
        }

        cinemachineCamera.Target.TrackingTarget =
            transform;

        BindMapBounds();
    }

    public void BindMapBounds()
    {
        BindMapBounds(
            mapController.CurrentMapId
        );
    }

    public void BindMapBounds(MapId mapId)
    {
        if (mapSceneManager == null ||
            cinemachineConfiner == null)
        {
            return;
        }

        if (!mapSceneManager.TryGetLoadedScene(
                mapId,
                out Scene mapScene))
        {
            Debug.LogError(
                $"로드된 맵 씬을 찾지 못했습니다: " +
                $"{mapId}",
                this
            );

            return;
        }

        foreach (GameObject root in
                 mapScene.GetRootGameObjects())
        {
            MapCameraBounds mapBounds =
                root.GetComponentInChildren
                    <MapCameraBounds>(true);

            if (mapBounds == null)
                continue;

            cinemachineConfiner.BoundingShape2D =
                mapBounds.BoundsCollider;

            cinemachineConfiner
                .InvalidateBoundingShapeCache();

            Debug.Log(
                $"카메라 경계 연결 완료: " +
                $"{mapId} / {mapScene.name}",
                this
            );

            return;
        }

        Debug.LogError(
            $"CameraBounds를 찾지 못했습니다: " +
            $"{mapScene.name}",
            this
        );
    }

    private void UnbindCamera()
    {
        if (cinemachineCamera != null &&
            cinemachineCamera.Target.TrackingTarget ==
            transform)
        {
            cinemachineCamera.Target.TrackingTarget =
                null;
        }

        cinemachineCamera = null;
        cinemachineConfiner = null;
        mapSceneManager = null;
    }
}