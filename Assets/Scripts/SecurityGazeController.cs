using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class SecurityGazeController : MonoBehaviour
{
    private const int CircleTextureSize = 128;
    private const float CircleEdgeSoftness = 2f;

    [SerializeField, Min(0f)] private float alertDuration = 1f;
    [SerializeField] private Color alertColor = Color.red;

    private SpriteRenderer gazeRenderer;
    private SpriteRenderer securityRenderer;
    private SecurityPatrolController securityPatrolController;
    private Color defaultSecurityColor;
    private Coroutine restoreColorRoutine;
    private static Sprite circleSprite;

    private void Awake()
    {
        gazeRenderer = GetComponent<SpriteRenderer>();
        EnsureCircularVisual();
        EnsureTriggerCollider();
        securityRenderer = FindSecurityRenderer();
        securityPatrolController = FindSecurityPatrolController();

        if (securityRenderer != null)
        {
            defaultSecurityColor = securityRenderer.color;
        }
        else
        {
            Debug.LogWarning($"{name} could not find a parent SpriteRenderer to tint.", this);
        }

        if (securityPatrolController == null)
        {
            Debug.LogWarning($"{name} could not find a SecurityPatrolController on its parent.", this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryGetPlayerTransform(other, out Transform playerTransform))
        {
            return;
        }

        securityPatrolController?.BeginChase(playerTransform);
        TriggerAlert();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!TryGetPlayerTransform(other, out Transform playerTransform))
        {
            return;
        }

        securityPatrolController?.StopChase(playerTransform);
    }

    private void OnDisable()
    {
        StopAlertRoutine();
        RestoreDefaultSecurityColor();
        securityPatrolController?.CancelChase();
    }

    private void EnsureTriggerCollider()
    {
        CircleCollider2D trigger = GetComponent<CircleCollider2D>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<CircleCollider2D>();
        }

        trigger.isTrigger = true;
        trigger.radius = 0.5f;
        trigger.offset = Vector2.zero;
    }

    private void EnsureCircularVisual()
    {
        if (gazeRenderer == null)
        {
            return;
        }

        gazeRenderer.sprite = GetOrCreateCircleSprite();
        gazeRenderer.drawMode = SpriteDrawMode.Simple;
    }

    private SpriteRenderer FindSecurityRenderer()
    {
        Transform parent = transform.parent;
        return parent != null ? parent.GetComponentInParent<SpriteRenderer>() : null;
    }

    private SecurityPatrolController FindSecurityPatrolController()
    {
        return GetComponentInParent<SecurityPatrolController>();
    }

    private bool TryGetPlayerTransform(Collider2D other, out Transform playerTransform)
    {
        PlayerMovement playerMovement = other != null ? other.GetComponentInParent<PlayerMovement>() : null;
        if (playerMovement == null)
        {
            playerTransform = null;
            return false;
        }

        playerTransform = playerMovement.transform;
        return true;
    }

    private void TriggerAlert()
    {
        if (securityRenderer == null)
        {
            return;
        }

        StopAlertRoutine();
        securityRenderer.color = alertColor;

        if (alertDuration <= 0f)
        {
            RestoreDefaultSecurityColor();
            return;
        }

        restoreColorRoutine = StartCoroutine(RestoreSecurityColorAfterDelay());
    }

    private IEnumerator RestoreSecurityColorAfterDelay()
    {
        yield return new WaitForSeconds(alertDuration);
        RestoreDefaultSecurityColor();
        restoreColorRoutine = null;
    }

    private void StopAlertRoutine()
    {
        if (restoreColorRoutine != null)
        {
            StopCoroutine(restoreColorRoutine);
            restoreColorRoutine = null;
        }
    }

    private void RestoreDefaultSecurityColor()
    {
        if (securityRenderer != null)
        {
            securityRenderer.color = defaultSecurityColor;
        }
    }

    private static Sprite GetOrCreateCircleSprite()
    {
        if (circleSprite != null)
        {
            return circleSprite;
        }

        Texture2D texture = new Texture2D(CircleTextureSize, CircleTextureSize, TextureFormat.RGBA32, false)
        {
            name = "SecurityGazeCircleTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[CircleTextureSize * CircleTextureSize];
        float center = (CircleTextureSize - 1f) * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < CircleTextureSize; y++)
        {
            for (int x = 0; x < CircleTextureSize; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((radius - distance) / CircleEdgeSoftness);
                alpha = Mathf.SmoothStep(0f, 1f, alpha);
                pixels[(y * CircleTextureSize) + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        circleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, CircleTextureSize, CircleTextureSize),
            new Vector2(0.5f, 0.5f),
            CircleTextureSize,
            0u,
            SpriteMeshType.FullRect);
        circleSprite.name = "SecurityGazeCircleSprite";
        circleSprite.hideFlags = HideFlags.HideAndDontSave;
        return circleSprite;
    }
}
