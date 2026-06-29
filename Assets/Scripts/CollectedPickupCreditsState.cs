using System.Collections.Generic;
using UnityEngine;

public static class CollectedPickupCreditsState
{
    private static readonly List<Sprite> collectedSprites = new List<Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ResetCollectedSprites();
    }

    public static void RegisterCollectedSprite(Sprite sprite)
    {
        if (sprite == null)
        {
            return;
        }

        collectedSprites.Add(sprite);
    }

    public static void ResetCollectedSprites()
    {
        collectedSprites.Clear();
    }

    public static Sprite[] GetCollectedSprites()
    {
        return collectedSprites.ToArray();
    }
}
