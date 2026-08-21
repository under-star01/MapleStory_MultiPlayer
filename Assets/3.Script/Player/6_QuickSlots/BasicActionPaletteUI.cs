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
    private PlayerQuickSlotController quickSlotController;

    [SerializeField]
    private QuickSlotSettingUI quickSlotSettingUI;

    [Header("Initial Basic Actions")]
    [SerializeField]
    private List<BasicActionEntry> basicActions = new();

    private readonly List<BasicActionSlotUI>
        slots = new();

    private readonly Dictionary
        <BasicActionId, IQuickSlotCommand>
        commands = new();

    private void Awake()
    {
        CollectSlots();
        CreateCommands();
    }

    private IEnumerator Start()
    {
        /*
         * PlayerQuickSlotController가 Start에서
         * 기본 스킬을 먼저 등록하도록 한 프레임 기다립니다.
         */
        yield return null;

        InitializePalette();
    }

    /// <summary>
    /// 기본 기능을 생성한 뒤,
    /// 기본 키가 지정되어 있으면 해당 키에 등록합니다.
    /// 등록하지 못하면 팔레트에 표시합니다.
    /// </summary>
    private void InitializePalette()
    {
        foreach (BasicActionEntry entry in basicActions)
        {
            if (!IsValid(entry))
                continue;

            // 이미 다른 키에 존재한다면 중복 생성하지 않습니다.
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
             * 기본 키를 사용하고 해당 키가 비어 있다면
             * 시작 시 바로 바인딩합니다.
             */
            if (entry.useDefaultBinding &&
                quickSlotController != null &&
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
            return;

        commands.Add(
            BasicActionId.OpenQuickSlotSetting,
            new OpenQuickSlotSettingCommand(
                quickSlotSettingUI
            )
        );

        /*
         * 기능 구현 후 추가합니다.
         *
         * commands.Add(
         *     BasicActionId.Interact,
         *     new InteractCommand(...)
         * );
         *
         * commands.Add(
         *     BasicActionId.OpenSkillWindow,
         *     new OpenSkillWindowCommand(...)
         * );
         *
         * commands.Add(
         *     BasicActionId.OpenInventory,
         *     new OpenInventoryCommand(...)
         * );
         */
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