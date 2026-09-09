using System.Collections.Generic;
using System.Threading.Tasks;
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

    public UserRepository UserRepository
    {
        get;
        private set;
    }

    public UserAccountService UserAccountService
    {
        get;
        private set;
    }

    public PlayerInventoryRepository PlayerInventoryRepository
    {
        get;
        private set;
    }

    public PlayerQuickSlotRepository PlayerQuickSlotRepository
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
        IsInitialized = false;

        _ = InitializeAsync();
    }

    // DB Repository 생성 및 공통 정적 데이터 초기화
    private async Task InitializeAsync()
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

            MonsterDropRepository dropRepository =
                new MonsterDropRepository(
                    connectionString
                );

            UserRepository userRepository =
                new UserRepository(
                    connectionString
                );

            UserAccountService userAccountService =
                new UserAccountService(
                    userRepository
                );

            PlayerInventoryRepository playerInventoryRepository =
                new PlayerInventoryRepository(
                    connectionString
                );

            PlayerQuickSlotRepository playerQuickSlotRepository =
                new PlayerQuickSlotRepository(
                    connectionString
                );

            List<MonsterRecord> monsters =
                await monsterRepository
                    .LoadAllAsync();

            List<ItemRecord> items =
                await itemRepository
                    .LoadAllAsync();

            List<MonsterDropRecord> monsterDrops =
                await dropRepository
                    .LoadAllAsync();

            StaticData =
                new StaticGameDataCache();

            StaticData.SetMonsters(
                monsters
            );

            StaticData.SetItems(
                items
            );

            StaticData.SetMonsterDrops(
                monsterDrops
            );

            // 모든 초기화가 성공한 뒤 외부에 Repository 공개
            UserRepository =
                userRepository;

            UserAccountService =
                userAccountService;

            PlayerInventoryRepository =
                playerInventoryRepository;

            PlayerQuickSlotRepository =
                playerQuickSlotRepository;

            IsInitialized = true;

            Debug.Log(
                $"[Database] 데이터 초기화 완료 / " +
                $"Monster: {StaticData.MonsterCount}, " +
                $"Item: {StaticData.ItemCount}, " +
                $"MonsterDrop: {StaticData.MonsterDropCount}, " +
                $"UserService: Ready, " +
                $"InventoryRepository: Ready, " +
                $"QuickSlotRepository: Ready"
            );
        }
        catch (System.Exception exception)
        {
            StaticData = null;
            UserRepository = null;
            UserAccountService = null;
            PlayerInventoryRepository = null;
            PlayerQuickSlotRepository = null;

            IsInitialized = false;

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