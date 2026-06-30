using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class HealthPickup : MonoBehaviour
{
    [SerializeField, Min(1)] private int restoreAmount = 5;
    [SerializeField, TextArea(2, 4)]
    private string creditsDescription =
        "상처를 추스르고 다시 나아갈 힘을 얻었습니다.";

    private BoxCollider2D pickupCollider;
    private SpriteRenderer pickupRenderer;

    private void Awake()
    {
        pickupRenderer = GetComponent<SpriteRenderer>();
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

        CollectedPickupCreditsState.RegisterCollectedSprite(
            pickupRenderer != null ? pickupRenderer.sprite : null,
            creditsDescription);
        playerHealth.RestoreHealth(restoreAmount);
        Destroy(gameObject);
    }
}
