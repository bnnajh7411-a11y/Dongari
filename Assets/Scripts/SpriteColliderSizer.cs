using UnityEngine;

public static class SpriteColliderSizer
{
    public static void FitBoxCollidersToSpriteRenderers(Transform root)
    {
        if (root == null)
        {
            return;
        }

        BoxCollider2D[] colliders = root.GetComponentsInChildren<BoxCollider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            BoxCollider2D collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            SpriteRenderer spriteRenderer = collider.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                continue;
            }

            Vector2 fittedSize = GetRenderedSize(spriteRenderer);
            if (fittedSize.x <= 0f || fittedSize.y <= 0f)
            {
                continue;
            }

            collider.size = fittedSize;
            collider.offset = GetPivotOffset(spriteRenderer, fittedSize);
        }
    }

    private static Vector2 GetRenderedSize(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer.drawMode == SpriteDrawMode.Simple)
        {
            return spriteRenderer.sprite.bounds.size;
        }

        return spriteRenderer.size;
    }

    private static Vector2 GetPivotOffset(SpriteRenderer spriteRenderer, Vector2 renderedSize)
    {
        Sprite sprite = spriteRenderer.sprite;
        if (sprite == null)
        {
            return Vector2.zero;
        }

        Rect spriteRect = sprite.rect;
        if (spriteRect.width <= 0f || spriteRect.height <= 0f)
        {
            return Vector2.zero;
        }

        Vector2 pivotNormalized = new Vector2(
            sprite.pivot.x / spriteRect.width,
            sprite.pivot.y / spriteRect.height);

        return new Vector2(
            (0.5f - pivotNormalized.x) * renderedSize.x,
            (0.5f - pivotNormalized.y) * renderedSize.y);
    }
}
