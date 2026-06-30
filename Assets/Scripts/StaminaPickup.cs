using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class StaminaPickup : MonoBehaviour
{
    [SerializeField, Min(0f)] private float restoreAmount = 10f;
    [SerializeField, TextArea(2, 4)]
    private string creditsDescription =
        "다시 달릴 힘을 되찾았습니다.";

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

        PlayerStamina playerStamina = other.GetComponentInParent<PlayerStamina>();
        if (playerStamina == null)
        {
            return;
        }

        CollectedPickupCreditsState.RegisterCollectedSprite(
            pickupRenderer != null ? pickupRenderer.sprite : null,
            creditsDescription);
        playerStamina.RestoreStamina(restoreAmount);
        Destroy(gameObject);
    }
}
