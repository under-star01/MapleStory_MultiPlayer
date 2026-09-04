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

        playerCount = 0;
        isMapActive = false;
    }

    // 첫 번째 플레이어 입장 시 맵의 몬스터 활성화
    [Server]
    public void OnPlayerEnteredMap()
    {
        playerCount++;

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

    // 마지막 플레이어 퇴장 시 몬스터 비활성화 대기 시작
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

    // 맵의 모든 몬스터 생성 또는 재사용
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

    // 몬스터 인스턴스 최초 생성
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
            Destroy(
                monster.gameObject
            );

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

    // DB의 몬스터 데이터를 생성된 인스턴스에 적용
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

    // 풀에 보관된 몬스터 재등장
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

    // 사망 완료 후 몬스터 반환 및 리스폰 대기
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

        if (!isMapActive ||
            playerCount <= 0)
        {
            yield break;
        }

        SpawnMonster(entry);
    }

    // 일정 시간 동안 빈 맵이면 몬스터 비활성화
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

    // 맵의 모든 몬스터와 리스폰 작업 정리
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

    // 네트워크에서 제거 후 서버 풀 상태로 보관
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