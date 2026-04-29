using UnityEngine;

/// <summary>
/// LPC Terrain 스프라이트 시트(Resources/Map/terrain.png)에서
/// 타일 스프라이트를 추출하는 유틸리티.
///
/// 텍스처 로드에 실패하면 기존 절차적 생성 방식으로 자동 폴백합니다.
///
/// ── LPC Terrain 좌표 규칙 ──────────────────────────────────────────
///   텍스처 크기 : 1024 × 1024 px, 타일 1개 = 32 × 32 px (32 열 × 32 행)
///   타일 ID N  → col = N % 32,  row = N / 32  (행은 위→아래)
///   Sprite.Create 의 Rect.y : 텍스처 하단(bottom-left)이 0
///                           → y = 1024 − (row + 1) × 32
/// </summary>
public static class TileTextureGenerator
{
    // ── 상수 ─────────────────────────────────────────────────────────
    const int   TEX_W   = 1024;
    const int   TEX_H   = 1024;
    const int   TILE_SZ = 32;
    const float PPU     = 32f;          // Pixels Per Unit (타일 1개 = 1 Unity 유닛)

    static readonly Vector2 PIVOT = new Vector2(0.5f, 0.5f);

    // ── 스프라이트 추출 좌표 (Sprite.Create Rect, y = 텍스처 하단 기준) ──
    //
    //   풀밭 벽 타일 4종 (미로 벽에 사용, 위치별로 변형을 골라 자연스럽게)
    //     tile 292 : Grass solid       row=9,  col=4
    //     tile 295 : Dark Grass solid  row=9,  col=7
    //     tile 298 : Short Grass solid row=9,  col=10
    //     tile 301 : Long Grass solid  row=9,  col=13
    //
    //   흙길 타일 (미로 통로에 사용)
    //     tile 676 : Earth solid       row=21, col=4
    //
    static readonly Rect[] GRASS_RECTS =
    {
        MakeRect(292),   // Grass
        MakeRect(295),   // Dark Grass
        MakeRect(298),   // Short Grass
        MakeRect(301),   // Long Grass
    };

    static readonly Rect EARTH_RECT = MakeRect(676);   // Earth (흙)

    // ── 캐시 ─────────────────────────────────────────────────────────
    static Sprite[] _grassSprites;
    static Sprite   _dirtSprite;
    static Sprite   _grassFallback;
    static Sprite   _dirtFallback;

    // ── 공개 API ─────────────────────────────────────────────────────

    /// <summary>
    /// 풀밭(벽) 스프라이트를 반환합니다.
    /// variant(0~3) 로 LPC 풀밭 변형 4종 중 하나를 선택합니다.
    /// 타일 좌표 (x+y)%4 를 넘기면 위치마다 자연스럽게 달라집니다.
    /// </summary>
    public static Sprite GetGrassSprite(int variant = 0)
    {
        if (_grassSprites == null)
            _grassSprites = BuildGrassSprites();

        if (_grassSprites != null)
            return _grassSprites[variant % _grassSprites.Length];

        // 폴백: 절차적 생성
        if (_grassFallback == null)
            _grassFallback = CreateGrassSprite();
        return _grassFallback;
    }

    /// <summary>흙길(통로) 스프라이트를 반환합니다.</summary>
    public static Sprite GetDirtSprite()
    {
        if (_dirtSprite == null)
            _dirtSprite = BuildDirtSprite();

        if (_dirtSprite != null)
            return _dirtSprite;

        // 폴백: 절차적 생성
        if (_dirtFallback == null)
            _dirtFallback = CreateDirtSprite();
        return _dirtFallback;
    }

    // ── LPC 텍스처 로더 ───────────────────────────────────────────────

    static Texture2D LoadTerrain()
    {
        var tex = Resources.Load<Texture2D>("Map/terrain");
        if (tex == null)
        {
            Debug.LogWarning("[TileTextureGenerator] Resources/Map/terrain.png 로드 실패 → 절차적 생성으로 폴백");
            return null;
        }
        // 픽셀아트 선명도 유지
        tex.filterMode = FilterMode.Point;
        tex.wrapMode   = TextureWrapMode.Clamp;
        return tex;
    }

    static Sprite[] BuildGrassSprites()
    {
        var tex = LoadTerrain();
        if (tex == null) return null;

        var arr = new Sprite[GRASS_RECTS.Length];
        for (int i = 0; i < GRASS_RECTS.Length; i++)
            arr[i] = Sprite.Create(tex, GRASS_RECTS[i], PIVOT, PPU);
        return arr;
    }

    static Sprite BuildDirtSprite()
    {
        var tex = LoadTerrain();
        return tex != null ? Sprite.Create(tex, EARTH_RECT, PIVOT, PPU) : null;
    }

    // ── 좌표 계산 ────────────────────────────────────────────────────

    /// <summary>
    /// 타일 ID → Sprite.Create 용 Rect 변환
    /// (y = 텍스처 하단 기준)
    /// </summary>
    static Rect MakeRect(int tileId)
    {
        int col = tileId % (TEX_W / TILE_SZ);
        int row = tileId / (TEX_W / TILE_SZ);
        float x = col * TILE_SZ;
        float y = TEX_H - (row + 1) * TILE_SZ;
        return new Rect(x, y, TILE_SZ, TILE_SZ);
    }

    // ── 절차적 폴백 (LPC 텍스처 없을 때 사용) ───────────────────────

    static Sprite CreateGrassSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float scale   = 4f;
        float offsetX = Random.Range(0f, 100f);
        float offsetY = Random.Range(0f, 100f);

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float nx = (float)x / size * scale + offsetX;
            float ny = (float)y / size * scale + offsetY;

            float noise    = Mathf.PerlinNoise(nx, ny);
            float detail   = Mathf.PerlinNoise(nx * 3f, ny * 3f) * 0.3f;
            float combined = noise * 0.7f + detail;

            Color darkGreen  = new Color(0.13f, 0.42f, 0.10f);
            Color midGreen   = new Color(0.22f, 0.58f, 0.16f);
            Color lightGreen = new Color(0.34f, 0.70f, 0.22f);

            Color c = combined < 0.4f
                ? Color.Lerp(darkGreen, midGreen,   combined / 0.4f)
                : Color.Lerp(midGreen,  lightGreen, (combined - 0.4f) / 0.6f);

            if (noise > 0.72f && detail > 0.18f)
                c = Color.Lerp(c, new Color(0.45f, 0.80f, 0.25f), 0.4f);

            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return SpriteFromTexture(tex);
    }

    static Sprite CreateDirtSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float scale   = 5f;
        float offsetX = Random.Range(0f, 100f);
        float offsetY = Random.Range(0f, 100f);

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float nx = (float)x / size * scale + offsetX;
            float ny = (float)y / size * scale + offsetY;

            float noise    = Mathf.PerlinNoise(nx, ny);
            float detail   = Mathf.PerlinNoise(nx * 4f, ny * 4f) * 0.25f;
            float track    = Mathf.PerlinNoise(nx * 0.8f, ny * 6f) * 0.15f;
            float combined = noise * 0.6f + detail + track;

            Color darkBrown  = new Color(0.38f, 0.23f, 0.08f);
            Color midBrown   = new Color(0.55f, 0.36f, 0.14f);
            Color lightBrown = new Color(0.70f, 0.52f, 0.26f);
            Color sandColor  = new Color(0.80f, 0.65f, 0.38f);

            Color c;
            if      (combined < 0.35f) c = Color.Lerp(darkBrown,  midBrown,   combined / 0.35f);
            else if (combined < 0.65f) c = Color.Lerp(midBrown,   lightBrown, (combined - 0.35f) / 0.30f);
            else                       c = Color.Lerp(lightBrown,  sandColor,  (combined - 0.65f) / 0.35f);

            if (noise > 0.68f && detail < 0.08f)
                c = Color.Lerp(c, new Color(0.60f, 0.58f, 0.52f), 0.25f);

            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return SpriteFromTexture(tex);
    }

    static Sprite SpriteFromTexture(Texture2D tex)
        => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                         new Vector2(0.5f, 0.5f), tex.width);
}
