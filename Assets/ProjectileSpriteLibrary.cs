using UnityEngine;

public static class ProjectileSpriteLibrary
{
    const string CleanArrowPath = "Allies/pixel_allies/clean_arrow_projectile";
    const string RawArrowPath = "Allies/pixel_allies/arrow";
    const int ArrowFrameCount = 8;
    const float ArrowPixelsPerUnit = 170f;

    static Sprite[] arrowFrames;
    static Sprite fallbackArrow;

    public static Sprite[] GetArrowFrames()
    {
        if (arrowFrames != null)
            return arrowFrames;

        Texture2D sheet = Resources.Load<Texture2D>(CleanArrowPath);
        if (sheet == null)
            sheet = Resources.Load<Texture2D>(RawArrowPath);

        if (sheet == null)
        {
            arrowFrames = new[] { GetFallbackArrow() };
            return arrowFrames;
        }

        sheet.filterMode = FilterMode.Point;
        sheet.wrapMode = TextureWrapMode.Clamp;

        int frameCount = GuessFrameCount(sheet);
        float frameW = sheet.width / (float)frameCount;
        float frameH = sheet.height;
        arrowFrames = new Sprite[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            var rect = new Rect(i * frameW, 0f, frameW, frameH);
            arrowFrames[i] = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f), ArrowPixelsPerUnit);
        }

        return arrowFrames;
    }

    public static Sprite GetArrowSprite()
    {
        Sprite[] frames = GetArrowFrames();
        return frames.Length > 0 ? frames[0] : GetFallbackArrow();
    }

    static int GuessFrameCount(Texture2D sheet)
    {
        float ratio = sheet.width / Mathf.Max(1f, (float)sheet.height);
        if (ratio > 6.5f && ratio < 9.5f)
            return ArrowFrameCount;

        return Mathf.Max(1, Mathf.RoundToInt(ratio));
    }

    static Sprite GetFallbackArrow()
    {
        if (fallbackArrow != null)
            return fallbackArrow;

        int w = 48, h = 14;
        Color[] pixels = new Color[w * h];
        float cy = (h - 1) * 0.5f;
        int shaftEnd = Mathf.RoundToInt(w * 0.62f);
        var shaft = new Color(0.92f, 0.74f, 0.34f, 1f);
        var tip = new Color(0.92f, 0.95f, 1f, 1f);
        var outline = new Color(0.16f, 0.10f, 0.08f, 1f);

        for (int x = 2; x < shaftEnd; x++)
        for (int y = 0; y < h; y++)
        {
            float dist = Mathf.Abs(y - cy);
            if (dist <= 2.0f)
                pixels[y * w + x] = dist > 1.25f ? outline : shaft;
        }

        for (int x = shaftEnd; x < w - 2; x++)
        for (int y = 0; y < h; y++)
        {
            float t = (float)(x - shaftEnd) / Mathf.Max(w - shaftEnd - 3, 1);
            float halfH = Mathf.Lerp(h * 0.42f, 0.5f, t);
            float dist = Mathf.Abs(y - cy);
            if (dist <= halfH)
                pixels[y * w + x] = dist > halfH - 1.2f ? outline : tip;
        }

        for (int i = 0; i < 6; i++)
        {
            SetPixel(pixels, w, h, 3 + i, 3 - i / 3, tip);
            SetPixel(pixels, w, h, 3 + i, h - 4 + i / 3, tip);
            SetPixel(pixels, w, h, 2 + i, 2 - i / 3, outline);
            SetPixel(pixels, w, h, 2 + i, h - 3 + i / 3, outline);
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels(pixels);
        tex.Apply();
        fallbackArrow = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 28f);
        return fallbackArrow;
    }

    static void SetPixel(Color[] pixels, int w, int h, int x, int y, Color color)
    {
        if (x >= 0 && x < w && y >= 0 && y < h)
            pixels[y * w + x] = color;
    }
}
