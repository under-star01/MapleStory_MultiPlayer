using Mirror;
using UnityEngine;

public class PlayerMove : NetworkBehaviour
{
    [SerializeField]
    private float moveSpeed = 3f;

    Animator animator;
    SpriteRenderer sprite;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (!isOwned)
            return;

        float x = Input.GetAxisRaw("Horizontal");

        transform.position +=
            Vector3.right * x * moveSpeed * Time.deltaTime;

        if(x > 0)
        {
            sprite.flipX = true;
            animator.SetBool("isWalk", true);

        }
        else if (x < 0)
        {
            sprite.flipX = false;
            animator.SetBool("isWalk", true);
        }
        else
        {
            animator.SetBool("isWalk", false);
        }
    }
}