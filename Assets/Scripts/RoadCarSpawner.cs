using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-20)]
public class RoadCarSpawner : MonoBehaviour
{
    private const string TargetSceneName = "Road";
    private const string GroundObjectName = "Ground";
    private const string PlayerObjectName = "Player";
    private const string CarResourcesPath = "RoadCars";

#if UNITY_EDITOR
    private const string EditorCarAssetFolder = "Assets/Sprites";
#endif

    [SerializeField, Min(0.2f)] private float minSpawnInterval = 1.1f;
    [SerializeField, Min(0.2f)] private float maxSpawnInterval = 2.1f;
    [SerializeField, Min(0.5f)] private float minCarSpeed = 6f;
    [SerializeField, Min(0.5f)] private float maxCarSpeed = 9.5f;
    [SerializeField, Min(0f)] private float spawnPadding = 1.25f;
    [SerializeField, Min(0f)] private float verticalPadding = 1.25f;
    [SerializeField, Min(1)] private int laneCount = 8;
    [SerializeField, Min(0f)] private float laneFollowGap = 1.1f;
    [SerializeField, Min(0f)] private float spawnLaneGap = 1.6f;
    [SerializeField, Min(0.1f)] private float carScale = 2.5f;
    [SerializeField, Range(0.1f, 1f)] private float colliderWidthFactor = 0.68f;
    [SerializeField, Range(0.1f, 1f)] private float colliderHeightFactor = 0.12f;
    [SerializeField, Range(-0.5f, 0.5f)] private float colliderVerticalOffsetFactor = -10f;
    [SerializeField, Min(1)] private int carDamage = 1;
    [SerializeField] private int sortingOrder = 1;
    [SerializeField, Min(1f)] private float pixelsPerUnit = 100f;

    private readonly List<Sprite> carSprites = new List<Sprite>();
    private readonly List<List<RoadCar>> carsByLane = new List<List<RoadCar>>();

    private Collider2D groundCollider;
    private float nextSpawnTime;
    private float carZPosition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSpawnerOnActiveScene()
    {
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != TargetSceneName || Object.FindAnyObjectByType<RoadCarSpawner>() != null)
        {
            return;
        }

        GameObject groundObject = GameObject.Find(GroundObjectName);
        if (groundObject != null)
        {
            groundObject.AddComponent<RoadCarSpawner>();
        }
    }

    private void Awake()
    {
        TryAssignGroundCollider();
        CacheCarZPosition();
        EnsurePlayerHealthOnPlayer();
        LoadCarSprites();
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != TargetSceneName || groundCollider == null || carSprites.Count == 0)
        {
            enabled = false;
            return;
        }

        EnsureLaneRegistry();
        ScheduleNextSpawn();
    }

    private void Update()
    {
        if (Time.time < nextSpawnTime)
        {
            return;
        }

        SpawnCar();
        ScheduleNextSpawn();
    }

    private void TryAssignGroundCollider()
    {
        groundCollider = GetComponent<Collider2D>();
        if (groundCollider != null)
        {
            return;
        }

        GameObject groundObject = GameObject.Find(GroundObjectName);
        if (groundObject != null)
        {
            groundCollider = groundObject.GetComponent<Collider2D>();
        }
    }

    private void CacheCarZPosition()
    {
        GameObject playerObject = GameObject.Find(PlayerObjectName);
        carZPosition = playerObject != null ? playerObject.transform.position.z : 0f;
    }

    private void EnsurePlayerHealthOnPlayer()
    {
        GameObject playerObject = GameObject.Find(PlayerObjectName);
        if (playerObject == null || playerObject.GetComponent<PlayerHealth>() != null)
        {
            return;
        }

        playerObject.AddComponent<PlayerHealth>();
    }

    private void LoadCarSprites()
    {
        carSprites.Clear();

        Texture2D[] resourceTextures = Resources.LoadAll<Texture2D>(CarResourcesPath);
        AddTexturesAsSprites(resourceTextures);

#if UNITY_EDITOR
        if (carSprites.Count == 0)
        {
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { EditorCarAssetFolder });
            for (int i = 0; i < textureGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                AddTextureAsSprite(texture);
            }
        }
#endif

        carSprites.Sort((left, right) => string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty));
    }

    private void AddTexturesAsSprites(Texture2D[] textures)
    {
        if (textures == null)
        {
            return;
        }

        for (int i = 0; i < textures.Length; i++)
        {
            AddTextureAsSprite(textures[i]);
        }
    }

    private void AddTextureAsSprite(Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        for (int i = 0; i < carSprites.Count; i++)
        {
            if (carSprites[i] != null && carSprites[i].name == texture.name)
            {
                return;
            }
        }

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit);
        sprite.name = texture.name;
        carSprites.Add(sprite);
    }

    private void ScheduleNextSpawn()
    {
        float minInterval = Mathf.Max(0.2f, minSpawnInterval);
        float maxInterval = Mathf.Max(minInterval, maxSpawnInterval);
        nextSpawnTime = Time.time + Random.Range(minInterval, maxInterval);
    }

    private void SpawnCar()
    {
        if (groundCollider == null || carSprites.Count == 0)
        {
            return;
        }

        Sprite sprite = carSprites[Random.Range(0, carSprites.Count)];
        if (sprite == null)
        {
            return;
        }

        Bounds groundBounds = groundCollider.bounds;
        Vector2 localSpriteSize = sprite.bounds.size;
        float halfWidth = localSpriteSize.x * carScale * 0.5f;
        float halfHeight = localSpriteSize.y * carScale * 0.5f;

        float minY = groundBounds.min.y + verticalPadding + halfHeight;
        float maxY = groundBounds.max.y - verticalPadding - halfHeight;
        float spawnX = groundBounds.max.x + spawnPadding + halfWidth;
        if (!TrySelectSpawnLane(spawnX, halfWidth, out int laneIndex))
        {
            return;
        }

        float spawnY = GetLaneCenterY(laneIndex, minY, maxY);
        float moveSpeed = Random.Range(minCarSpeed, maxCarSpeed);

        GameObject carObject = new GameObject("RoadCar");
        carObject.transform.position = new Vector3(spawnX, spawnY, carZPosition);
        carObject.transform.localScale = new Vector3(carScale, carScale, 1f);

        SpriteRenderer spriteRenderer = carObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.flipX = true;

        BoxCollider2D collider = carObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        float colliderWidth = Mathf.Max(0.1f, localSpriteSize.x * colliderWidthFactor);
        float colliderHeight = Mathf.Max(0.1f, localSpriteSize.y * colliderHeightFactor);
        collider.size = new Vector2(colliderWidth, colliderHeight);
        collider.offset = new Vector2(0f, localSpriteSize.y * colliderVerticalOffsetFactor);

        Rigidbody2D rigidbody2D = carObject.AddComponent<Rigidbody2D>();
        rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
        rigidbody2D.interpolation = RigidbodyInterpolation2D.Interpolate;
        rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rigidbody2D.freezeRotation = true;

        RoadCar roadCar = carObject.AddComponent<RoadCar>();
        roadCar.Initialize(
            this,
            laneIndex,
            rigidbody2D,
            moveSpeed,
            groundBounds.min.x,
            laneFollowGap,
            carDamage);
    }

    public void RegisterCar(RoadCar roadCar, int laneIndex)
    {
        if (roadCar == null)
        {
            return;
        }

        if (!TryGetLaneCars(laneIndex, out List<RoadCar> laneCars))
        {
            return;
        }

        PruneLaneCars(laneCars);
        if (!laneCars.Contains(roadCar))
        {
            laneCars.Add(roadCar);
        }
    }

    public void UnregisterCar(RoadCar roadCar, int laneIndex)
    {
        if (roadCar == null || !TryGetLaneCars(laneIndex, out List<RoadCar> laneCars))
        {
            return;
        }

        laneCars.Remove(roadCar);
    }

    public RoadCar GetFrontCar(RoadCar requester, int laneIndex)
    {
        if (requester == null || !TryGetLaneCars(laneIndex, out List<RoadCar> laneCars))
        {
            return null;
        }

        RoadCar nearestFrontCar = null;
        float nearestFrontX = float.NegativeInfinity;

        for (int i = laneCars.Count - 1; i >= 0; i--)
        {
            RoadCar candidate = laneCars[i];
            if (candidate == null || !candidate.IsActiveOnRoad)
            {
                laneCars.RemoveAt(i);
                continue;
            }

            if (candidate == requester || candidate.transform.position.x >= requester.transform.position.x)
            {
                continue;
            }

            if (candidate.transform.position.x > nearestFrontX)
            {
                nearestFrontX = candidate.transform.position.x;
                nearestFrontCar = candidate;
            }
        }

        return nearestFrontCar;
    }

    private void EnsureLaneRegistry()
    {
        int totalLanes = Mathf.Max(1, laneCount);

        while (carsByLane.Count < totalLanes)
        {
            carsByLane.Add(new List<RoadCar>());
        }

        while (carsByLane.Count > totalLanes)
        {
            carsByLane.RemoveAt(carsByLane.Count - 1);
        }
    }

    private bool TrySelectSpawnLane(float spawnX, float halfWidth, out int laneIndex)
    {
        EnsureLaneRegistry();

        int totalLanes = carsByLane.Count;
        int startIndex = Random.Range(0, totalLanes);
        float spawnFrontEdge = spawnX - halfWidth;

        for (int offset = 0; offset < totalLanes; offset++)
        {
            int candidateLaneIndex = (startIndex + offset) % totalLanes;
            RoadCar rearMostCar = GetRearMostCar(candidateLaneIndex);

            if (rearMostCar == null || spawnFrontEdge - rearMostCar.RearEdgeX >= spawnLaneGap)
            {
                laneIndex = candidateLaneIndex;
                return true;
            }
        }

        laneIndex = 0;
        return false;
    }

    private RoadCar GetRearMostCar(int laneIndex)
    {
        if (laneIndex < 0 || laneIndex >= carsByLane.Count)
        {
            return null;
        }

        List<RoadCar> laneCars = carsByLane[laneIndex];
        PruneLaneCars(laneCars);
        RoadCar rearMostCar = null;
        float rearMostX = float.NegativeInfinity;

        for (int i = laneCars.Count - 1; i >= 0; i--)
        {
            RoadCar candidate = laneCars[i];
            if (candidate.transform.position.x > rearMostX)
            {
                rearMostX = candidate.transform.position.x;
                rearMostCar = candidate;
            }
        }

        return rearMostCar;
    }

    private float GetLaneCenterY(int laneIndex, float minY, float maxY)
    {
        if (minY >= maxY)
        {
            return (minY + maxY) * 0.5f;
        }

        int totalLanes = Mathf.Max(1, laneCount);
        float laneHeight = (maxY - minY) / totalLanes;
        int clampedLaneIndex = Mathf.Clamp(laneIndex, 0, totalLanes - 1);

        return minY + (laneHeight * (clampedLaneIndex + 0.5f));
    }

    private bool TryGetLaneCars(int laneIndex, out List<RoadCar> laneCars)
    {
        EnsureLaneRegistry();

        if (laneIndex < 0 || laneIndex >= carsByLane.Count)
        {
            laneCars = null;
            return false;
        }

        laneCars = carsByLane[laneIndex];
        return true;
    }

    private static void PruneLaneCars(List<RoadCar> laneCars)
    {
        laneCars.RemoveAll(car => car == null || !car.IsActiveOnRoad);
    }
}

[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
public class RoadCar : MonoBehaviour
{
    private RoadCarSpawner owner;
    private Rigidbody2D cachedBody;
    private float halfWidth;
    private float roadLeftEdgeX;
    private float baseMoveSpeed;
    private float followGap;
    private int damage = 1;
    private int laneIndex = -1;

    public bool IsActiveOnRoad => gameObject.activeInHierarchy;
    public float RearEdgeX => transform.position.x + halfWidth;
    public float FrontEdgeX => transform.position.x - halfWidth;
    public float CurrentRoadSpeed => cachedBody == null ? 0f : Mathf.Max(0f, -cachedBody.linearVelocity.x);

    public void Initialize(
        RoadCarSpawner spawner,
        int assignedLaneIndex,
        Rigidbody2D body,
        float moveSpeed,
        float roadLeftBoundaryX,
        float followDistance,
        int damageAmount)
    {
        owner = spawner;
        laneIndex = assignedLaneIndex;
        cachedBody = body;
        roadLeftEdgeX = roadLeftBoundaryX;
        baseMoveSpeed = Mathf.Max(0.5f, moveSpeed);
        followGap = Mathf.Max(0f, followDistance);
        damage = Mathf.Max(1, damageAmount);

        if (TryGetComponent(out SpriteRenderer spriteRenderer))
        {
            halfWidth = spriteRenderer.bounds.extents.x;
        }

        owner?.RegisterCar(this, laneIndex);
        cachedBody.linearVelocity = Vector2.left * baseMoveSpeed;
    }

    private void Update()
    {
        UpdateRoadSpeed();

        if (transform.position.x - halfWidth <= roadLeftEdgeX - 0.01f)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            playerHealth = other.GetComponentInParent<PlayerHealth>();
        }

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
        }
    }

    private void OnDestroy()
    {
        owner?.UnregisterCar(this, laneIndex);
    }

    private void UpdateRoadSpeed()
    {
        if (cachedBody == null)
        {
            return;
        }

        float targetSpeed = baseMoveSpeed;
        RoadCar frontCar = owner != null ? owner.GetFrontCar(this, laneIndex) : null;

        if (frontCar != null)
        {
            float gap = FrontEdgeX - frontCar.RearEdgeX;
            float fixedDeltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            float maxAllowedSpeed = frontCar.CurrentRoadSpeed + Mathf.Max(0f, gap - followGap) / fixedDeltaTime;
            targetSpeed = Mathf.Min(targetSpeed, Mathf.Max(0f, maxAllowedSpeed));
        }

        cachedBody.linearVelocity = new Vector2(-targetSpeed, 0f);
    }
}
