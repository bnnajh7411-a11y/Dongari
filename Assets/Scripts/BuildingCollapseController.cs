using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-25)]
public class BuildingCollapseController : MonoBehaviour
{
    private const string TargetSceneName = "Building";
    private const string FloorObjectName = "Floor";
    private const string LeftWallObjectName = "LWall";
    private const string RightWallObjectName = "RWall";
    private const string PlayerObjectName = "Player";
    private const string BaseBlockObjectName = "1";
    private const float CollapseEpsilon = 0.01f;

    [SerializeField, Min(0f)] private float collapseDelay = 6f;
    [SerializeField, Min(1f)] private float collapseDuration = 42f;
    [SerializeField] private bool excludeBaseBlock = true;
    [SerializeField, Min(0f)] private float minimumShardBlockWidth = 0.25f;
    [SerializeField, Min(0.25f)] private float targetShardWidth = 1.5f;
    [SerializeField, Min(0.25f)] private float targetShardHeight = 1.5f;
    [SerializeField, Min(1)] private int maxShardColumns = 10;
    [SerializeField, Min(1)] private int maxShardRows = 2;
    [SerializeField, Min(0f)] private float shardHorizontalSpeed = 1.6f;
    [SerializeField, Min(0f)] private float shardUpwardSpeed = 1.2f;
    [SerializeField, Min(0f)] private float shardGravityScale = 1.35f;
    [SerializeField, Min(0.1f)] private float shardLifetime = 3f;
    [SerializeField] private int collapseLayer = 3;

    private readonly List<CollapseBlock> collapseBlocks = new List<CollapseBlock>();
    private readonly List<Collider2D> playerColliders = new List<Collider2D>();

    private WallState leftWall;
    private WallState rightWall;
    private float collapseStartTime;
    private float collapseStartY;
    private float collapseEndY;
    private int nextCollapseIndex;
    private Collider2D firstPlatformCollider;
    private PlayerHealth playerHealth;
    private bool isInitialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != TargetSceneName || Object.FindAnyObjectByType<BuildingCollapseController>() != null)
        {
            return;
        }

        GameObject floorObject = GameObject.Find(FloorObjectName);
        if (floorObject != null)
        {
            floorObject.AddComponent<BuildingCollapseController>();
        }
    }

    private void Awake()
    {
        TryInitialize();
    }

    private void Start()
    {
        if (!TryInitialize())
        {
            enabled = false;
            return;
        }

        collapseStartTime = Time.time + collapseDelay;
    }

    private void Update()
    {
        if (GamePauseState.IsPaused)
        {
            return;
        }

        if (!isInitialized || Time.time < collapseStartTime)
        {
            return;
        }

        float progress = Mathf.Clamp01((Time.time - collapseStartTime) / collapseDuration);
        float currentCollapseY = Mathf.Lerp(collapseStartY, collapseEndY, progress);
        float removedDistance = collapseStartY - currentCollapseY;

        UpdateWall(ref leftWall, removedDistance);
        UpdateWall(ref rightWall, removedDistance);

        while (nextCollapseIndex < collapseBlocks.Count && collapseBlocks[nextCollapseIndex].TopY >= currentCollapseY - CollapseEpsilon)
        {
            CollapseBlockAt(nextCollapseIndex);
            nextCollapseIndex++;
        }

        if (progress >= 1f && nextCollapseIndex >= collapseBlocks.Count)
        {
            enabled = false;
        }
    }

    private bool TryInitialize()
    {
        if (isInitialized || SceneManager.GetActiveScene().name != TargetSceneName)
        {
            return isInitialized;
        }

        CollectCollapseBlocks();
        CacheWalls();
        RefreshPlayerColliders();
        CachePlayerHealth();
        CacheFirstPlatformCollider();

        if (collapseBlocks.Count == 0)
        {
            return false;
        }

        collapseStartY = collapseBlocks[0].TopY;
        collapseEndY = collapseBlocks[collapseBlocks.Count - 1].TopY - CollapseEpsilon;
        isInitialized = true;
        return true;
    }

    private void CollectCollapseBlocks()
    {
        collapseBlocks.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.gameObject.layer != collapseLayer)
            {
                continue;
            }

            if (excludeBaseBlock && child.name == BaseBlockObjectName)
            {
                continue;
            }

            if (!child.TryGetComponent(out SpriteRenderer spriteRenderer) || !child.TryGetComponent(out Collider2D collider))
            {
                continue;
            }

            collapseBlocks.Add(new CollapseBlock(child.gameObject, spriteRenderer, collider));
        }

        collapseBlocks.Sort((first, second) => second.TopY.CompareTo(first.TopY));
    }

    private void CacheWalls()
    {
        leftWall = CreateWallState(LeftWallObjectName);
        rightWall = CreateWallState(RightWallObjectName);
    }

    private void RefreshPlayerColliders()
    {
        playerColliders.Clear();

        GameObject playerObject = GameObject.Find(PlayerObjectName);
        if (playerObject == null)
        {
            return;
        }

        playerColliders.AddRange(playerObject.GetComponentsInChildren<Collider2D>(true));
    }

    private void CachePlayerHealth()
    {
        playerHealth = FindPlayerHealth();
    }

    private void CacheFirstPlatformCollider()
    {
        firstPlatformCollider = FindFirstPlatformCollider();
    }

    private WallState CreateWallState(string wallObjectName)
    {
        GameObject wallObject = GameObject.Find(wallObjectName);
        if (wallObject == null || !wallObject.TryGetComponent(out Collider2D collider))
        {
            return default;
        }

        Transform wallTransform = wallObject.transform;
        return new WallState(
            wallTransform,
            collider,
            wallTransform.position,
            wallTransform.localScale,
            collider.bounds.size.y,
            collider.bounds.min.y);
    }

    private void UpdateWall(ref WallState wall, float removedDistance)
    {
        if (!wall.IsValid)
        {
            return;
        }

        float targetHeight = Mathf.Max(0.1f, wall.InitialHeight - removedDistance);
        float scaleRatio = wall.InitialHeight > 0f ? targetHeight / wall.InitialHeight : 1f;

        Vector3 nextScale = wall.InitialScale;
        nextScale.y = wall.InitialScale.y * scaleRatio;
        wall.Transform.localScale = nextScale;

        Vector3 nextPosition = wall.InitialPosition;
        nextPosition.y = wall.BottomY + targetHeight * 0.5f;
        wall.Transform.position = nextPosition;
    }

    private void CollapseBlockAt(int index)
    {
        if (index < 0 || index >= collapseBlocks.Count)
        {
            return;
        }

        CollapseBlock block = collapseBlocks[index];
        if (block.GameObject == null || !block.GameObject.activeSelf)
        {
            return;
        }

        SpawnShards(block);
        block.GameObject.SetActive(false);
    }

    private PlayerHealth FindPlayerHealth()
    {
        GameObject playerObject = GameObject.Find(PlayerObjectName);
        if (playerObject != null)
        {
            PlayerHealth playerHealth = playerObject.GetComponentInChildren<PlayerHealth>(true);
            if (playerHealth != null)
            {
                return playerHealth;
            }
        }

        return Object.FindAnyObjectByType<PlayerHealth>();
    }

    private Collider2D FindFirstPlatformCollider()
    {
        Transform firstPlatformTransform = transform.Find(BaseBlockObjectName);
        if (firstPlatformTransform != null && firstPlatformTransform.TryGetComponent(out Collider2D collider))
        {
            return collider;
        }

        GameObject firstPlatformObject = GameObject.Find(BaseBlockObjectName);
        if (firstPlatformObject != null && firstPlatformObject.TryGetComponent(out Collider2D fallbackCollider))
        {
            return fallbackCollider;
        }

        return null;
    }

    private void SpawnShards(CollapseBlock block)
    {
        Bounds bounds = block.Renderer.bounds;
        if (bounds.size.x < minimumShardBlockWidth || bounds.size.y <= CollapseEpsilon)
        {
            return;
        }

        int columns = Mathf.Clamp(Mathf.CeilToInt(bounds.size.x / targetShardWidth), 1, maxShardColumns);
        int rows = Mathf.Clamp(Mathf.CeilToInt(bounds.size.y / targetShardHeight), 1, maxShardRows);
        float cellWidth = bounds.size.x / columns;
        float pieceWidth = cellWidth * 0.5f;
        float pieceHeight = bounds.size.y / rows;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column += 2)
            {
                Vector3 shardPosition = new Vector3(
                    bounds.min.x + ((column + 0.5f) * cellWidth),
                    bounds.min.y + ((row + 0.5f) * pieceHeight),
                    block.Transform.position.z);

                CreateShard(block, shardPosition, pieceWidth, pieceHeight);
            }
        }
    }

    private void CreateShard(CollapseBlock block, Vector3 position, float width, float height)
    {
        if (playerColliders.Count == 0)
        {
            RefreshPlayerColliders();
        }

        GameObject shardObject = new GameObject(block.GameObject.name + "_Shard");
        shardObject.transform.position = position;
        shardObject.transform.rotation = block.Transform.rotation;
        shardObject.transform.localScale = new Vector3(width, height, 1f);

        shardObject.layer = block.GameObject.layer;

        SpriteRenderer shardRenderer = shardObject.AddComponent<SpriteRenderer>();
        shardRenderer.sprite = block.Renderer.sprite;
        shardRenderer.color = block.Renderer.color;
        shardRenderer.sortingLayerID = block.Renderer.sortingLayerID;
        shardRenderer.sortingOrder = block.Renderer.sortingOrder;
        shardRenderer.flipX = block.Renderer.flipX;
        shardRenderer.flipY = block.Renderer.flipY;

        BoxCollider2D shardCollider = shardObject.AddComponent<BoxCollider2D>();
        Rigidbody2D shardBody = shardObject.AddComponent<Rigidbody2D>();
        shardBody.gravityScale = shardGravityScale;
        shardBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        shardBody.linearVelocity = new Vector2(
            Random.Range(-shardHorizontalSpeed, shardHorizontalSpeed),
            Random.Range(0f, shardUpwardSpeed));
        shardBody.angularVelocity = Random.Range(-180f, 180f);

        for (int i = 0; i < playerColliders.Count; i++)
        {
            Collider2D playerCollider = playerColliders[i];
            if (playerCollider != null)
            {
                Physics2D.IgnoreCollision(shardCollider, playerCollider, true);
            }
        }

        ShardFirstPlatformImpact shardImpact = shardObject.AddComponent<ShardFirstPlatformImpact>();
        shardImpact.Initialize(firstPlatformCollider, playerHealth, BaseBlockObjectName);

        Destroy(shardObject, shardLifetime);
    }

    private readonly struct WallState
    {
        public WallState(Transform transform, Collider2D collider, Vector3 initialPosition, Vector3 initialScale, float initialHeight, float bottomY)
        {
            Transform = transform;
            Collider = collider;
            InitialPosition = initialPosition;
            InitialScale = initialScale;
            InitialHeight = initialHeight;
            BottomY = bottomY;
        }

        public Transform Transform { get; }
        public Collider2D Collider { get; }
        public Vector3 InitialPosition { get; }
        public Vector3 InitialScale { get; }
        public float InitialHeight { get; }
        public float BottomY { get; }
        public bool IsValid => Transform != null && Collider != null;
    }

    private sealed class CollapseBlock
    {
        public CollapseBlock(GameObject gameObject, SpriteRenderer renderer, Collider2D collider)
        {
            GameObject = gameObject;
            Transform = gameObject.transform;
            Renderer = renderer;
            Collider = collider;
            TopY = renderer.bounds.max.y;
        }

        public GameObject GameObject { get; }
        public Transform Transform { get; }
        public SpriteRenderer Renderer { get; }
        public Collider2D Collider { get; }
        public float TopY { get; }
    }
}

[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
public class ShardFirstPlatformImpact : MonoBehaviour
{
    private Collider2D firstPlatformCollider;
    private PlayerHealth playerHealth;
    private string targetObjectName;
    private bool hasTriggered;

    public void Initialize(Collider2D platformCollider, PlayerHealth playerHealth, string targetObjectName)
    {
        firstPlatformCollider = platformCollider;
        this.playerHealth = playerHealth;
        this.targetObjectName = targetObjectName;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasTriggered || !IsTargetCollision(collision))
        {
            return;
        }

        if (playerHealth == null)
        {
            playerHealth = Object.FindAnyObjectByType<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            return;
        }

        hasTriggered = true;
        playerHealth.SetHealthToZero();
    }

    private bool IsTargetCollision(Collision2D collision)
    {
        if (collision.collider == null)
        {
            return false;
        }

        if (firstPlatformCollider != null)
        {
            return collision.collider == firstPlatformCollider;
        }

        return !string.IsNullOrEmpty(targetObjectName) && collision.collider.gameObject.name == targetObjectName;
    }
}
