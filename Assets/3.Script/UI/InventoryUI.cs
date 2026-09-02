using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class InventoryUI :
    MonoBehaviour,
    ILocalPlayerUI
{
    [Header("References")]
    [SerializeField]
    private Transform slotsRoot;

    [SerializeField]
    private QuickSlotSettingUI quickSlotSettingUI;

    [Header("Consumables")]
    [SerializeField]
    private List<ConsumableId>
        displayedConsumables = new();

    [Header("Window")]
    [SerializeField]
    private bool startOpened;

    private readonly List<InventorySlotUI>
        slots = new();

    private PlayerInventory inventory;
    private CanvasGroup canvasGroup;

    private bool isInventorySubscribed;

    public bool IsOpened { get; private set; }

    private void Awake()
    {
        canvasGroup =
            GetComponent<CanvasGroup>();

        CollectSlots(); 
        
        SetWindowVisible(
            startOpened,
            false
        );
    }

    private void OnEnable()
    {
        SubscribeInventory();
        RefreshAllSlots();
    }

    private void OnDisable()
    {
        UnsubscribeInventory();
    }

    /// <summary>
    /// 현재 클라이언트의 로컬 플레이어를 연결합니다.
    /// </summary>
    public void Bind(
        LocalPlayerContext context)
    {
        if (context?.Player == null)
        {
            Debug.LogError(
                $"{nameof(LocalPlayerContext)}의 " +
                "Player가 없습니다.",
                this
            );

            return;
        }

        PlayerInventory newInventory =
            context.Player.GetComponent
                <PlayerInventory>();

        if (newInventory == null)
        {
            Debug.LogError(
                $"{nameof(PlayerInventory)}를 " +
                "찾지 못했습니다.",
                context.Player
            );

            return;
        }

        if (inventory != newInventory)
        {
            Unbind();
            inventory = newInventory;
        }

        SubscribeInventory();
        RefreshAllSlots();
    }

    /// <summary>
    /// 현재 플레이어와 연결된 이벤트와 참조를 정리합니다.
    /// </summary>
    public void Unbind()
    {
        UnsubscribeInventory();

        inventory = null;

        ClearAllSlots();
    }

    /// <summary>
    /// 인벤토리 슬롯에서 소비 아이템을 클릭했을 때 호출합니다.
    /// 퀵슬롯 설정 창이 열려 있을 때만 아이템을 집을 수 있습니다.
    /// </summary>
    public void OnConsumableClicked(
        ConsumableId consumableId)
    {
        if (inventory == null ||
            quickSlotSettingUI == null ||
            !quickSlotSettingUI.IsOpened ||
            consumableId == ConsumableId.None)
        {
            return;
        }

        if (inventory.GetConsumableCount(
                consumableId) <= 0)
        {
            return;
        }

        quickSlotSettingUI.PickConsumable(
            consumableId
        );
    }

    public void Open()
    {
        SetWindowVisible(true);
        RefreshAllSlots();
    }

    public void Close()
    {
        SetWindowVisible(false);
    }

    public void Toggle()
    {
        SetWindowVisible(
            !IsOpened
        );

        if (IsOpened)
        {
            RefreshAllSlots();
        }
    }

    /// <summary>
    /// 현재 보유 중인 소비 아이템을
    /// 앞쪽 슬롯부터 차례대로 표시합니다.
    /// </summary>
    public void RefreshAllSlots()
    {
        ClearAllSlots();

        if (inventory == null)
            return;

        int slotIndex = 0;

        foreach (ConsumableId consumableId
                 in displayedConsumables)
        {
            if (slotIndex >= slots.Count)
                break;

            int count =
                inventory.GetConsumableCount(
                    consumableId
                );

            if (count <= 0)
                continue;

            if (!inventory.TryGetConsumableData(
                    consumableId,
                    out ConsumableData data))
            {
                continue;
            }

            slots[slotIndex].Refresh(
                data,
                count
            );

            slotIndex++;
        }
    }

    private void SubscribeInventory()
    {
        if (!isActiveAndEnabled ||
            isInventorySubscribed ||
            inventory == null)
        {
            return;
        }

        inventory.InventoryChanged +=
            RefreshAllSlots;

        isInventorySubscribed = true;
    }

    private void UnsubscribeInventory()
    {
        if (!isInventorySubscribed)
            return;

        if (inventory != null)
        {
            inventory.InventoryChanged -=
                RefreshAllSlots;
        }

        isInventorySubscribed = false;
    }

    private void CollectSlots()
    {
        slots.Clear();

        if (slotsRoot == null)
        {
            Debug.LogError(
                "Slots Root가 연결되지 않았습니다.",
                this
            );

            return;
        }

        InventorySlotUI[] foundSlots =
            slotsRoot.GetComponentsInChildren
                <InventorySlotUI>(true);

        foreach (InventorySlotUI slot
                 in foundSlots)
        {
            slots.Add(slot);
            slot.Initialize(this);
        }
    }

    private void ClearAllSlots()
    {
        foreach (InventorySlotUI slot
                 in slots)
        {
            slot.Clear();
        }
    }

    private void SetWindowVisible(
        bool visible,
        bool playSound = true)
    {
        IsOpened = visible;

        if (playSound)
        {
            AudioManager.Instance?.PlayEffect(
                visible
                    ? EffectSoundId.UIOpen
                    : EffectSoundId.UIClose
            );
        }

        canvasGroup.alpha =
            visible ? 1f : 0f;

        canvasGroup.interactable =
            visible;

        canvasGroup.blocksRaycasts =
            visible;
    }
}