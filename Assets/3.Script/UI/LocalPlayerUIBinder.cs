using Mirror;
using UnityEngine;

public class LocalPlayerUIBinder : NetworkBehaviour
{
    private LocalPlayerUIRoot uiRoot;
    private LocalPlayerContext playerContext;

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();

        BindPlayerUI();
    }

    public override void OnStopAuthority()
    {
        UnbindPlayerUI();

        base.OnStopAuthority();
    }

    private void OnDisable()
    {
        UnbindPlayerUI();
    }

    // 로컬 플레이어 Context를 생성해 UI Root에 연결
    private void BindPlayerUI()
    {
        if (uiRoot != null)
            return;

        uiRoot =
            FindFirstObjectByType<LocalPlayerUIRoot>(
                FindObjectsInactive.Include
            );

        if (uiRoot == null)
        {
            Debug.LogError(
                $"{nameof(LocalPlayerUIRoot)}를 " +
                "씬에서 찾지 못했습니다.",
                this
            );

            return;
        }

        playerContext =
            new LocalPlayerContext(gameObject);

        uiRoot.Bind(
            playerContext
        );
    }

    // 현재 플레이어와 연결된 UI 해제
    private void UnbindPlayerUI()
    {
        if (uiRoot != null)
        {
            uiRoot.Unbind();
        }

        uiRoot = null;
        playerContext = null;
    }
}