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

    /// <summary>
    /// 로컬 플레이어의 Context를 생성하고
    /// 씬의 플레이어 UI Root에 전달합니다.
    /// </summary>
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

        uiRoot.Bind(playerContext);
    }

    /// <summary>
    /// 현재 플레이어와 연결된 UI를 해제합니다.
    /// </summary>
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