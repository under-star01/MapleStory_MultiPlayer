using UnityEngine;

public class MapSpawnPoint : MonoBehaviour
{
    [SerializeField]
    private MapId mapId;

    [SerializeField]
    private string spawnId = "Default";

    public MapId MapId => mapId;
    public string SpawnId => spawnId;
}