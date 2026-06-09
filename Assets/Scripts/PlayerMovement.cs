using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
    private const string MainSceneName = "Main";
    private const string BuildingSceneName = "Building";
    private const string RoadSceneName = "Road";
    private const string DrainSceneName = "Drain";
    private const string ArtificialRiverSceneName = "ArtificialRiver";
    private const string MountainSceneName = "Mountain";
    private const string GreenAlgaeObjectName = "GreenAlgae";
    private const string ExitObjectName = "Next";
    private const string GroundObjectName = "Ground";
    private const string WaterObjectName = "Water";
    private static readonly string[] ArtificialRiverColliderObjectNames =
    {
        GreenAlgaeObjectName,
        WaterObjectName,
        ExitObjectName
    };
    private const float GreenAlgaeSlowMultiplier = 0.5f;
    private const float GreenAlgaeSlowDuration = 3f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float jumpForce = 13f;

    [Header("Gravity")]
    [SerializeField, Min(1f)] private float riseGravityMultiplier = 1.2f;
    [SerializeField, Min(1f)] private float fallGravityMultiplier = 2.4f;
    [SerializeField, Min(0f)] private float maxFallSpeed = 20f;

    [Header("Water")]
    [SerializeField, Min(0f)] private float waterSinkSpeed = 2.5f;
    [SerializeField, Min(1f)] private float waterFastSinkMultiplier = 1.75f;
    [SerializeField, Min(0f)] private float waterRiseSpeed = 4f;
    [SerializeField, Min(1f)] private float waterFastRiseMultiplier = 1.3f;

    private const float FacingThreshold = 0.01f;
    private const float GroundNormalThreshold = 0.5f;
    private const float VerticalVelocityThreshold = 0.01f;

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private float baseGravityScale;
    private float horizontalInput;
    private float verticalInput;
    private bool isRunning;
    private bool isGrounded;
    private bool isTopDownScene;
    private bool isWaterScene;
    private bool hasMovementBounds;
    private bool jumpRequested;
    private Vector3 defaultScale;
    private Bounds movementBounds;
    private float movementSpeedMultiplier = 1f;
    private Coroutine movementSpeedModifierRoutine;
    private readonly HashSet<Collider2D> groundColliders = new HashSet<Collider2D>();
    private readonly HashSet<Collider2D> exitColliders = new HashSet<Collider2D>();
    private readonly HashSet<Collider2D> waterColliders = new HashSet<Collider2D>();

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        baseGravityScale = rb.gravityScale;

        defaultScale = transform.localScale;
        defaultScale.x = Mathf.Abs(defaultScale.x);

        ConfigureMovementMode();
        EnsurePlayerHealthComponent();
        EnsureWaterSceneSystems();
        EnsureArtificialRiverColliderSizing();
    }

    protected virtual void OnDisable()
    {
        ResetMovementSpeedModifier();
    }

    protected virtual void Start()
    {
        CacheMovementBounds();
    }

    protected virtual void Update()
    {
        horizontalInput = ReadHorizontalInput();
        verticalInput = (isTopDownScene || isWaterScene) ? ReadVerticalInput() : 0f;
        isRunning = IsRunPressed();

        if (!isTopDownScene && !IsWaterMovementActive() && WasJumpPressed() && isGrounded)
        {
            jumpRequested = true;
        }

        if (WasInteractPressed() && CanInteractWithExit())
        {
            LoadExitScene();
        }

        UpdateFacingDirection();
    }

    protected virtual void FixedUpdate()
    {
        if (IsWaterMovementActive())
        {
            jumpRequested = false;
            ApplyWaterMovement();
            return;
        }

        if (isTopDownScene)
        {
            ApplyTopDownMovement();
            ClampPositionToGroundBounds();
            return;
        }

        float currentSpeed = (isRunning ? runSpeed : moveSpeed) * movementSpeedMultiplier;
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

    private float ReadVerticalInput()
    {
        return Input.GetAxisRaw("Vertical");
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
        HandleCollisionContact(collision, true);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleCollisionContact(collision, true);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        HandleCollisionContact(collision, false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTriggerContact(other, true);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleTriggerContact(other, true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        HandleTriggerContact(other, false);
    }

    private void HandleCollisionContact(Collision2D collision, bool isContacting)
    {
        if (collision == null || collision.collider == null)
        {
            return;
        }

        Collider2D otherCollider = collision.collider;
        bool hasGroundContact = false;

        if (isContacting)
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y > GroundNormalThreshold)
                {
                    hasGroundContact = true;
                    break;
                }
            }
        }

        SetTrackedContact(groundColliders, otherCollider, hasGroundContact);

        isGrounded = groundColliders.Count > 0;
    }

    private void HandleTriggerContact(Collider2D other, bool isContacting)
    {
        UpdateExitContacts(other, isContacting);
        UpdateWaterContacts(other, isContacting);

        if (isContacting)
        {
            TryApplyGreenAlgaeSlow(other);
        }
    }

    private void UpdateExitContacts(Collider2D other, bool isContacting)
    {
        if (!IsExitCollider(other))
        {
            return;
        }

        SetTrackedContact(exitColliders, other, isContacting);
    }

    private void UpdateWaterContacts(Collider2D other, bool isContacting)
    {
        if (!IsWaterCollider(other))
        {
            return;
        }

        SetTrackedContact(waterColliders, other, isContacting);
    }

    private void TryApplyGreenAlgaeSlow(Collider2D other)
    {
        if (!IsGreenAlgaeCollider(other))
        {
            return;
        }

        ApplyTemporaryMovementSpeedMultiplier(GreenAlgaeSlowMultiplier, GreenAlgaeSlowDuration);
    }

    private bool IsExitCollider(Collider2D other)
    {
        return other != null
            && other.gameObject.name == ExitObjectName
            && TryGetExitSceneName(SceneManager.GetActiveScene().name, out _);
    }

    private bool CanInteractWithExit()
    {
        return exitColliders.Count > 0
            && TryGetExitSceneName(SceneManager.GetActiveScene().name, out _);
    }

    private bool IsWaterCollider(Collider2D other)
    {
        return isWaterScene
            && other != null
            && other.gameObject.name == WaterObjectName;
    }

    private bool IsGreenAlgaeCollider(Collider2D other)
    {
        return isWaterScene
            && other != null
            && other.transform.root != null
            && other.transform.root.name == GreenAlgaeObjectName;
    }

    private bool IsWaterMovementActive()
    {
        return isWaterScene && waterColliders.Count > 0;
    }

    private void LoadExitScene()
    {
        if (TryGetExitSceneName(SceneManager.GetActiveScene().name, out string nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    private void ConfigureMovementMode()
    {
        isTopDownScene = IsTopDownScene(SceneManager.GetActiveScene().name);
        isWaterScene = SceneManager.GetActiveScene().name == ArtificialRiverSceneName;
        rb.gravityScale = isTopDownScene ? 0f : baseGravityScale;
    }

    private void EnsurePlayerHealthComponent()
    {
        if (GetComponent<PlayerHealth>() != null)
        {
            return;
        }

        gameObject.AddComponent<PlayerHealth>();
    }

    private void EnsureWaterSceneSystems()
    {
        if (!isWaterScene || GetComponent<PlayerOxygen>() != null)
        {
            return;
        }

        gameObject.AddComponent<PlayerOxygen>();
    }

    private void EnsureArtificialRiverColliderSizing()
    {
        if (!isWaterScene)
        {
            return;
        }

        for (int i = 0; i < ArtificialRiverColliderObjectNames.Length; i++)
        {
            FitSpriteColliders(ArtificialRiverColliderObjectNames[i]);
        }
    }

    private void FitSpriteColliders(string objectName)
    {
        GameObject sceneObject = GameObject.Find(objectName);
        if (sceneObject == null)
        {
            return;
        }

        SpriteColliderSizer.FitBoxCollidersToSpriteRenderers(sceneObject.transform);
    }

    private static void SetTrackedContact(HashSet<Collider2D> contacts, Collider2D other, bool isContacting)
    {
        if (other == null)
        {
            return;
        }

        if (isContacting)
        {
            contacts.Add(other);
            return;
        }

        contacts.Remove(other);
    }

    private bool TryGetExitSceneName(string currentSceneName, out string nextSceneName)
    {
        switch (currentSceneName)
        {
            case MainSceneName:
            case BuildingSceneName:
                nextSceneName = RoadSceneName;
                return true;
            case RoadSceneName:
                nextSceneName = DrainSceneName;
                return true;
            case DrainSceneName:
                nextSceneName = ArtificialRiverSceneName;
                return true;
            case ArtificialRiverSceneName:
                nextSceneName = MountainSceneName;
                return true;
            default:
                nextSceneName = string.Empty;
                return false;
        }
    }

    private bool IsTopDownScene(string sceneName)
    {
        return sceneName == RoadSceneName;
    }

    private void CacheMovementBounds()
    {
        hasMovementBounds = isTopDownScene && TryGetGroundBounds(out movementBounds);
    }

    private bool TryGetGroundBounds(out Bounds bounds)
    {
        GameObject groundObject = GameObject.Find(GroundObjectName);

        if (groundObject == null)
        {
            bounds = default;
            return false;
        }

        Collider2D groundCollider = groundObject.GetComponent<Collider2D>();
        if (groundCollider != null)
        {
            bounds = groundCollider.bounds;
            return true;
        }

        SpriteRenderer groundRenderer = groundObject.GetComponent<SpriteRenderer>();
        if (groundRenderer != null)
        {
            bounds = groundRenderer.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    private void ClampPositionToGroundBounds()
    {
        if (!isTopDownScene || !hasMovementBounds || playerCollider == null)
        {
            return;
        }

        Bounds playerBounds = playerCollider.bounds;
        float halfPlayerWidth = playerBounds.extents.x;
        float halfPlayerHeight = playerBounds.extents.y;
        float minX = movementBounds.min.x + halfPlayerWidth;
        float maxX = movementBounds.max.x - halfPlayerWidth;
        float minY = movementBounds.min.y + halfPlayerHeight;
        float maxY = movementBounds.max.y - halfPlayerHeight;

        if (minX > maxX)
        {
            float centerX = movementBounds.center.x;
            minX = centerX;
            maxX = centerX;
        }

        if (minY > maxY)
        {
            float centerY = movementBounds.center.y;
            minY = centerY;
            maxY = centerY;
        }

        Vector2 clampedPosition = rb.position;
        float clampedX = Mathf.Clamp(clampedPosition.x, minX, maxX);
        float clampedY = Mathf.Clamp(clampedPosition.y, minY, maxY);

        if (Mathf.Approximately(clampedPosition.x, clampedX)
            && Mathf.Approximately(clampedPosition.y, clampedY))
        {
            return;
        }

        clampedPosition.x = clampedX;
        clampedPosition.y = clampedY;
        rb.position = clampedPosition;

        Vector2 clampedVelocity = rb.linearVelocity;

        if ((clampedX <= minX && clampedVelocity.x < 0f)
            || (clampedX >= maxX && clampedVelocity.x > 0f))
        {
            clampedVelocity.x = 0f;
        }

        if ((clampedY <= minY && clampedVelocity.y < 0f)
            || (clampedY >= maxY && clampedVelocity.y > 0f))
        {
            clampedVelocity.y = 0f;
        }

        rb.linearVelocity = clampedVelocity;
    }

    private void ApplyTopDownMovement()
    {
        float currentSpeed = (isRunning ? runSpeed : moveSpeed) * movementSpeedMultiplier;
        Vector2 movementInput = new Vector2(horizontalInput, verticalInput);

        if (movementInput.sqrMagnitude > 1f)
        {
            movementInput.Normalize();
        }

        rb.linearVelocity = movementInput * currentSpeed;
    }

    private void ApplyWaterMovement()
    {
        float currentSpeed = (isRunning ? runSpeed : moveSpeed) * movementSpeedMultiplier;
        float verticalSpeed;

        if (verticalInput > 0f)
        {
            verticalSpeed = waterRiseSpeed * waterFastRiseMultiplier * verticalInput * movementSpeedMultiplier;
        }
        else if (verticalInput < 0f)
        {
            verticalSpeed = -waterSinkSpeed * waterFastSinkMultiplier * movementSpeedMultiplier;
        }
        else
        {
            verticalSpeed = -waterSinkSpeed * movementSpeedMultiplier;
        }

        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(horizontalInput * currentSpeed, verticalSpeed);
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

    private void ApplyTemporaryMovementSpeedMultiplier(float multiplier, float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        movementSpeedMultiplier = Mathf.Max(0f, multiplier);

        if (movementSpeedModifierRoutine != null)
        {
            StopCoroutine(movementSpeedModifierRoutine);
        }

        movementSpeedModifierRoutine = StartCoroutine(ResetMovementSpeedMultiplierAfterDelay(duration));
    }

    private IEnumerator ResetMovementSpeedMultiplierAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);

        movementSpeedMultiplier = 1f;
        movementSpeedModifierRoutine = null;
    }

    private void ResetMovementSpeedModifier()
    {
        if (movementSpeedModifierRoutine != null)
        {
            StopCoroutine(movementSpeedModifierRoutine);
            movementSpeedModifierRoutine = null;
        }

        movementSpeedMultiplier = 1f;
    }
}
