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

    // 로컬 플레이어 Context를 연결된 UI들에 전달
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

    // 연결된 UI들의 플레이어 참조 및 이벤트 구독 해제
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

    // 하위에서 ILocalPlayerUI를 구현한 UI 수집
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