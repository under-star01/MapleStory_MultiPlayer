using Mirror;
using Unity.Cinemachine;
using UnityEngine;

public class LocalPlayerCameraBinder : NetworkBehaviour
{
    private CinemachineCamera cinemachineCamera;

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

    private void OnDisable()
    {
        if (isOwned)
        {
            UnbindCamera();
        }
    }

    private void BindCamera()
    {
        cinemachineCamera =
            FindFirstObjectByType<CinemachineCamera>();

        if (cinemachineCamera == null)
        {
            Debug.LogError(
                "씬에서 CinemachineCamera를 찾지 못했습니다.",
                this
            );

            return;
        }

        cinemachineCamera.Target.TrackingTarget =
            transform;
    }

    private void UnbindCamera()
    {
        if (cinemachineCamera == null)
            return;

        if (cinemachineCamera.Target.TrackingTarget ==
            transform)
        {
            cinemachineCamera.Target.TrackingTarget =
                null;
        }

        cinemachineCamera = null;
    }
}