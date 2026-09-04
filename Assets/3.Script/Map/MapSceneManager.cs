using Mirror;
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

    // 서버의 모든 맵을 독립된 2D 물리 공간으로 Additive 로드
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

    // 클라이언트에서 사용할 특정 맵을 Additive 로드
    public IEnumerator LoadClientMap(
        MapId mapId)
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
            SceneManager.GetSceneByName(
                sceneName
            );

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

        loadedScenes[mapId] =
            scene;
    }

    public bool TryGetLoadedScene(
        MapId mapId,
        out Scene scene)
    {
        return loadedScenes.TryGetValue(
            mapId,
            out scene
        );
    }

    // 지정한 맵의 스폰 위치 조회
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

    private bool IsValid(
        MapSceneEntry entry)
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

    // 별도 클라이언트에서 이전 맵 언로드
    public IEnumerator UnloadClientMap(
        MapId mapId)
    {
        if (NetworkServer.active)
            yield break;

        if (!TryGetSceneName(
                mapId,
                out string sceneName))
        {
            yield break;
        }

        Scene scene =
            SceneManager.GetSceneByName(
                sceneName
            );

        if (!scene.isLoaded)
        {
            loadedScenes.Remove(
                mapId
            );

            yield break;
        }

        AsyncOperation operation =
            SceneManager.UnloadSceneAsync(
                scene
            );

        if (operation == null)
            yield break;

        yield return operation;

        loadedScenes.Remove(
            mapId
        );

        Debug.Log(
            $"클라이언트 이전 맵 언로드 완료: " +
            $"{mapId} / {sceneName}",
            this
        );
    }
}