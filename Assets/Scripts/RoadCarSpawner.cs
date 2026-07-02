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
    private const string CollisionAudioResourcesPath = "Audios/K드라마 효과음 (1342)";
    private const string CollisionAudioSourceObjectName = "RoadCarImpactAudioSource";
    private const int SecondFromBottomLaneIndex = 1;
    private const int SecondFromBottomLaneSortingOrderOffset = 2;
    private const float LowerLaneYOffsetPercent = 0.25f;
    private const float BottomLaneExtraYOffsetPercent = 0.10f;
    private const float UpperLaneYOffsetPercent = 0.20f;
    private static readonly float[] FourLaneFineTuneYOffsetPercents = { -0.20f, -0.13f, -0.11f, -0.14f };

#if UNITY_EDITOR
    private const string EditorCarAssetFolder = "Assets/Sprites";
#endif

    [SerializeField, Min(0.2f)] private float minSpawnInterval = 1.1f;
    [SerializeField, Min(0.2f)] private float maxSpawnInterval = 2.1f;
    [SerializeField, Min(0.5f)] private float minCarSpeed = 6f;
    [SerializeField, Min(0.5f)] private float maxCarSpeed = 9.5f;
    [SerializeField, Min(0f)] private float spawnPadding = 1.25f;
    [SerializeField, Min(0f)] private float verticalPadding = 1.25f;
    [SerializeField, Min(1)] private int laneCount = 4;
    [SerializeField, Min(0f)] private float laneFollowGap = 1.1f;
    [SerializeField, Min(0f)] private float spawnLaneGap = 1.6f;
    [SerializeField, Min(0.1f)] private float carScale = 3.0f;
    [SerializeField, Range(0.1f, 1f)] private float colliderWidthFactor = 0.68f;
    [SerializeField, Range(0.1f, 1f)] private float colliderHeightFactor = 0.12f;
    [SerializeField, Range(-0.5f, 0.5f)] private float colliderVerticalOffsetFactor = -10f;
    [SerializeField, Min(1)] private int carDamage = 1;
    [SerializeField] private int sortingOrder = 1;
    [SerializeField, Min(1f)] private float pixelsPerUnit = 100f;
    [SerializeField, Range(0f, 1f)] private float collisionSoundVolume = 1f;

    private readonly List<Sprite> carSprites = new List<Sprite>();
    private readonly List<List<RoadCar>> carsByLane = new List<List<RoadCar>>();

    private Collider2D groundCollider;
    private AudioClip collisionSoundClip;
    private AudioSource collisionSoundSource;
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
        LoadCollisionSound();
        EnsureCollisionAudioSource();
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
        if (GamePauseState.IsPaused)
        {
            return;
        }

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

    private void LoadCollisionSound()
    {
        collisionSoundClip = Resources.Load<AudioClip>(CollisionAudioResourcesPath);
        if (collisionSoundClip == null)
        {
            Debug.LogWarning($"Could not load road collision audio clip at Resources path '{CollisionAudioResourcesPath}'.", this);
        }
    }

    private void EnsureCollisionAudioSource()
    {
        if (collisionSoundSource != null)
        {
            return;
        }

        Transform existingAudioSourceTransform = transform.Find(CollisionAudioSourceObjectName);
        if (existingAudioSourceTransform != null && existingAudioSourceTransform.TryGetComponent(out AudioSource existingAudioSource))
        {
            collisionSoundSource = existingAudioSource;
            ConfigureCollisionAudioSource(collisionSoundSource);
            return;
        }

        GameObject audioSourceObject = new GameObject(CollisionAudioSourceObjectName, typeof(AudioSource));
        audioSourceObject.transform.SetParent(transform, false);
        collisionSoundSource = audioSourceObject.GetComponent<AudioSource>();
        ConfigureCollisionAudioSource(collisionSoundSource);
    }

    private static void ConfigureCollisionAudioSource(AudioSource audioSource)
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    public void PlayCollisionSound()
    {
        if (collisionSoundClip == null || collisionSoundSource == null)
        {
            return;
        }

        float volumeScale = Mathf.Clamp01(collisionSoundVolume) * AudioSettingsStore.SoundEffectVolume;
        collisionSoundSource.PlayOneShot(collisionSoundClip, volumeScale);
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
        if (!TrySelectSpawnLane(groundBounds, halfWidth, out int laneIndex, out bool movesRight))
        {
            return;
        }

        float spawnX = GetSpawnX(groundBounds, halfWidth, movesRight);
        float spawnY = GetLaneCenterY(laneIndex, minY, maxY);
        float moveSpeed = Random.Range(minCarSpeed, maxCarSpeed);

        GameObject carObject = new GameObject("RoadCar");
        carObject.transform.position = new Vector3(spawnX, spawnY, carZPosition);
        carObject.transform.localScale = new Vector3(carScale, carScale, 1f);

        SpriteRenderer spriteRenderer = carObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = GetSortingOrderForLane(laneIndex);
        spriteRenderer.flipX = !movesRight;

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
            groundBounds.max.x,
            laneFollowGap,
            carDamage,
            movesRight);
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

        bool movesRight = IsLaneRightMoving(laneIndex);
        RoadCar nearestFrontCar = null;
        float nearestFrontX = movesRight ? float.PositiveInfinity : float.NegativeInfinity;

        for (int i = laneCars.Count - 1; i >= 0; i--)
        {
            RoadCar candidate = laneCars[i];
            if (candidate == null || !candidate.IsActiveOnRoad)
            {
                laneCars.RemoveAt(i);
                continue;
            }

            float candidateX = candidate.transform.position.x;
            float requesterX = requester.transform.position.x;
            if (candidate == requester)
            {
                continue;
            }

            if (movesRight)
            {
                if (candidateX <= requesterX || candidateX >= nearestFrontX)
                {
                    continue;
                }
            }
            else if (candidateX >= requesterX || candidateX <= nearestFrontX)
            {
                continue;
            }

            nearestFrontX = candidateX;
            nearestFrontCar = candidate;
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

    private bool TrySelectSpawnLane(Bounds groundBounds, float halfWidth, out int laneIndex, out bool movesRight)
    {
        EnsureLaneRegistry();

        int totalLanes = carsByLane.Count;
        int startIndex = Random.Range(0, totalLanes);

        for (int offset = 0; offset < totalLanes; offset++)
        {
            int candidateLaneIndex = (startIndex + offset) % totalLanes;
            bool candidateMovesRight = IsLaneRightMoving(candidateLaneIndex);
            float candidateSpawnX = GetSpawnX(groundBounds, halfWidth, candidateMovesRight);
            RoadCar boundaryCar = GetBoundaryCarClosestToSpawn(candidateLaneIndex, candidateMovesRight);

            if (boundaryCar == null || HasSpawnGap(candidateSpawnX, halfWidth, candidateMovesRight, boundaryCar))
            {
                laneIndex = candidateLaneIndex;
                movesRight = candidateMovesRight;
                return true;
            }
        }

        laneIndex = 0;
        movesRight = false;
        return false;
    }

    private RoadCar GetBoundaryCarClosestToSpawn(int laneIndex, bool movesRight)
    {
        if (laneIndex < 0 || laneIndex >= carsByLane.Count)
        {
            return null;
        }

        List<RoadCar> laneCars = carsByLane[laneIndex];
        PruneLaneCars(laneCars);
        RoadCar boundaryCar = null;
        float boundaryX = movesRight ? float.PositiveInfinity : float.NegativeInfinity;

        for (int i = laneCars.Count - 1; i >= 0; i--)
        {
            RoadCar candidate = laneCars[i];
            if (candidate == null || !candidate.IsActiveOnRoad)
            {
                continue;
            }

            float candidateX = candidate.transform.position.x;
            if (movesRight)
            {
                if (candidateX < boundaryX)
                {
                    boundaryX = candidateX;
                    boundaryCar = candidate;
                }
            }
            else if (candidateX > boundaryX)
            {
                boundaryX = candidateX;
                boundaryCar = candidate;
            }
        }

        return boundaryCar;
    }

    private bool HasSpawnGap(float spawnX, float halfWidth, bool movesRight, RoadCar boundaryCar)
    {
        float spawnFrontEdge = GetFrontEdgeX(spawnX, halfWidth, movesRight);
        float boundaryRearEdge = boundaryCar.RearEdgeX;

        if (movesRight)
        {
            return boundaryRearEdge - spawnFrontEdge >= spawnLaneGap;
        }

        return spawnFrontEdge - boundaryRearEdge >= spawnLaneGap;
    }

    private float GetSpawnX(Bounds groundBounds, float halfWidth, bool movesRight)
    {
        return movesRight
            ? groundBounds.min.x - spawnPadding - halfWidth
            : groundBounds.max.x + spawnPadding + halfWidth;
    }

    private static float GetFrontEdgeX(float centerX, float halfWidth, bool movesRight)
    {
        return movesRight ? centerX + halfWidth : centerX - halfWidth;
    }

    private bool IsLaneRightMoving(int laneIndex)
    {
        int totalLanes = Mathf.Max(1, laneCount);
        return laneIndex >= totalLanes / 2;
    }

    private int GetSortingOrderForLane(int laneIndex)
    {
        int laneSortingOrder = sortingOrder;
        if (laneIndex == SecondFromBottomLaneIndex)
        {
            laneSortingOrder += SecondFromBottomLaneSortingOrderOffset;
        }

        return laneSortingOrder;
    }

    private static float GetFineTuneLaneYOffsetPercent(int laneIndex, int totalLanes)
    {
        int clampedLaneIndex = Mathf.Clamp(laneIndex, 0, Mathf.Max(0, totalLanes - 1));

        if (totalLanes == FourLaneFineTuneYOffsetPercents.Length)
        {
            return FourLaneFineTuneYOffsetPercents[clampedLaneIndex];
        }

        if (clampedLaneIndex == 0)
        {
            return FourLaneFineTuneYOffsetPercents[0];
        }

        if (clampedLaneIndex == totalLanes - 1)
        {
            return FourLaneFineTuneYOffsetPercents[3];
        }

        if (clampedLaneIndex >= totalLanes - 2)
        {
            return FourLaneFineTuneYOffsetPercents[2];
        }

        return FourLaneFineTuneYOffsetPercents[1];
    }

    private float GetLaneCenterY(int laneIndex, float minY, float maxY)
    {
        if (minY >= maxY)
        {
            return (minY + maxY) * 0.5f;
        }

        int totalLanes = Mathf.Max(1, laneCount);
        float laneRange = maxY - minY;
        float laneHeight = laneRange / totalLanes;
        int clampedLaneIndex = Mathf.Clamp(laneIndex, 0, totalLanes - 1);
        float laneCenterY = minY + (laneHeight * (clampedLaneIndex + 0.5f));

        if (IsLaneRightMoving(clampedLaneIndex))
        {
            laneCenterY += laneRange * UpperLaneYOffsetPercent;
        }
        else
        {
            laneCenterY += laneRange * LowerLaneYOffsetPercent;

            if (clampedLaneIndex == 0)
            {
                laneCenterY += laneRange * BottomLaneExtraYOffsetPercent;
            }
        }

        laneCenterY += laneRange * GetFineTuneLaneYOffsetPercent(clampedLaneIndex, totalLanes);

        return Mathf.Clamp(laneCenterY, minY, maxY);
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
    private float roadRightEdgeX;
    private float baseMoveSpeed;
    private float followGap;
    private int damage = 1;
    private int laneIndex = -1;
    private bool movesRight;

    public bool IsActiveOnRoad => gameObject.activeInHierarchy;
    public float RearEdgeX => movesRight ? transform.position.x - halfWidth : transform.position.x + halfWidth;
    public float FrontEdgeX => movesRight ? transform.position.x + halfWidth : transform.position.x - halfWidth;
    public float CurrentRoadSpeed => cachedBody == null ? 0f : Mathf.Abs(cachedBody.linearVelocity.x);

    public void Initialize(
        RoadCarSpawner spawner,
        int assignedLaneIndex,
        Rigidbody2D body,
        float moveSpeed,
        float roadLeftBoundaryX,
        float roadRightBoundaryX,
        float followDistance,
        int damageAmount,
        bool laneMovesRight)
    {
        owner = spawner;
        laneIndex = assignedLaneIndex;
        cachedBody = body;
        roadLeftEdgeX = roadLeftBoundaryX;
        roadRightEdgeX = roadRightBoundaryX;
        baseMoveSpeed = Mathf.Max(0.5f, moveSpeed);
        followGap = Mathf.Max(0f, followDistance);
        damage = Mathf.Max(1, damageAmount);
        movesRight = laneMovesRight;

        if (TryGetComponent(out SpriteRenderer spriteRenderer))
        {
            halfWidth = spriteRenderer.bounds.extents.x;
        }

        owner?.RegisterCar(this, laneIndex);
        cachedBody.linearVelocity = new Vector2(movesRight ? baseMoveSpeed : -baseMoveSpeed, 0f);
    }

    private void Update()
    {
        if (GamePauseState.IsPaused)
        {
            if (cachedBody != null)
            {
                cachedBody.linearVelocity = Vector2.zero;
            }

            return;
        }

        UpdateRoadSpeed();

        if (movesRight)
        {
            if (transform.position.x + halfWidth >= roadRightEdgeX + 0.01f)
            {
                Destroy(gameObject);
            }
        }
        else if (transform.position.x - halfWidth <= roadLeftEdgeX - 0.01f)
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
            if (playerHealth.TakeDamage(damage))
            {
                owner?.PlayCollisionSound();
            }
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
            float gap = movesRight
                ? frontCar.RearEdgeX - FrontEdgeX
                : FrontEdgeX - frontCar.RearEdgeX;
            float fixedDeltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            float maxAllowedSpeed = frontCar.CurrentRoadSpeed + Mathf.Max(0f, gap - followGap) / fixedDeltaTime;
            targetSpeed = Mathf.Min(targetSpeed, Mathf.Max(0f, maxAllowedSpeed));
        }

        cachedBody.linearVelocity = new Vector2(movesRight ? targetSpeed : -targetSpeed, 0f);
    }
}
