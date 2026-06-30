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
    private const string ResultSceneName = "Result";
    private const string ArtificialRiverSceneName = "ArtificialRiver";
    private const string ArtificialRiverEntryCircleObjectName = "Circle";
    private const string UnderwaterMovementAudioSourceObjectName = "UnderwaterMovementAudioSource";
    private const string UnderwaterMovementAudioResourcesPath = "Audios/물속에서 호흡하는(보글)2";
    private const string GreenAlgaeObjectName = "GreenAlgae";
    private const string NextObjectName = "Next";
    private const string GroundObjectName = "Ground";
    private const string RopeObjectName = "Rope";
    private const string WaterObjectName = "Water";
    private const float ArtificialRiverEntryArcRatio = 0.7f;
    private const float ArtificialRiverEntryWaterInset = 1.1f;
    private const float ArtificialRiverBubbleLifetime = 0.5f;
    private const float ArtificialRiverBubbleSpawnInterval = 0.08f;
    private const float ArtificialRiverBubbleSpawnOffset = 0.42f;
    private const float ArtificialRiverBubbleDriftSpeed = 0.9f;
    private const float ArtificialRiverBubbleSideScatter = 0.18f;
    private const float ArtificialRiverBubbleForwardScatter = 0.12f;
    private const float ArtificialRiverBubbleVerticalScatter = 0.1f;
    private const int ArtificialRiverBubbleSpriteTextureSize = 48;
    private const float ArtificialRiverBubbleSpriteEdgeSoftness = 2f;
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
    [SerializeField, Range(0f, 1f)] private float underwaterMovementSoundVolume = 1f;
    [SerializeField, Min(0f)] private float artificialRiverEntryDuration = 1.5f;

    private const float FacingThreshold = 0.01f;
    private const float GroundNormalThreshold = 0.5f;
    private const float VerticalVelocityThreshold = 0.01f;

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private PlayerStamina playerStamina;
    private float baseGravityScale;
    private RigidbodyType2D baseBodyType;
    private float horizontalInput;
    private float verticalInput;
    private bool isRunning;
    private bool isGrounded;
    private bool isClimbing;
    private bool suppressClimbWhileHoldingDown;
    private bool isTopDownScene;
    private bool isWaterScene;
    private bool hasMovementBounds;
    private bool jumpRequested;
    private bool jumpPressedThisFrame;
    private Vector3 defaultScale;
    private Bounds movementBounds;
    private float movementSpeedMultiplier = 1f;
    private Coroutine movementSpeedModifierRoutine;
    private AudioSource underwaterMovementAudioSource;
    private SpriteRenderer primarySpriteRenderer;
    private Vector2 artificialRiverEntryCircleCenter;
    private Vector2 artificialRiverEntryCircleExtents;
    private Vector2 artificialRiverEntryWaterTargetPosition;
    private Vector2 artificialRiverEntryPreviousPosition;
    private readonly HashSet<Collider2D> groundColliders = new HashSet<Collider2D>();
    private readonly HashSet<Collider2D> nextColliders = new HashSet<Collider2D>();
    private readonly HashSet<Collider2D> ropeColliders = new HashSet<Collider2D>();
    private readonly HashSet<Collider2D> wallColliders = new HashSet<Collider2D>();
    private readonly HashSet<Collider2D> waterColliders = new HashSet<Collider2D>();

    private static AudioClip underwaterMovementAudioClip;
    private static Sprite artificialRiverBubbleSprite;
    private bool isArtificialRiverEntrySequenceActive;
    private float artificialRiverEntryElapsed;
    private float nextArtificialRiverBubbleSpawnTime;

    public float HorizontalInput => horizontalInput;
    public float VerticalInput => verticalInput;
    public bool IsRunning => isRunning;
    public bool IsGrounded => isGrounded;
    public bool IsClimbing => isClimbing;
    public bool UsesTopDownMovement => isTopDownScene;
    public bool UsesWaterMovement => isWaterScene;
    public bool JumpPressedThisFrame => jumpPressedThisFrame;
    public bool HasMovementInput => HasMovementInputValue();
    public bool IsAirborne => !isTopDownScene && !isWaterScene && !isClimbing && !isGrounded;
    public Vector2 MovementInput => new Vector2(horizontalInput, verticalInput);
    public Vector2 CurrentVelocity => rb != null ? rb.linearVelocity : Vector2.zero;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        primarySpriteRenderer = GetComponent<SpriteRenderer>();
        baseGravityScale = rb.gravityScale;
        baseBodyType = rb.bodyType;

        defaultScale = transform.localScale;
        defaultScale.x = Mathf.Abs(defaultScale.x);

        ConfigureMovementMode();
        EnsurePlayerHealthComponent();
        EnsureMountainFallDamageComponent();
        EnsurePlayerStaminaComponent();
        EnsureWaterSceneSystems();
        EnsureUnderwaterMovementAudio();
        EnsureArtificialRiverColliderSizing();
        StartSceneController.EnsurePauseMenuInstance();
    }

    protected virtual void OnEnable()
    {
        AudioSettingsStore.VolumesChanged += HandleVolumesChanged;
        ApplyUnderwaterMovementSoundVolume();
    }

    protected virtual void OnDisable()
    {
        AudioSettingsStore.VolumesChanged -= HandleVolumesChanged;
        StopUnderwaterMovementSound();
        ResetMovementSpeedModifier();
        nextArtificialRiverBubbleSpawnTime = 0f;
    }

    protected virtual void Start()
    {
        CacheMovementBounds();
        TryStartArtificialRiverEntrySequence();
    }

    protected virtual void Update()
    {
        jumpPressedThisFrame = false;

        if (GamePauseState.IsPaused)
        {
            StopUnderwaterMovementSound();
            return;
        }

        if (isArtificialRiverEntrySequenceActive)
        {
            UpdateFacingDirection();
            UpdateUnderwaterMovementSound();
            return;
        }

        horizontalInput = ReadHorizontalInput();
        verticalInput = ReadVerticalInput();
        jumpPressedThisFrame = WasJumpPressed();
        isRunning = IsRunPressed() && CanSprint() && HasMovementInputValue();
        UpdateClimbingState();

        if (!isClimbing && !isTopDownScene && !IsWaterMovementActive() && jumpPressedThisFrame && isGrounded)
        {
            jumpRequested = true;
        }

        if (WasInteractPressed() && CanInteractWithNext())
        {
            LoadNextScene();
        }

        UpdateFacingDirection();
        UpdateUnderwaterMovementSound();
    }

    protected virtual void FixedUpdate()
    {
        if (GamePauseState.IsPaused)
        {
            rb.linearVelocity = Vector2.zero;
            StopUnderwaterMovementSound();
            return;
        }

        if (isArtificialRiverEntrySequenceActive)
        {
            UpdateArtificialRiverEntrySequence();
            return;
        }

        if (IsWaterMovementActive())
        {
            jumpRequested = false;
            ApplyWaterMovement();
            UpdateArtificialRiverBubbleTrail(HasMovementInputValue() ? rb.linearVelocity : Vector2.zero);
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

    private bool HasMovementInputValue()
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
            if (nextSceneName == ResultSceneName)
            {
                ResultSceneState.LoadCreditsResult();
                return;
            }

            SceneFadeTransition.LoadScene(nextSceneName);
        }
    }

    private void ConfigureMovementMode()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        isTopDownScene = IsTopDownScene(activeSceneName);
        isWaterScene = activeSceneName == ArtificialRiverSceneName;
        rb.bodyType = isTopDownScene ? RigidbodyType2D.Kinematic : baseBodyType;
        rb.gravityScale = isTopDownScene ? 0f : baseGravityScale;
        rb.linearVelocity = Vector2.zero;
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

    private void EnsureUnderwaterMovementAudio()
    {
        if (!isWaterScene)
        {
            return;
        }

        if (underwaterMovementAudioSource == null)
        {
            Transform existingAudioSourceTransform = transform.Find(UnderwaterMovementAudioSourceObjectName);
            if (existingAudioSourceTransform != null)
            {
                underwaterMovementAudioSource = existingAudioSourceTransform.GetComponent<AudioSource>();
            }
        }

        if (underwaterMovementAudioSource == null)
        {
            GameObject audioSourceObject = new GameObject(UnderwaterMovementAudioSourceObjectName, typeof(AudioSource));
            audioSourceObject.transform.SetParent(transform, false);
            underwaterMovementAudioSource = audioSourceObject.GetComponent<AudioSource>();
        }

        if (underwaterMovementAudioSource == null)
        {
            return;
        }

        if (underwaterMovementAudioClip == null)
        {
            underwaterMovementAudioClip = Resources.Load<AudioClip>(UnderwaterMovementAudioResourcesPath);
        }

        underwaterMovementAudioSource.playOnAwake = false;
        underwaterMovementAudioSource.loop = true;
        underwaterMovementAudioSource.spatialBlend = 0f;
        underwaterMovementAudioSource.clip = underwaterMovementAudioClip;
        ApplyUnderwaterMovementSoundVolume();
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

    private void TryStartArtificialRiverEntrySequence()
    {
        if (!isWaterScene || artificialRiverEntryDuration <= 0f || playerCollider == null)
        {
            return;
        }

        GameObject circleObject = GameObject.Find(ArtificialRiverEntryCircleObjectName);
        GameObject waterObject = GameObject.Find(WaterObjectName);
        if (circleObject == null || waterObject == null)
        {
            return;
        }

        SpriteRenderer circleRenderer = circleObject.GetComponent<SpriteRenderer>();
        Collider2D waterCollider = waterObject.GetComponent<Collider2D>();
        if (circleRenderer == null || waterCollider == null)
        {
            return;
        }

        Bounds circleBounds = circleRenderer.bounds;
        artificialRiverEntryCircleCenter = circleBounds.center;
        artificialRiverEntryCircleExtents = circleBounds.extents;

        Vector2 rightEdgePosition = GetArtificialRiverCirclePoint(1f);
        Vector2 waterSurfacePosition = waterCollider.ClosestPoint(
            rightEdgePosition + Vector2.right * Mathf.Max(0.5f, playerCollider.bounds.extents.x));
        Vector2 toWaterInterior = (Vector2)waterCollider.bounds.center - waterSurfacePosition;
        if (toWaterInterior.sqrMagnitude <= Mathf.Epsilon)
        {
            toWaterInterior = Vector2.right;
        }

        artificialRiverEntryWaterTargetPosition =
            waterSurfacePosition + (toWaterInterior.normalized * ArtificialRiverEntryWaterInset);

        Vector2 startPosition = GetArtificialRiverCirclePoint(0f);
        rb.position = startPosition;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        horizontalInput = 0f;
        verticalInput = -1f;
        isRunning = false;
        artificialRiverEntryPreviousPosition = startPosition;
        artificialRiverEntryElapsed = 0f;
        isArtificialRiverEntrySequenceActive = true;
    }

    private void UpdateArtificialRiverEntrySequence()
    {
        artificialRiverEntryElapsed = Mathf.Min(
            artificialRiverEntryDuration,
            artificialRiverEntryElapsed + Time.fixedDeltaTime);

        float normalizedTime = artificialRiverEntryDuration <= 0f
            ? 1f
            : artificialRiverEntryElapsed / artificialRiverEntryDuration;
        Vector2 nextPosition = EvaluateArtificialRiverEntryPosition(normalizedTime);
        Vector2 movementDelta = nextPosition - artificialRiverEntryPreviousPosition;

        horizontalInput = GetDirectionalInputValue(movementDelta.x);
        verticalInput = GetDirectionalInputValue(movementDelta.y);
        isRunning = false;

        rb.linearVelocity = Vector2.zero;
        rb.MovePosition(nextPosition);
        artificialRiverEntryPreviousPosition = nextPosition;
        UpdateArtificialRiverBubbleTrail(IsWaterMovementActive() ? movementDelta : Vector2.zero);

        if (normalizedTime >= 1f)
        {
            FinishArtificialRiverEntrySequence();
        }
    }

    private Vector2 EvaluateArtificialRiverEntryPosition(float normalizedTime)
    {
        float clampedTime = Mathf.Clamp01(normalizedTime);
        if (clampedTime <= ArtificialRiverEntryArcRatio)
        {
            float arcProgress = ArtificialRiverEntryArcRatio <= 0f
                ? 1f
                : Mathf.Clamp01(clampedTime / ArtificialRiverEntryArcRatio);
            return GetArtificialRiverCirclePoint(Mathf.SmoothStep(0f, 1f, arcProgress));
        }

        float lineProgress = ArtificialRiverEntryArcRatio >= 1f
            ? 1f
            : Mathf.Clamp01((clampedTime - ArtificialRiverEntryArcRatio) / (1f - ArtificialRiverEntryArcRatio));
        return Vector2.Lerp(
            GetArtificialRiverCirclePoint(1f),
            artificialRiverEntryWaterTargetPosition,
            Mathf.SmoothStep(0f, 1f, lineProgress));
    }

    private Vector2 GetArtificialRiverCirclePoint(float normalizedArcProgress)
    {
        float angleRadians = Mathf.Lerp(Mathf.PI * 0.5f, 0f, Mathf.Clamp01(normalizedArcProgress));
        return artificialRiverEntryCircleCenter + new Vector2(
            Mathf.Cos(angleRadians) * artificialRiverEntryCircleExtents.x,
            Mathf.Sin(angleRadians) * artificialRiverEntryCircleExtents.y);
    }

    private void FinishArtificialRiverEntrySequence()
    {
        isArtificialRiverEntrySequenceActive = false;
        artificialRiverEntryElapsed = 0f;
        horizontalInput = 0f;
        verticalInput = 0f;
        isRunning = false;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = IsWaterMovementActive() ? 0f : baseGravityScale;
    }

    private static float GetDirectionalInputValue(float value)
    {
        if (value > VerticalVelocityThreshold)
        {
            return 1f;
        }

        if (value < -VerticalVelocityThreshold)
        {
            return -1f;
        }

        return 0f;
    }

    private void UpdateArtificialRiverBubbleTrail(Vector2 movementVector)
    {
        if (!isWaterScene
            || !IsWaterMovementActive()
            || movementVector.sqrMagnitude <= VerticalVelocityThreshold * VerticalVelocityThreshold
            || Time.time < nextArtificialRiverBubbleSpawnTime)
        {
            return;
        }

        SpawnArtificialRiverBubble(movementVector.normalized);
        nextArtificialRiverBubbleSpawnTime = Time.time + ArtificialRiverBubbleSpawnInterval;
    }

    private void SpawnArtificialRiverBubble(Vector2 movementDirection)
    {
        Sprite bubbleSprite = GetArtificialRiverBubbleSprite();
        if (bubbleSprite == null)
        {
            return;
        }

        Vector2 backwardsDirection = movementDirection.sqrMagnitude > Mathf.Epsilon
            ? -movementDirection.normalized
            : Vector2.left;
        Vector2 lateralDirection = new Vector2(-backwardsDirection.y, backwardsDirection.x);
        Vector2 scatterOffset =
            (lateralDirection * Random.Range(-ArtificialRiverBubbleSideScatter, ArtificialRiverBubbleSideScatter))
            + (movementDirection * Random.Range(-ArtificialRiverBubbleForwardScatter, ArtificialRiverBubbleForwardScatter))
            + (Vector2.up * Random.Range(-ArtificialRiverBubbleVerticalScatter, ArtificialRiverBubbleVerticalScatter));
        Vector2 spawnPosition = rb.position + (backwardsDirection * ArtificialRiverBubbleSpawnOffset) + scatterOffset;
        float bubbleScale = Random.Range(0.14f, 0.26f);

        GameObject bubbleObject = new GameObject("ArtificialRiverBubble", typeof(Transform), typeof(SpriteRenderer));
        bubbleObject.transform.position = new Vector3(spawnPosition.x, spawnPosition.y, transform.position.z + 0.01f);
        bubbleObject.transform.localScale = Vector3.one * bubbleScale;

        SpriteRenderer bubbleRenderer = bubbleObject.GetComponent<SpriteRenderer>();
        bubbleRenderer.sprite = bubbleSprite;
        bubbleRenderer.color = new Color(0.86f, 0.97f, 1f, Random.Range(0.45f, 0.72f));
        bubbleRenderer.sortingLayerID = primarySpriteRenderer != null ? primarySpriteRenderer.sortingLayerID : 0;
        bubbleRenderer.sortingOrder = primarySpriteRenderer != null ? primarySpriteRenderer.sortingOrder : 0;

        Vector2 driftDirection = (
            (backwardsDirection * Random.Range(0.2f, 0.55f))
            + (lateralDirection * Random.Range(-0.7f, 0.7f))
            + (Vector2.up * Random.Range(0.65f, 1.05f))).normalized;
        StartCoroutine(AnimateArtificialRiverBubble(
            bubbleObject.transform,
            bubbleRenderer,
            driftDirection * Random.Range(ArtificialRiverBubbleDriftSpeed * 0.8f, ArtificialRiverBubbleDriftSpeed * 1.25f),
            bubbleScale));
    }

    private IEnumerator AnimateArtificialRiverBubble(
        Transform bubbleTransform,
        SpriteRenderer bubbleRenderer,
        Vector2 driftVelocity,
        float initialScale)
    {
        if (bubbleTransform == null || bubbleRenderer == null)
        {
            yield break;
        }

        Color initialColor = bubbleRenderer.color;
        float elapsed = 0f;

        while (elapsed < ArtificialRiverBubbleLifetime)
        {
            if (bubbleTransform == null || bubbleRenderer == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / ArtificialRiverBubbleLifetime);
            bubbleTransform.position += (Vector3)(driftVelocity * Time.deltaTime);
            bubbleTransform.localScale = Vector3.one * Mathf.Lerp(initialScale, initialScale * 1.35f, progress);

            Color nextColor = initialColor;
            nextColor.a = Mathf.Lerp(initialColor.a, 0f, progress);
            bubbleRenderer.color = nextColor;
            yield return null;
        }

        if (bubbleTransform != null)
        {
            Destroy(bubbleTransform.gameObject);
        }
    }

    private static Sprite GetArtificialRiverBubbleSprite()
    {
        if (artificialRiverBubbleSprite != null)
        {
            return artificialRiverBubbleSprite;
        }

        Texture2D texture = new Texture2D(
            ArtificialRiverBubbleSpriteTextureSize,
            ArtificialRiverBubbleSpriteTextureSize,
            TextureFormat.RGBA32,
            false)
        {
            name = "ArtificialRiverBubbleTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[ArtificialRiverBubbleSpriteTextureSize * ArtificialRiverBubbleSpriteTextureSize];
        float center = (ArtificialRiverBubbleSpriteTextureSize - 1f) * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < ArtificialRiverBubbleSpriteTextureSize; y++)
        {
            for (int x = 0; x < ArtificialRiverBubbleSpriteTextureSize; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((radius - distance) / ArtificialRiverBubbleSpriteEdgeSoftness);
                alpha = Mathf.SmoothStep(0f, 1f, alpha);
                pixels[(y * ArtificialRiverBubbleSpriteTextureSize) + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        artificialRiverBubbleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, ArtificialRiverBubbleSpriteTextureSize, ArtificialRiverBubbleSpriteTextureSize),
            new Vector2(0.5f, 0.5f),
            ArtificialRiverBubbleSpriteTextureSize,
            0u,
            SpriteMeshType.FullRect);
        artificialRiverBubbleSprite.name = "ArtificialRiverBubbleSprite";
        artificialRiverBubbleSprite.hideFlags = HideFlags.HideAndDontSave;
        return artificialRiverBubbleSprite;
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

    private void UpdateUnderwaterMovementSound()
    {
        if (!ShouldPlayUnderwaterMovementSound())
        {
            StopUnderwaterMovementSound();
            return;
        }

        if (underwaterMovementAudioSource == null || underwaterMovementAudioClip == null)
        {
            EnsureUnderwaterMovementAudio();
        }

        if (underwaterMovementAudioSource == null
            || underwaterMovementAudioClip == null
            || underwaterMovementAudioSource.isPlaying)
        {
            return;
        }

        underwaterMovementAudioSource.Play();
    }

    private bool ShouldPlayUnderwaterMovementSound()
    {
        return isWaterScene
            && IsWaterMovementActive()
            && HasMovementInputValue();
    }

    private void StopUnderwaterMovementSound()
    {
        if (underwaterMovementAudioSource != null && underwaterMovementAudioSource.isPlaying)
        {
            underwaterMovementAudioSource.Stop();
        }
    }

    private void HandleVolumesChanged()
    {
        ApplyUnderwaterMovementSoundVolume();
    }

    private void ApplyUnderwaterMovementSoundVolume()
    {
        if (underwaterMovementAudioSource == null)
        {
            return;
        }

        underwaterMovementAudioSource.volume =
            Mathf.Clamp01(underwaterMovementSoundVolume) * AudioSettingsStore.SoundEffectVolume;
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
        bool isHoldingDownInput = verticalInput < -VerticalVelocityThreshold;

        if (!isHoldingDownInput)
        {
            suppressClimbWhileHoldingDown = false;
        }

        if (!isClimbing)
        {
            if (!suppressClimbWhileHoldingDown
                && ropeColliders.Count > 0
                && hasClimbInput)
            {
                BeginClimbing();
            }

            return;
        }

        if (isHoldingDownInput)
        {
            suppressClimbWhileHoldingDown = true;
            StopClimbing(resetVerticalVelocity: true);
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

    private void StopClimbing(bool resetVerticalVelocity = false)
    {
        if (resetVerticalVelocity)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        }

        isClimbing = false;
        rb.gravityScale = baseGravityScale;
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
