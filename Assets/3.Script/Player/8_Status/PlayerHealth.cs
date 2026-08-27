using System;
using System.Collections;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(SpriteRenderer))]
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

    [SyncVar(hook = nameof(OnCurrentHpChanged))]
    private int currentHp;

    [SyncVar(hook = nameof(OnDeadStateChanged))]
    private bool isDead;

    private PlayerMove playerMove;
    private SpriteRenderer spriteRenderer;

    /*
     * 서버에서만 사용하는 무적 판정입니다.
     * 클라이언트에 동기화할 필요는 없습니다.
     */
    private bool isInvincible;

    private Coroutine invincibilityCoroutine;
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
        isInvincible = false;
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

    [Server]
    public void TakeDamage(
        int damage,
        Vector2 attackerPosition)
    {
        if (isDead)
            return;

        if (isInvincible)
            return;

        if (damage <= 0)
            return;

        /*
         * 같은 프레임이나 연속 Trigger 판정으로
         * 피해가 중복 적용되지 않도록 먼저 무적 처리합니다.
         */
        isInvincible = true;

        currentHp = Mathf.Max(
            currentHp - damage,
            0
        );

        /*
         * 사망하는 공격이어도 마지막 넉백과
         * 점멸 반응은 그대로 적용합니다.
         */
        playerMove.ApplyKnockback(
            attackerPosition
        );

        RpcPlayBlink();

        invincibilityCoroutine =
            StartCoroutine(
                InvincibilityTimer()
            );

        if (currentHp == 0)
        {
            Die();
        }
    }

    [Server]
    public void Heal(int amount)
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

    [Server]
    private IEnumerator InvincibilityTimer()
    {
        yield return new WaitForSeconds(
            invincibilityDuration
        );

        isInvincible = false;
        invincibilityCoroutine = null;
    }

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
    }

    [Command]
    public void CmdRequestRevive()
    {
        if (!isDead)
            return;

        Revive();
    }

    [Server]
    private void Revive()
    {
        if (!isDead)
            return;

        if (invincibilityCoroutine != null)
        {
            StopCoroutine(
                invincibilityCoroutine
            );

            invincibilityCoroutine = null;
        }

        isInvincible = false;
        currentHp = maxHp;
        isDead = false;
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