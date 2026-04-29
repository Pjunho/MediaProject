using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지별 타일 스프라이트를 여러 오픈소스 아틀라스에서 추출합니다.
///
/// ┌────────────────────────────────────────────────────────────────────┐
/// │  사용 텍스처 (모두 Resources/Map/ 에 배치)                        │
/// │  terrain.png      – LPC_Terrain 기본 아틀라스 (1024×1024, 32px)  │
/// │  terrain_atlas.png – Atlas 폴더 아틀라스     (1024×1024, 32px)  │
/// │  base_out_atlas.png – 야외 건물/바위 아틀라스 (1024×1024, 32px)  │
/// │                                                                    │
/// │  스테이지 테마                                                     │
/// │  Stage 1 (초원의 전투)                                            │
/// │    벽  : terrain_atlas 순수 초록 잔디 (ID 640·641·642·672)       │
/// │    길  : terrain Earth 흙길 (ID 676)                             │
/// │                                                                    │
/// │  Stage 2 (사막의 요새)                                            │
/// │    벽  : terrain_atlas 황금 모래 타일 (ID 352·353·384·386)       │
/// │    길  : terrain Brick Road 벽돌길 (ID 491·492·493·494)         │
/// │                                                                    │
/// │  Stage 3 (화산의 심판)                                            │
/// │    벽  : base_out 어두운 화산암·적갈색 화산바위                   │
/// │            (ID 352·353·288·289 – 보라-회색+적갈 혼합)            │
/// │    길  : terrain Red Dirt 붉은 화산 통로 (ID 103·166·167)        │
/// └────────────────────────────────────────────────────────────────────┘
///
/// 타일 ID 규칙 (모든 1024×1024, 32px 아틀라스 공통):
///   tile id N → col = N % 32,  row = N / 32  (행 0 = 이미지 상단)
///   Sprite.Create Rect.y = 1024 − (row + 1) × 32  (Unity 하단 기준)
/// </summary>
public static class TileTextureGenerator
{
    // ── 텍스처 상수 ──────────────────────────────────────────────────
    const int   TEX_W   = 1024;
    const int   TEX_H   = 1024;
    const int   TILE_SZ = 32;
    const float PPU     = 32f;
    static readonly Vector2 PIVOT = new Vector2(0.5f, 0.5f);

    // ── 텍스처 리소스 경로 ──────────────────────────────────────────
    const string TEX_TERRAIN       = "Map/terrain";
    const string TEX_TERRAIN_ATLAS = "Map/terrain_atlas";
    const string TEX_BASE_OUT      = "Map/base_out_atlas";

    // ── 스테이지별 벽 텍스처 ───────────────────────────────────────
    //   [0] 미사용
    static readonly string[] STAGE_WALL_TEX =
    {
        null,
        TEX_TERRAIN_ATLAS,   // Stage 1: terrain_atlas 순수 잔디
        TEX_TERRAIN_ATLAS,   // Stage 2: terrain_atlas 황금 모래
        TEX_BASE_OUT,        // Stage 3: base_out 화산암
    };

    // ── 스테이지별 벽 타일 ID ────────────────────────────────────────
    //
    //   Stage 1 – terrain_atlas.png 순수 초록 잔디 (R=100 G=164 B=44)
    //     640 row20,col0 · 641 row20,col1 · 642 row20,col2 · 672 row21,col0
    //
    //   Stage 2 – terrain_atlas.png 황금빛 모래 (R=212 G=178 B=43)
    //     352 row11,col0 · 353 row11,col1 · 384 row12,col0 · 386 row12,col2
    //
    //   Stage 3 – base_out_atlas.png 화산암 혼합
    //     352 row11,col0 (R= 77 G= 74 B= 93, 어두운 보라-회색 화산암)
    //     353 row11,col1 (동일 색조)
    //     288 row09,col0 (R=144 G= 66 B= 55, 적갈색 화산 바위)
    //     289 row09,col1 (동일 색조)
    //
    static readonly int[][] STAGE_WALL_IDS =
    {
        null,
        new[] { 640, 641, 642, 672 },   // Stage 1
        new[] { 352, 353, 384, 386 },   // Stage 2
        new[] { 352, 353, 288, 289 },   // Stage 3
    };

    // ── 스테이지별 길 텍스처 (모두 terrain.png) ─────────────────────
    static readonly string[] STAGE_PATH_TEX =
    {
        null,
        TEX_TERRAIN,   // Stage 1
        TEX_TERRAIN,   // Stage 2
        TEX_TERRAIN,   // Stage 3
    };

    // ── 스테이지별 길 타일 ID (terrain.png) ─────────────────────────
    //
    //   Stage 1 – Earth 흙길 (676 row21,col4)
    //   Stage 2 – Brick Road 벽돌 (491-494 row15,col11-14)
    //   Stage 3 – Red Dirt 붉은 화산 통로 (103,166,167 row3/5)
    //
    static readonly int[][] STAGE_PATH_IDS =
    {
        null,
        new[] { 676 },
        new[] { 491, 492, 493, 494 },
        new[] { 103, 166, 167 },
    };

    // ── 텍스처 캐시 ─────────────────────────────────────────────────
    static readonly Dictionary<string, Texture2D> _texCache
        = new Dictionary<string, Texture2D>();

    // ── 스프라이트 캐시 [stageIdx][variantIdx] ───────────────────────
    static readonly Sprite[][] _wallCache = new Sprite[4][];
    static readonly Sprite[][] _pathCache = new Sprite[4][];

    // 절차적 폴백 캐시
    static Sprite _grassFallback;
    static Sprite _dirtFallback;

    // ────────────────────────────────────────────────────────────────
    //  공개 API
    // ────────────────────────────────────────────────────────────────

    /// <summary>스테이지에 맞는 벽(미로 벽) 스프라이트를 반환합니다.</summary>
    public static Sprite GetWallSprite(int stageIndex, int variant = 0)
    {
        int idx = ClampStage(stageIndex);

        if (_wallCache[idx] == null)
        {
            string texPath = STAGE_WALL_TEX[idx];
            _wallCache[idx] = BuildSpriteArray(texPath, STAGE_WALL_IDS[idx]);
        }

        if (_wallCache[idx] != null)
            return _wallCache[idx][variant % _wallCache[idx].Length];

        return GetFallback(ref _grassFallback, CreateGrassSprite);
    }

    /// <summary>스테이지에 맞는 길(미로 통로) 스프라이트를 반환합니다.</summary>
    public static Sprite GetPathSprite(int stageIndex, int variant = 0)
    {
        int idx = ClampStage(stageIndex);

        if (_pathCache[idx] == null)
        {
            string texPath = STAGE_PATH_TEX[idx];
            _pathCache[idx] = BuildSpriteArray(texPath, STAGE_PATH_IDS[idx]);
        }

        if (_pathCache[idx] != null)
            return _pathCache[idx][variant % _pathCache[idx].Length];

        return GetFallback(ref _dirtFallback, CreateDirtSprite);
    }

    // ── 하위 호환 API (Stage 1 고정) ─────────────────────────────────
    public static Sprite GetGrassSprite(int variant = 0) => GetWallSprite(1, variant);
    public static Sprite GetDirtSprite()                  => GetPathSprite(1, 0);

    // ────────────────────────────────────────────────────────────────
    //  내부 로더
    // ────────────────────────────────────────────────────────────────

    static Texture2D GetOrLoadTex(string resourcePath)
    {
        if (_texCache.TryGetValue(resourcePath, out var cached))
            return cached;

        var tex = Resources.Load<Texture2D>(resourcePath);
        if (tex == null)
        {
            Debug.LogWarning(
                $"[TileTextureGenerator] {resourcePath} 로드 실패 → 절차적 폴백 사용.\n" +
                "해결: Inspector에서 해당 PNG의 Texture Type을 'Default'로 설정하세요.");
        }
        else
        {
            tex.filterMode = FilterMode.Point;
            tex.wrapMode   = TextureWrapMode.Clamp;
        }

        _texCache[resourcePath] = tex;
        return tex;
    }

    /// <summary>타일 ID 배열 → Sprite 배열로 변환 (캐시용)</summary>
    static Sprite[] BuildSpriteArray(string texPath, int[] ids)
    {
        if (ids == null || ids.Length == 0) return null;
        var tex = GetOrLoadTex(texPath);
        if (tex == null) return null;

        var arr = new Sprite[ids.Length];
        for (int i = 0; i < ids.Length; i++)
            arr[i] = Sprite.Create(tex, TileRect(ids[i]), PIVOT, PPU);
        return arr;
    }

    /// <summary>타일 ID → Sprite.Create 용 Rect (y = 텍스처 하단 기준)</summary>
    static Rect TileRect(int id)
    {
        int col = id % (TEX_W / TILE_SZ);
        int row = id / (TEX_W / TILE_SZ);
        float x = col * TILE_SZ;
        float y = TEX_H - (row + 1) * TILE_SZ;
        return new Rect(x, y, TILE_SZ, TILE_SZ);
    }

    static int ClampStage(int s) => (s >= 1 && s <= 3) ? s : 1;

    static Sprite GetFallback(ref Sprite cache, System.Func<Sprite> creator)
    {
        if (cache == null) cache = creator();
        return cache;
    }

    // ────────────────────────────────────────────────────────────────
    //  절차적 폴백 (아틀라스 로드 실패 시 자동 사용)
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
