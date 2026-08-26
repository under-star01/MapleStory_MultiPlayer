using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(SortingGroup))]
public class DamageNumberView : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform digitsRoot;

    [Header("Layout")]
    [SerializeField]
    private float characterSpacing = 0.02f;

    [Header("Animation")]
    [SerializeField]
    private float displayDuration = 0.6f;

    [SerializeField]
    private float riseDistance = 0.5f;

    [SerializeField]
    [Range(0f, 1f)]
    private float fadeStartRatio = 0.6f;

    private readonly List<SpriteRenderer>
        digitRenderers = new();

    private Coroutine displayCoroutine;

    private Action<DamageNumberView>
        returnCallback;

    private Vector3 startPosition;

    private void Awake()
    {
        if (digitsRoot == null)
        {
            digitsRoot = transform;
        }
    }

    /// <summary>
    /// 데미지 숫자를 표시하고 연출을 시작합니다.
    /// 연출이 끝나면 returnCallback을 호출합니다.
    /// </summary>
    public void Show(
        int damage,
        bool isCritical,
        Sprite[] normalDigits,
        Sprite[] criticalDigits,
        Action<DamageNumberView> returnCallback)
    {
        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
        }

        this.returnCallback =
            returnCallback;

        Sprite[] selectedDigits =
            isCritical
                ? criticalDigits
                : normalDigits;

        BuildNumber(
            damage,
            selectedDigits
        );

        startPosition =
            transform.position;

        ResetRendererColors();

        displayCoroutine =
            StartCoroutine(
                PlayDisplayAnimation()
            );
    }

    /// <summary>
    /// 전달받은 숫자를 자릿수별 SpriteRenderer로 구성합니다.
    /// </summary>
    private void BuildNumber(
        int damage,
        Sprite[] digitSprites)
    {
        if (digitSprites == null ||
            digitSprites.Length < 10)
        {
            Debug.LogError(
                "데미지 숫자 스프라이트는 " +
                "0부터 9까지 총 10개가 필요합니다.",
                this
            );

            return;
        }

        damage = Mathf.Max(
            damage,
            0
        );

        string damageText =
            damage.ToString();

        EnsureRendererCount(
            damageText.Length
        );

        float totalWidth =
            CalculateTotalWidth(
                damageText,
                digitSprites
            );

        float currentX =
            -totalWidth * 0.5f;

        for (int i = 0;
             i < digitRenderers.Count;
             i++)
        {
            SpriteRenderer renderer =
                digitRenderers[i];

            if (i >= damageText.Length)
            {
                renderer.gameObject
                    .SetActive(false);

                continue;
            }

            int digit =
                damageText[i] - '0';

            Sprite sprite =
                digitSprites[digit];

            renderer.gameObject
                .SetActive(true);

            renderer.sprite =
                sprite;

            float spriteWidth =
                sprite.bounds.size.x;

            renderer.transform.localPosition =
                new Vector3(
                    currentX +
                    spriteWidth * 0.5f,
                    0f,
                    0f
                );

            currentX +=
                spriteWidth +
                characterSpacing;
        }
    }

    /// <summary>
    /// 필요한 자릿수만큼 SpriteRenderer를 확보합니다.
    /// 이미 생성한 Renderer는 계속 재사용합니다.
    /// </summary>
    private void EnsureRendererCount(
        int requiredCount)
    {
        while (digitRenderers.Count <
               requiredCount)
        {
            GameObject digitObject =
                new GameObject(
                    $"Digit_{digitRenderers.Count}"
                );

            digitObject.transform.SetParent(
                digitsRoot,
                false
            );

            SpriteRenderer renderer =
                digitObject.AddComponent
                    <SpriteRenderer>();

            digitRenderers.Add(
                renderer
            );
        }
    }

    private float CalculateTotalWidth(
        string damageText,
        Sprite[] digitSprites)
    {
        float totalWidth = 0f;

        for (int i = 0;
             i < damageText.Length;
             i++)
        {
            int digit =
                damageText[i] - '0';

            Sprite sprite =
                digitSprites[digit];

            totalWidth +=
                sprite.bounds.size.x;

            if (i < damageText.Length - 1)
            {
                totalWidth +=
                    characterSpacing;
            }
        }

        return totalWidth;
    }

    private IEnumerator
        PlayDisplayAnimation()
    {
        float elapsedTime = 0f;

        while (elapsedTime <
               displayDuration)
        {
            elapsedTime +=
                Time.deltaTime;

            float progress =
                displayDuration > 0f
                    ? Mathf.Clamp01(
                        elapsedTime /
                        displayDuration
                    )
                    : 1f;

            transform.position =
                startPosition +
                Vector3.up *
                riseDistance *
                progress;

            float alpha =
                CalculateAlpha(
                    progress
                );

            SetRendererAlpha(
                alpha
            );

            yield return null;
        }

        displayCoroutine = null;

        Action<DamageNumberView> callback =
            returnCallback;

        returnCallback = null;

        callback?.Invoke(
            this
        );
    }

    private float CalculateAlpha(
        float progress)
    {
        if (progress <= fadeStartRatio)
            return 1f;

        float fadeLength =
            1f - fadeStartRatio;

        if (fadeLength <= 0f)
            return 0f;

        float fadeProgress =
            (progress - fadeStartRatio) /
            fadeLength;

        return 1f -
            Mathf.Clamp01(
                fadeProgress
            );
    }

    private void ResetRendererColors()
    {
        SetRendererAlpha(
            1f
        );
    }

    private void SetRendererAlpha(
        float alpha)
    {
        foreach (
            SpriteRenderer renderer
            in digitRenderers)
        {
            if (!renderer.gameObject
                    .activeSelf)
            {
                continue;
            }

            Color color =
                renderer.color;

            color.a =
                alpha;

            renderer.color =
                color;
        }
    }

    private void OnDisable()
    {
        if (displayCoroutine != null)
        {
            StopCoroutine(
                displayCoroutine
            );

            displayCoroutine = null;
        }

        returnCallback = null;
    }
}