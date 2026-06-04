using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float jumpForce = 5f;

    private const float FacingThreshold = 0.01f;
    private const float GroundNormalThreshold = 0.5f;

    private Rigidbody2D rb;
    private float horizontalInput;
    private bool isRunning;
    private bool isGrounded;
    private bool jumpRequested;
    private Vector3 defaultScale;
    private readonly HashSet<Collider2D> groundColliders = new HashSet<Collider2D>();

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        defaultScale = transform.localScale;
        defaultScale.x = Mathf.Abs(defaultScale.x);
    }

    protected virtual void Update()
    {
        horizontalInput = ReadHorizontalInput();
        isRunning = IsRunPressed();

        if (WasJumpPressed() && isGrounded)
        {
            jumpRequested = true;
        }

        UpdateFacingDirection();
    }

    protected virtual void FixedUpdate()
    {
        float currentSpeed = isRunning ? runSpeed : moveSpeed;
        rb.linearVelocity = new Vector2(horizontalInput * currentSpeed, rb.linearVelocity.y);

        if (!jumpRequested)
        {
            return;
        }

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

        isGrounded = false;
        groundColliders.Clear();
        jumpRequested = false;
    }

    private void UpdateFacingDirection()
    {
        if (horizontalInput > FacingThreshold)
        {
            transform.localScale = new Vector3(defaultScale.x, defaultScale.y, defaultScale.z);
        }
        else if (horizontalInput < -FacingThreshold)
        {
            transform.localScale = new Vector3(-defaultScale.x, defaultScale.y, defaultScale.z);
        }
    }

    private float ReadHorizontalInput()
    {
        return Input.GetAxisRaw("Horizontal");
    }

    private bool IsRunPressed()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    private bool WasJumpPressed()
    {
        return Input.GetKeyDown(KeyCode.Space);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        UpdateGroundContact(collision, true);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        UpdateGroundContact(collision, true);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        UpdateGroundContact(collision, false);
    }

    private void UpdateGroundContact(Collision2D collision, bool isContacting)
    {
        if (collision == null || collision.collider == null)
        {
            return;
        }

        Collider2D otherCollider = collision.collider;

        if (isContacting)
        {
            if (IsGroundCollision(collision))
            {
                groundColliders.Add(otherCollider);
            }
        }
        else
        {
            groundColliders.Remove(otherCollider);
        }

        isGrounded = groundColliders.Count > 0;
    }

    private bool IsGroundCollision(Collision2D collision)
    {
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > GroundNormalThreshold)
            {
                return true;
            }
        }

        return false;
    }
}
