using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    private const string StartSceneName = "Start";
    private const string MountainSceneName = "Mountain";
    private const string BuildingSceneName = "Building";
    private const string HealthCanvasObjectName = "HealthCanvas";
    private const string HealthGaugeObjectName = "HealthGauge";
    private const string HealthLabelObjectName = "Label";
    private const string DamageAudioSourceObjectName = "PlayerDamageAudioSource";
    private const string MountainDamageAudioResourcePath = "Audios/foley-fighting-thud-05";
    private const string BuildingDamageAudioResourcePath = "Audios/foley-carpet drop-03";
    private static int savedCurrentHealth;
    private static bool hasSavedCurrentHealth;
    private static AudioSource damageAudioSource;
    private static AudioClip mountainDamageAudioClip;
    private static AudioClip buildingDamageAudioClip;

    public static event Action<PlayerHealth, int, int> DamageTaken;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        DamageTaken = null;
        damageAudioSource = null;
        mountainDamageAudioClip = null;
        buildingDamageAudioClip = null;
        ResetPersistentHealth();
    }

    [SerializeField, Min(1)] private int maxHealth = 20;
    [SerializeField, Min(0f)] private float damageCooldown = 0.75f;
    [SerializeField, Min(0f)] private float damageFlashDuration = 0.3f;
    [SerializeField] private Color damageFlashColor = Color.red;
    [Header("UI")]
    [SerializeField] private Vector2 gaugeSize = new Vector2(320f, 24f);
    [SerializeField] private Vector2 gaugeAnchoredPosition = new Vector2(24f, -24f);
    [SerializeField] private bool showBackgroundFrame = false;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color healthFillColor = new Color(0.86f, 0.2f, 0.2f, 1f);
    [SerializeField] private string gaugeLabel = "체력";
    [SerializeField] private Vector2 gaugeLabelOffset = new Vector2(16f, 0f);
    [SerializeField] private Vector2 gaugeLabelSize = new Vector2(180f, 28f);
    [SerializeField] private int gaugeLabelFontSize = 20;
    [SerializeField] private Color gaugeLabelColor = Color.white;

    public int CurrentHealth { get; private set; }

    private float nextDamageTime;
    private bool isDead;
    private SpriteRenderer[] spriteRenderers;
    private Color[] cachedSpriteColors;
    private Coroutine damageFlashRoutine;
    private Canvas healthCanvas;
    private RectTransform healthFillRectTransform;
    private float healthFillBaseWidth;
    private float healthFillHeight;

    private void Awake()
    {
        CurrentHealth = hasSavedCurrentHealth
            ? Mathf.Clamp(savedCurrentHealth, 0, maxHealth)
            : maxHealth;

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        EnsureHealthHud();
        RefreshHud();
        SaveCurrentHealth();
    }

    private void OnDisable()
    {
        SaveCurrentHealthIfAlive();
        RestoreSpriteColors();
        damageFlashRoutine = null;
    }

    private void OnDestroy()
    {
        SaveCurrentHealthIfAlive();
    }

    public static void ResetPersistentHealth()
    {
        savedCurrentHealth = 0;
        hasSavedCurrentHealth = false;
    }

    public bool TakeDamage(int damageAmount)
    {
        return TakeDamage(damageAmount, false);
    }

    public bool TakeDamage(int damageAmount, bool ignoreCooldown)
    {
        if (isDead || damageAmount <= 0 || (!ignoreCooldown && Time.time < nextDamageTime))
        {
            return false;
        }

        CurrentHealth = Mathf.Max(0, CurrentHealth - damageAmount);
        SaveCurrentHealth();
        if (!ignoreCooldown)
        {
            nextDamageTime = Time.time + damageCooldown;
        }
        TriggerDamageFlash();
        RefreshHud();
        PlaySceneDamageSound();
        DamageTaken?.Invoke(this, damageAmount, CurrentHealth);

        if (CurrentHealth == 0)
        {
            HandleDeath();
        }

        return true;
    }

    public void SetHealthToZero()
    {
        if (isDead)
        {
            return;
        }

        int lostHealth = CurrentHealth;
        CurrentHealth = 0;
        SaveCurrentHealth();
        RefreshHud();

        if (lostHealth > 0)
        {
            PlaySceneDamageSound();
            DamageTaken?.Invoke(this, lostHealth, CurrentHealth);
        }

        HandleDeath();
    }

    public bool RestoreHealth(int healAmount)
    {
        if (isDead || healAmount <= 0 || CurrentHealth >= maxHealth)
        {
            return false;
        }

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + healAmount);
        SaveCurrentHealth();
        RefreshHud();
        return true;
    }

    private void HandleDeath()
    {
        isDead = true;
        PlayerStamina.ResetPersistentStamina();
        ResetPersistentHealth();

        if (TryGetComponent(out Rigidbody2D rb))
        {
            rb.linearVelocity = Vector2.zero;
        }

        SceneFadeTransition.LoadScene(StartSceneName);
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

    private void PlaySceneDamageSound()
    {
        AudioClip damageClip = GetDamageClipForActiveScene();
        if (damageClip == null)
        {
            return;
        }

        AudioSource audioSource = EnsureDamageAudioSource();
        if (audioSource == null)
        {
            return;
        }

        audioSource.PlayOneShot(damageClip, AudioSettingsStore.SoundEffectVolume);
    }

    private static AudioClip GetDamageClipForActiveScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == MountainSceneName)
        {
            if (mountainDamageAudioClip == null)
            {
                mountainDamageAudioClip = Resources.Load<AudioClip>(MountainDamageAudioResourcePath);
            }

            return mountainDamageAudioClip;
        }

        if (sceneName == BuildingSceneName)
        {
            if (buildingDamageAudioClip == null)
            {
                buildingDamageAudioClip = Resources.Load<AudioClip>(BuildingDamageAudioResourcePath);
            }

            return buildingDamageAudioClip;
        }

        return null;
    }

    private static AudioSource EnsureDamageAudioSource()
    {
        if (damageAudioSource != null)
        {
            return damageAudioSource;
        }

        GameObject existingAudioObject = GameObject.Find(DamageAudioSourceObjectName);
        if (existingAudioObject != null)
        {
            damageAudioSource = existingAudioObject.GetComponent<AudioSource>();
        }

        if (damageAudioSource == null)
        {
            GameObject audioObject = new GameObject(DamageAudioSourceObjectName, typeof(AudioSource));
            DontDestroyOnLoad(audioObject);
            damageAudioSource = audioObject.GetComponent<AudioSource>();
        }

        if (damageAudioSource == null)
        {
            return null;
        }

        damageAudioSource.playOnAwake = false;
        damageAudioSource.loop = false;
        damageAudioSource.spatialBlend = 0f;
        return damageAudioSource;
    }

    private void EnsureHealthHud()
    {
        if (healthFillRectTransform != null)
        {
            return;
        }

        GameObject existingCanvasObject = GameObject.Find(HealthCanvasObjectName);
        if (existingCanvasObject != null && existingCanvasObject.TryGetComponent(out Canvas existingCanvas))
        {
            healthCanvas = existingCanvas;
        }
        else
        {
            GameObject canvasObject = new GameObject(
                HealthCanvasObjectName,
                typeof(Canvas),
                typeof(CanvasScaler));

            canvasObject.transform.SetParent(transform, false);

            healthCanvas = canvasObject.GetComponent<Canvas>();
            healthCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            healthCanvas.sortingOrder = 110;

            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;
        }

        if (healthCanvas != null)
        {
            healthCanvas.sortingOrder = 110;
        }

        if (healthCanvas == null || GameObject.Find(HealthGaugeObjectName) != null)
        {
            return;
        }

        Sprite uiSprite = RuntimeUiSpriteUtility.GetWhiteSprite();
        GameObject gaugeObject = new GameObject(
            HealthGaugeObjectName,
            typeof(RectTransform),
            typeof(Image));

        gaugeObject.transform.SetParent(healthCanvas.transform, false);

        RectTransform gaugeRectTransform = gaugeObject.GetComponent<RectTransform>();
        gaugeRectTransform.anchorMin = new Vector2(0f, 1f);
        gaugeRectTransform.anchorMax = new Vector2(0f, 1f);
        gaugeRectTransform.pivot = new Vector2(0f, 1f);
        gaugeRectTransform.sizeDelta = gaugeSize;
        gaugeRectTransform.anchoredPosition = gaugeAnchoredPosition;

        Image backgroundImage = gaugeObject.GetComponent<Image>();
        backgroundImage.sprite = uiSprite;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.color = showBackgroundFrame ? backgroundColor : Color.clear;
        backgroundImage.enabled = showBackgroundFrame;
        backgroundImage.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(gaugeObject.transform, false);

        RectTransform fillRectTransform = fillObject.GetComponent<RectTransform>();
        fillRectTransform.anchorMin = new Vector2(0f, 1f);
        fillRectTransform.anchorMax = new Vector2(0f, 1f);
        fillRectTransform.pivot = new Vector2(0f, 1f);
        fillRectTransform.anchoredPosition = new Vector2(3f, -3f);

        healthFillBaseWidth = Mathf.Max(0f, gaugeSize.x - 6f);
        healthFillHeight = Mathf.Max(0f, gaugeSize.y - 6f);
        fillRectTransform.sizeDelta = new Vector2(healthFillBaseWidth, healthFillHeight);

        healthFillRectTransform = fillRectTransform;

        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.sprite = uiSprite;
        fillImage.type = Image.Type.Simple;
        fillImage.color = healthFillColor;
        fillImage.raycastTarget = false;

        CreateGaugeLabel(gaugeObject.transform);
    }

    private void SaveCurrentHealth()
    {
        savedCurrentHealth = CurrentHealth;
        hasSavedCurrentHealth = true;
    }

    private void SaveCurrentHealthIfAlive()
    {
        if (!isDead)
        {
            SaveCurrentHealth();
        }
    }

    private void RefreshHud()
    {
        if (healthFillRectTransform == null)
        {
            return;
        }

        float healthRatio = maxHealth <= 0 ? 0f : (float)CurrentHealth / maxHealth;
        healthFillRectTransform.sizeDelta = new Vector2(healthFillBaseWidth * healthRatio, healthFillHeight);
    }

    private void CreateGaugeLabel(Transform parent)
    {
        GameObject labelObject = new GameObject(HealthLabelObjectName, typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);

        RectTransform labelRectTransform = labelObject.GetComponent<RectTransform>();
        labelRectTransform.anchorMin = new Vector2(1f, 0.5f);
        labelRectTransform.anchorMax = new Vector2(1f, 0.5f);
        labelRectTransform.pivot = new Vector2(0f, 0.5f);
        labelRectTransform.anchoredPosition = gaugeLabelOffset;
        labelRectTransform.sizeDelta = gaugeLabelSize;

        Text labelText = labelObject.GetComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = gaugeLabelFontSize;
        labelText.color = gaugeLabelColor;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        labelText.verticalOverflow = VerticalWrapMode.Overflow;
        labelText.raycastTarget = false;
        labelText.text = gaugeLabel;
    }
}
