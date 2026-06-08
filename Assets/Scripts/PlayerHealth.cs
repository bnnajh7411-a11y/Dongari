using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 3;
    [SerializeField, Min(0f)] private float damageCooldown = 0.75f;
    [SerializeField, Min(0f)] private float damageFlashDuration = 0.3f;
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private bool reloadSceneOnDeath = true;

    public int CurrentHealth { get; private set; }

    private float nextDamageTime;
    private bool isDead;
    private SpriteRenderer[] spriteRenderers;
    private Color[] cachedSpriteColors;
    private Coroutine damageFlashRoutine;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void OnDisable()
    {
        RestoreSpriteColors();
        damageFlashRoutine = null;
    }

    public bool TakeDamage(int damageAmount)
    {
        if (isDead || damageAmount <= 0 || Time.time < nextDamageTime)
        {
            return false;
        }

        CurrentHealth = Mathf.Max(0, CurrentHealth - damageAmount);
        nextDamageTime = Time.time + damageCooldown;
        TriggerDamageFlash();

        if (CurrentHealth == 0)
        {
            HandleDeath();
        }

        return true;
    }

    private void HandleDeath()
    {
        isDead = true;

        if (TryGetComponent(out Rigidbody2D rb))
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (reloadSceneOnDeath)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        gameObject.SetActive(false);
    }

    private void TriggerDamageFlash()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0 || damageFlashDuration <= 0f)
        {
            return;
        }

        if (damageFlashRoutine != null)
        {
            StopCoroutine(damageFlashRoutine);
            RestoreSpriteColors();
        }

        damageFlashRoutine = StartCoroutine(DamageFlashRoutine());
    }

    private IEnumerator DamageFlashRoutine()
    {
        cachedSpriteColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            cachedSpriteColors[i] = spriteRenderer.color;
            spriteRenderer.color = damageFlashColor;
        }

        yield return new WaitForSeconds(damageFlashDuration);

        RestoreSpriteColors();
        damageFlashRoutine = null;
    }

    private void RestoreSpriteColors()
    {
        if (spriteRenderers == null || cachedSpriteColors == null)
        {
            return;
        }

        int colorCount = Mathf.Min(spriteRenderers.Length, cachedSpriteColors.Length);
        for (int i = 0; i < colorCount; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            spriteRenderer.color = cachedSpriteColors[i];
        }
    }
}
