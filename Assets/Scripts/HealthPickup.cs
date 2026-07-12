using UnityEngine;

public abstract class TriggerSpritePickupBase : MonoBehaviour
{
    private const string PickupAudioSourceObjectName = "PickupAudioSource";
    private const string PickupAudioResourcePath = "Audios/freesound_community-item-pick-up-38258";

    private static AudioSource pickupAudioSource;
    private static AudioClip pickupAudioClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPickupAudioState()
    {
        pickupAudioSource = null;
        pickupAudioClip = null;
    }

    protected SpriteRenderer PickupRenderer { get; private set; }

    protected Sprite PickupSprite => PickupRenderer != null ? PickupRenderer.sprite : null;

    protected virtual void Awake()
    {
        PickupRenderer = GetComponent<SpriteRenderer>();
        EnsurePickupCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTriggerEnter(other);
    }

    protected abstract void HandleTriggerEnter(Collider2D other);

    protected T GetCollectorInParent<T>(Collider2D other) where T : Component
    {
        return other != null ? other.GetComponentInParent<T>() : null;
    }

    protected void CollectAndDestroy(string creditsDescription = null, bool registerCredits = true)
    {
        if (registerCredits)
        {
            CollectedPickupCreditsState.RegisterCollectedSprite(PickupSprite, creditsDescription);
        }

        PlayPickupSound();
        Destroy(gameObject);
    }

    private static void PlayPickupSound()
    {
        if (pickupAudioClip == null)
        {
            pickupAudioClip = Resources.Load<AudioClip>(PickupAudioResourcePath);
        }

        if (pickupAudioClip == null)
        {
            return;
        }

        AudioSource audioSource = EnsurePickupAudioSource();
        if (audioSource == null)
        {
            return;
        }

        audioSource.PlayOneShot(pickupAudioClip, AudioSettingsStore.SoundEffectVolume);
    }

    private static AudioSource EnsurePickupAudioSource()
    {
        if (pickupAudioSource != null)
        {
            return pickupAudioSource;
        }

        GameObject existingAudioObject = GameObject.Find(PickupAudioSourceObjectName);
        if (existingAudioObject != null)
        {
            pickupAudioSource = existingAudioObject.GetComponent<AudioSource>();
        }

        if (pickupAudioSource == null)
        {
            GameObject audioObject = new GameObject(PickupAudioSourceObjectName, typeof(AudioSource));
            DontDestroyOnLoad(audioObject);
            pickupAudioSource = audioObject.GetComponent<AudioSource>();
        }

        if (pickupAudioSource == null)
        {
            return null;
        }

        pickupAudioSource.playOnAwake = false;
        pickupAudioSource.loop = false;
        pickupAudioSource.spatialBlend = 0f;
        return pickupAudioSource;
    }

    private void EnsurePickupCollider()
    {
        BoxCollider2D pickupCollider = GetComponent<BoxCollider2D>();
        if (pickupCollider == null)
        {
            pickupCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        pickupCollider.isTrigger = true;
        SpriteColliderSizer.FitBoxCollidersToSpriteRenderers(transform);
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class HealthPickup : TriggerSpritePickupBase
{
    [SerializeField, Min(1)] private int restoreAmount = 5;
    [SerializeField, TextArea(2, 4)]
    private string creditsDescription =
        "";

    protected override void HandleTriggerEnter(Collider2D other)
    {
        PlayerHealth playerHealth = GetCollectorInParent<PlayerHealth>(other);
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.RestoreHealth(restoreAmount);
        CollectAndDestroy(creditsDescription);
    }
}
