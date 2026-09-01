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

    /*
     * 유저 데이터는 전체 캐싱하지 않습니다.
     * 로그인·회원가입 시 필요한 유저만 DB에서 조회합니다.
     */
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

    private async Task InitializeAsync()
    {
        try
        {
            string connectionString =
                DatabaseConfig
                    .CreateConnectionString();

            /*
             * 공통 정적 데이터 Repository
             */
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

            /*
             * 유저 데이터 Repository와 Service
             *
             * 유저 데이터는 서버 시작 시 전체 조회하지 않고,
             * 회원가입·로그인 시 필요한 유저만 조회합니다.
             */
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

            /*
             * 모든 초기화가 성공한 뒤 공개합니다.
             * 초기화 도중 오류가 나면 외부 시스템이
             * 불완전한 Repository를 사용하지 않습니다.
             */
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