using System;
using System.Collections.Generic;
using UnityEngine;

public class DefaultActionPaletteUI : MonoBehaviour
{
    [Serializable]
    private class DefaultActionEntry
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

    [SerializeField]
    private SkillUI skillUI;

    [Header("Default Actions")]
    [SerializeField]
    private List<DefaultActionEntry>
        defaultActions = new();

    private readonly List<DefaultActionSlotUI>
        slots = new();

    private PlayerQuickSlotController
        quickSlotController;

    private PlayerBasicActionController
        basicActionController;

    private void Awake()
    {
        CollectSlots();
    }

    // 로컬 플레이어의 퀵슬롯 연결
    public void Bind(
        PlayerQuickSlotController controller)
    {
        Unbind();

        if (controller == null)
            return;

        quickSlotController =
            controller;

        basicActionController =
            controller.GetComponent
                <PlayerBasicActionController>();

        if (basicActionController == null)
        {
            Debug.LogError(
                $"{nameof(PlayerBasicActionController)}를 " +
                "찾지 못했습니다.",
                controller
            );

            return;
        }

        RegisterActions();
        ClearPalette();
    }

    public void Unbind()
    {
        basicActionController?.Clear();

        quickSlotController = null;
        basicActionController = null;

        ClearPalette();
    }

    // 미등록 기본 행동을 기본 키 또는 팔레트에 배치
    public void InitializePalette(
        bool useDefaultBindings)
    {
        ClearPalette();

        if (quickSlotController == null ||
            basicActionController == null)
        {
            return;
        }

        foreach (DefaultActionEntry entry
                 in defaultActions)
        {
            if (entry == null ||
                entry.actionId ==
                    BasicActionId.None)
            {
                continue;
            }

            if (!basicActionController.TryGetAction(
                    entry.actionId,
                    out BasicActionData actionData))
            {
                continue;
            }

            if (IsBound(entry.actionId))
                continue;

            if (useDefaultBindings &&
                entry.useDefaultBinding &&
                quickSlotController.IsSlotEmpty(
                    entry.defaultKey) &&
                quickSlotController.BindBasicAction(
                    entry.defaultKey,
                    entry.actionId))
            {
                continue;
            }

            AddToPalette(
                actionData
            );
        }
    }

    // 퀵슬롯에서 제거된 기본 행동을 팔레트에 복원
    public bool RestoreAction(
        QuickSlotBinding binding)
    {
        if (binding.Type !=
            QuickSlotBindingType.BasicAction)
        {
            return false;
        }

        BasicActionId actionId =
            binding.BasicActionId;

        if (actionId ==
                BasicActionId.None ||
            IsBound(actionId) ||
            Contains(actionId))
        {
            return false;
        }

        if (!TryGetBasicActionData(
                actionId,
                out BasicActionData actionData))
        {
            return false;
        }

        return AddToPalette(
            actionData
        );
    }

    public bool TryGetBasicActionData(
        BasicActionId actionId,
        out BasicActionData actionData)
    {
        actionData = null;

        if (basicActionController == null)
            return false;

        return basicActionController.TryGetAction(
            actionId,
            out actionData
        );
    }

    // 기본 행동 데이터와 Command 등록
    private void RegisterActions()
    {
        basicActionController.Clear();

        foreach (DefaultActionEntry entry
                 in defaultActions)
        {
            if (!TryCreateActionData(
                    entry,
                    out BasicActionData actionData))
            {
                continue;
            }

            if (!basicActionController.Register(
                    actionData))
            {
                Debug.LogWarning(
                    $"BasicAction 등록 실패: " +
                    $"{entry.actionId}",
                    this
                );
            }
        }
    }

    private bool TryCreateActionData(
        DefaultActionEntry entry,
        out BasicActionData actionData)
    {
        actionData = null;

        if (entry == null ||
            entry.actionId ==
                BasicActionId.None ||
            entry.icon == null)
        {
            return false;
        }

        IQuickSlotCommand command =
            CreateCommand(
                entry.actionId
            );

        if (command == null)
        {
            Debug.LogWarning(
                $"Command를 생성할 수 없습니다: " +
                $"{entry.actionId}",
                this
            );

            return false;
        }

        actionData =
            new BasicActionData(
                entry.actionId,
                entry.icon,
                command
            );

        return true;
    }

    private IQuickSlotCommand CreateCommand(
        BasicActionId actionId)
    {
        return actionId switch
        {
            BasicActionId.OpenQuickSlotUI =>
                new OpenQuickSlotSettingCommand(
                    quickSlotSettingUI
                ),

            BasicActionId.OpenInventoryUI =>
                new OpenInventoryCommand(
                    inventoryUI
                ),

            BasicActionId.OpenSkillUI =>
                new OpenSkillUICommand(
                    skillUI
                ),

            _ => null
        };
    }

    private bool AddToPalette(
        BasicActionData actionData)
    {
        if (actionData == null)
            return false;

        foreach (DefaultActionSlotUI slot
                 in slots)
        {
            if (!slot.IsEmpty)
                continue;

            slot.SetBasicAction(
                actionData.ActionId,
                actionData.Icon,
                quickSlotSettingUI
            );

            return true;
        }

        Debug.LogWarning(
            $"BasicAction을 배치할 빈 팔레트 슬롯이 없습니다: " +
            $"{actionData.ActionId}",
            this
        );

        return false;
    }

    private bool IsBound(
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
                binding.BasicActionId ==
                    actionId)
            {
                return true;
            }
        }

        return false;
    }

    private bool Contains(
        BasicActionId actionId)
    {
        foreach (DefaultActionSlotUI slot
                 in slots)
        {
            if (!slot.IsEmpty &&
                slot.ActionId == actionId)
            {
                return true;
            }
        }

        return false;
    }

    private void CollectSlots()
    {
        slots.Clear();

        DefaultActionSlotUI[] foundSlots =
            GetComponentsInChildren
                <DefaultActionSlotUI>(true);

        Array.Sort(
            foundSlots,
            (a, b) =>
                a.transform.GetSiblingIndex()
                    .CompareTo(
                        b.transform.GetSiblingIndex()
                    )
        );

        slots.AddRange(
            foundSlots
        );
    }

    private void ClearPalette()
    {
        foreach (DefaultActionSlotUI slot
                 in slots)
        {
            slot.Clear();
        }
    }
}