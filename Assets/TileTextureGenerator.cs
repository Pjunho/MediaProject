using UnityEngine;

/// <summary>
/// 풀밭/흙길 텍스처를 절차적으로 생성하는 유틸리티
/// </summary>
public static class TileTextureGenerator
{
    private static Sprite _grassSprite;
    private static Sprite _dirtSprite;

    public static Sprite GetGrassSprite()
    {
        if (_grassSprite == null)
            _grassSprite = CreateGrassSprite();
        return _grassSprite;
    }

    public static Sprite GetDirtSprite()
    {
        if (_dirtSprite == null)
            _dirtSprite = CreateDirtSprite();
        return _dirtSprite;
    }

    // ── 풀밭 텍스처 ────────────────────────────────────────────────
    static Sprite CreateGrassSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float scale = 4f;
        float offsetX = Random.Range(0f, 100f);
        float offsetY = Random.Range(0f, 100f);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float nx = (float)x / size * scale + offsetX;
                float ny = (float)y / size * scale + offsetY;

                // 기본 노이즈
                float noise = Mathf.PerlinNoise(nx, ny);
                // 세부 노이즈 (잔디 결)
                float detail = Mathf.PerlinNoise(nx * 3f, ny * 3f) * 0.3f;
                float combined = noise * 0.7f + detail;

                // 풀밭 색상 범위: 진한 초록 ~ 밝은 초록
                Color darkGreen  = new Color(0.13f, 0.42f, 0.10f);
                Color midGreen   = new Color(0.22f, 0.58f, 0.16f);
                Color lightGreen = new Color(0.34f, 0.70f, 0.22f);

                Color c;
                if (combined < 0.4f)
                    c = Color.Lerp(darkGreen, midGreen, combined / 0.4f);
                else
                    c = Color.Lerp(midGreen, lightGreen, (combined - 0.4f) / 0.6f);

                // 간간이 더 밝은 풀잎 강조
                if (noise > 0.72f && detail > 0.18f)
                    c = Color.Lerp(c, new Color(0.45f, 0.80f, 0.25f), 0.4f);

                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return SpriteFromTexture(tex);
    }

    // ── 흙길 텍스처 ────────────────────────────────────────────────
    static Sprite CreateDirtSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float scale = 5f;
        float offsetX = Random.Range(0f, 100f);
        float offsetY = Random.Range(0f, 100f);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float nx = (float)x / size * scale + offsetX;
                float ny = (float)y / size * scale + offsetY;

                float noise  = Mathf.PerlinNoise(nx, ny);
                float detail = Mathf.PerlinNoise(nx * 4f, ny * 4f) * 0.25f;
                // 가로 방향 발자국/바퀴 자국 느낌
                float track  = Mathf.PerlinNoise(nx * 0.8f, ny * 6f) * 0.15f;
                float combined = noise * 0.6f + detail + track;

                // 흙길 색상 범위: 진한 갈색 ~ 밝은 황토색
                Color darkBrown  = new Color(0.38f, 0.23f, 0.08f);
                Color midBrown   = new Color(0.55f, 0.36f, 0.14f);
                Color lightBrown = new Color(0.70f, 0.52f, 0.26f);
                Color sandColor  = new Color(0.80f, 0.65f, 0.38f);

                Color c;
                if (combined < 0.35f)
                    c = Color.Lerp(darkBrown, midBrown, combined / 0.35f);
                else if (combined < 0.65f)
                    c = Color.Lerp(midBrown, lightBrown, (combined - 0.35f) / 0.30f);
                else
                    c = Color.Lerp(lightBrown, sandColor, (combined - 0.65f) / 0.35f);

                // 작은 돌멩이/모래알 느낌
                if (noise > 0.68f && detail < 0.08f)
                    c = Color.Lerp(c, new Color(0.60f, 0.58f, 0.52f), 0.25f);

                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return SpriteFromTexture(tex);
    }

    // ── 공통 유틸 ──────────────────────────────────────────────────
    static Sprite SpriteFromTexture(Texture2D tex)
    {
        return Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            tex.width   // pixels per unit = 텍스처 크기 → 1유닛 크기
        );
    }
}
