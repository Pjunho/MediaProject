using UnityEngine;

/// <summary>
/// Shared UI font selector. Picks one square Korean font for a consistent retro UI tone.
/// </summary>
public static class UiPixelFont
{
    static Font cachedFont;

    public static Font Get()
    {
        if (cachedFont != null)
            return cachedFont;

        cachedFont = Resources.Load<Font>("Fonts/DNFBitBitv2");
        if (cachedFont != null)
        {
            UseCrispTexture(cachedFont);
            return cachedFont;
        }

        string[] candidates =
        {
            "DNF Bit Bit v2",
            "DNFBitBitv2",
            "HCR Dotum",
            "Han Santteut Dotum",
            "Hancom MalangMalang Bold",
            "Malgun Gothic"
        };

        foreach (string candidate in candidates)
        {
            var font = Font.CreateDynamicFontFromOSFont(candidate, 16);
            if (SupportsUiText(font))
            {
                cachedFont = font;
                UseCrispTexture(cachedFont);
                break;
            }
        }

        if (cachedFont == null)
            cachedFont = Font.CreateDynamicFontFromOSFont(candidates, 16);

        if (cachedFont == null)
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        UseCrispTexture(cachedFont);
        return cachedFont;
    }

    static void UseCrispTexture(Font font)
    {
        if (font == null || font.material == null || font.material.mainTexture == null)
            return;

        font.material.mainTexture.filterMode = FilterMode.Point;
    }

    static bool SupportsUiText(Font font)
    {
        if (font == null)
            return false;

        font.RequestCharactersInTexture("가힣ABC123");
        return font.HasCharacter('가') && font.HasCharacter('A') && font.HasCharacter('1');
    }
}
