using UnityEngine;

public static class RuntimeUiSpriteUtility
{
    private static Sprite cachedWhiteSprite;

    public static Sprite GetWhiteSprite()
    {
        if (cachedWhiteSprite != null)
        {
            return cachedWhiteSprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        cachedWhiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width);
        cachedWhiteSprite.name = "RuntimeWhiteSprite";

        return cachedWhiteSprite;
    }
}
