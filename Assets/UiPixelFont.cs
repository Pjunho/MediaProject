using UnityEngine;

/// <summary>
/// Shared UI font selector. Picks one retro-styled font that can render both Korean and English.
/// </summary>
public static class UiPixelFont
{
    static Font cachedFont;

    public static Font Get()
    {
        if (cachedFont != null)
            return cachedFont;

        string[] candidates =
        {
            "NewJumja",
            "D2Coding",
            "Cascadia Mono",
            "Consolas",
            "Hancom Gothic Bold",
            "HanSantteutDotum-Bold",
            "Malgun Gothic"
        };

        foreach (string candidate in candidates)
        {
            var font = Font.CreateDynamicFontFromOSFont(candidate, 16);
            if (font != null && (candidate == "NewJumja" || SupportsUiText(font)))
            {
                cachedFont = font;
                break;
            }
        }

        if (cachedFont == null)
            cachedFont = Font.CreateDynamicFontFromOSFont(candidates, 16);

        if (cachedFont == null)
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return cachedFont;
    }

    static bool SupportsUiText(Font font)
    {
        if (font == null)
            return false;

        font.RequestCharactersInTexture("가힣ABC123");
        return font.HasCharacter('가') && font.HasCharacter('A') && font.HasCharacter('1');
    }
}
