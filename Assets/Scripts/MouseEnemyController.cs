using UnityEngine;

[DisallowMultipleComponent]
public class MouseEnemyController : MonoBehaviour
{
    private const float DefaultFallbackRangeWidth = 4f;

    [Header("Movement")]
    [SerializeField, Min(0.01f)] private float moveSpeed = 1.75f;
    [SerializeField, Min(0f)] private float edgePadding = 0.08f;
    [SerializeField] private bool startMovingRight = true;
    [SerializeField, Min(0.1f)] private float rangeSearchDistance = 200f;
    [SerializeField] private Transform movementRangeOverride;

    [Header("Damage")]
    [SerializeField, Min(1)] private int contactDamage = 1;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private BoxCollider2D bodyCollider;
    private Bounds movementBounds;
    private bool hasMovementBounds;
    private bool defaultFlipX;
    private float movementDirection;
    private float ownHalfWidth;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        defaultFlipX = spriteRenderer != null && spriteRenderer.flipX;
        movementDirection = startMovingRight ? 1f : -1f;

        ResolveMovementBounds();
        EnsurePhysicsComponents();
        FitOwnColliderToSprite();
        CacheOwnSize();
        SnapIntoRange();
        UpdateFacing();
    }

    private void FixedUpdate()
    {
        if (GamePauseState.IsPaused || rb == null || !hasMovementBounds)
        {
            return;
        }

        float minX = GetMinX();
        float maxX = GetMaxX();
        Vector2 currentPosition = rb.position;
        float nextX = currentPosition.x + (movementDirection * moveSpeed * Time.fixedDeltaTime);

        if (nextX >= maxX)
        {
            nextX = maxX;
            movementDirection = -Mathf.Abs(movementDirection);
            UpdateFacing();
        }
        else if (nextX <= minX)
        {
            nextX = minX;
            movementDirection = Mathf.Abs(movementDirection);
            UpdateFacing();
        }

        rb.MovePosition(new Vector2(nextX, currentPosition.y));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDealDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDealDamage(other);
    }

    private void EnsurePhysicsComponents()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        bodyCollider = GetComponent<BoxCollider2D>();
        if (bodyCollider == null)
        {
            bodyCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        bodyCollider.isTrigger = true;
    }

    private void FitOwnColliderToSprite()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        SpriteColliderSizer.FitBoxCollidersToSpriteRenderers(transform);
    }

    private void CacheOwnSize()
    {
        if (spriteRenderer != null)
        {
            ownHalfWidth = Mathf.Max(0f, spriteRenderer.bounds.extents.x);
            return;
        }

        if (bodyCollider != null)
        {
            ownHalfWidth = Mathf.Max(0f, bodyCollider.size.x * Mathf.Abs(transform.lossyScale.x) * 0.5f);
            return;
        }

        ownHalfWidth = 0.25f;
    }

    private void ResolveMovementBounds()
    {
        if (movementRangeOverride != null && TryGetBoundsFromTransform(movementRangeOverride, out movementBounds))
        {
            hasMovementBounds = true;
            return;
        }

        if (TryFindBoundsBelowMouse(out movementBounds))
        {
            hasMovementBounds = true;
            return;
        }

        movementBounds = new Bounds(transform.position, new Vector3(DefaultFallbackRangeWidth, 1f, 1f));
        hasMovementBounds = true;
        Debug.LogWarning($"{name} could not find a collider below it, so it is using a fallback patrol range.", this);
    }

    private bool TryFindBoundsBelowMouse(out Bounds bounds)
    {
        Vector2 origin = transform.position;
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, rangeSearchDistance);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            bounds = hitCollider.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    private bool TryGetBoundsFromTransform(Transform source, out Bounds bounds)
    {
        Collider2D collider = source.GetComponentInChildren<Collider2D>(true);
        if (collider != null)
        {
            bounds = collider.bounds;
            return true;
        }

        SpriteRenderer renderer = source.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer != null)
        {
            bounds = renderer.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    private void SnapIntoRange()
    {
        if (rb == null || !hasMovementBounds)
        {
            return;
        }

        Vector2 currentPosition = rb.position;
        currentPosition.x = Mathf.Clamp(currentPosition.x, GetMinX(), GetMaxX());
        rb.position = currentPosition;
    }

    private float GetMinX()
    {
        float minX = movementBounds.min.x + ownHalfWidth + edgePadding;
        float maxX = movementBounds.max.x - ownHalfWidth - edgePadding;

        if (minX > maxX)
        {
            float centerX = movementBounds.center.x;
            return centerX;
        }

        return minX;
    }

    private float GetMaxX()
    {
        float minX = movementBounds.min.x + ownHalfWidth + edgePadding;
        float maxX = movementBounds.max.x - ownHalfWidth - edgePadding;

        if (minX > maxX)
        {
            float centerX = movementBounds.center.x;
            return centerX;
        }

        return maxX;
    }

    private void UpdateFacing()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.flipX = movementDirection < 0f ? !defaultFlipX : defaultFlipX;
    }

    private void TryDealDamage(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.TakeDamage(contactDamage);
    }
}
