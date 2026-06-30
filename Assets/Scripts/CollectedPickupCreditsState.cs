using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class CollectedPickupCreditsState
{
    public sealed class CreditEntry
    {
        public CreditEntry(Sprite sprite, string description)
        {
            Sprite = sprite;
            Description = description ?? string.Empty;
        }

        public Sprite Sprite { get; }

        public string Description { get; }
    }

    private sealed class CreditBucket
    {
        private readonly List<string> descriptions = new List<string>();
        private readonly HashSet<string> descriptionLookup = new HashSet<string>();

        public CreditBucket(Sprite sprite)
        {
            Sprite = sprite;
        }

        public Sprite Sprite { get; }

        public void AddDescription(string description)
        {
            string normalizedDescription = NormalizeDescription(description);
            if (string.IsNullOrEmpty(normalizedDescription)
                || !descriptionLookup.Add(normalizedDescription))
            {
                return;
            }

            descriptions.Add(normalizedDescription);
        }

        public CreditEntry ToEntry()
        {
            return new CreditEntry(Sprite, string.Join("\n", descriptions));
        }
    }

    private static readonly List<CreditBucket> collectedCreditBuckets = new List<CreditBucket>();
    private static readonly Dictionary<Sprite, CreditBucket> creditBucketsBySprite =
        new Dictionary<Sprite, CreditBucket>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ResetCollectedSprites();
    }

    public static void RegisterCollectedSprite(Sprite sprite, string description = null)
    {
        if (sprite == null)
        {
            return;
        }

        if (!creditBucketsBySprite.TryGetValue(sprite, out CreditBucket creditBucket))
        {
            creditBucket = new CreditBucket(sprite);
            creditBucketsBySprite.Add(sprite, creditBucket);
            collectedCreditBuckets.Add(creditBucket);
        }

        creditBucket.AddDescription(description);
    }

    public static void ResetCollectedSprites()
    {
        collectedCreditBuckets.Clear();
        creditBucketsBySprite.Clear();
    }

    public static Sprite[] GetCollectedSprites()
    {
        Sprite[] collectedSprites = new Sprite[collectedCreditBuckets.Count];
        for (int i = 0; i < collectedCreditBuckets.Count; i++)
        {
            collectedSprites[i] = collectedCreditBuckets[i].Sprite;
        }

        return collectedSprites;
    }

    public static CreditEntry[] GetCollectedEntries()
    {
        CreditEntry[] collectedEntries = new CreditEntry[collectedCreditBuckets.Count];
        for (int i = 0; i < collectedCreditBuckets.Count; i++)
        {
            collectedEntries[i] = collectedCreditBuckets[i].ToEntry();
        }

        return collectedEntries;
    }

    private static string NormalizeDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        string decodedDescription = DecodeInspectorEscapes(description);
        return decodedDescription.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    }

    private static string DecodeInspectorEscapes(string value)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0)
        {
            return value ?? string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);

        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (current != '\\' || i >= value.Length - 1)
            {
                builder.Append(current);
                continue;
            }

            char next = value[i + 1];
            switch (next)
            {
                case '\\':
                    builder.Append('\\');
                    i++;
                    break;
                case 'n':
                    builder.Append('\n');
                    i++;
                    break;
                case 'r':
                    builder.Append('\r');
                    i++;
                    break;
                case 't':
                    builder.Append('\t');
                    i++;
                    break;
                case 'u':
                    if (TryDecodeUnicodeEscape(value, i + 2, 4, out char unicodeCharacter))
                    {
                        builder.Append(unicodeCharacter);
                        i += 5;
                        break;
                    }

                    builder.Append(current);
                    break;
                case 'U':
                    if (TryDecodeUnicodeEscape(value, i + 2, 8, out char unicodePlaneCharacter))
                    {
                        builder.Append(unicodePlaneCharacter);
                        i += 9;
                        break;
                    }

                    builder.Append(current);
                    break;
                default:
                    builder.Append(current);
                    break;
            }
        }

        return builder.ToString();
    }

    private static bool TryDecodeUnicodeEscape(
        string value,
        int startIndex,
        int digitCount,
        out char decodedCharacter)
    {
        decodedCharacter = default;

        if (startIndex < 0 || startIndex + digitCount > value.Length)
        {
            return false;
        }

        int codePoint = 0;
        for (int i = 0; i < digitCount; i++)
        {
            int hexValue = ParseHexDigit(value[startIndex + i]);
            if (hexValue < 0)
            {
                return false;
            }

            codePoint = (codePoint * 16) + hexValue;
        }

        if (codePoint < char.MinValue || codePoint > char.MaxValue)
        {
            return false;
        }

        decodedCharacter = (char)codePoint;
        return true;
    }

    private static int ParseHexDigit(char character)
    {
        if (character >= '0' && character <= '9')
        {
            return character - '0';
        }

        if (character >= 'a' && character <= 'f')
        {
            return 10 + (character - 'a');
        }

        if (character >= 'A' && character <= 'F')
        {
            return 10 + (character - 'A');
        }

        return -1;
    }
}
