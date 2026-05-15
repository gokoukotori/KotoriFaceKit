namespace Aoyon.FaceTune.Gui;

internal readonly struct HandGestureIconInfo
{
    public HandGestureIconInfo(HandGesture gesture, string? textureName, string? textureGuid, string fallbackText)
    {
        Gesture = gesture;
        TextureName = textureName;
        TextureGuid = textureGuid;
        FallbackText = fallbackText;
    }

    public HandGesture Gesture { get; }
    public string? TextureName { get; }
    public string? TextureGuid { get; }
    public string FallbackText { get; }
}

internal static class HandGestureIconSet
{
    public static readonly HandGestureIconInfo[] Icons =
    {
        new(HandGesture.Neutral, null, null, "N"),
        new(HandGesture.Fist, "oncoming-fist.png", "b34d4cba1bd74447b57d2432a3f6a8f7", ""),
        new(HandGesture.HandOpen, "raised-hand.png", "0710a1397573466baece6caf2ebcfe34", ""),
        new(HandGesture.FingerPoint, "backhand-index-pointing-right.png", "f2602f5e37794cf08864f17e1244300b", ""),
        new(HandGesture.Victory, "victory-hand.png", "722475bca67f498b89c99405074c92df", ""),
        new(HandGesture.RockNRoll, "sign-of-the-horns.png", "62cb279e7cb048958546c36925264bf4", ""),
        new(HandGesture.HandGun, "love-you-gesture.png", "0fc1c9781eb943a2a881a7b3be49574f", ""),
        new(HandGesture.ThumbsUp, "thumbs-up.png", "dd506ae08d524967b120760b3689a44e", "")
    };

    private static readonly Dictionary<string, Texture2D?> SourceMaskCache = new();
    private static readonly Dictionary<string, Texture2D?> TintedTextureCache = new();

    public static GUIContent ContentFor(int gestureIndex, Color textColor)
    {
        var icon = Icons[gestureIndex];
        var tooltip = TooltipFor(icon.Gesture);
        var texture = LoadTintedTexture(icon, textColor);
        var fallbackText = string.IsNullOrEmpty(icon.FallbackText) ? ShortTextFor(icon.Gesture) : icon.FallbackText;
        return texture != null
            ? new GUIContent(texture, tooltip)
            : new GUIContent(fallbackText, tooltip);
    }

    public static Texture2D CreateTintedTexture(Texture2D source, Color textColor)
    {
        var sourcePixels = source.GetPixels32();
        var tint = (Color32)textColor;
        var tintedPixels = new Color32[sourcePixels.Length];

        for (var i = 0; i < sourcePixels.Length; i++)
        {
            tintedPixels[i] = new Color32(tint.r, tint.g, tint.b, sourcePixels[i].a);
        }

        var tinted = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        tinted.SetPixels32(tintedPixels);
        tinted.Apply(false, false);
        return tinted;
    }

    private static Texture2D? LoadTintedTexture(HandGestureIconInfo icon, Color textColor)
    {
        if (icon.TextureGuid == null) return null;

        var cacheKey = $"{icon.TextureGuid}:{ColorUtility.ToHtmlStringRGBA(textColor)}";
        if (TintedTextureCache.TryGetValue(cacheKey, out var cached)) return cached;

        var source = LoadSourceMask(icon.TextureGuid);
        var tinted = source == null ? null : CreateTintedTexture(source, textColor);
        TintedTextureCache[cacheKey] = tinted;
        return tinted;
    }

    private static Texture2D? LoadSourceMask(string textureGuid)
    {
        if (SourceMaskCache.TryGetValue(textureGuid, out var cached)) return cached;

        Texture2D? texture = null;
        var path = AssetDatabase.GUIDToAssetPath(textureGuid);
        if (!string.IsNullOrEmpty(path))
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            if (!ImageConversion.LoadImage(texture, System.IO.File.ReadAllBytes(path), false))
            {
                Object.DestroyImmediate(texture);
                texture = null;
            }
        }

        SourceMaskCache[textureGuid] = texture;
        return texture;
    }

    private static string TooltipFor(HandGesture gesture)
    {
        var key = $"HandGesture:enum:{gesture}";
        return Localization.TryGetLocalizedString(key, out var tooltip) ? tooltip : gesture.ToString();
    }

    private static string ShortTextFor(HandGesture gesture)
    {
        return gesture switch
        {
            HandGesture.Fist => "F",
            HandGesture.HandOpen => "O",
            HandGesture.FingerPoint => "P",
            HandGesture.Victory => "V",
            HandGesture.RockNRoll => "R",
            HandGesture.HandGun => "G",
            HandGesture.ThumbsUp => "T",
            _ => "N"
        };
    }
}
