using System.Collections.Generic;
using UnityEngine;

public class DatabaseManager : MonoBehaviour
{
    public static DatabaseManager Instance
    {
        get;
        private set;
    }

    public StaticGameDataCache StaticData
    {
        get;
        private set;
    }

    public bool IsInitializing
    {
        get;
        private set;
    }

    public bool IsInitializationComplete
    {
        get;
        private set;
    }

    public bool IsInitialized
    {
        get;
        private set;
    }

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void BeginInitialization()
    {
        if (IsInitializing ||
            IsInitializationComplete)
        {
            return;
        }

        IsInitializing = true;

        _ = InitializeAsync();
    }

    private async System.Threading.Tasks.Task
        InitializeAsync()
    {
        try
        {
            string connectionString =
                DatabaseConfig
                    .CreateConnectionString();

            MonsterRepository monsterRepository =
                new MonsterRepository(
                    connectionString
                );

            ItemRepository itemRepository =
                new ItemRepository(
                    connectionString
                );

            List<MonsterRecord> monsters =
                await monsterRepository.LoadAllAsync();

            List<ItemRecord> items =
                await itemRepository.LoadAllAsync();

            StaticData =
                new StaticGameDataCache();

            StaticData.SetMonsters(
                monsters
            );

            StaticData.SetItems(
                items
            );

            IsInitialized = true;

            Debug.Log(
                $"[Database] 정적 데이터 초기화 완료 / " +
                $"Monster: {StaticData.MonsterCount}, " +
                $"Item: {StaticData.ItemCount}"
            );
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                $"[Database] 데이터 초기화 오류\n" +
                $"{exception}"
            );
        }
        finally
        {
            IsInitializing = false;
            IsInitializationComplete = true;
        }
    }
}