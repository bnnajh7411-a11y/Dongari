using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class StaminaPickup : MonoBehaviour
{
    [SerializeField, Min(0f)] private float restoreAmount = 10f;

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

        PlayerStamina playerStamina = other.GetComponentInParent<PlayerStamina>();
        if (playerStamina == null)
        {
            return;
        }

        playerStamina.RestoreStamina(restoreAmount);
        Destroy(gameObject);
    }
}
