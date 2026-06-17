using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class HealthPickup : MonoBehaviour
{
    [SerializeField, Min(1)] private int restoreAmount = 5;

    private BoxCollider2D pickupCollider;

    private void Awake()
    {
        EnsurePickupCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void EnsurePickupCollider()
    {
        pickupCollider = GetComponent<BoxCollider2D>();
        if (pickupCollider == null)
        {
            pickupCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        pickupCollider.isTrigger = true;
        SpriteColliderSizer.FitBoxCollidersToSpriteRenderers(transform);
    }

    private void TryCollect(Collider2D other)
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

        playerHealth.RestoreHealth(restoreAmount);
        Destroy(gameObject);
    }
}
