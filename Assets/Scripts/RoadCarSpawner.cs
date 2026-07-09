using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Splines;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-20)]
public class RoadCarSpawner : MonoBehaviour
{
    private const string TargetSceneName = "Road";
    private const string GroundObjectName = "Ground";
    private const string RoadObjectName = "Road";
    private const string PlayerObjectName = "Player";
    private const string CarResourcesPath = "RoadCars";
    private const string CollisionAudioResourcesPath = "Audios/K드라마 효과음 (1342)";
    private const string CollisionAudioSourceObjectName = "RoadCarImpactAudioSource";
    private const int PlayerSortingOrderOffset = 1;
    private const int SecondFromBottomLaneIndex = 1;
    private const int SecondFromBottomLaneSortingOrderOffset = 5;
    private const float LaneSplineSampleT = 0.5f;
    private const float ReferenceCarTextureSize = 64f;
    private const float AnimationSourcePixelsPerUnit = 100f;

#if UNITY_EDITOR
    private const string EditorCarAssetFolder = "Assets/Sprites/Graphic Art/Map/Road/Cars";
    private const string EditorCarAnimationAssetFolder = "Assets/Animation/Road";
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
    [SerializeField] private Texture2D[] carTextureOverrides;
    [SerializeField] private AnimationClip[] carAnimationOverrides;

    private readonly List<Sprite> carSprites = new List<Sprite>();
    private readonly List<AnimationClip> carAnimations = new List<AnimationClip>();
    private readonly List<List<RoadCar>> carsByLane = new List<List<RoadCar>>();
    private readonly List<SplineContainer> laneSplines = new List<SplineContainer>();

    private Collider2D groundCollider;
    private AudioClip collisionSoundClip;
    private AudioSource collisionSoundSource;
    private SpriteRenderer playerSpriteRenderer;
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
        CachePlayerRendering();
        EnsurePlayerHealthOnPlayer();
        LoadCollisionSound();
        EnsureCollisionAudioSource();
        LoadCarSprites();
        LoadCarAnimations();
        CacheLaneSplines();
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

    private void CachePlayerRendering()
    {
        GameObject playerObject = GameObject.Find(PlayerObjectName);
        carZPosition = playerObject != null ? playerObject.transform.position.z : 0f;
        playerSpriteRenderer = playerObject != null
            ? playerObject.GetComponentInChildren<SpriteRenderer>()
            : null;
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

        if (carTextureOverrides != null && carTextureOverrides.Length > 0)
        {
            AddTexturesAsSprites(carTextureOverrides);
        }

        if (carSprites.Count == 0)
        {
            Texture2D[] resourceTextures = Resources.LoadAll<Texture2D>(CarResourcesPath);
            AddTexturesAsSprites(resourceTextures);
        }

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

        if (!HasAssignedCarAnimations())
        {
            carSprites.Sort((left, right) => string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty));
        }
    }

    private void LoadCarAnimations()
    {
        carAnimations.Clear();

        if (carAnimationOverrides == null)
        {
            return;
        }

        bool hasAssignedAnimation = false;
        for (int i = 0; i < carAnimationOverrides.Length; i++)
        {
            AnimationClip clip = carAnimationOverrides[i];
            carAnimations.Add(clip);
            hasAssignedAnimation |= clip != null;
        }

#if UNITY_EDITOR
        if (!hasAssignedAnimation)
        {
            TryLoadEditorCarAnimationClip("Wcar");
            TryLoadEditorCarAnimationClip("Bcar");
            TryLoadEditorCarAnimationClip("Bcar2");
            TryLoadEditorCarAnimationClip("Wcar2");
            TryLoadEditorCarAnimationClip("Gcar");
        }
#endif
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

        float normalizedPixelsPerUnit = AnimationSourcePixelsPerUnit;

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            normalizedPixelsPerUnit);
        sprite.name = texture.name;
        carSprites.Add(sprite);
    }

    private bool HasAssignedCarAnimations()
    {
        return carAnimationOverrides != null && carAnimationOverrides.Length > 0;
    }

    private float ComputeCarVisualScale(Texture2D texture)
    {
        if (texture == null)
        {
            return carScale;
        }

        float textureSize = Mathf.Max(texture.width, texture.height);
        if (textureSize <= 0f)
        {
            return carScale;
        }

        return carScale * ReferenceCarTextureSize * AnimationSourcePixelsPerUnit / (pixelsPerUnit * textureSize);
    }

#if UNITY_EDITOR
    private void TryLoadEditorCarAnimationClip(string animationName)
    {
        string[] animationGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { EditorCarAnimationAssetFolder });
        for (int i = 0; i < animationGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(animationGuids[i]);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip != null && clip.name == animationName)
            {
                carAnimations.Add(clip);
                return;
            }
        }
    }
#endif

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

        bool useAnimations = carAnimations.Count > 0;
        int carIndex = useAnimations
            ? Random.Range(0, Mathf.Min(carSprites.Count, carAnimations.Count))
            : Random.Range(0, carSprites.Count);

        Sprite sprite = carSprites[carIndex];
        if (sprite == null)
        {
            return;
        }

        AnimationClip animationClip = useAnimations && carIndex < carAnimations.Count
            ? carAnimations[carIndex]
            : null;
        float visualScale = ComputeCarVisualScale(sprite.texture);

        Bounds groundBounds = groundCollider.bounds;
        Vector2 localSpriteSize = sprite.bounds.size;
        float halfWidth = localSpriteSize.x * visualScale * 0.5f;
        float halfHeight = localSpriteSize.y * visualScale * 0.5f;

        float minY = groundBounds.min.y + verticalPadding + halfHeight;
        float maxY = groundBounds.max.y - verticalPadding - halfHeight;
        if (!TrySelectSpawnLane(groundBounds, halfWidth, out int laneIndex, out bool movesRight))
        {
            return;
        }

        float spawnX = GetSpawnX(groundBounds, halfWidth, movesRight);
        float spawnY = Mathf.Clamp(GetLaneCenterY(laneIndex, minY, maxY), minY, maxY);
        float moveSpeed = Random.Range(minCarSpeed, maxCarSpeed);

        GameObject carObject = new GameObject("RoadCar");
        carObject.transform.position = new Vector3(spawnX, spawnY, carZPosition);
        carObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        carObject.transform.localScale = new Vector3(visualScale, visualScale, 1f);

        SpriteRenderer spriteRenderer = carObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        ApplyPlayerSortingLayer(spriteRenderer);
        spriteRenderer.sortingOrder = GetSortingOrderForLane(laneIndex);
        spriteRenderer.flipX = !movesRight;

        if (animationClip != null)
        {
            RoadCarAnimationPlayer animationPlayer = carObject.AddComponent<RoadCarAnimationPlayer>();
            animationPlayer.Play(animationClip);
        }

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
        int totalLanes = GetTotalLaneCount();

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

    private void CacheLaneSplines()
    {
        laneSplines.Clear();

        GameObject roadObject = GameObject.Find(RoadObjectName);
        if (roadObject == null)
        {
            return;
        }

        Transform roadTransform = roadObject.transform;
        for (int i = 0; i < roadTransform.childCount; i++)
        {
            Transform child = roadTransform.GetChild(i);
            if (child != null && child.TryGetComponent(out SplineContainer laneSpline))
            {
                laneSplines.Add(laneSpline);
            }
        }

        laneSplines.Sort(CompareLaneSplinesByWorldY);
        if (laneSplines.Count > 0)
        {
            laneCount = laneSplines.Count;
        }
    }

    private bool IsLaneRightMoving(int laneIndex)
    {
        int totalLanes = GetTotalLaneCount();
        return laneIndex >= totalLanes / 2;
    }

    private int GetSortingOrderForLane(int laneIndex)
    {
        int laneSortingOrder = sortingOrder;
        if (playerSpriteRenderer != null)
        {
            laneSortingOrder = Mathf.Max(
                laneSortingOrder,
                playerSpriteRenderer.sortingOrder + PlayerSortingOrderOffset);
        }

        if (laneIndex == SecondFromBottomLaneIndex)
        {
            laneSortingOrder += SecondFromBottomLaneSortingOrderOffset;
        }

        return laneSortingOrder;
    }

    private void ApplyPlayerSortingLayer(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null || playerSpriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
    }

    private int GetTotalLaneCount()
    {
        if (laneSplines.Count == 0)
        {
            CacheLaneSplines();
        }

        return Mathf.Max(1, laneSplines.Count > 0 ? laneSplines.Count : laneCount);
    }

    private float GetLaneCenterY(int laneIndex, float minY, float maxY)
    {
        if (minY >= maxY)
        {
            return (minY + maxY) * 0.5f;
        }

        if (TryGetLaneWorldCenterY(laneIndex, out float laneCenterY))
        {
            return Mathf.Clamp(laneCenterY, minY, maxY);
        }

        int totalLanes = GetTotalLaneCount();
        int clampedLaneIndex = Mathf.Clamp(laneIndex, 0, totalLanes - 1);
        float laneHeight = (maxY - minY) / totalLanes;
        return minY + (laneHeight * (clampedLaneIndex + 0.5f));
    }

    private bool TryGetLaneWorldCenterY(int laneIndex, out float laneCenterY)
    {
        if (laneSplines.Count == 0)
        {
            CacheLaneSplines();
        }

        if (laneIndex < 0 || laneIndex >= laneSplines.Count)
        {
            laneCenterY = 0f;
            return false;
        }

        laneCenterY = GetLaneSplineWorldCenterY(laneSplines[laneIndex]);
        return true;
    }

    private static int CompareLaneSplinesByWorldY(SplineContainer left, SplineContainer right)
    {
        if (left == right)
        {
            return 0;
        }

        if (left == null)
        {
            return -1;
        }

        if (right == null)
        {
            return 1;
        }

        int yComparison = GetLaneSplineWorldCenterY(left).CompareTo(GetLaneSplineWorldCenterY(right));
        if (yComparison != 0)
        {
            return yComparison;
        }

        return left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex());
    }

    private static float GetLaneSplineWorldCenterY(SplineContainer laneSpline)
    {
        if (laneSpline == null)
        {
            return 0f;
        }

        if (laneSpline.CalculateLength() <= 0f)
        {
            return laneSpline.transform.position.y;
        }

        var lanePosition = laneSpline.EvaluatePosition(LaneSplineSampleT);
        return lanePosition.y;
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

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class RoadCarAnimationPlayer : MonoBehaviour
{
    private Animator animator;
    private PlayableGraph graph;
    private AnimationPlayableOutput output;
    private AnimationClipPlayable currentPlayable;
    private bool hasPlayable;

    public void Play(AnimationClip clip)
    {
        if (clip == null)
        {
            return;
        }

        EnsureAnimator();
        EnsureGraph();

        if (hasPlayable && currentPlayable.IsValid())
        {
            graph.DestroyPlayable(currentPlayable);
        }

        currentPlayable = AnimationClipPlayable.Create(graph, clip);
        currentPlayable.SetApplyFootIK(false);
        currentPlayable.SetTime(0d);
        currentPlayable.SetSpeed(GamePauseState.IsPaused ? 0d : 1d);
        output.SetSourcePlayable(currentPlayable);
        hasPlayable = true;

        if (!graph.IsPlaying())
        {
            graph.Play();
        }

        graph.Evaluate(0f);
    }

    private void EnsureAnimator()
    {
        if (animator != null)
        {
            return;
        }

        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = gameObject.AddComponent<Animator>();
        }

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.runtimeAnimatorController = null;
    }

    private void EnsureGraph()
    {
        if (graph.IsValid())
        {
            return;
        }

        graph = PlayableGraph.Create($"{nameof(RoadCarAnimationPlayer)}_{name}");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        output = AnimationPlayableOutput.Create(graph, "Animation", animator);
    }

    private void Update()
    {
        if (hasPlayable && currentPlayable.IsValid())
        {
            currentPlayable.SetSpeed(GamePauseState.IsPaused ? 0d : 1d);
        }
    }

    private void OnEnable()
    {
        if (graph.IsValid())
        {
            graph.Play();
        }
    }

    private void OnDisable()
    {
        if (graph.IsValid())
        {
            graph.Stop();
        }
    }

    private void OnDestroy()
    {
        if (graph.IsValid())
        {
            graph.Destroy();
        }
    }
}

[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
public class RoadCar : MonoBehaviour
{
    private const float ExhaustParticleLifetime = 0.55f;
    private const float ExhaustParticleSpawnInterval = 0.05f;
    private const float ExhaustParticleMinSpeed = 0.75f;
    private const float ExhaustParticleSpawnOffsetMultiplier = 0.92f;
    private const float ExhaustParticleBottomOffsetMultiplier = 0.55f;
    private const float ExhaustParticleSideScatter = 0.18f;
    private const float ExhaustParticleRearScatter = 0.12f;
    private const float ExhaustParticleDriftSpeed = 1.45f;
    private const int ExhaustParticleSpriteTextureSize = 48;
    private const float ExhaustParticleSpriteEdgeSoftness = 2f;

    private RoadCarSpawner owner;
    private Rigidbody2D cachedBody;
    private SpriteRenderer cachedSpriteRenderer;
    private float halfWidth;
    private float halfHeight;
    private float roadLeftEdgeX;
    private float roadRightEdgeX;
    private float baseMoveSpeed;
    private float followGap;
    private float nextExhaustParticleSpawnTime;
    private int damage = 1;
    private int laneIndex = -1;
    private bool movesRight;
    private static Sprite exhaustParticleSprite;

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
            cachedSpriteRenderer = spriteRenderer;
            halfWidth = spriteRenderer.bounds.extents.x;
            halfHeight = spriteRenderer.bounds.extents.y;
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
        UpdateExhaustTrail();

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

    private void UpdateExhaustTrail()
    {
        if (cachedBody == null
            || cachedSpriteRenderer == null
            || Time.time < nextExhaustParticleSpawnTime)
        {
            return;
        }

        Vector2 movementVector = cachedBody.linearVelocity;
        if (movementVector.sqrMagnitude < ExhaustParticleMinSpeed * ExhaustParticleMinSpeed)
        {
            return;
        }

        SpawnExhaustParticle(movementVector.normalized);
        nextExhaustParticleSpawnTime = Time.time + ExhaustParticleSpawnInterval;
    }

    private void SpawnExhaustParticle(Vector2 movementDirection)
    {
        Sprite particleSprite = GetExhaustParticleSprite();
        if (particleSprite == null)
        {
            return;
        }

        Vector2 backwardsDirection = movementDirection.sqrMagnitude > Mathf.Epsilon
            ? -movementDirection.normalized
            : Vector2.left;
        Vector2 lateralDirection = new Vector2(-backwardsDirection.y, backwardsDirection.x);
        Vector2 scatterOffset =
            (lateralDirection * Random.Range(-ExhaustParticleSideScatter, ExhaustParticleSideScatter))
            + (backwardsDirection * Random.Range(-ExhaustParticleRearScatter, ExhaustParticleRearScatter));
        Vector2 spawnPosition = (Vector2)transform.position
            + (backwardsDirection * Mathf.Max(0.32f, halfWidth * ExhaustParticleSpawnOffsetMultiplier))
            + (Vector2.down * Mathf.Max(0.18f, halfHeight * ExhaustParticleBottomOffsetMultiplier))
            + scatterOffset;
        float particleScale = Random.Range(0.48f, 0.84f);

        GameObject particleObject = new GameObject("RoadCarExhaustParticle", typeof(Transform), typeof(SpriteRenderer));
        particleObject.transform.position = new Vector3(spawnPosition.x, spawnPosition.y, transform.position.z + 0.01f);
        particleObject.transform.localScale = Vector3.one * particleScale;

        SpriteRenderer particleRenderer = particleObject.GetComponent<SpriteRenderer>();
        particleRenderer.sprite = particleSprite;
        particleRenderer.color = new Color(0.18f, 0.18f, 0.18f, Random.Range(0.45f, 0.72f));
        particleRenderer.sortingLayerID = cachedSpriteRenderer.sortingLayerID;
        particleRenderer.sortingOrder = cachedSpriteRenderer.sortingOrder;

        Vector2 driftDirection = (
            (backwardsDirection * Random.Range(0.45f, 0.95f))
            + (lateralDirection * Random.Range(-0.75f, 0.75f))).normalized;
        float driftSpeed = Random.Range(ExhaustParticleDriftSpeed * 0.8f, ExhaustParticleDriftSpeed * 1.25f);

        IEnumerator exhaustRoutine = AnimateExhaustParticle(
            particleObject.transform,
            particleRenderer,
            driftDirection * driftSpeed,
            particleScale);
        if (owner != null)
        {
            owner.StartCoroutine(exhaustRoutine);
        }
        else
        {
            StartCoroutine(exhaustRoutine);
        }
    }

    private IEnumerator AnimateExhaustParticle(
        Transform particleTransform,
        SpriteRenderer particleRenderer,
        Vector2 driftVelocity,
        float initialScale)
    {
        if (particleTransform == null || particleRenderer == null)
        {
            yield break;
        }

        Color initialColor = particleRenderer.color;
        float elapsed = 0f;

        while (elapsed < ExhaustParticleLifetime)
        {
            if (particleTransform == null || particleRenderer == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / ExhaustParticleLifetime);
            particleTransform.position += (Vector3)(driftVelocity * Time.deltaTime);
            particleTransform.localScale = Vector3.one * Mathf.Lerp(initialScale, initialScale * 1.9f, progress);

            Color nextColor = initialColor;
            nextColor.a = Mathf.Lerp(initialColor.a, 0f, progress);
            particleRenderer.color = nextColor;
            yield return null;
        }

        if (particleTransform != null)
        {
            Destroy(particleTransform.gameObject);
        }
    }

    private static Sprite GetExhaustParticleSprite()
    {
        if (exhaustParticleSprite != null)
        {
            return exhaustParticleSprite;
        }

        Texture2D texture = new Texture2D(
            ExhaustParticleSpriteTextureSize,
            ExhaustParticleSpriteTextureSize,
            TextureFormat.RGBA32,
            false)
        {
            name = "RoadCarExhaustParticleTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[ExhaustParticleSpriteTextureSize * ExhaustParticleSpriteTextureSize];
        float center = (ExhaustParticleSpriteTextureSize - 1f) * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < ExhaustParticleSpriteTextureSize; y++)
        {
            for (int x = 0; x < ExhaustParticleSpriteTextureSize; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((radius - distance) / ExhaustParticleSpriteEdgeSoftness);
                alpha = Mathf.SmoothStep(0f, 1f, alpha);
                pixels[(y * ExhaustParticleSpriteTextureSize) + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        exhaustParticleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, ExhaustParticleSpriteTextureSize, ExhaustParticleSpriteTextureSize),
            new Vector2(0.5f, 0.5f),
            ExhaustParticleSpriteTextureSize,
            0u,
            SpriteMeshType.FullRect);
        exhaustParticleSprite.name = "RoadCarExhaustParticleSprite";
        exhaustParticleSprite.hideFlags = HideFlags.HideAndDontSave;
        return exhaustParticleSprite;
    }
}
