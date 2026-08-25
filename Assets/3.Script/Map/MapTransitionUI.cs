using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class MapTransitionUI : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField]
    private float fadeOutDuration = 0.5f;

    [SerializeField]
    private float fadeInDuration = 1f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup =
            GetComponent<CanvasGroup>();

        SetAlpha(0f);
    }

    /// <summary>
    /// 화면을 완전히 검게 가립니다.
    /// </summary>
    public IEnumerator FadeOut()
    {
        yield return Fade(
            canvasGroup.alpha,
            1f,
            fadeOutDuration
        );
    }

    /// <summary>
    /// 검은 화면을 서서히 해제합니다.
    /// </summary>
    public IEnumerator FadeIn()
    {
        yield return Fade(
            canvasGroup.alpha,
            0f,
            fadeInDuration
        );
    }

    private IEnumerator Fade(
        float startAlpha,
        float targetAlpha,
        float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float ratio =
                Mathf.Clamp01(
                    elapsed / duration
                );

            SetAlpha(
                Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    ratio
                )
            );

            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void SetAlpha(float alpha)
    {
        canvasGroup.alpha = alpha;

        bool isVisible =
            alpha > 0f;

        canvasGroup.blocksRaycasts =
            isVisible;

        canvasGroup.interactable =
            isVisible;
    }
}