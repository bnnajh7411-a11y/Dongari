using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float jumpForce = 5f;

    [Header("Crouch")]
    [SerializeField] private float crouchHeightMultiplier = 0.6f;
    [SerializeField] private Color crouchColor = Color.blue;
    [SerializeField] private bool isCrouching;

    [Header("Hide")]
    [SerializeField] private string hideZoneTag = "HideZone";
    [SerializeField] private bool isHidden;
    [SerializeField] private Behaviour[] detectionBehavioursToDisableWhileHidden;
    [SerializeField] private GameObject[] detectionObjectsToDisableWhileHidden;

    private Rigidbody2D rb;
    private CapsuleCollider2D capsuleCollider;
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    private float horizontalInput;
    private bool isRunning;
    private bool isGrounded;
    private bool jumpRequested;
    private int hideZoneContactCount;
    private Vector3 defaultScale;
    private Vector2 defaultCapsuleSize;
    private Vector2 defaultCapsuleOffset;
    private Vector2 defaultBoxSize;
    private Vector2 defaultBoxOffset;
    private Color defaultSpriteColor;
    private readonly HashSet<Collider2D> groundColliders = new HashSet<Collider2D>();

    public bool IsCrouching => isCrouching;
    public bool IsHidden => isHidden;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        defaultScale = transform.localScale;
        defaultScale.x = Mathf.Abs(defaultScale.x);
        defaultSpriteColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

        if (capsuleCollider != null)
        {
            defaultCapsuleSize = capsuleCollider.size;
            defaultCapsuleOffset = capsuleCollider.offset;
        }

        if (boxCollider != null)
        {
            defaultBoxSize = boxCollider.size;
            defaultBoxOffset = boxCollider.offset;
        }

        ApplyCrouchState(isCrouching);
        UpdateHiddenState();
    }

    protected virtual void Update()
    {
        horizontalInput = ReadHorizontalInput();

        bool shouldCrouch = IsCrouchPressed();

        if (isCrouching != shouldCrouch)
        {
            ApplyCrouchState(shouldCrouch);
        }

        isRunning = IsRunPressed() && !isCrouching;

        if (WasJumpPressed() && isGrounded && !isCrouching)
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

    private void ApplyCrouchState(bool crouching)
    {
        isCrouching = crouching;
        ApplyColliderCrouch();
        ApplyCrouchVisual();
        UpdateHiddenState();
    }

    private void ApplyColliderCrouch()
    {
        float heightMultiplier = Mathf.Clamp(crouchHeightMultiplier, 0.3f, 1f);

        if (capsuleCollider != null)
        {
            if (isCrouching)
            {
                float crouchedHeight = defaultCapsuleSize.y * heightMultiplier;
                float heightDelta = defaultCapsuleSize.y - crouchedHeight;
                capsuleCollider.size = new Vector2(defaultCapsuleSize.x, crouchedHeight);
                capsuleCollider.offset = defaultCapsuleOffset + Vector2.down * (heightDelta * 0.5f);
            }
            else
            {
                capsuleCollider.size = defaultCapsuleSize;
                capsuleCollider.offset = defaultCapsuleOffset;
            }

            return;
        }

        if (boxCollider != null)
        {
            if (isCrouching)
            {
                float crouchedHeight = defaultBoxSize.y * heightMultiplier;
                float heightDelta = defaultBoxSize.y - crouchedHeight;
                boxCollider.size = new Vector2(defaultBoxSize.x, crouchedHeight);
                boxCollider.offset = defaultBoxOffset + Vector2.down * (heightDelta * 0.5f);
            }
            else
            {
                boxCollider.size = defaultBoxSize;
                boxCollider.offset = defaultBoxOffset;
            }
        }
    }

    private void ApplyCrouchVisual()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.color = isCrouching ? crouchColor : defaultSpriteColor;
    }

    private void UpdateFacingDirection()
    {
        if (horizontalInput > 0.01f)
        {
            transform.localScale = new Vector3(defaultScale.x, defaultScale.y, defaultScale.z);
        }
        else if (horizontalInput < -0.01f)
        {
            transform.localScale = new Vector3(-defaultScale.x, defaultScale.y, defaultScale.z);
        }
    }

    private bool IsCrouchPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            return keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
        }
#endif

        return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
    }

    private float ReadHorizontalInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            float left = (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) ? -1f : 0f;
            float right = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) ? 1f : 0f;
            return Mathf.Clamp(left + right, -1f, 1f);
        }
#endif

        return Input.GetAxisRaw("Horizontal");
    }

    private bool IsRunPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        }
#endif

        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    private bool WasJumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            return keyboard.spaceKey.wasPressedThisFrame;
        }
#endif

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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsHideZone(other))
        {
            return;
        }

        hideZoneContactCount++;
        UpdateHiddenState();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsHideZone(other))
        {
            return;
        }

        hideZoneContactCount = Mathf.Max(0, hideZoneContactCount - 1);
        UpdateHiddenState();
    }

    private bool IsHideZone(Collider2D other)
    {
        return other != null && other.gameObject.tag == hideZoneTag;
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
            if (contact.normal.y > 0.5f)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateHiddenState()
    {
        bool shouldHide = isCrouching && hideZoneContactCount > 0;

        if (isHidden == shouldHide)
        {
            return;
        }

        isHidden = shouldHide;
        ApplyHiddenState();
    }

    private void ApplyHiddenState()
    {
        if (detectionBehavioursToDisableWhileHidden != null)
        {
            foreach (Behaviour behaviour in detectionBehavioursToDisableWhileHidden)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = !isHidden;
                }
            }
        }

        if (detectionObjectsToDisableWhileHidden != null)
        {
            foreach (GameObject target in detectionObjectsToDisableWhileHidden)
            {
                if (target != null)
                {
                    target.SetActive(!isHidden);
                }
            }
        }
    }

}
