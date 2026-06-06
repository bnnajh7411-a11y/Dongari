using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
    private const string MainSceneName = "Main";
    private const string RoadSceneName = "Road";
    private const string ExitObjectName = "Next";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float jumpForce = 5f;

    [Header("Gravity")]
    [SerializeField, Min(1f)] private float riseGravityMultiplier = 1.2f;
    [SerializeField, Min(1f)] private float fallGravityMultiplier = 2.4f;
    [SerializeField, Min(0f)] private float maxFallSpeed = 20f;

    private const float FacingThreshold = 0.01f;
    private const float GroundNormalThreshold = 0.5f;
    private const float VerticalVelocityThreshold = 0.01f;

    private Rigidbody2D rb;
    private float baseGravityScale;
    private float horizontalInput;
    private bool isRunning;
    private bool isGrounded;
    private bool jumpRequested;
    private Vector3 defaultScale;
    private readonly HashSet<Collider2D> groundColliders = new HashSet<Collider2D>();
    private readonly HashSet<Collider2D> exitColliders = new HashSet<Collider2D>();

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        baseGravityScale = rb.gravityScale;

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

        if (WasInteractPressed() && CanInteractWithExit())
        {
            LoadRoadScene();
        }

        UpdateFacingDirection();
    }

    protected virtual void FixedUpdate()
    {
        float currentSpeed = isRunning ? runSpeed : moveSpeed;
        rb.linearVelocity = new Vector2(horizontalInput * currentSpeed, rb.linearVelocity.y);

        if (jumpRequested)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

            isGrounded = false;
            groundColliders.Clear();
            jumpRequested = false;
        }

        ApplyGravity();
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

    private bool WasInteractPressed()
    {
        return Input.GetKeyDown(KeyCode.Z);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        UpdateCollisionContacts(collision, true);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        UpdateCollisionContacts(collision, true);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        UpdateCollisionContacts(collision, false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        UpdateExitContacts(other, true);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        UpdateExitContacts(other, true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        UpdateExitContacts(other, false);
    }

    private void UpdateCollisionContacts(Collision2D collision, bool isContacting)
    {
        if (collision == null || collision.collider == null)
        {
            return;
        }

        Collider2D otherCollider = collision.collider;

        if (!isContacting)
        {
            groundColliders.Remove(otherCollider);
        }
        else
        {
            bool hasGroundContact = false;

            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y > GroundNormalThreshold)
                {
                    hasGroundContact = true;
                }
            }

            UpdateGroundContactSet(otherCollider, hasGroundContact);
        }

        isGrounded = groundColliders.Count > 0;
    }

    private void UpdateGroundContactSet(Collider2D otherCollider, bool hasGroundContact)
    {
        if (hasGroundContact)
        {
            groundColliders.Add(otherCollider);
            return;
        }

        groundColliders.Remove(otherCollider);
    }

    private void UpdateExitContacts(Collider2D other, bool isContacting)
    {
        if (!IsExitCollider(other))
        {
            return;
        }

        if (isContacting)
        {
            exitColliders.Add(other);
            return;
        }

        exitColliders.Remove(other);
    }

    private bool IsExitCollider(Collider2D other)
    {
        return other != null
            && SceneManager.GetActiveScene().name == MainSceneName
            && other.gameObject.name == ExitObjectName;
    }

    private bool CanInteractWithExit()
    {
        return exitColliders.Count > 0 && SceneManager.GetActiveScene().name == MainSceneName;
    }

    private void LoadRoadScene()
    {
        SceneManager.LoadScene(RoadSceneName);
    }

    private void ApplyGravity()
    {
        float gravityMultiplier = 1f;

        if (rb.linearVelocity.y > VerticalVelocityThreshold)
        {
            gravityMultiplier = riseGravityMultiplier;
        }
        else if (rb.linearVelocity.y < -VerticalVelocityThreshold)
        {
            gravityMultiplier = fallGravityMultiplier;
        }

        rb.gravityScale = baseGravityScale * gravityMultiplier;

        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
        }
    }
}
