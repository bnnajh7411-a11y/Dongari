using UnityEngine;

public abstract class TriggerSpritePickupBase : MonoBehaviour
{
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

        Destroy(gameObject);
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
