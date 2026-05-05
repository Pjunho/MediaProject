using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지별 타일 스프라이트를 terrain_atlas.png (1024×1024, 32px 타일) 단일 아틀라스에서 추출합니다.
/// 각 스테이지는 terrain_atlas 내 별도 구역을 사용하여 시각적 통일감을 유지합니다.
///
/// 타일 ID 규칙:
///   id = col + row * 32   (col = 0~31, row = 0~31)
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
    // 전 스테이지 terrain_atlas 단일 아틀라스 사용 (1024×1024 확인됨)
    const string TEX_TERRAIN_ATLAS = "Map/terrain_atlas";

    const int MAX_STAGE = 5;

    // ── 스테이지별 벽 텍스처 (전부 terrain_atlas) ──────────────────
    static readonly string[] STAGE_WALL_TEX =
    {
        null,
        TEX_TERRAIN_ATLAS,   // Stage 1: 초원 잔디
        TEX_TERRAIN_ATLAS,   // Stage 2: 사막 모래
        TEX_TERRAIN_ATLAS,   // Stage 3: 화산 용암암
        TEX_TERRAIN_ATLAS,   // Stage 4: 어둠의 미궁
        TEX_TERRAIN_ATLAS,   // Stage 5: 최후의 요새
    };

    // ── 스테이지별 벽 타일 ID (terrain_atlas 기준) ─────────────────
    //
    //   Stage 1 – 잔디 지대  (row 20-22, col 0-2 / IDs 640-706)
    //   Stage 2 – 사막 모래  (row 11-13, col 0-2 / IDs 352-418)
    //   Stage 3 – 화산 암반  (row 0-1, col 0-3  / IDs 0-35  → 매우 어두운 동굴암)
    //   Stage 4 – 어두운 석조 (row 4-6, col 0-2 / IDs 128-194)
    //   Stage 5 – 요새 석벽  (row 14-15, col 0-3 / IDs 448-483)
    //
    static readonly int[][] STAGE_WALL_IDS =
    {
        null,
        new[] { 640, 641, 642, 672, 673, 674, 704, 705 },   // Stage 1: 잔디 (기존 동작 확인됨)
        new[] { 352, 353, 354, 384, 385, 386, 416, 417 },   // Stage 2: 모래 (기존 동작 확인됨)
        new[] { 0, 1, 2, 3, 32, 33, 34, 35 },               // Stage 3: 화산 암반 (어두운 상단 구역)
        new[] { 128, 129, 130, 160, 161, 162, 192, 193 },   // Stage 4: 어두운 석조
        new[] { 448, 449, 450, 480, 481, 482, 512, 513 },   // Stage 5: 요새 석벽
    };

    // ── 길 타일: 전부 절차적 생성 (GetConnectedPathSprite) ──────────
    // 아래 PATH_TEX/IDS는 폴백 전용 (기본적으로 사용 안 됨)
    static readonly string[] STAGE_PATH_TEX =
    {
        null,
        TEX_TERRAIN_ATLAS, TEX_TERRAIN_ATLAS, TEX_TERRAIN_ATLAS,
        TEX_TERRAIN_ATLAS, TEX_TERRAIN_ATLAS,
    };

    static readonly int[][] STAGE_PATH_IDS =
    {
        null,
        new[] { 676, 677, 708, 709 },
        new[] { 354, 355, 386, 387 },
        new[] { 66, 67, 68, 98, 99 },
        new[] { 130, 131, 162, 163 },
        new[] { 450, 451, 482, 483 },
    };

    // ── 장식 타일 (전부 terrain_atlas, 낮은 배치율로 사용) ─────────
    static readonly string[] STAGE_DECOR_TEX =
    {
        null,
        TEX_TERRAIN_ATLAS,   // Stage 1
        TEX_TERRAIN_ATLAS,   // Stage 2
        TEX_TERRAIN_ATLAS,   // Stage 3
        TEX_TERRAIN_ATLAS,   // Stage 4
        TEX_TERRAIN_ATLAS,   // Stage 5
    };

    // 장식 ID: 각 스테이지 벽 구역 인근 타일 사용
    static readonly int[][] STAGE_DECOR_IDS =
    {
        null,
        new[] { 707, 708, 709, 739, 740, 741 },   // Stage 1: 잔디 구역 우측
        new[] { 355, 356, 387, 388, 419, 420 },   // Stage 2: 사막 구역 인근
        new[] { 66, 67, 68, 98, 99, 100 },        // Stage 3: 용암/화산 구역
        new[] { 130, 131, 162, 163, 194, 195 },   // Stage 4: 어두운 석조 인근
        new[] { 450, 451, 482, 483, 514, 515 },   // Stage 5: 요새 구역 인근
    };

    // ── 경계 블렌드 색상 ─────────────────────────────────────────────
    static readonly Color[] STAGE_EDGE_COLORS =
    {
        Color.clear,
        new Color(0.18f, 0.50f, 0.13f, 0.74f),   // Stage 1: 초록
        new Color(0.83f, 0.63f, 0.25f, 0.66f),   // Stage 2: 모래색
        new Color(0.55f, 0.12f, 0.05f, 0.78f),   // Stage 3: 짙은 적색 (용암 크러스트)
        new Color(0.15f, 0.17f, 0.28f, 0.74f),   // Stage 4: 어두운 청회색
        new Color(0.33f, 0.34f, 0.37f, 0.70f),   // Stage 5: 회색
    };

    // ── 카메라 배경 색상 ─────────────────────────────────────────────
    static readonly Color[] STAGE_BACKDROP_COLORS =
    {
        Color.black,
        new Color(0.33f, 0.55f, 0.23f),          // Stage 1: 초원 초록
        new Color(0.66f, 0.53f, 0.27f),          // Stage 2: 모래색
        new Color(0.06f, 0.04f, 0.03f),          // Stage 3: 거의 검정 (용암 지하)
        new Color(0.10f, 0.11f, 0.18f),          // Stage 4: 어두운 청회
        new Color(0.23f, 0.24f, 0.27f),          // Stage 5: 회색
    };

    // ── 벽 타일 기저 색상 ────────────────────────────────────────────
    static readonly Color[] STAGE_GROUND_BASE_COLORS =
    {
        Color.black,
        new Color(0.28f, 0.55f, 0.17f),          // Stage 1: 초록
        new Color(0.70f, 0.58f, 0.30f),          // Stage 2: 모래
        new Color(0.09f, 0.06f, 0.05f),          // Stage 3: 매우 어두운 화산암
        new Color(0.12f, 0.13f, 0.20f),          // Stage 4: 어두운 청회
        new Color(0.24f, 0.25f, 0.27f),          // Stage 5: 회색
    };

    // ── 길 타일 주 색상 (용암/흙길 등) ──────────────────────────────
    static readonly Color[] STAGE_PATH_MAIN_COLORS =
    {
        Color.black,
        new Color(0.57f, 0.36f, 0.15f),          // Stage 1: 흙길
        new Color(0.76f, 0.63f, 0.35f),          // Stage 2: 모래길
        new Color(0.92f, 0.44f, 0.06f),          // Stage 3: 밝은 오렌지 용암 ★
        new Color(0.25f, 0.24f, 0.33f),          // Stage 4: 어두운 보라회
        new Color(0.48f, 0.47f, 0.43f),          // Stage 5: 석재
    };

    // ── 길 테두리 어두운 색상 ────────────────────────────────────────
    static readonly Color[] STAGE_PATH_EDGE_DARK_COLORS =
    {
        Color.black,
        new Color(0.28f, 0.18f, 0.08f),          // Stage 1
        new Color(0.50f, 0.40f, 0.21f),          // Stage 2
        new Color(0.52f, 0.14f, 0.04f),          // Stage 3: 어두운 적갈 크러스트 ★
        new Color(0.11f, 0.11f, 0.17f),          // Stage 4
        new Color(0.27f, 0.27f, 0.26f),          // Stage 5
    };

    // ── 텍스처 캐시 ─────────────────────────────────────────────────
    static readonly Dictionary<string, Texture2D> _texCache
        = new Dictionary<string, Texture2D>();

    // ── 스프라이트 캐시 ──────────────────────────────────────────────
    static readonly Sprite[][] _wallCache          = new Sprite[MAX_STAGE + 1][];
    static readonly Sprite[][] _pathCache          = new Sprite[MAX_STAGE + 1][];
    static readonly Sprite[][] _decorCache         = new Sprite[MAX_STAGE + 1][];
    static readonly Sprite[][] _edgeCache          = new Sprite[MAX_STAGE + 1][];
    static readonly Sprite[][] _connectedPathCache = new Sprite[MAX_STAGE + 1][];

    // 절차적 폴백
    static Sprite _grassFallback;
    static Sprite _dirtFallback;

    // ────────────────────────────────────────────────────────────────
    //  공개 API
    // ────────────────────────────────────────────────────────────────

    public static Sprite GetWallSprite(int stageIndex, int variant = 0)
    {
        int idx = ClampStage(stageIndex);

        if (_wallCache[idx] == null)
            _wallCache[idx] = BuildSpriteArray(STAGE_WALL_TEX[idx], STAGE_WALL_IDS[idx]);

        if (_wallCache[idx] != null)
            return _wallCache[idx][variant % _wallCache[idx].Length];

        return GetFallback(ref _grassFallback, CreateGrassSprite);
    }

    public static Sprite GetPathSprite(int stageIndex, int variant = 0)
    {
        int idx = ClampStage(stageIndex);

        if (_pathCache[idx] == null)
            _pathCache[idx] = BuildSpriteArray(STAGE_PATH_TEX[idx], STAGE_PATH_IDS[idx]);

        if (_pathCache[idx] != null)
            return _pathCache[idx][variant % _pathCache[idx].Length];

        return GetFallback(ref _dirtFallback, CreateDirtSprite);
    }

    public static Sprite GetConnectedPathSprite(int stageIndex, int connectionMask, int variant = 0)
    {
        int idx = ClampStage(stageIndex);
        connectionMask &= 0x0F;

        if (_connectedPathCache[idx] == null)
        {
            _connectedPathCache[idx] = new Sprite[16];
            for (int i = 0; i < 16; i++)
            {
                // Stage 3는 용암 전용 렌더링
                _connectedPathCache[idx][i] = (idx == 3)
                    ? CreateLavaPathSprite(
                        STAGE_GROUND_BASE_COLORS[idx],
                        STAGE_PATH_MAIN_COLORS[idx],
                        STAGE_PATH_EDGE_DARK_COLORS[idx],
                        i, variant)
                    : CreateConnectedPathSprite(
                        STAGE_GROUND_BASE_COLORS[idx],
                        STAGE_PATH_MAIN_COLORS[idx],
                        STAGE_PATH_EDGE_DARK_COLORS[idx],
                        i, idx, variant);
            }
        }

        return _connectedPathCache[idx][connectionMask];
    }

    public static Sprite GetDecorSprite(int stageIndex, int variant = 0)
    {
        int idx = ClampStage(stageIndex);

        if (_decorCache[idx] == null)
            _decorCache[idx] = BuildSpriteArray(STAGE_DECOR_TEX[idx], STAGE_DECOR_IDS[idx]);

        if (_decorCache[idx] != null)
            return _decorCache[idx][Mathf.Abs(variant) % _decorCache[idx].Length];

        return null;
    }

    public static Sprite GetPathEdgeSprite(int stageIndex, int edgeMask)
    {
        int idx = ClampStage(stageIndex);
        edgeMask &= 0x0F;

        if (_edgeCache[idx] == null)
        {
            _edgeCache[idx] = new Sprite[16];
            for (int i = 0; i < 16; i++)
                _edgeCache[idx][i] = CreatePathEdgeSprite(STAGE_EDGE_COLORS[idx], i);
        }

        return _edgeCache[idx][edgeMask];
    }

    public static Color GetBackdropColor(int stageIndex)
        => STAGE_BACKDROP_COLORS[ClampStage(stageIndex)];

    // 하위 호환 API
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

    static Sprite[] BuildSpriteArray(string texPath, int[] ids)
    {
        if (ids == null || ids.Length == 0) return null;
        var tex = GetOrLoadTex(texPath);
        if (tex == null) return null;

        var arr = new Sprite[ids.Length];
        for (int i = 0; i < ids.Length; i++)
            arr[i] = Sprite.Create(tex, TileRect(ids[i], tex.width, tex.height), PIVOT, PPU);
        return arr;
    }

    /// <summary>타일 ID → Sprite.Create 용 Rect (실제 텍스처 크기 기반)</summary>
    static Rect TileRect(int id, int texW, int texH)
    {
        int cols = texW / TILE_SZ;
        int col  = id % cols;
        int row  = id / cols;
        float x  = col * TILE_SZ;
        float y  = texH - (row + 1) * TILE_SZ;
        // 범위 클램프
        x = Mathf.Clamp(x, 0, texW - TILE_SZ);
        y = Mathf.Clamp(y, 0, texH - TILE_SZ);
        return new Rect(x, y, TILE_SZ, TILE_SZ);
    }

    static int ClampStage(int s) => (s >= 1 && s <= MAX_STAGE) ? s : 1;

    static Sprite GetFallback(ref Sprite cache, System.Func<Sprite> creator)
    {
        if (cache == null) cache = creator();
        return cache;
    }

    // ────────────────────────────────────────────────────────────────
    //  절차적 폴백 스프라이트
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
            float n  = Mathf.PerlinNoise(nx, ny);
            float d  = Mathf.PerlinNoise(nx * 3f, ny * 3f) * 0.3f;
            float t  = n * 0.7f + d;
            Color c  = t < 0.4f
                ? Color.Lerp(new Color(0.13f, 0.42f, 0.10f), new Color(0.22f, 0.58f, 0.16f), t / 0.4f)
                : Color.Lerp(new Color(0.22f, 0.58f, 0.16f), new Color(0.34f, 0.70f, 0.22f), (t - 0.4f) / 0.6f);
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
            float t  = n * 0.6f + d;
            Color c;
            if      (t < 0.35f) c = Color.Lerp(new Color(0.38f,0.23f,0.08f), new Color(0.55f,0.36f,0.14f), t/0.35f);
            else if (t < 0.65f) c = Color.Lerp(new Color(0.55f,0.36f,0.14f), new Color(0.70f,0.52f,0.26f), (t-0.35f)/0.30f);
            else                c = Color.Lerp(new Color(0.70f,0.52f,0.26f), new Color(0.80f,0.65f,0.38f), (t-0.65f)/0.35f);
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return SpriteFromTex(tex);
    }

    static Sprite CreatePathEdgeSprite(Color edgeColor, int mask)
    {
        const int size  = 64;
        const int edge  = 8;
        const int fringe = 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color light = Color.Lerp(edgeColor, Color.white, 0.14f);
        light.a = Mathf.Min(0.55f, edgeColor.a);

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            Color c = clear;

            bool top    = (mask & 1) != 0 && y >= size - edge;
            bool right  = (mask & 2) != 0 && x >= size - edge;
            bool bottom = (mask & 4) != 0 && y < edge;
            bool left   = (mask & 8) != 0 && x < edge;

            if (top || right || bottom || left)
            {
                c = edgeColor;
                float checker = ((x / 2 + y / 3) % 3 == 0) ? 0.10f : 0f;
                c = Color.Lerp(c, light, checker);
            }

            bool innerTop    = (mask & 1) != 0 && y >= size - edge - fringe && y < size - edge;
            bool innerRight  = (mask & 2) != 0 && x >= size - edge - fringe && x < size - edge;
            bool innerBottom = (mask & 4) != 0 && y >= edge && y < edge + fringe;
            bool innerLeft   = (mask & 8) != 0 && x >= edge && x < edge + fringe;

            if (innerTop || innerRight || innerBottom || innerLeft)
            {
                Color dark = new Color(0f, 0f, 0f, 0.18f);
                c = Color.Lerp(c, dark, c.a > 0f ? 0.20f : 1f);
            }

            tex.SetPixel(x, y, c);
        }

        tex.Apply();
        return SpriteFromTex(tex);
    }

    // ── Stage 1 전용: 유기적 잔디 경계 처리 ─────────────────────────
    static Sprite CreateConnectedPathSprite(
        Color ground, Color path, Color pathEdge,
        int mask, int stageIndex, int variant)
    {
        if (stageIndex == 1)
            return CreateOrganicGrassPathSprite(ground, path, pathEdge, mask);

        const int size     = 64;
        const int roadHalf = 15;
        const int edgeHalf = 19;
        const int center   = size / 2;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        bool top    = (mask & 1) != 0;
        bool right  = (mask & 2) != 0;
        bool bottom = (mask & 4) != 0;
        bool left   = (mask & 8) != 0;
        if (mask == 0) top = right = bottom = left = true;

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            int dx = x - center;
            int dy = y - center;

            float groundNoise = HashNoise(stageIndex, variant, x, y, 5);
            Color c = Color.Lerp(ground * 0.88f, ground * 1.12f, groundNoise);

            bool inCore =
                Mathf.Abs(dx) <= roadHalf && Mathf.Abs(dy) <= roadHalf;
            bool inConnector =
                (top    && Mathf.Abs(dx) <= roadHalf && dy >= -roadHalf) ||
                (right  && Mathf.Abs(dy) <= roadHalf && dx >= -roadHalf) ||
                (bottom && Mathf.Abs(dx) <= roadHalf && dy <= roadHalf)  ||
                (left   && Mathf.Abs(dy) <= roadHalf && dx <= roadHalf);
            bool inEdgeCore =
                Mathf.Abs(dx) <= edgeHalf && Mathf.Abs(dy) <= edgeHalf;
            bool inEdgeConnector =
                (top    && Mathf.Abs(dx) <= edgeHalf && dy >= -edgeHalf) ||
                (right  && Mathf.Abs(dy) <= edgeHalf && dx >= -edgeHalf) ||
                (bottom && Mathf.Abs(dx) <= edgeHalf && dy <= edgeHalf)  ||
                (left   && Mathf.Abs(dy) <= edgeHalf && dx <= edgeHalf);

            if (inEdgeCore || inEdgeConnector)
                c = Color.Lerp(c, pathEdge, 0.88f);

            if (inCore || inConnector)
            {
                float pathNoise = HashNoise(stageIndex, variant, x, y, 17);
                c = Color.Lerp(path * 0.82f, path * 1.18f, pathNoise);

                bool pebble = HashNoise(stageIndex, variant, x / 2, y / 2, 29) > 0.88f;
                if (pebble) c = Color.Lerp(c, Color.white, 0.12f);
            }

            tex.SetPixel(x, y, ClampColor(c));
        }

        tex.Apply();
        return SpriteFromTex(tex);
    }

    // ── Stage 3 전용: 용암 흐름 렌더링 ─────────────────────────────
    /// <summary>
    /// 중심부 → 밝은 황색 오렌지 (1000°C+ 용암),
    /// 가장자리 → 어두운 적갈색 (굳어가는 크러스트)
    /// </summary>
    static Sprite CreateLavaPathSprite(
        Color ground, Color lavaMain, Color lavaCrust,
        int mask, int variant)
    {
        const int size     = 64;
        const int roadHalf = 15;
        const int edgeHalf = 19;
        const int center   = size / 2;

        // 용암 중심 밝은 황색-오렌지 (열기 표현)
        Color lavaCenter = new Color(1.00f, 0.82f, 0.14f);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        bool top    = (mask & 1) != 0;
        bool right  = (mask & 2) != 0;
        bool bottom = (mask & 4) != 0;
        bool left   = (mask & 8) != 0;
        if (mask == 0) top = right = bottom = left = true;

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            int dx = x - center;
            int dy = y - center;

            // 화산 암반 배경
            float groundNoise = HashNoise(3, variant, x, y, 5);
            Color c = Color.Lerp(ground * 0.80f, ground * 1.20f, groundNoise);

            bool inCore =
                Mathf.Abs(dx) <= roadHalf && Mathf.Abs(dy) <= roadHalf;
            bool inConnector =
                (top    && Mathf.Abs(dx) <= roadHalf && dy >= -roadHalf) ||
                (right  && Mathf.Abs(dy) <= roadHalf && dx >= -roadHalf) ||
                (bottom && Mathf.Abs(dx) <= roadHalf && dy <= roadHalf)  ||
                (left   && Mathf.Abs(dy) <= roadHalf && dx <= roadHalf);
            bool inEdgeCore =
                Mathf.Abs(dx) <= edgeHalf && Mathf.Abs(dy) <= edgeHalf;
            bool inEdgeConnector =
                (top    && Mathf.Abs(dx) <= edgeHalf && dy >= -edgeHalf) ||
                (right  && Mathf.Abs(dy) <= edgeHalf && dx >= -edgeHalf) ||
                (bottom && Mathf.Abs(dx) <= edgeHalf && dy <= edgeHalf)  ||
                (left   && Mathf.Abs(dy) <= edgeHalf && dx <= edgeHalf);

            // 크러스트 테두리 (어두운 적갈)
            if (inEdgeCore || inEdgeConnector)
                c = Color.Lerp(c, lavaCrust, 0.88f);

            // 용암 본체: 중심에 가까울수록 밝은 황색
            if (inCore || inConnector)
            {
                // 길 단면 중심으로부터 거리 계산 (연결 방향 기준)
                float distX = Mathf.Abs(dx);
                float distY = Mathf.Abs(dy);
                float crossDist;
                if ((top || bottom) && !(right || left))
                    crossDist = distX;                      // 수직 통로: 가로 거리
                else if ((right || left) && !(top || bottom))
                    crossDist = distY;                      // 수평 통로: 세로 거리
                else
                    crossDist = Mathf.Min(distX, distY);   // 교차점: 최소값

                float centerFactor = 1f - Mathf.Clamp01((float)crossDist / roadHalf);
                centerFactor = centerFactor * centerFactor; // 제곱 → 중심부만 더 밝게

                float pathNoise = HashNoise(3, variant, x, y, 17);
                Color lavaColor = Color.Lerp(lavaMain, lavaCenter, centerFactor * 0.72f);
                lavaColor = Color.Lerp(lavaColor * 0.85f, lavaColor * 1.15f, pathNoise);

                // 간헐적 어두운 크러스트 반점 (식어가는 표면)
                bool crust = HashNoise(3, variant, x / 3, y / 3, 41) > 0.84f;
                if (crust)
                    lavaColor = Color.Lerp(lavaColor, lavaCrust * 1.3f, 0.30f);

                c = lavaColor;
            }

            tex.SetPixel(x, y, ClampColor(c));
        }

        tex.Apply();
        return SpriteFromTex(tex);
    }

    // ── Stage 1 유기적 잔디 경계 ────────────────────────────────────
    static Sprite CreateOrganicGrassPathSprite(Color ground, Color path, Color pathEdge, int mask)
    {
        const int size    = 64;
        const float center   = 31.5f;
        const float roadHalf = 13.5f;
        const float edgeHalf = 18f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        bool top    = (mask & 1) != 0;
        bool right  = (mask & 2) != 0;
        bool bottom = (mask & 4) != 0;
        bool left   = (mask & 8) != 0;
        if (mask == 0) top = right = bottom = left = true;

        Vector2 cpt       = new Vector2(center, center);
        Vector2 topPt     = new Vector2(center, size + edgeHalf);
        Vector2 rightPt   = new Vector2(size + edgeHalf, center);
        Vector2 bottomPt  = new Vector2(center, -edgeHalf);
        Vector2 leftPt    = new Vector2(-edgeHalf, center);

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            Vector2 p          = new Vector2(x + 0.5f, y + 0.5f);
            float groundNoise  = HashNoise(1, mask, x, y, 5);
            Color color        = Color.Lerp(ground * 0.90f, ground * 1.10f, groundNoise);

            float dist = Vector2.Distance(p, cpt);
            if (top)    dist = Mathf.Min(dist, DistanceToSegment(p, cpt, topPt));
            if (right)  dist = Mathf.Min(dist, DistanceToSegment(p, cpt, rightPt));
            if (bottom) dist = Mathf.Min(dist, DistanceToSegment(p, cpt, bottomPt));
            if (left)   dist = Mathf.Min(dist, DistanceToSegment(p, cpt, leftPt));

            float rough = (HashNoise(1, mask, x / 2, y / 2, 23) - 0.5f) * 4.8f;
            rough += Mathf.Sin((x + mask * 7) * 0.33f) * 0.9f;
            rough += Mathf.Sin((y + mask * 11) * 0.27f) * 0.7f;

            float organicRoad = roadHalf + rough;
            float organicEdge = edgeHalf + rough * 0.75f;

            if (dist < organicEdge)
            {
                float edgeT = Mathf.InverseLerp(organicEdge, organicRoad, dist);
                color = Color.Lerp(color, pathEdge, Mathf.Lerp(0.55f, 0.95f, edgeT));
            }

            if (dist < organicRoad)
            {
                float pathNoise = HashNoise(1, mask, x, y, 37);
                Color pathColor = Color.Lerp(path * 0.82f, path * 1.14f, pathNoise);
                float centerWear = Mathf.Clamp01(1f - dist / (organicRoad + 0.01f));
                pathColor = Color.Lerp(pathColor, new Color(0.70f, 0.50f, 0.25f), centerWear * 0.16f);
                bool pebble = HashNoise(1, mask, x / 2, y / 2, 47) > 0.91f;
                if (pebble) pathColor = Color.Lerp(pathColor, Color.white, 0.11f);
                color = pathColor;
            }

            tex.SetPixel(x, y, ClampColor(color));
        }

        tex.Apply();
        return SpriteFromTex(tex);
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t    = Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, Vector2.Dot(ab, ab));
        t = Mathf.Clamp01(t);
        return Vector2.Distance(p, a + ab * t);
    }

    static float HashNoise(int stageIndex, int variant, int x, int y, int salt)
    {
        uint h = (uint)(stageIndex * 374761393 + variant * 668265263
                      + x * 2246822519u + y * 3266489917u + salt * 1274126177u);
        h ^= h >> 13;
        h *= 1274126177u;
        return (h & 0x00FFFFFF) / 16777215f;
    }

    static Color ClampColor(Color c)
    {
        c.r = Mathf.Clamp01(c.r);
        c.g = Mathf.Clamp01(c.g);
        c.b = Mathf.Clamp01(c.b);
        c.a = 1f;
        return c;
    }

    static Sprite SpriteFromTex(Texture2D tex)
        => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                         new Vector2(0.5f, 0.5f), tex.width);
}
