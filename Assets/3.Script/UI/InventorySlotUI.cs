using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class InventorySlotUI :
    MonoBehaviour,
    IPointerClickHandler
{
    [SerializeField]
    private TMP_Text countText;

    private InventoryUI inventoryUI;
    private Image iconImage;

    private ConsumableId consumableId =
        ConsumableId.None;

    private bool IsEmpty =>
        consumableId == ConsumableId.None;

    private void Awake()
    {
        iconImage =
            GetComponent<Image>();

        Clear();
    }

    /// <summary>
    /// 클릭 결과를 전달할 인벤토리 UI를 연결합니다.
    /// </summary>
    public void Initialize(
        InventoryUI owner)
    {
        inventoryUI = owner;
    }

    /// <summary>
    /// 슬롯에 소비 아이템 정보와 수량을 표시합니다.
    /// </summary>
    public void Refresh(
        ConsumableData data,
        int count)
    {
        if (data == null ||
            data.Id == ConsumableId.None ||
            data.Icon == null ||
            count <= 0)
        {
            Clear();
            return;
        }

        consumableId =
            data.Id;

        iconImage.sprite =
            data.Icon;

        SetIconVisible(true);

        if (countText == null)
            return;

        countText.text =
            count.ToString();

        countText.gameObject.SetActive(true);
    }

    /// <summary>
    /// 슬롯을 빈 상태로 표시합니다.
    /// </summary>
    public void Clear()
    {
        consumableId =
            ConsumableId.None;

        EnsureIconImage();

        iconImage.sprite = null;
        SetIconVisible(false);

        if (countText == null)
            return;

        countText.text =
            string.Empty;

        countText.gameObject.SetActive(false);
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (eventData.button !=
                PointerEventData.InputButton.Left ||
            IsEmpty ||
            inventoryUI == null)
        {
            return;
        }

        inventoryUI.OnConsumableClicked(
            consumableId
        );
    }

    private void SetIconVisible(
        bool visible)
    {
        EnsureIconImage();

        Color color =
            iconImage.color;

        color.a =
            visible ? 1f : 0f;

        iconImage.color =
            color;
    }

    private void EnsureIconImage()
    {
        if (iconImage == null)
        {
            iconImage =
                GetComponent<Image>();
        }
    }
}