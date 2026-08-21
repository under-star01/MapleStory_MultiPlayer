using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class QuickSlotSettingUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private PlayerQuickSlotController quickSlotController;

    [SerializeField]
    private Transform keyboardArea;

    [SerializeField]
    private Image pickedIcon;

    [Header("Test Skill Icons")]
    [SerializeField]
    private Sprite jumpIcon;

    [SerializeField]
    private Sprite basicAttackIcon;

    private readonly Dictionary<QuickKey, QuickKeySlotUI>
        keySlots = new();

    // 현재 마우스로 집은 스킬
    private SkillId pickedSkillId = SkillId.None;

    /*
     * 키보드 슬롯에서 집은 경우 원래 키를 보관합니다.
     * 스킬 팔레트에서 집은 경우에는 null입니다.
     */
    private QuickKey? pickedSourceKey;

    private bool IsPicking =>
        pickedSkillId != SkillId.None;

    private void Awake()
    {
        CollectKeySlots();
        HidePickedIcon();
    }

    private void OnEnable()
    {
        if (quickSlotController != null)
        {
            quickSlotController.BindingsChanged +=
                RefreshAllSlots;
        }

        RefreshAllSlots();
    }

    private void OnDisable()
    {
        if (quickSlotController != null)
        {
            quickSlotController.BindingsChanged -=
                RefreshAllSlots;
        }
    }

    private void Update()
    {
        FollowMouse();
    }

    /// <summary>
    /// QuickKeySlotUI에서 키가 클릭됐을 때 호출합니다.
    /// </summary>
    public void OnKeySlotClicked(QuickKey clickedKey)
    {
        if (quickSlotController == null)
            return;

        if (!IsPicking)
        {
            PickFromKey(clickedKey);
            return;
        }

        PlaceOnKey(clickedKey);
    }

    /// <summary>
    /// 임시 스킬 팔레트에서 스킬을 집을 때 호출합니다.
    /// </summary>
    public void PickSkill(SkillId skillId)
    {
        Sprite icon = GetSkillIcon(skillId);

        if (skillId == SkillId.None ||
            icon == null)
        {
            return;
        }

        pickedSkillId = skillId;
        pickedSourceKey = null;

        ShowPickedIcon(icon);
    }

    /// <summary>
    /// BG나 BackgroundBlocker 같은 빈 공간을
    /// 클릭했을 때 호출합니다.
    /// </summary>
    public void OnEmptyAreaClicked()
    {
        if (!IsPicking)
            return;

        /*
         * 키보드에서 집은 아이콘이면
         * 원래 키의 바인딩을 제거합니다.
         *
         * 스킬 팔레트에서 집은 아이콘이면
         * 선택만 취소합니다.
         */
        if (pickedSourceKey.HasValue)
        {
            quickSlotController.ClearSlot(
                pickedSourceKey.Value
            );
        }

        CancelPick();
    }

    /// <summary>
    /// 현재 선택 상태만 취소합니다.
    /// 기존 바인딩은 유지합니다.
    /// </summary>
    public void CancelPick()
    {
        pickedSkillId = SkillId.None;
        pickedSourceKey = null;

        HidePickedIcon();
        RefreshAllSlots();
    }

    public void Open()
    {
        gameObject.SetActive(true);
    }

    public void Close()
    {
        CancelPick();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 모든 키 슬롯을 실제 바인딩 상태에 맞춰 갱신합니다.
    /// </summary>
    public void RefreshAllSlots()
    {
        if (quickSlotController == null)
            return;

        foreach (KeyValuePair<QuickKey, QuickKeySlotUI>
                 pair in keySlots)
        {
            QuickKey key = pair.Key;
            QuickKeySlotUI slotUI = pair.Value;

            if (!quickSlotController.TryGetBoundSkillId(
                    key,
                    out SkillId skillId))
            {
                slotUI.Clear();
                continue;
            }

            Sprite icon = GetSkillIcon(skillId);

            if (icon == null)
            {
                slotUI.Clear();
                continue;
            }

            bool hideIcon =
                pickedSourceKey.HasValue &&
                pickedSourceKey.Value == key;

            slotUI.Refresh(
                icon,
                hideIcon
            );
        }
    }

    private void PickFromKey(QuickKey key)
    {
        if (!quickSlotController.TryGetBoundSkillId(
                key,
                out SkillId skillId))
        {
            return;
        }

        Sprite icon = GetSkillIcon(skillId);

        if (icon == null)
            return;

        pickedSkillId = skillId;
        pickedSourceKey = key;

        ShowPickedIcon(icon);
        RefreshAllSlots();
    }

    private void PlaceOnKey(QuickKey targetKey)
    {
        bool success;

        if (pickedSourceKey.HasValue)
        {
            QuickKey sourceKey =
                pickedSourceKey.Value;

            // 집었던 원래 키를 다시 클릭하면 제자리에 놓습니다.
            if (sourceKey == targetKey)
            {
                CancelPick();
                return;
            }

            success = quickSlotController.MoveOrSwap(
                sourceKey,
                targetKey
            );
        }
        else
        {
            /*
             * 스킬 팔레트에서 가져온 스킬은
             * 대상 키에 새로 바인딩합니다.
             */
            success = quickSlotController.BindSkill(
                targetKey,
                pickedSkillId
            );
        }

        if (!success)
            return;

        pickedSkillId = SkillId.None;
        pickedSourceKey = null;

        HidePickedIcon();
        RefreshAllSlots();
    }

    private void CollectKeySlots()
    {
        keySlots.Clear();

        if (keyboardArea == null)
        {
            Debug.LogError(
                "KeyboardArea가 연결되지 않았습니다.",
                this
            );

            return;
        }

        QuickKeySlotUI[] slots =
            keyboardArea.GetComponentsInChildren
                <QuickKeySlotUI>(true);

        foreach (QuickKeySlotUI slot in slots)
        {
            if (keySlots.ContainsKey(slot.Key))
            {
                Debug.LogWarning(
                    $"중복된 키 슬롯입니다: {slot.Key}",
                    slot
                );

                continue;
            }

            keySlots.Add(
                slot.Key,
                slot
            );

            slot.Initialize(this);
        }
    }

    private Sprite GetSkillIcon(SkillId skillId)
    {
        return skillId switch
        {
            SkillId.Jump =>
                jumpIcon,

            SkillId.BasicAttack =>
                basicAttackIcon,

            _ =>
                null
        };
    }

    private void ShowPickedIcon(Sprite icon)
    {
        if (pickedIcon == null)
            return;

        pickedIcon.sprite = icon;
        SetImageAlpha(pickedIcon, 1f);
        pickedIcon.gameObject.SetActive(true);
    }

    private void HidePickedIcon()
    {
        if (pickedIcon == null)
            return;

        pickedIcon.sprite = null;
        SetImageAlpha(pickedIcon, 0f);
        pickedIcon.gameObject.SetActive(false);
    }

    private void FollowMouse()
    {
        if (!IsPicking ||
            pickedIcon == null ||
            Mouse.current == null)
        {
            return;
        }

        pickedIcon.transform.position =
            Mouse.current.position.ReadValue();
    }

    private void SetImageAlpha(
        Image image,
        float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}