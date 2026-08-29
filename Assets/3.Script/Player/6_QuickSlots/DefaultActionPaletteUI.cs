using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DefaultActionPaletteUI : MonoBehaviour
{
    [Serializable]
    private class DefaultActionEntry
    {
        public QuickSlotBindingType type;

        [Header("Action")]
        public SkillId skillId;
        public BasicActionId basicActionId;
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

    [Header("Initial Default Actions")]
    [SerializeField]
    private List<DefaultActionEntry>
        defaultActions = new();

    private readonly List<DefaultActionSlotUI>
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

        initializeCoroutine =
            StartCoroutine(InitializeNextFrame());
    }

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

        if (quickSlotController != null)
        {
            InitializePalette();
        }
    }

    /// <summary>
    /// 등록된 기본 제공 항목을 기본 키 또는 팔레트에 배치합니다.
    /// </summary>
    private void InitializePalette()
    {
        ClearPalette();

        foreach (DefaultActionEntry entry
                 in defaultActions)
        {
            if (!TryCreateAction(
                    entry,
                    out QuickSlotBinding binding,
                    out BasicActionData actionData))
            {
                continue;
            }

            /*
             * 이미 퀵슬롯에 등록되어 있다면
             * 팔레트에 중복으로 표시하지 않습니다.
             */
            if (IsBound(binding))
                continue;

            if (entry.useDefaultBinding &&
                quickSlotController.IsSlotEmpty(
                    entry.defaultKey) &&
                BindToKey(
                    entry.defaultKey,
                    binding,
                    actionData))
            {
                continue;
            }

            RestoreAction(
                binding,
                actionData
            );
        }
    }

    /// <summary>
    /// 퀵슬롯에서 제거된 기본 제공 항목을
    /// 팔레트의 첫 번째 빈 슬롯에 복구합니다.
    ///
    /// Default Actions에 등록되지 않은 일반 스킬은
    /// 복구하지 않습니다.
    /// </summary>
    public bool RestoreAction(
        QuickSlotBinding binding,
        BasicActionData actionData = null)
    {
        DefaultActionEntry entry =
            FindEntry(binding);

        if (entry == null ||
            Contains(binding))
        {
            return false;
        }

        foreach (DefaultActionSlotUI slot
                 in slots)
        {
            if (!slot.IsEmpty)
                continue;

            switch (binding.Type)
            {
                case QuickSlotBindingType.Skill:
                    if (!quickSlotController.TryGetSkillIcon(
                            binding.SkillId,
                            out Sprite skillIcon))
                    {
                        return false;
                    }

                    slot.SetSkill(
                        binding.SkillId,
                        skillIcon,
                        quickSlotSettingUI
                    );

                    return true;

                case QuickSlotBindingType.BasicAction:
                    if (actionData == null)
                        return false;

                    slot.SetBasicAction(
                        actionData,
                        quickSlotSettingUI
                    );

                    return true;

                default:
                    return false;
            }
        }

        Debug.LogWarning(
            $"DefaultActionPalette에 빈 슬롯이 없습니다: " +
            $"{binding.Type}",
            this
        );

        return false;
    }

    /// <summary>
    /// Inspector에 등록된 항목을
    /// 실제 바인딩 데이터로 변환합니다.
    /// </summary>
    private bool TryCreateAction(
        DefaultActionEntry entry,
        out QuickSlotBinding binding,
        out BasicActionData actionData)
    {
        binding =
            QuickSlotBinding.Empty();

        actionData = null;

        if (entry == null)
            return false;

        switch (entry.type)
        {
            case QuickSlotBindingType.Skill:
                if (entry.skillId == SkillId.None ||
                    !quickSlotController.TryGetSkillIcon(
                        entry.skillId,
                        out _))
                {
                    return false;
                }

                binding =
                    QuickSlotBinding.FromSkill(
                        entry.skillId
                    );

                return true;

            case QuickSlotBindingType.BasicAction:
                if (entry.basicActionId ==
                        BasicActionId.None ||
                    entry.icon == null ||
                    !commands.TryGetValue(
                        entry.basicActionId,
                        out IQuickSlotCommand command))
                {
                    return false;
                }

                binding =
                    QuickSlotBinding.FromBasicAction(
                        entry.basicActionId
                    );

                actionData =
                    new BasicActionData(
                        entry.basicActionId,
                        entry.icon,
                        command
                    );

                return true;

            default:
                return false;
        }
    }

    private bool BindToKey(
        QuickKey key,
        QuickSlotBinding binding,
        BasicActionData actionData)
    {
        switch (binding.Type)
        {
            case QuickSlotBindingType.Skill:
                return quickSlotController.BindSkill(
                    key,
                    binding.SkillId
                );

            case QuickSlotBindingType.BasicAction:
                return quickSlotController.BindBasicAction(
                    key,
                    actionData
                );

            default:
                return false;
        }
    }

    /// <summary>
    /// Default Actions에 등록된 항목인지 확인합니다.
    /// </summary>
    private DefaultActionEntry FindEntry(
        QuickSlotBinding binding)
    {
        foreach (DefaultActionEntry entry
                 in defaultActions)
        {
            if (entry == null ||
                entry.type != binding.Type)
            {
                continue;
            }

            switch (binding.Type)
            {
                case QuickSlotBindingType.Skill:
                    if (entry.skillId ==
                        binding.SkillId)
                    {
                        return entry;
                    }

                    break;

                case QuickSlotBindingType.BasicAction:
                    if (entry.basicActionId ==
                        binding.BasicActionId)
                    {
                        return entry;
                    }

                    break;
            }
        }

        return null;
    }

    private bool Contains(
        QuickSlotBinding target)
    {
        foreach (DefaultActionSlotUI slot
                 in slots)
        {
            if (!slot.IsEmpty &&
                IsSameBinding(
                    slot.Binding,
                    target))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsBound(
        QuickSlotBinding target)
    {
        foreach (QuickKey key in
                 Enum.GetValues(typeof(QuickKey)))
        {
            if (quickSlotController.TryGetBinding(
                    key,
                    out QuickSlotBinding binding) &&
                IsSameBinding(
                    binding,
                    target))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSameBinding(
        QuickSlotBinding left,
        QuickSlotBinding right)
    {
        if (left.Type != right.Type)
            return false;

        switch (left.Type)
        {
            case QuickSlotBindingType.Skill:
                return left.SkillId ==
                       right.SkillId;

            case QuickSlotBindingType.BasicAction:
                return left.BasicActionId ==
                       right.BasicActionId;

            default:
                return false;
        }
    }

    private void CollectSlots()
    {
        slots.Clear();

        DefaultActionSlotUI[] foundSlots =
            GetComponentsInChildren
                <DefaultActionSlotUI>(true);

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

    private void ClearPalette()
    {
        foreach (DefaultActionSlotUI slot
                 in slots)
        {
            slot.Clear();
        }
    }

    private void CreateCommands()
    {
        commands.Clear();

        if (quickSlotSettingUI != null)
        {
            commands.Add(
                BasicActionId.OpenQuickSlotUI,
                new OpenQuickSlotSettingCommand(
                    quickSlotSettingUI
                )
            );
        }

        if (inventoryUI != null)
        {
            commands.Add(
                BasicActionId.OpenInventoryUI,
                new OpenInventoryCommand(
                    inventoryUI
                )
            );
        }

        if (skillUI != null)
        {
            commands.Add(
                BasicActionId.OpenSkillUI,
                new OpenSkillUICommand(
                    skillUI
                )
            );
        }
    }
}