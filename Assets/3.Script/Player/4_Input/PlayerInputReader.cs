using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerQuickSlotController))]
[RequireComponent(typeof(PlayerMapController))]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerInputReader : NetworkBehaviour
{
    [Serializable]
    private class QuickKeyInputBinding
    {
        public QuickKey key;
        public InputActionReference action;
    }

    [Header("Movement Input")]
    [SerializeField]
    private InputActionReference moveAction;

    [Header("Quick Slot Inputs")]
    [SerializeField]
    private List<QuickKeyInputBinding>
        quickKeyInputs = new();

    private readonly Dictionary<InputAction, QuickKey>
        quickKeyByAction = new();

    private PlayerMove playerMove;
    private PlayerQuickSlotController quickSlotController;
    private PlayerMapController mapController;
    private PlayerHealth playerHealth;

    private Vector2 moveInput;

    private bool inputEnabled;
    private bool inputBlocked;
    private bool wasUpPressed;
    private bool isDead;

    private void Awake()
    {
        playerMove =
            GetComponent<PlayerMove>();

        quickSlotController =
            GetComponent<PlayerQuickSlotController>();

        mapController =
            GetComponent<PlayerMapController>();

        playerHealth =
            GetComponent<PlayerHealth>();

        CreateQuickKeyLookup();
    }

    /// <summary>
    /// 이 클라이언트가 해당 플레이어의 권한을 받았을 때
    /// 로컬 입력을 활성화합니다.
    /// </summary>
    public override void OnStartAuthority()
    {
        base.OnStartAuthority();

        playerHealth.Died +=
            OnPlayerDied;

        playerHealth.Revived +=
            OnPlayerRevived;

        isDead =
            playerHealth.IsDead;

        RefreshInputState();
    }

    /// <summary>
    /// 플레이어에 대한 권한을 잃었을 때
    /// 입력과 이벤트 연결을 해제합니다.
    /// </summary>
    public override void OnStopAuthority()
    {
        playerHealth.Died -=
            OnPlayerDied;

        playerHealth.Revived -=
            OnPlayerRevived;

        isDead = false;

        DisableInputs();

        base.OnStopAuthority();
    }

    private void OnDisable()
    {
        DisableInputs();
    }

    /// <summary>
    /// 맵 전환 등으로 로컬 플레이어 입력을
    /// 일시적으로 차단하거나 다시 허용합니다.
    /// </summary>
    public void SetInputBlocked(
        bool blocked)
    {
        if (!isOwned)
            return;

        inputBlocked = blocked;

        RefreshInputState();
    }

    private void OnMovePerformed(
        InputAction.CallbackContext context)
    {
        if (inputBlocked)
            return;

        moveInput =
            context.ReadValue<Vector2>();

        CmdSetMoveInput(
            moveInput.x
        );

        bool isUpPressed =
            moveInput.y > 0.5f;

        /*
         * 위 방향 입력이 눌리는 순간에만
         * 한 번 포탈 사용을 요청합니다.
         */
        if (isUpPressed &&
            !wasUpPressed)
        {
            CmdRequestUsePortal();
        }

        wasUpPressed =
            isUpPressed;
    }

    private void OnMoveCanceled(
        InputAction.CallbackContext context)
    {
        moveInput =
            Vector2.zero;

        wasUpPressed =
            false;

        CmdSetMoveInput(0f);
    }

    /// <summary>
    /// 입력된 키의 퀵슬롯 바인딩을 확인하고
    /// 종류에 맞는 실행 경로로 전달합니다.
    /// </summary>
    private void OnQuickKeyPerformed(
        InputAction.CallbackContext context)
    {
        if (inputBlocked ||
            !quickKeyByAction.TryGetValue(
                context.action,
                out QuickKey key) ||
            !quickSlotController.TryGetBinding(
                key,
                out QuickSlotBinding binding))
        {
            return;
        }

        switch (binding.Type)
        {
            case QuickSlotBindingType.Skill:
                CmdExecuteSkill(
                    binding.SkillId,
                    moveInput
                );
                break;

            case QuickSlotBindingType.Consumable:
                CmdExecuteConsumable(
                    binding.ConsumableId
                );
                break;

            case QuickSlotBindingType.BasicAction:
                quickSlotController
                    .ExecuteBasicAction(
                        key,
                        moveInput
                    );
                break;
        }
    }

    private void EnableInputs()
    {
        if (inputEnabled)
            return;

        EnableMoveInput();
        EnableQuickKeyInputs();

        inputEnabled = true;
    }

    private void DisableInputs()
    {
        if (!inputEnabled)
            return;

        DisableMoveInput();
        DisableQuickKeyInputs();

        moveInput =
            Vector2.zero;

        wasUpPressed =
            false;

        if (isOwned &&
            NetworkClient.active)
        {
            CmdSetMoveInput(0f);
        }

        inputEnabled = false;
    }

    private void EnableMoveInput()
    {
        if (moveAction?.action == null)
            return;

        moveAction.action.performed +=
            OnMovePerformed;

        moveAction.action.canceled +=
            OnMoveCanceled;

        moveAction.action.Enable();
    }

    private void DisableMoveInput()
    {
        if (moveAction?.action == null)
            return;

        moveAction.action.performed -=
            OnMovePerformed;

        moveAction.action.canceled -=
            OnMoveCanceled;

        moveAction.action.Disable();
    }

    private void EnableQuickKeyInputs()
    {
        foreach (QuickKeyInputBinding binding
                 in quickKeyInputs)
        {
            if (!IsValid(binding))
                continue;

            InputAction action =
                binding.action.action;

            action.performed +=
                OnQuickKeyPerformed;

            action.Enable();
        }
    }

    private void DisableQuickKeyInputs()
    {
        foreach (QuickKeyInputBinding binding
                 in quickKeyInputs)
        {
            if (!IsValid(binding))
                continue;

            InputAction action =
                binding.action.action;

            action.performed -=
                OnQuickKeyPerformed;

            action.Disable();
        }
    }

    private void CreateQuickKeyLookup()
    {
        quickKeyByAction.Clear();

        foreach (QuickKeyInputBinding binding
                 in quickKeyInputs)
        {
            if (!IsValid(binding))
                continue;

            InputAction action =
                binding.action.action;

            if (!quickKeyByAction.TryAdd(
                    action,
                    binding.key))
            {
                Debug.LogWarning(
                    $"중복된 Input Action입니다: " +
                    $"{action.name}",
                    this
                );
            }
        }
    }

    private static bool IsValid(
        QuickKeyInputBinding binding)
    {
        return binding?.action?.action != null;
    }

    [Command]
    private void CmdSetMoveInput(
        float input)
    {
        playerMove.SetMoveInput(
            input
        );
    }

    [Command]
    private void CmdRequestUsePortal()
    {
        mapController.RequestUsePortal();
    }

    [Command]
    private void CmdExecuteSkill(
        SkillId skillId,
        Vector2 inputDirection)
    {
        inputDirection.x =
            Mathf.Clamp(
                inputDirection.x,
                -1f,
                1f
            );

        inputDirection.y =
            Mathf.Clamp(
                inputDirection.y,
                -1f,
                1f
            );

        quickSlotController.ExecuteSkill(
            skillId,
            inputDirection
        );
    }

    [Command]
    private void CmdExecuteConsumable(
        ConsumableId consumableId)
    {
        quickSlotController.ExecuteConsumable(
            consumableId
        );
    }

    private void RefreshInputState()
    {
        if (isOwned &&
            isActiveAndEnabled &&
            !inputBlocked &&
            !isDead)
        {
            EnableInputs();
            return;
        }

        DisableInputs();
    }

    private void OnPlayerDied()
    {
        isDead = true;

        RefreshInputState();
    }

    private void OnPlayerRevived()
    {
        isDead = false;

        RefreshInputState();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        HashSet<QuickKey> registeredKeys =
            new();

        foreach (QuickKeyInputBinding binding
                 in quickKeyInputs)
        {
            if (binding == null)
                continue;

            if (!registeredKeys.Add(
                    binding.key))
            {
                Debug.LogWarning(
                    $"중복된 QuickKey 입력입니다: " +
                    $"{binding.key}",
                    this
                );
            }
        }
    }
#endif
}