using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicActionPaletteUI : MonoBehaviour
{
    [Serializable]
    private class BasicActionEntry
    {
        public BasicActionId actionId;
        public Sprite icon;

        [Header("Default Binding")]
        public bool useDefaultBinding;
        public QuickKey defaultKey;
    }

    [Header("References")]
    [SerializeField]
    private QuickSlotSettingUI quickSlotSettingUI;

    [SerializeField]
    private InventoryUI inventoryUI;

    [Header("Initial Basic Actions")]
    [SerializeField]
    private List<BasicActionEntry> basicActions = new();

    private readonly List<BasicActionSlotUI>
        slots = new();

    private readonly Dictionary<BasicActionId, IQuickSlotCommand>
        commands = new();

    private PlayerQuickSlotController quickSlotController;
    private Coroutine initializeCoroutine;

    private void Awake()
    {
        CollectSlots();
        CreateCommands();
    }

    /// <summary>
    /// 로컬 플레이어의 퀵슬롯 컨트롤러를 연결하고
    /// 기본 기능 팔레트를 초기화합니다.
    /// </summary>
    public void Bind(
        PlayerQuickSlotController controller)
    {
        if (controller == null)
        {
            Debug.LogError(
                $"{nameof(PlayerQuickSlotController)}가 null입니다.",
                this
            );

            return;
        }

        if (quickSlotController == controller)
            return;

        Unbind();

        quickSlotController = controller;

        /*
         * PlayerQuickSlotController의 Start에서
         * 기본 스킬 바인딩이 먼저 처리되도록
         * 한 프레임 뒤 초기화합니다.
         */
        initializeCoroutine =
            StartCoroutine(InitializeNextFrame());
    }

    /// <summary>
    /// 현재 플레이어와의 연결을 해제하고
    /// 팔레트 표시를 초기화합니다.
    /// </summary>
    public void Unbind()
    {
        if (initializeCoroutine != null)
        {
            StopCoroutine(initializeCoroutine);
            initializeCoroutine = null;
        }

        quickSlotController = null;

        ClearPalette();
    }

    private IEnumerator InitializeNextFrame()
    {
        yield return null;

        initializeCoroutine = null;

        if (quickSlotController == null)
            yield break;

        InitializePalette();
    }

    /// <summary>
    /// 기본 기능을 생성한 뒤,
    /// 기본 키가 지정되어 있으면 해당 키에 등록합니다.
    /// 등록하지 못하면 팔레트에 표시합니다.
    /// </summary>
    private void InitializePalette()
    {
        ClearPalette();

        foreach (BasicActionEntry entry in basicActions)
        {
            if (!IsValid(entry))
                continue;

            // 이미 다른 키에 바인딩되어 있다면
            // 팔레트에 중복 생성하지 않습니다.
            if (IsActionBound(entry.actionId))
                continue;

            if (!commands.TryGetValue(
                    entry.actionId,
                    out IQuickSlotCommand command))
            {
                Debug.LogWarning(
                    $"Command가 등록되지 않은 기본 기능입니다: " +
                    $"{entry.actionId}",
                    this
                );

                continue;
            }

            BasicActionData actionData =
                new BasicActionData(
                    entry.actionId,
                    entry.icon,
                    command
                );

            /*
             * 기본 키를 사용하며 해당 키가 비어 있다면
             * 시작 시 바로 바인딩합니다.
             */
            if (entry.useDefaultBinding &&
                quickSlotController.IsSlotEmpty(
                    entry.defaultKey))
            {
                bool success =
                    quickSlotController.BindBasicAction(
                        entry.defaultKey,
                        actionData
                    );

                if (success)
                    continue;
            }

            // 기본 키에 넣지 못했다면 팔레트에 표시합니다.
            RestoreAction(actionData);
        }
    }

    /// <summary>
    /// QuickSlot에서 제거된 기본 기능을
    /// 첫 번째 빈 팔레트 슬롯에 되돌립니다.
    /// </summary>
    public bool RestoreAction(
        BasicActionData actionData)
    {
        if (actionData == null)
            return false;

        if (ContainsAction(actionData.ActionId))
            return false;

        foreach (BasicActionSlotUI slot in slots)
        {
            if (!slot.IsEmpty)
                continue;

            slot.SetAction(
                actionData,
                quickSlotSettingUI
            );

            return true;
        }

        Debug.LogWarning(
            $"BasicActionPalette에 빈 슬롯이 없습니다: " +
            $"{actionData.ActionId}",
            this
        );

        return false;
    }

    private void ClearPalette()
    {
        foreach (BasicActionSlotUI slot in slots)
        {
            slot.Clear();
        }
    }

    private void CollectSlots()
    {
        slots.Clear();

        BasicActionSlotUI[] foundSlots =
            GetComponentsInChildren
                <BasicActionSlotUI>(true);

        Array.Sort(
            foundSlots,
            (left, right) =>
                left.transform
                    .GetSiblingIndex()
                    .CompareTo(
                        right.transform
                            .GetSiblingIndex()
                    )
        );

        slots.AddRange(foundSlots);
    }

    private void CreateCommands()
    {
        commands.Clear();

        if (quickSlotSettingUI == null)
        {
            Debug.LogError(
                $"{nameof(QuickSlotSettingUI)}가 " +
                "연결되지 않았습니다.",
                this
            );
        }
        else
        {
            commands.Add(
                BasicActionId.OpenQuickSlotSetting,
                new OpenQuickSlotSettingCommand(
                    quickSlotSettingUI
                )
            );
        }

        if (inventoryUI == null)
        {
            Debug.LogError(
                $"{nameof(InventoryUI)}가 " +
                "연결되지 않았습니다.",
                this
            );
        }
        else
        {
            commands.Add(
                BasicActionId.OpenInventory,
                new OpenInventoryCommand(
                    inventoryUI
                )
            );
        }
    }

    private bool ContainsAction(
        BasicActionId actionId)
    {
        foreach (BasicActionSlotUI slot in slots)
        {
            if (slot.IsEmpty)
                continue;

            if (slot.ActionData.ActionId == actionId)
                return true;
        }

        return false;
    }

    private bool IsActionBound(
        BasicActionId actionId)
    {
        if (quickSlotController == null)
            return false;

        foreach (QuickKey key in
                 Enum.GetValues(typeof(QuickKey)))
        {
            if (!quickSlotController.TryGetBinding(
                    key,
                    out QuickSlotBinding binding))
            {
                continue;
            }

            if (binding.Type ==
                    QuickSlotBindingType.BasicAction &&
                binding.BasicActionId == actionId)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsValid(
        BasicActionEntry entry)
    {
        return entry != null &&
               entry.actionId != BasicActionId.None &&
               entry.icon != null;
    }
}