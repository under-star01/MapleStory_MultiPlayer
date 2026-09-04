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

    // 로컬 권한 획득 시 플레이어 상태 이벤트 연결 및
    // 입력 활성화 상태 갱신
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

    // 로컬 권한 해제 시 상태 이벤트 및
    // 입력 연결 정리
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

    // 맵 전환 등 일시적으로 입력을 막아야 하는 상황의
    // 입력 차단 상태 설정
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

        // 위 방향 입력이 처음 눌린 순간에만
        // 포탈 사용 요청
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

    // 입력된 InputAction을 QuickKey로 변환한 뒤
    // 퀵슬롯 Binding 타입에 맞는 실행 경로로 전달
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
                quickSlotController.ExecuteBasicAction(
                    key
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

        // 입력 비활성화 시 서버에 남아 있을 수 있는
        // 이동 입력값 초기화
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

    // Inspector에 설정된 InputAction과 QuickKey를
    // 런타임 조회용 Dictionary로 변환
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

    // 이동 입력을 서버에 전달
    [Command]
    private void CmdSetMoveInput(
        float input)
    {
        playerMove.SetMoveInput(
            input
        );
    }

    // 포탈 사용 요청을 서버에 전달
    [Command]
    private void CmdRequestUsePortal()
    {
        mapController.RequestUsePortal();
    }

    // 스킬 실행 요청과 실행 순간의 방향 입력을
    // 서버에 전달
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

    // 소비 아이템 사용 요청을 서버에 전달
    [Command]
    private void CmdExecuteConsumable(
        ConsumableId consumableId)
    {
        quickSlotController.ExecuteConsumable(
            consumableId
        );
    }

    // 소유권, 오브젝트 활성화, 입력 차단, 사망 상태를 기준으로
    // 최종 입력 활성화 여부 결정
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