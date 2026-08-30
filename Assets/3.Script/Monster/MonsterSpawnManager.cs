using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MonsterSpawnManager : NetworkBehaviour
{
    [Serializable]
    private class SpawnEntry
    {
        [Header("Spawn")]
        [Min(1)]
        public int monsterId;
        
        public MonsterHealth monsterPrefab;
        public Transform spawnPoint;

        [Min(0f)]
        public float respawnDelay = 5f;

        [NonSerialized]
        public MonsterHealth monsterInstance;

        [NonSerialized]
        public Coroutine respawnCoroutine;

        [NonSerialized]
        public bool isSpawned;
    }

    [Header("Monster Spawns")]
    [SerializeField]
    private List<SpawnEntry> spawnEntries = new();

    [Header("Empty Map")]
    [SerializeField]
    [Min(0f)]
    private float emptyMapDeactivateDelay = 30f;

    private int playerCount;
    private bool isMapActive;

    private Coroutine deactivateCoroutine;

    public override void OnStartServer()
    {
        base.OnStartServer();

        /*
         * 맵이 로드되더라도 플레이어가 없으면
         * 몬스터를 생성하지 않습니다.
         */
        playerCount = 0;
        isMapActive = false;
    }

    /// <summary>
    /// 서버에서 플레이어가 이 맵에 들어왔을 때 호출합니다.
    /// 첫 번째 플레이어라면 몬스터들을 활성화합니다.
    /// </summary>
    [Server]
    public void OnPlayerEnteredMap()
    {
        playerCount++;

        /*
         * 빈 맵 비활성화 대기 중이었다면
         * 몬스터 반환을 취소합니다.
         */
        if (deactivateCoroutine != null)
        {
            StopCoroutine(
                deactivateCoroutine
            );

            deactivateCoroutine = null;
        }

        if (!isMapActive)
        {
            ActivateMonsters();
        }
    }

    /// <summary>
    /// 서버에서 플레이어가 이 맵을 떠났을 때 호출합니다.
    /// 마지막 플레이어가 나가면 비활성화 대기를 시작합니다.
    /// </summary>
    [Server]
    public void OnPlayerExitedMap()
    {
        playerCount =
            Mathf.Max(
                playerCount - 1,
                0
            );

        if (playerCount > 0 ||
            deactivateCoroutine != null)
        {
            return;
        }

        deactivateCoroutine =
            StartCoroutine(
                DeactivateAfterDelay()
            );
    }

    /// <summary>
    /// 맵의 모든 생성 지점을 활성화합니다.
    /// 최초라면 생성하고, 기존 인스턴스가 있으면 재사용합니다.
    /// </summary>
    [Server]
    private void ActivateMonsters()
    {
        isMapActive = true;

        foreach (SpawnEntry entry in spawnEntries)
        {
            if (!IsValidEntry(entry))
                continue;

            if (entry.respawnCoroutine != null)
            {
                StopCoroutine(
                    entry.respawnCoroutine
                );

                entry.respawnCoroutine = null;
            }

            if (entry.monsterInstance == null)
            {
                CreateMonster(entry);
            }
            else
            {
                SpawnMonster(entry);
            }
        }
    }

    /// <summary>
    /// 몬스터 인스턴스를 최초로 생성합니다.
    /// </summary>
    [Server]
    private void CreateMonster(
        SpawnEntry entry)
    {
        if (!IsValidEntry(entry))
            return;

        MonsterHealth monster =
    Instantiate(
        entry.monsterPrefab,
        entry.spawnPoint.position,
        entry.spawnPoint.rotation
    );

        SceneManager.MoveGameObjectToScene(
            monster.gameObject,
            gameObject.scene
        );

        if (!TryApplyMonsterData(
                entry.monsterId,
                monster))
        {
            Destroy(monster.gameObject);
            return;
        }

        entry.monsterInstance =
            monster;

        monster.DeathCompleted +=
            OnMonsterDeathCompleted;

        NetworkServer.Spawn(
            monster.gameObject
        );

        entry.isSpawned = true;
    }

    private bool TryApplyMonsterData(
        int monsterId,
        MonsterHealth monsterHealth)
    {
        DatabaseManager databaseManager =
            DatabaseManager.Instance;

        if (databaseManager == null ||
            !databaseManager.IsInitialized)
        {
            Debug.LogError(
                "DB 데이터가 초기화되지 않았습니다.",
                this
            );

            return false;
        }

        if (!databaseManager.StaticData.TryGetMonster(
                monsterId,
                out MonsterRecord record))
        {
            Debug.LogError(
                $"몬스터 DB 데이터를 찾지 못했습니다: " +
                $"{monsterId}",
                this
            );

            return false;
        }

        MonsterMovement monsterMovement =
            monsterHealth.GetComponent
                <MonsterMovement>();

        MonsterCombat monsterCombat =
            monsterHealth.GetComponent
                <MonsterCombat>();

        if (monsterMovement == null ||
            monsterCombat == null)
        {
            Debug.LogError(
                $"몬스터 초기화 컴포넌트를 찾지 못했습니다: " +
                $"{monsterId}",
                monsterHealth
            );

            return false;
        }

        monsterHealth.ApplyMonsterId(
            record.MonsterId
        );

        monsterHealth.ApplyMaxHp(
            record.MaxHp
        );

        monsterMovement.ApplyMoveSpeed(
            record.MoveSpeed
        );

        monsterCombat.ApplyContactDamage(
            record.AttackPower
        );

        Debug.Log(
            $"[Monster Initialize] " +
            $"Id: {record.MonsterId}, " +
            $"Name: {record.MonsterName}, " +
            $"HP: {record.MaxHp}, " +
            $"Attack: {record.AttackPower}, " +
            $"Speed: {record.MoveSpeed}",
            monsterHealth
        );

        return true;
    }

    /// <summary>
    /// 풀에 보관된 몬스터를 원래 생성 위치에서 다시 등장시킵니다.
    /// </summary>
    [Server]
    private void SpawnMonster(
        SpawnEntry entry)
    {
        if (!IsValidEntry(entry) ||
            entry.monsterInstance == null ||
            entry.isSpawned)
        {
            return;
        }

        MonsterHealth monster =
            entry.monsterInstance;

        monster.transform.SetPositionAndRotation(
            entry.spawnPoint.position,
            entry.spawnPoint.rotation
        );

        monster.gameObject.SetActive(
            true
        );

        NetworkServer.Spawn(
            monster.gameObject
        );

        entry.isSpawned = true;
    }

    /// <summary>
    /// 사망 애니메이션이 끝난 몬스터를 풀 상태로 전환하고
    /// 맵이 활성 상태라면 리스폰 대기를 시작합니다.
    /// </summary>
    [Server]
    private void OnMonsterDeathCompleted(
        MonsterHealth monster)
    {
        SpawnEntry entry =
            FindEntry(monster);

        if (entry == null ||
            !entry.isSpawned)
        {
            return;
        }

        UnSpawnMonster(entry);

        /*
         * 맵이 비활성 상태이거나 플레이어가 없다면
         * 리스폰 타이머를 시작하지 않습니다.
         */
        if (!isMapActive ||
            playerCount <= 0)
        {
            return;
        }

        if (entry.respawnCoroutine != null)
        {
            StopCoroutine(
                entry.respawnCoroutine
            );
        }

        entry.respawnCoroutine =
            StartCoroutine(
                RespawnAfterDelay(entry)
            );
    }

    [Server]
    private IEnumerator RespawnAfterDelay(
        SpawnEntry entry)
    {
        yield return new WaitForSeconds(
            entry.respawnDelay
        );

        entry.respawnCoroutine = null;

        /*
         * 기다리는 동안 맵이 비활성화되었다면
         * 몬스터를 다시 등장시키지 않습니다.
         */
        if (!isMapActive ||
            playerCount <= 0)
        {
            yield break;
        }

        SpawnMonster(entry);
    }

    /// <summary>
    /// 마지막 플레이어가 나간 뒤 일정 시간 동안
    /// 아무도 들어오지 않으면 몬스터를 비활성화합니다.
    /// </summary>
    [Server]
    private IEnumerator DeactivateAfterDelay()
    {
        yield return new WaitForSeconds(
            emptyMapDeactivateDelay
        );

        deactivateCoroutine = null;

        if (playerCount > 0)
            yield break;

        DeactivateMonsters();
    }

    /// <summary>
    /// 맵의 모든 몬스터와 리스폰 작업을 정리합니다.
    /// 다음 입장 시 모든 몬스터는 초기 상태로 재등장합니다.
    /// </summary>
    [Server]
    private void DeactivateMonsters()
    {
        isMapActive = false;

        foreach (SpawnEntry entry in spawnEntries)
        {
            if (entry == null)
                continue;

            if (entry.respawnCoroutine != null)
            {
                StopCoroutine(
                    entry.respawnCoroutine
                );

                entry.respawnCoroutine = null;
            }

            if (entry.monsterInstance == null ||
                !entry.isSpawned)
            {
                continue;
            }

            UnSpawnMonster(entry);
        }
    }

    /// <summary>
    /// 네트워크에서 몬스터를 제거하고
    /// 서버 풀 상태로 보관합니다.
    /// </summary>
    [Server]
    private void UnSpawnMonster(
        SpawnEntry entry)
    {
        if (entry == null ||
            entry.monsterInstance == null ||
            !entry.isSpawned)
        {
            return;
        }

        NetworkServer.UnSpawn(
            entry.monsterInstance.gameObject
        );

        entry.monsterInstance.gameObject.SetActive(
            false
        );

        entry.isSpawned = false;
    }

    private SpawnEntry FindEntry(
        MonsterHealth monster)
    {
        foreach (SpawnEntry entry in spawnEntries)
        {
            if (entry != null &&
                entry.monsterInstance == monster)
            {
                return entry;
            }
        }

        return null;
    }

    private bool IsValidEntry(
        SpawnEntry entry)
    {
        if (entry != null &&
            entry.monsterId > 0 &&
            entry.monsterPrefab != null &&
            entry.spawnPoint != null)
        {
            return true;
        }

        Debug.LogWarning(
            "몬스터 생성 정보가 올바르지 않습니다.",
            this
        );

        return false;
    }

    public override void OnStopServer()
    {
        if (deactivateCoroutine != null)
        {
            StopCoroutine(
                deactivateCoroutine
            );

            deactivateCoroutine = null;
        }

        foreach (SpawnEntry entry in spawnEntries)
        {
            if (entry == null)
                continue;

            if (entry.respawnCoroutine != null)
            {
                StopCoroutine(
                    entry.respawnCoroutine
                );

                entry.respawnCoroutine = null;
            }

            if (entry.monsterInstance != null)
            {
                entry.monsterInstance.DeathCompleted -=
                    OnMonsterDeathCompleted;
            }
        }

        base.OnStopServer();
    }
}