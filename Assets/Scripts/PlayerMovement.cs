using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
    private const string StartSceneName = "Start";
    private const string RoadSceneName = "Road";
    private const string ZooSceneName = "Zoo";
    private const string MountainSceneName = "Mountain";
    private const string ArtificialRiverSceneName = "ArtificialRiver";
    private const string GreenAlgaeObjectName = "GreenAlgae";
    private const string NextObjectName = "Next";
    private const string GroundObjectName = "Ground";
    private const string RopeObjectName = "Rope";
    private const string WaterObjectName = "Water";
    private static readonly string[] ArtificialRiverColliderObjectNames =
    {
        GreenAlgaeObjectName,
        WaterObjectName,
        NextObjectName
    };
    private const float GreenAlgaeSlowMultiplier = 0.5f;
    private const float GreenAlgaeSlowDuration = 3f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float jumpForce = 13f;

    [Header("Climbing")]
    [SerializeField, Min(0f)] private float climbSpeed = 4.5f;

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
    private PlayerStamina playerStamina;
    private float baseGravityScale;
    private float horizontalInput;
    private float verticalInput;
    private bool isRunning;
    private bool isGrounded;
    private bool isClimbing;
    private bool isTopDownScene;
    private bool isWaterScene;
    private bool hasMovementBounds;
    private bool jumpRequested;
    private Vector3 defaultScale;
    private Bounds movementBounds;
    private float movementSpeedMultiplier = 1f;
    private Coroutine movementSpeedModifierRoutine;
    private readonly HashSet<Collider2D> groundColliders = new HashSet<Collider2D>();
    private readonly HashSet<Collider2D> nextColliders = new HashSet<Collider2D>();
    private readonly HashSet<Collider2D> ropeColliders = new HashSet<Collider2D>();
    private readonly HashSet<Collider2D> wallColliders = new HashSet<Collider2D>();
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
        EnsureMountainFallDamageComponent();
        EnsurePlayerStaminaComponent();
        EnsureWaterSceneSystems();
        EnsureArtificialRiverColliderSizing();
        StartSceneController.EnsurePauseMenuInstance();
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
        if (GamePauseState.IsPaused)
        {
            return;
        }

        horizontalInput = ReadHorizontalInput();
        verticalInput = ReadVerticalInput();
        isRunning = IsRunPressed() && CanSprint() && HasMovementInput();
        UpdateClimbingState();

        if (!isClimbing && !isTopDownScene && !IsWaterMovementActive() && WasJumpPressed() && isGrounded)
        {
            jumpRequested = true;
        }

        if (WasInteractPressed() && CanInteractWithNext())
        {
            LoadNextScene();
        }

        UpdateFacingDirection();
    }

    protected virtual void FixedUpdate()
    {
        if (GamePauseState.IsPaused)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (IsWaterMovementActive())
        {
            jumpRequested = false;
            ApplyWaterMovement();
            ConsumeRunStamina();
            return;
        }

        if (isTopDownScene)
        {
            ApplyTopDownMovement();
            ClampPositionToGroundBounds();
            ConsumeRunStamina();
            return;
        }

        if (isClimbing)
        {
            jumpRequested = false;
            ApplyClimbMovement();
            ConsumeRunStamina();
            return;
        }

        float currentSpeed = (isRunning ? runSpeed : moveSpeed) * movementSpeedMultiplier;
        float horizontalSpeed = (!isGrounded && wallColliders.Count > 0)
            ? 0f
            : horizontalInput * currentSpeed;

        rb.linearVelocity = new Vector2(horizontalSpeed, rb.linearVelocity.y);

        if (jumpRequested)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

            isGrounded = false;
            groundColliders.Clear();
            jumpRequested = false;
        }

        ApplyGravity();
        ConsumeRunStamina();
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
        return PlayerInputBindings.GetHorizontalInput();
    }

    private float ReadVerticalInput()
    {
        return PlayerInputBindings.GetVerticalInput();
    }

    private bool IsRunPressed()
    {
        return PlayerInputBindings.IsRunPressed();
    }

    private bool CanSprint()
    {
        return playerStamina == null || playerStamina.CanSprint;
    }

    private bool WasJumpPressed()
    {
        return PlayerInputBindings.WasJumpPressedThisFrame();
    }

    private bool WasInteractPressed()
    {
        return PlayerInputBindings.WasInteractPressedThisFrame();
    }

    private bool HasMovementInput()
    {
        return Mathf.Abs(horizontalInput) > VerticalVelocityThreshold
            || Mathf.Abs(verticalInput) > VerticalVelocityThreshold;
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
        UpdateRopeContacts(otherCollider, isContacting);
        bool hasGroundContact = false;
        bool hasSideContact = false;

        if (isContacting)
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y > GroundNormalThreshold)
                {
                    hasGroundContact = true;
                    break;
                }

                if (Mathf.Abs(contact.normal.x) > GroundNormalThreshold
                    && Mathf.Abs(contact.normal.y) < GroundNormalThreshold)
                {
                    hasSideContact = true;
                }
            }
        }

        SetTrackedContact(groundColliders, otherCollider, hasGroundContact);
        SetTrackedContact(wallColliders, otherCollider, hasSideContact);

        isGrounded = groundColliders.Count > 0;
    }

    private void HandleTriggerContact(Collider2D other, bool isContacting)
    {
        UpdateNextContacts(other, isContacting);
        UpdateRopeContacts(other, isContacting);
        UpdateWaterContacts(other, isContacting);

        if (isContacting)
        {
            TryApplyGreenAlgaeSlow(other);
        }
    }

    private void UpdateNextContacts(Collider2D other, bool isContacting)
    {
        if (!IsNextCollider(other))
        {
            return;
        }

        SetTrackedContact(nextColliders, other, isContacting);
    }

    private void UpdateWaterContacts(Collider2D other, bool isContacting)
    {
        if (!IsWaterCollider(other))
        {
            return;
        }

        SetTrackedContact(waterColliders, other, isContacting);
    }

    private void UpdateRopeContacts(Collider2D other, bool isContacting)
    {
        if (!IsRopeCollider(other))
        {
            return;
        }

        SetTrackedContact(ropeColliders, other, isContacting);

        if (!isContacting && ropeColliders.Count == 0)
        {
            StopClimbing();
        }
    }

    private void TryApplyGreenAlgaeSlow(Collider2D other)
    {
        if (!IsGreenAlgaeCollider(other))
        {
            return;
        }

        ApplyTemporaryMovementSpeedMultiplier(GreenAlgaeSlowMultiplier, GreenAlgaeSlowDuration);
    }

    private bool IsNextCollider(Collider2D other)
    {
        return other != null
            && other.gameObject.name == NextObjectName
            && TryGetNextSceneName(SceneManager.GetActiveScene().name, out _);
    }

    private bool CanInteractWithNext()
    {
        return nextColliders.Count > 0
            && TryGetNextSceneName(SceneManager.GetActiveScene().name, out _);
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

    private bool IsRopeCollider(Collider2D other)
    {
        if (isTopDownScene || isWaterScene || other == null)
        {
            return false;
        }

        Transform current = other.transform;

        while (current != null)
        {
            if (current.name.StartsWith(RopeObjectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void LoadNextScene()
    {
        if (TryGetNextSceneName(SceneManager.GetActiveScene().name, out string nextSceneName))
        {
            SceneFadeTransition.LoadScene(nextSceneName);
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

    private void EnsurePlayerStaminaComponent()
    {
        playerStamina = GetComponent<PlayerStamina>();
        if (playerStamina != null)
        {
            return;
        }

        playerStamina = gameObject.AddComponent<PlayerStamina>();
    }

    private void EnsureMountainFallDamageComponent()
    {
        if (SceneManager.GetActiveScene().name != MountainSceneName || GetComponent<MountainFallDamageController>() != null)
        {
            return;
        }

        gameObject.AddComponent<MountainFallDamageController>();
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

    private void ConsumeRunStamina()
    {
        if (playerStamina == null || !isRunning)
        {
            return;
        }

        playerStamina.ConsumeRunStamina(Time.fixedDeltaTime);

        if (!playerStamina.CanSprint)
        {
            isRunning = false;
        }
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

    private bool TryGetNextSceneName(string currentSceneName, out string nextSceneName)
    {
        // The start scene is a menu, so it uses its own button instead of the Next trigger.
        if (string.Equals(currentSceneName, StartSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            nextSceneName = string.Empty;
            return false;
        }

        int currentBuildIndex = SceneManager.GetActiveScene().buildIndex;
        if (currentBuildIndex < 0)
        {
            nextSceneName = string.Empty;
            return false;
        }

        int sceneCountInBuildSettings = SceneManager.sceneCountInBuildSettings;
        for (int buildIndex = currentBuildIndex + 1; buildIndex < sceneCountInBuildSettings; buildIndex++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                continue;
            }

            string candidateSceneName = Path.GetFileNameWithoutExtension(scenePath);
            nextSceneName = candidateSceneName;
            return true;
        }

        nextSceneName = string.Empty;
        return false;
    }

    private bool IsTopDownScene(string sceneName)
    {
        return sceneName == RoadSceneName || sceneName == ZooSceneName;
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

    private void ApplyClimbMovement()
    {
        float currentSpeed = (isRunning ? runSpeed : moveSpeed) * movementSpeedMultiplier;
        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(horizontalInput * currentSpeed, verticalInput * climbSpeed);
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

    private void UpdateClimbingState()
    {
        if (isTopDownScene || isWaterScene)
        {
            StopClimbing();
            return;
        }

        bool hasClimbInput = Mathf.Abs(verticalInput) > VerticalVelocityThreshold;

        if (!isClimbing)
        {
            if (ropeColliders.Count > 0 && hasClimbInput)
            {
                BeginClimbing();
            }

            return;
        }

        if (ropeColliders.Count == 0 || (isGrounded && !hasClimbInput))
        {
            StopClimbing();
        }
    }

    private void BeginClimbing()
    {
        isClimbing = true;
        jumpRequested = false;
        isGrounded = false;
        groundColliders.Clear();
        rb.gravityScale = 0f;
    }

    private void StopClimbing()
    {
        isClimbing = false;
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
