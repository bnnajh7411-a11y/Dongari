using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class NextSpriteAlpha : MonoBehaviour
{
    private SpriteRenderer sprite;
    private bool fadeOut;

    [SerializeField] private float fadeSpeed = 1f;

    void Start()
    {
        sprite = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        Color color = sprite.color;

        if (color.a >= 1f)
        {
            fadeOut = true;
        }
        else if (color.a <= 0f)
        {
            fadeOut = false;
        }

        if (fadeOut)
        {
            color.a -= fadeSpeed * Time.deltaTime;
        }
        else
        {
            color.a += fadeSpeed * Time.deltaTime;
        }

        color.a = Mathf.Clamp01(color.a);
        sprite.color = color;
    }
}