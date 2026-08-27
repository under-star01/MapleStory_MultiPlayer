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

        /*
         * EffectPlayer 인스턴스마다 독립적인
         * OverrideController를 생성합니다.
         *
         * 여러 이펙트가 동시에 재생될 때 서로의
         * 클립을 덮어쓰지 않도록 하기 위함입니다.
         */
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

        /*
         * 좌우 방향에 따라 X 위치 보정값도 반전합니다.
         */
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