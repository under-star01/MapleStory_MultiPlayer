using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerQuickSlotController))]
public class PlayerInputReader : MonoBehaviour
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

    private Vector2 moveInput;

    private void Awake()
    {
        playerMove =
            GetComponent<PlayerMove>();

        quickSlotController =
            GetComponent<PlayerQuickSlotController>();

        CreateQuickKeyLookup();
    }

    private void OnEnable()
    {
        EnableMoveInput();
        EnableQuickKeyInputs();
    }

    private void OnDisable()
    {
        DisableMoveInput();
        DisableQuickKeyInputs();

        moveInput = Vector2.zero;

        if (playerMove != null)
        {
            playerMove.SetMoveInput(0f);
        }
    }

    private void Update()
    {
        playerMove.SetMoveInput(moveInput.x);
    }

    private void OnMovePerformed(
        InputAction.CallbackContext context)
    {
        moveInput =
            context.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(
        InputAction.CallbackContext context)
    {
        moveInput = Vector2.zero;
    }

    /// <summary>
    /// 모든 단축키 Action이 공통으로 사용하는 콜백입니다.
    /// 실행된 Action을 QuickKey로 변환한 뒤
    /// 해당 슬롯의 Command를 실행합니다.
    /// </summary>
    private void OnQuickKeyPerformed(
        InputAction.CallbackContext context)
    {
        if (!quickKeyByAction.TryGetValue(
                context.action,
                out QuickKey key))
        {
            return;
        }

        quickSlotController.Execute(
            key,
            moveInput
        );
    }

    private void EnableMoveInput()
    {
        if (moveAction == null ||
            moveAction.action == null)
        {
            return;
        }

        moveAction.action.performed +=
            OnMovePerformed;

        moveAction.action.canceled +=
            OnMoveCanceled;

        moveAction.action.Enable();
    }

    private void DisableMoveInput()
    {
        if (moveAction == null ||
            moveAction.action == null)
        {
            return;
        }

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

    private bool IsValid(
        QuickKeyInputBinding binding)
    {
        return binding != null &&
               binding.action != null &&
               binding.action.action != null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        HashSet<QuickKey> registeredKeys = new();

        foreach (QuickKeyInputBinding binding
                 in quickKeyInputs)
        {
            if (binding == null)
                continue;

            if (!registeredKeys.Add(binding.key))
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