using UnityEngine;

public class FallingHazardSpawner : MonoBehaviour
{
    [SerializeField, Min(0.2f)] private float minSpawnInterval = 0.8f;
    [SerializeField, Min(0.2f)] private float maxSpawnInterval = 1.8f;
    [SerializeField, Min(0f)] private float horizontalPadding = 0.75f;
    [SerializeField, Min(0f)] private float spawnHeightOffset = 1.5f;
    [SerializeField, Min(0.1f)] private float minHazardSize = 0.6f;
    [SerializeField, Min(0.1f)] private float maxHazardSize = 1.2f;
    [SerializeField, Min(0f)] private float initialFallSpeed = 0.75f;
    [SerializeField, Min(0f)] private float fallGravityScale = 0.8f;
    [SerializeField, Min(1)] private int hazardDamage = 1;
    [SerializeField, Min(0.5f)] private float hazardLifetime = 10f;
    [SerializeField] private Color hazardColor = new Color(0.45f, 0.43f, 0.4f, 1f);
    [SerializeField] private int sortingOrder = 5;

    private static Sprite cachedSprite;

    private Camera mainCamera;
    private float nextSpawnTime;

    private void OnEnable()
    {
        ScheduleNextSpawn();
    }

    private void Update()
    {
        if (GamePauseState.IsPaused)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }
        }

        if (Time.time < nextSpawnTime)
        {
            return;
        }

        SpawnHazard();
        ScheduleNextSpawn();
    }

    private void SpawnHazard()
    {
        float spawnPlaneDistance = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 leftEdge = mainCamera.ViewportToWorldPoint(new Vector3(0f, 1f, spawnPlaneDistance));
        Vector3 rightEdge = mainCamera.ViewportToWorldPoint(new Vector3(1f, 1f, spawnPlaneDistance));

        float minX = Mathf.Min(leftEdge.x, rightEdge.x) + horizontalPadding;
        float maxX = Mathf.Max(leftEdge.x, rightEdge.x) - horizontalPadding;
        float spawnX = minX <= maxX ? Random.Range(minX, maxX) : (leftEdge.x + rightEdge.x) * 0.5f;
        float spawnY = Mathf.Max(leftEdge.y, rightEdge.y) + spawnHeightOffset;
        float hazardSize = Random.Range(minHazardSize, maxHazardSize);

        GameObject hazardObject = new GameObject("FallingHazard");
        hazardObject.transform.SetParent(transform);
        hazardObject.transform.position = new Vector3(spawnX, spawnY, transform.position.z);
        hazardObject.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        hazardObject.transform.localScale = new Vector3(hazardSize, hazardSize, 1f);

        SpriteRenderer spriteRenderer = hazardObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetHazardSprite();
        spriteRenderer.color = hazardColor;
        spriteRenderer.sortingOrder = sortingOrder;

        BoxCollider2D collider = hazardObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;

        Rigidbody2D rigidbody2D = hazardObject.AddComponent<Rigidbody2D>();
        rigidbody2D.gravityScale = fallGravityScale;
        rigidbody2D.linearVelocity = Vector2.down * initialFallSpeed;
        rigidbody2D.freezeRotation = true;
        rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rigidbody2D.interpolation = RigidbodyInterpolation2D.Interpolate;

        FallingHazard hazard = hazardObject.AddComponent<FallingHazard>();
        hazard.Initialize(hazardDamage, hazardLifetime);
    }

    private void ScheduleNextSpawn()
    {
        float minInterval = Mathf.Max(0.2f, minSpawnInterval);
        float maxInterval = Mathf.Max(minInterval, maxSpawnInterval);
        nextSpawnTime = Time.time + Random.Range(minInterval, maxInterval);
    }

    private static Sprite GetHazardSprite()
    {
        if (cachedSprite != null)
        {
            return cachedSprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        cachedSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width);

        return cachedSprite;
    }
}

[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
public class FallingHazard : MonoBehaviour
{
    private int damage = 1;
    private float lifetime = 6f;

    public void Initialize(int damageAmount, float lifetimeSeconds)
    {
        damage = Mathf.Max(1, damageAmount);
        lifetime = Mathf.Max(0.5f, lifetimeSeconds);
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || other.GetComponent<FallingHazard>() != null)
        {
            return;
        }

        if (other.TryGetComponent(out PlayerHealth playerHealth))
        {
            playerHealth.TakeDamage(damage);
        }
    }
}
