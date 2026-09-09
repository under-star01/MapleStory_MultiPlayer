using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class EffectPlayer : MonoBehaviour
{
    private static readonly int EffectStateHash =
        Animator.StringToHash("Effect");

    private const string OverrideClipName =
        "EffectPlaceholder";

    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private AnimatorOverrideController
        overrideController;

    private Coroutine playCoroutine;
    private EffectPool ownerPool;

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        animator =
            GetComponent<Animator>();

        spriteRenderer =
            GetComponent<SpriteRenderer>();

        // 각 이펙트가 독립적인 OverrideController 사용
        overrideController =
            new AnimatorOverrideController(
                animator.runtimeAnimatorController
            );

        animator.runtimeAnimatorController =
            overrideController;

        gameObject.SetActive(false);
    }

    public void Initialize(
        EffectPool pool)
    {
        ownerPool = pool;
    }

    // 이펙트 데이터에 맞춰 위치와 애니메이션 설정 후 재생
    public void Play(
        EffectData effectData,
        Vector3 position,
        bool flipX)
    {
        if (effectData == null ||
            effectData.AnimationClip == null)
        {
            ownerPool.Return(this);
            return;
        }

        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
        }

        AnimationClip clip =
            effectData.AnimationClip;

        Vector2 offset =
            effectData.PositionOffset;

        if (flipX)
        {
            offset.x *= -1f;
        }

        transform.position =
            position + (Vector3)offset;

        transform.localScale =
            new Vector3(
                effectData.Scale.x,
                effectData.Scale.y,
                1f
            );

        spriteRenderer.flipX =
            flipX;

        spriteRenderer.sortingOrder =
            effectData.SortingOrder;

        overrideController[
            OverrideClipName
        ] = clip;

        gameObject.SetActive(true);
        IsPlaying = true;

        animator.Play(
            EffectStateHash,
            0,
            0f
        );

        playCoroutine =
            StartCoroutine(
                ReturnAfterAnimation(
                    clip.length
                )
            );
    }

    // 애니메이션 종료 후 풀에 반환
    private IEnumerator ReturnAfterAnimation(
        float duration)
    {
        yield return new WaitForSeconds(
            duration
        );

        ownerPool.Return(this);
    }

    public void Stop()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        IsPlaying = false;

        spriteRenderer.sprite = null;

        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        playCoroutine = null;
    }
}