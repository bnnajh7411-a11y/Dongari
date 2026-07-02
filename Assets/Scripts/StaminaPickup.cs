using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class StaminaPickup : TriggerSpritePickupBase
{
    [SerializeField, Min(0f)] private float restoreAmount = 10f;
    [SerializeField, TextArea(2, 4)]
    private string creditsDescription =
        "";

    protected override void HandleTriggerEnter(Collider2D other)
    {
        PlayerStamina playerStamina = GetCollectorInParent<PlayerStamina>(other);
        if (playerStamina == null)
        {
            return;
        }

        playerStamina.RestoreStamina(restoreAmount);
        CollectAndDestroy(creditsDescription);
    }
}
