using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapSceneManager : MonoBehaviour
{
    [Serializable]
    private class MapSceneEntry
    {
        public MapId mapId;
        public string sceneName;
    }

    [Header("Map Scenes")]
    [SerializeField]
    private List<MapSceneEntry> mapScenes = new();

    private readonly Dictionary<MapId, Scene>
        loadedScenes = new();

    public bool AreServerMapsLoaded { get; private set; }

    /// <summary>
    /// 서버가 관리할 모든 맵 씬을
    /// 독립된 2D 물리 공간과 함께 Additive로 로드합니다.
    /// </summary>
    public IEnumerator LoadAllServerMaps()
    {
        AreServerMapsLoaded = false;
        loadedScenes.Clear();

        if (mapScenes.Count == 0)
        {
            Debug.LogError(
                "등록된 맵 씬이 없습니다.",
                this
            );

            yield break;
        }

        foreach (MapSceneEntry entry in mapScenes)
        {
            if (!IsValid(entry))
                continue;

            if (loadedScenes.ContainsKey(entry.mapId))
            {
                Debug.LogError(
                    $"중복된 MapId입니다: {entry.mapId}",
                    this
                );

                continue;
            }

            Scene scene =
                SceneManager.GetSceneByName(
                    entry.sceneName
                );

            if (!scene.isLoaded)
            {
                LoadSceneParameters loadParameters =
                    new LoadSceneParameters(
                        LoadSceneMode.Additive,
                        LocalPhysicsMode.Physics2D
                    );

                AsyncOperation operation =
                    SceneManager.LoadSceneAsync(
                        entry.sceneName,
                        loadParameters
                    );

                if (operation == null)
                {
                    Debug.LogError(
                        $"씬 로드를 시작하지 못했습니다: " +
                        $"{entry.sceneName}",
                        this
                    );

                    continue;
                }

                yield return operation;

                scene =
                    SceneManager.GetSceneByName(
                        entry.sceneName
                    );
            }

            if (!scene.isLoaded)
            {
                Debug.LogError(
                    $"씬 로드에 실패했습니다: " +
                    $"{entry.sceneName}",
                    this
                );

                continue;
            }

            PhysicsScene2D physicsScene =
                scene.GetPhysicsScene2D();

            if (!physicsScene.IsValid())
            {
                Debug.LogError(
                    $"유효한 PhysicsScene2D가 없습니다: " +
                    $"{entry.sceneName}",
                    this
                );

                continue;
            }

            loadedScenes.Add(
                entry.mapId,
                scene
            );

            Debug.Log(
                $"서버 맵 씬 로드 완료: " +
                $"{entry.mapId} / {entry.sceneName}",
                this
            );
        }

        AreServerMapsLoaded =
            loadedScenes.Count == mapScenes.Count;

        if (AreServerMapsLoaded)
        {
            Debug.Log(
                "서버의 모든 맵 씬 로드가 완료되었습니다.",
                this
            );
        }
        else
        {
            Debug.LogWarning(
                $"맵 씬 로드 결과: " +
                $"{loadedScenes.Count}/" +
                $"{mapScenes.Count}",
                this
            );
        }
    }

    /// <summary>
    /// 클라이언트가 사용할 특정 맵 씬을
    /// 일반 Additive 방식으로 로드합니다.
    /// 클라이언트는 현재 맵 하나만 사용하므로
    /// 별도의 로컬 물리 씬을 만들지 않습니다.
    /// </summary>
    public IEnumerator LoadClientMap(MapId mapId)
    {
        if (!TryGetSceneName(
                mapId,
                out string sceneName))
        {
            Debug.LogError(
                $"씬 이름을 찾지 못했습니다: {mapId}",
                this
            );

            yield break;
        }

        Scene scene =
            SceneManager.GetSceneByName(sceneName);

        /*
         * Host에서는 서버가 이미 맵을 로드했으므로
         * 동일한 씬을 다시 로드하지 않습니다.
         */
        if (!scene.isLoaded)
        {
            AsyncOperation operation =
                SceneManager.LoadSceneAsync(
                    sceneName,
                    LoadSceneMode.Additive
                );

            if (operation == null)
            {
                Debug.LogError(
                    $"클라이언트 씬 로드를 시작하지 " +
                    $"못했습니다: {sceneName}",
                    this
                );

                yield break;
            }

            yield return operation;

            scene =
                SceneManager.GetSceneByName(
                    sceneName
                );
        }

        if (!scene.isLoaded)
        {
            Debug.LogError(
                $"클라이언트 씬 로드에 실패했습니다: " +
                $"{sceneName}",
                this
            );

            yield break;
        }

        /*
         * 별도 클라이언트에서도 MapId로
         * 로드된 맵 Scene을 조회할 수 있도록 등록합니다.
         */
        loadedScenes[mapId] = scene;
    }

    /// <summary>
    /// 현재 프로세스에 로드된 특정 맵 Scene을 반환합니다.
    /// </summary>
    public bool TryGetLoadedScene(
        MapId mapId,
        out Scene scene)
    {
        return loadedScenes.TryGetValue(
            mapId,
            out scene
        );
    }

    /// <summary>
    /// 지정한 맵의 스폰 포인트를 찾습니다.
    /// </summary>
    public Transform FindSpawnPoint(
        MapId mapId,
        string spawnId)
    {
        if (!TryGetLoadedScene(
                mapId,
                out Scene scene))
        {
            return null;
        }

        foreach (GameObject rootObject
                 in scene.GetRootGameObjects())
        {
            MapSpawnPoint[] spawnPoints =
                rootObject.GetComponentsInChildren
                    <MapSpawnPoint>(true);

            foreach (MapSpawnPoint spawnPoint
                     in spawnPoints)
            {
                if (spawnPoint.MapId != mapId)
                    continue;

                if (spawnPoint.SpawnId != spawnId)
                    continue;

                return spawnPoint.transform;
            }
        }

        return null;
    }

    public bool TryGetSceneName(
        MapId mapId,
        out string sceneName)
    {
        foreach (MapSceneEntry entry in mapScenes)
        {
            if (entry.mapId != mapId)
                continue;

            sceneName = entry.sceneName;
            return true;
        }

        sceneName = null;
        return false;
    }

    public void ClearServerMapState()
    {
        loadedScenes.Clear();
        AreServerMapsLoaded = false;
    }

    private bool IsValid(MapSceneEntry entry)
    {
        if (entry == null)
            return false;

        if (!string.IsNullOrWhiteSpace(
                entry.sceneName))
        {
            return true;
        }

        Debug.LogError(
            $"씬 이름이 비어 있습니다: {entry.mapId}",
            this
        );

        return false;
    }
}