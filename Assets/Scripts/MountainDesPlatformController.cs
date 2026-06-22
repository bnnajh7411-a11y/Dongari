using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-20)]
public class MountainDesPlatformController : MonoBehaviour
{
    private const string TargetSceneName = "Mountain";
    private const string TargetObjectName = "Des";

    [SerializeField, Min(0f)] private float collapseDelay = 1f;
    [SerializeField, Min(0f)] private float respawnDelay = 2f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != TargetSceneName || Object.FindAnyObjectByType<MountainDesPlatformController>() != null)
        {
            return;
        }

        GameObject desObject = GameObject.Find(TargetObjectName);
        if (desObject != null)
        {
            desObject.AddComponent<MountainDesPlatformController>();
        }
    }

    private void Awake()
    {
        InstallPlatformBehaviours();
    }

    private void InstallPlatformBehaviours()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null || !child.TryGetComponent(out Collider2D _))
            {
                continue;
            }

            RespawningCollapsePlatform platform = child.GetComponent<RespawningCollapsePlatform>();
            if (platform == null)
            {
                platform = child.gameObject.AddComponent<RespawningCollapsePlatform>();
            }

            platform.Configure(collapseDelay, respawnDelay);
        }
    }
}

[DisallowMultipleComponent]
public class RespawningCollapsePlatform : MonoBehaviour
{
    private const float MinimumTopContactTolerance = 0.08f;
    private const float RelativeTopContactTolerance = 0.2f;
    private const string CollapseAudioResourcesPath = "Audios/바위부서지는23";

    private static AudioClip cachedCollapseSoundClip;

    private float collapseDelay = 3f;
    private float respawnDelay = 2f;
    private float collapseTimer;
    private float hiddenTimer;
    private bool isHidden;
    private bool isCollapseTimerRunning;

    private Collider2D[] platformColliders;
    private SpriteRenderer[] platformRenderers;
    private Collider2D primaryCollider;
    private AudioSource collapseAudioSource;

    [SerializeField, Range(0f, 1f)] private float collapseSoundVolume = 1f;

    public void Configure(float collapseDelaySeconds, float respawnDelaySeconds)
    {
        collapseDelay = Mathf.Max(0f, collapseDelaySeconds);
        respawnDelay = Mathf.Max(0f, respawnDelaySeconds);
    }

    private void Awake()
    {
        CacheComponents();
        EnsureCollapseAudioSource();
    }

    private void Update()
    {
        if (GamePauseState.IsPaused)
        {
            return;
        }

        if (isHidden)
        {
            hiddenTimer += Time.deltaTime;
            if (hiddenTimer >= respawnDelay)
            {
                ShowPlatform();
            }

            return;
        }

        if (!isCollapseTimerRunning)
        {
            return;
        }

        collapseTimer += Time.deltaTime;
        if (collapseTimer >= collapseDelay)
        {
            HidePlatform();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryStartCollapseTimer(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryStartCollapseTimer(collision);
    }

    private void CacheComponents()
    {
        platformColliders = GetComponentsInChildren<Collider2D>(true);
        platformRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (platformColliders.Length > 0)
        {
            primaryCollider = platformColliders[0];
        }
    }

    private bool IsPlayerStandingOnTop(Collision2D collision)
    {
        if (collision.collider == null || primaryCollider == null)
        {
            return false;
        }

        if (collision.transform.GetComponentInParent<PlayerMovement>() == null)
        {
            return false;
        }

        Bounds platformBounds = primaryCollider.bounds;
        float topTolerance = Mathf.Max(MinimumTopContactTolerance, platformBounds.size.y * RelativeTopContactTolerance);

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            bool touchesTopSurface = contact.point.y >= platformBounds.max.y - topTolerance;
            bool playerIsAbovePlatform = collision.collider.bounds.min.y >= platformBounds.center.y;

            if (touchesTopSurface && playerIsAbovePlatform)
            {
                return true;
            }
        }

        return false;
    }

    private void TryStartCollapseTimer(Collision2D collision)
    {
        if (isHidden || isCollapseTimerRunning || !IsPlayerStandingOnTop(collision))
        {
            return;
        }

        isCollapseTimerRunning = true;
        collapseTimer = 0f;
    }

    private void HidePlatform()
    {
        PlayCollapseSound();
        SetPlatformVisible(false);
        isHidden = true;
        hiddenTimer = 0f;
        collapseTimer = 0f;
        isCollapseTimerRunning = false;
    }

    private void ShowPlatform()
    {
        SetPlatformVisible(true);
        isHidden = false;
        hiddenTimer = 0f;
        collapseTimer = 0f;
        isCollapseTimerRunning = false;
    }

    private void SetPlatformVisible(bool isVisible)
    {
        if (platformColliders == null || platformRenderers == null)
        {
            CacheComponents();
        }

        for (int i = 0; i < platformColliders.Length; i++)
        {
            if (platformColliders[i] != null)
            {
                platformColliders[i].enabled = isVisible;
            }
        }

        for (int i = 0; i < platformRenderers.Length; i++)
        {
            if (platformRenderers[i] != null)
            {
                platformRenderers[i].enabled = isVisible;
            }
        }
    }

    private void EnsureCollapseAudioSource()
    {
        if (collapseAudioSource == null)
        {
            collapseAudioSource = GetComponent<AudioSource>();
        }

        if (collapseAudioSource == null)
        {
            collapseAudioSource = gameObject.AddComponent<AudioSource>();
        }

        collapseAudioSource.playOnAwake = false;
        collapseAudioSource.loop = false;
        collapseAudioSource.spatialBlend = 0f;
    }

    private void PlayCollapseSound()
    {
        if (collapseAudioSource == null)
        {
            EnsureCollapseAudioSource();
        }

        if (cachedCollapseSoundClip == null)
        {
            cachedCollapseSoundClip = Resources.Load<AudioClip>(CollapseAudioResourcesPath);
        }

        if (collapseAudioSource == null || cachedCollapseSoundClip == null)
        {
            return;
        }

        float volumeScale = Mathf.Clamp01(collapseSoundVolume) * AudioSettingsStore.SoundEffectVolume;
        collapseAudioSource.PlayOneShot(cachedCollapseSoundClip, volumeScale);
    }
}
