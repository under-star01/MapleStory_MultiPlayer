using System.Collections.Generic;
using UnityEngine;

public class LocalPlayerUIRoot : MonoBehaviour
{
    private readonly List<ILocalPlayerUI>
        playerUIs = new();

    private LocalPlayerContext currentContext;

    private void Awake()
    {
        CollectPlayerUIs();
    }

    /// <summary>
    /// 로컬 플레이어의 Context를
    /// 연결 대상 UI들에 전달합니다.
    /// </summary>
    public void Bind(LocalPlayerContext context)
    {
        if (context == null)
            return;

        if (currentContext == context)
            return;

        Unbind();

        currentContext = context;

        foreach (ILocalPlayerUI playerUI in playerUIs)
        {
            playerUI.Bind(context);
        }
    }

    /// <summary>
    /// 연결된 UI들의 플레이어 참조와
    /// 이벤트 구독을 해제합니다.
    /// </summary>
    public void Unbind()
    {
        if (currentContext == null)
            return;

        foreach (ILocalPlayerUI playerUI in playerUIs)
        {
            playerUI.Unbind();
        }

        currentContext = null;
    }

    /// <summary>
    /// LocalPlayerUIRoot 하위에서
    /// ILocalPlayerUI를 구현한 UI만 수집합니다.
    /// </summary>
    private void CollectPlayerUIs()
    {
        playerUIs.Clear();

        MonoBehaviour[] behaviours =
            GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is not ILocalPlayerUI playerUI)
                continue;

            playerUIs.Add(playerUI);
        }
    }
}