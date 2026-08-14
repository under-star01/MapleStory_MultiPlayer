using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerMove : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 8f;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer sprite;

    private float moveInput;
    private bool jumpInput;
    private bool isGrounded;

    private static readonly int IsWalk =
        Animator.StringToHash("isWalk");

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        UpdateAnimation();
        UpdateDirection();

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            jumpInput = true;
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(
            moveInput * moveSpeed,
            rb.linearVelocity.y
        );

        if (jumpInput)
        {
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x,
                0f
            );

            rb.AddForce(
                Vector2.up * jumpForce,
                ForceMode2D.Impulse
            );

            jumpInput = false;
            isGrounded = false;
        }
    }

    private void UpdateAnimation()
    {
        bool isWalk = Mathf.Abs(moveInput) > 0.01f;
        animator.SetBool(IsWalk, isWalk);
    }

    private void UpdateDirection()
    {
        if (moveInput < 0f)
            sprite.flipX = false;
        else if (moveInput > 0f)
            sprite.flipX = true;
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                isGrounded = true;
                return;
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        isGrounded = false;
    }
}