using UnityEngine;

/// <summary>
/// LPC Terrain(Resources/Map/terrain.png)에서 스테이지별 타일 스프라이트를 추출.
///
/// ┌──────────────────────────────────────────────────────────────────┐
/// │  스테이지 테마                                                    │
/// │  Stage 1 (초원의 전투)                                           │
/// │    벽  : Grass · Dark Grass · Short Grass · Long Grass           │
/// │    길  : Earth (흙길)                                            │
/// │                                                                  │
/// │  Stage 2 (사막의 요새)                                           │
/// │    벽  : Sand solid × 4 variants                                 │
/// │    길  : Brick Road × 4 variants (요새 석재 통로)               │
/// │                                                                  │
/// │  Stage 3 (화산의 심판)                                           │
/// │    벽  : Dark Dirt × 3 + Lava × 1 (4타일 중 1개 용암 포켓)     │
/// │    길  : Red Dirt × 3 variants (붉은 화산암 통로)               │
/// └──────────────────────────────────────────────────────────────────┘
///
/// LPC Terrain 좌표 규칙:
///   텍스처 1024×1024 px, 타일 32×32 px (열 32개 × 행 32개)
///   tile id N → col = N % 32,  row = N / 32  (행은 위→아래)
///   Sprite.Create Rect.y = 1024 − (row + 1) × 32   (하단 = 0 기준)
/// </summary>
public static class TileTextureGenerator
{
    // ── 텍스처 상수 ──────────────────────────────────────────────────
    const int   TEX_W   = 1024;
    const int   TEX_H   = 1024;
    const int   TILE_SZ = 32;
    const float PPU     = 32f;      // Pixels Per Unit : 타일 1개 = 1 Unity 유닛
    static readonly Vector2 PIVOT = new Vector2(0.5f, 0.5f);

    // ── 스테이지별 벽 타일 ID ────────────────────────────────────────
    //   (x + y) % Length 로 위치마다 다른 variant 선택 → 자연스러운 분포
    //
    //   Stage 1 – 초원
    //     292 Grass solid       row=9,  col=4
    //     295 Dark Grass solid  row=9,  col=7
    //     298 Short Grass solid row=9,  col=10
    //     301 Long Grass solid  row=9,  col=13
    //
    //   Stage 2 – 사막
    //     307 Sand solid        row=9,  col=19   (대표)
    //     370 Sand variant-A    row=11, col=18
    //     371 Sand variant-B    row=11, col=19
    //     372 Sand variant-C    row=11, col=20
    //
    //   Stage 3 – 화산  (Dark Dirt 3종 + Lava 1개 → 25% 용암 포켓)
    //     100 Dark Dirt solid   row=3,  col=4
    //     163 Dark Dirt var-A   row=5,  col=3
    //     112 Lava solid        row=3,  col=16   ← 드라마틱 포인트
    //     164 Dark Dirt var-B   row=5,  col=4
    //
    static readonly int[][] STAGE_WALL_IDS =
    {
        null,                              // [0] 미사용
        new[] { 292, 295, 298, 301 },      // [1] 초원
        new[] { 307, 370, 371, 372 },      // [2] 사막
        new[] { 100, 163, 112, 164 },      // [3] 화산
    };

    // ── 스테이지별 길 타일 ID ────────────────────────────────────────
    //   Stage 1 – 흙길  (Earth solid 단일 → 깔끔한 흙 통로)
    //     676 Earth solid       row=21, col=4
    //
    //   Stage 2 – 벽돌길 (Brick Road × 4 → 요새 느낌)
    //     491 Brick Road solid  row=15, col=11
    //     492 Brick Road var-A  row=15, col=12
    //     493 Brick Road var-B  row=15, col=13
    //     494 Brick Road var-C  row=15, col=14
    //
    //   Stage 3 – 붉은 흙 (Red Dirt × 3 → 화산암 통로)
    //     103 Red Dirt solid    row=3,  col=7
    //     166 Red Dirt var-A    row=5,  col=6
    //     167 Red Dirt var-B    row=5,  col=7
    //
    static readonly int[][] STAGE_PATH_IDS =
    {
        null,                              // [0] 미사용
        new[] { 676 },                     // [1] 초원 : Earth
        new[] { 491, 492, 493, 494 },      // [2] 사막 : Brick Road × 4
        new[] { 103, 166, 167 },           // [3] 화산 : Red Dirt × 3
    };

    // ── 스프라이트 캐시 [stageIdx][variantIdx] ───────────────────────
    static readonly Sprite[][] _wallCache = new Sprite[4][];
    static readonly Sprite[][] _pathCache = new Sprite[4][];

    // 절차적 폴백 캐시
    static Sprite _grassFallback;
    static Sprite _dirtFallback;

    // ────────────────────────────────────────────────────────────────
    //  공개 API
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 스테이지에 맞는 벽(미로 벽) 스프라이트를 반환합니다.
    /// <paramref name="variant"/> : (x + y) % 4 등 위치 기반 값을 전달하면
    ///   각 variant가 공간에 자연스럽게 분포됩니다.
    /// </summary>
    public static Sprite GetWallSprite(int stageIndex, int variant = 0)
    {
        int idx = ClampStage(stageIndex);

        if (_wallCache[idx] == null)
            _wallCache[idx] = BuildSpriteArray(STAGE_WALL_IDS[idx]);

        if (_wallCache[idx] != null)
            return _wallCache[idx][variant % _wallCache[idx].Length];

        // LPC 텍스처 로드 실패 → 절차적 풀밭 폴백
        return GetFallback(ref _grassFallback, CreateGrassSprite);
    }

    /// <summary>
    /// 스테이지에 맞는 길(미로 통로) 스프라이트를 반환합니다.
    /// <paramref name="variant"/> : (x + y) % n 등으로 변형 적용 가능.
    /// </summary>
    public static Sprite GetPathSprite(int stageIndex, int variant = 0)
    {
        int idx = ClampStage(stageIndex);

        if (_pathCache[idx] == null)
            _pathCache[idx] = BuildSpriteArray(STAGE_PATH_IDS[idx]);

        if (_pathCache[idx] != null)
            return _pathCache[idx][variant % _pathCache[idx].Length];

        // LPC 텍스처 로드 실패 → 절차적 흙길 폴백
        return GetFallback(ref _dirtFallback, CreateDirtSprite);
    }

    // ── 하위 호환 API (Stage 1 고정) ─────────────────────────────────
    public static Sprite GetGrassSprite(int variant = 0) => GetWallSprite(1, variant);
    public static Sprite GetDirtSprite()                  => GetPathSprite(1, 0);

    // ────────────────────────────────────────────────────────────────
    //  내부 로더
    // ────────────────────────────────────────────────────────────────

    static Texture2D LoadTerrain()
    {
        var tex = Resources.Load<Texture2D>("Map/terrain");
        if (tex == null)
        {
            Debug.LogWarning(
                "[TileTextureGenerator] Resources/Map/terrain.png 로드 실패 " +
                "→ 절차적 생성으로 폴백합니다.\n" +
                "해결: Unity Inspector에서 terrain.png의 Texture Type을 'Default'로 변경하세요.");
            return null;
        }
        tex.filterMode = FilterMode.Point;   // 픽셀아트 선명도 유지
        tex.wrapMode   = TextureWrapMode.Clamp;
        return tex;
    }

    /// <summary>타일 ID 배열 → Sprite 배열로 변환 (캐시용)</summary>
    static Sprite[] BuildSpriteArray(int[] ids)
    {
        if (ids == null || ids.Length == 0) return null;
        var tex = LoadTerrain();
        if (tex == null) return null;

        var arr = new Sprite[ids.Length];
        for (int i = 0; i < ids.Length; i++)
            arr[i] = Sprite.Create(tex, TileRect(ids[i]), PIVOT, PPU);
        return arr;
    }

    /// <summary>타일 ID → Sprite.Create 용 Rect (y = 텍스처 하단 기준)</summary>
    static Rect TileRect(int id)
    {
        int col = id % (TEX_W / TILE_SZ);   // 열 (0 = 좌단)
        int row = id / (TEX_W / TILE_SZ);   // 행 (0 = 상단)
        float x = col * TILE_SZ;
        float y = TEX_H - (row + 1) * TILE_SZ;   // Unity Rect: y=0 = 하단
        return new Rect(x, y, TILE_SZ, TILE_SZ);
    }

    static int ClampStage(int s) => (s >= 1 && s <= 3) ? s : 1;

    static Sprite GetFallback(ref Sprite cache, System.Func<Sprite> creator)
    {
        if (cache == null) cache = creator();
        return cache;
    }

    // ────────────────────────────────────────────────────────────────
    //  절차적 폴백 (LPC 텍스처 없을 때 자동 사용)
    // ────────────────────────────────────────────────────────────────

    static Sprite CreateGrassSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float scale = 4f, ox = Random.Range(0f, 100f), oy = Random.Range(0f, 100f);

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float nx = (float)x / size * scale + ox;
            float ny = (float)y / size * scale + oy;
            float n = Mathf.PerlinNoise(nx, ny);
            float d = Mathf.PerlinNoise(nx * 3f, ny * 3f) * 0.3f;
            float t = n * 0.7f + d;

            Color c = t < 0.4f
                ? Color.Lerp(new Color(0.13f, 0.42f, 0.10f), new Color(0.22f, 0.58f, 0.16f), t / 0.4f)
                : Color.Lerp(new Color(0.22f, 0.58f, 0.16f), new Color(0.34f, 0.70f, 0.22f), (t - 0.4f) / 0.6f);
            if (n > 0.72f && d > 0.18f) c = Color.Lerp(c, new Color(0.45f, 0.80f, 0.25f), 0.4f);
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return SpriteFromTex(tex);
    }

    static Sprite CreateDirtSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float scale = 5f, ox = Random.Range(0f, 100f), oy = Random.Range(0f, 100f);

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float nx = (float)x / size * scale + ox;
            float ny = (float)y / size * scale + oy;
            float n  = Mathf.PerlinNoise(nx, ny);
            float d  = Mathf.PerlinNoise(nx * 4f, ny * 4f) * 0.25f;
            float tr = Mathf.PerlinNoise(nx * 0.8f, ny * 6f) * 0.15f;
            float t  = n * 0.6f + d + tr;

            Color c;
            if      (t < 0.35f) c = Color.Lerp(new Color(0.38f,0.23f,0.08f), new Color(0.55f,0.36f,0.14f), t/0.35f);
            else if (t < 0.65f) c = Color.Lerp(new Color(0.55f,0.36f,0.14f), new Color(0.70f,0.52f,0.26f), (t-0.35f)/0.30f);
            else                c = Color.Lerp(new Color(0.70f,0.52f,0.26f), new Color(0.80f,0.65f,0.38f), (t-0.65f)/0.35f);
            if (n > 0.68f && d < 0.08f) c = Color.Lerp(c, new Color(0.60f,0.58f,0.52f), 0.25f);
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return SpriteFromTex(tex);
    }

    static Sprite SpriteFromTex(Texture2D tex)
        => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                         new Vector2(0.5f, 0.5f), tex.width);
}
