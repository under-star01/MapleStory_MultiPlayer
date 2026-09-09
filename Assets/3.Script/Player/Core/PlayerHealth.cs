using System;
using System.Collections;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PlayerMapController))]
public class PlayerHealth : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField]
    [Min(1)]
    private int maxHp = 100;

    [Header("Hit Reaction")]
    [SerializeField]
    private float invincibilityDuration = 1f;

    [SerializeField]
    private float blinkInterval = 0.1f;

    [SerializeField]
    private Color damagedColor =
        new Color(0.5f, 0.5f, 0.5f, 1f);

    [SerializeField]
    [SyncVar(hook = nameof(OnCurrentHpChanged))]
    private int currentHp;

    [SyncVar(hook = nameof(OnDeadStateChanged))]
    private bool isDead;

    private PlayerMove playerMove;
    private PlayerMapController mapController;
    private SpriteRenderer spriteRenderer;

    private float invincibleUntil;
    private Coroutine blinkCoroutine;

    private Color originalColor;

    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;
    public bool IsDead => isDead;

    public event Action<int, int> HealthChanged;
    public event Action Died;
    public event Action Revived;

    private void Awake()
    {
        playerMove =
            GetComponent<PlayerMove>();

        mapController =
            GetComponent<PlayerMapController>();

        spriteRenderer =
            GetComponent<SpriteRenderer>();

        originalColor =
            spriteRenderer.color;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        currentHp = maxHp;
        isDead = false;
        invincibleUntil = 0f;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        HealthChanged?.Invoke(
            currentHp,
            maxHp
        );

        if (isDead)
        {
            Died?.Invoke();
        }
    }

    // 데미지 적용 및 피격 반응 처리
    [Server]
    public void TakeDamage(
        int damage,
        Vector2 attackerPosition)
    {
        if (isDead)
            return;

        if (Time.time < invincibleUntil)
            return;

        if (damage <= 0)
            return;

        invincibleUntil =
            Time.time + invincibilityDuration;

        currentHp = Mathf.Max(
            currentHp - damage,
            0
        );

        playerMove.ApplyKnockback(
            attackerPosition
        );

        RpcPlayBlink();

        if (currentHp == 0)
        {
            Die();
        }
    }

    [Server]
    public void Heal(
        int amount)
    {
        if (isDead)
            return;

        if (amount <= 0)
            return;

        currentHp = Mathf.Min(
            currentHp + amount,
            maxHp
        );
    }

    // 피격 무적 시간 동안 블랙 아웃 효과 실행
    [ClientRpc]
    private void RpcPlayBlink()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(
                blinkCoroutine
            );
        }

        blinkCoroutine =
            StartCoroutine(
                Blink()
            );
    }

    private IEnumerator Blink()
    {
        float elapsedTime = 0f;
        bool showDamagedColor = true;

        while (elapsedTime <
               invincibilityDuration)
        {
            spriteRenderer.color =
                showDamagedColor
                    ? damagedColor
                    : originalColor;

            showDamagedColor =
                !showDamagedColor;

            yield return new WaitForSeconds(
                blinkInterval
            );

            elapsedTime +=
                blinkInterval;
        }

        spriteRenderer.color =
            originalColor;

        blinkCoroutine = null;
    }

    [Server]
    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        Died?.Invoke();
    }

    // 사망 상태에서 부활 맵 이동 요청
    [Command]
    public void CmdRequestRevive()
    {
        if (!isDead)
            return;

        mapController.RequestReviveTransition();
    }

    // 맵 이동 완료 후 체력 및 사망 상태 초기화
    [Server]
    public void CompleteRevive()
    {
        if (!isDead)
            return;

        currentHp = maxHp;
        isDead = false;
        invincibleUntil = 0f;
    }

    private void OnCurrentHpChanged(
        int previousHp,
        int newHp)
    {
        HealthChanged?.Invoke(
            newHp,
            maxHp
        );
    }

    private void OnDeadStateChanged(
        bool previousState,
        bool newState)
    {
        if (isServer)
            return;

        if (newState)
        {
            Died?.Invoke();
            return;
        }

        Revived?.Invoke();
    }

    private void OnDisable()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color =
                originalColor;
        }
    }
}