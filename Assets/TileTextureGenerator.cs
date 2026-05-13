using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지별 타일 스프라이트를 오픈소스 아틀라스에서 추출합니다.
///
/// ▶ 아틀라스 규칙
///   terrain_atlas.png : 1024×1024, 32px 타일 (Stage 1 전용)
///   terrain.png       : 실제 크기 자동 인식, 32px 타일 (Stage 2·3 공통)
///
/// ▶ 타일 ID 규칙 (행 0 = 이미지 상단)
///   id = col + row × cols     cols = texW / 32
///   Sprite y = texH − (row + 1) × 32
///
/// ▶ 스테이지별 테마
///   Stage 1 – 초원의 전투   : terrain_atlas 잔디 구역, 흙길
///   Stage 2 – 어둠의 동굴   : terrain.png 어두운 암석, 동굴 바닥 + 화면 비네트
///   Stage 3 – 화산의 심판   : terrain.png 용암(벽) + 어두운 화산암 길, 체력 감소
/// </summary>
public static class TileTextureGenerator
{
    // ── 텍스처 상수 ──────────────────────────────────────────────────
    const int   TILE_SZ = 32;
    const float PPU     = 32f;
    static readonly Vector2 PIVOT = new Vector2(0.5f, 0.5f);

    // ── 텍스처 리소스 경로 ──────────────────────────────────────────
    const string TEX_TERRAIN_ATLAS = "Map/terrain_atlas";  // 1024×1024 (Stage 1)
    const string TEX_TERRAIN       = "Map/terrain";         // Stage 2·3 공용

    // 스테이지별 전용 스프라이트 시트 (Road=길, NRoad=배경벽)
    const string TEX_STAGE1_ROAD  = "Map/Stage1_Road_Map";
    const string TEX_STAGE1_NROAD = "Map/Stage1_NRoad_Map";
    const string TEX_STAGE2_ROAD  = "Map/Stage2_Road_Map";
    const string TEX_STAGE2_NROAD = "Map/Stage2_NRoad_Map";
    const string TEX_STAGE3_ROAD  = "Map/Stage3_Road_Map";
    const string TEX_STAGE3_NROAD = "Map/Stage3_NRoad_Map";

    const int MAX_STAGE = 5;

    // ── 벽 타일 텍스처 ──────────────────────────────────────────────
    static readonly string[] STAGE_WALL_TEX =
    {
        null,
        TEX_TERRAIN_ATLAS,  // Stage 1: 초원 — terrain_atlas 잔디 구역
        TEX_TERRAIN,        // Stage 2: 동굴 — terrain.png 어두운 암석
        TEX_TERRAIN,        // Stage 3: 화산 — terrain.png 용암 타일 (WALL = 용암)
        TEX_TERRAIN_ATLAS,  // Stage 4
        TEX_TERRAIN_ATLAS,  // Stage 5
    };

    // ── 벽 타일 ID ──────────────────────────────────────────────────
    //
    //   ▶ terrain_atlas.png (1024 wide, 32 cols)
    //     Stage 1 wall : row 20-22, col 0-2  → IDs 640-706
    //
    //   ▶ terrain.png (실제 width 자동 인식, 32px 타일)
    //     terrain.png 주요 32px 타일 좌표:
    //       row 2~4, col 9~11  : 어두운 회색 지대
    //       row 0~2, col 15~16 : 용암 지대
    //
    static readonly int[][] STAGE_WALL_IDS =
    {
        null,
        new[] { 640, 641, 642, 672, 673, 674, 704, 705 },  // Stage 1 (terrain_atlas)
        new[] { 1, 2, 1, 2, 1, 2 },                        // Stage 2 (어두운 암석, row 0)
        new[] { 16 },                                        // Stage 3 (terrain.png 순수 용암 바닥)
        new[] { 128, 129, 130, 160, 161, 162, 192, 193 },  // Stage 4 (terrain_atlas)
        new[] { 448, 449, 450, 480, 481, 482, 512, 513 },  // Stage 5 (terrain_atlas)
    };

    // ── 길(Path) 타일 — 폴백용 (기본적으로 절차적 사용) ────────────
    static readonly string[] STAGE_PATH_TEX =
    {
        null,
        TEX_TERRAIN_ATLAS, TEX_TERRAIN, TEX_TERRAIN,
        TEX_TERRAIN_ATLAS, TEX_TERRAIN_ATLAS,
    };

    static readonly int[][] STAGE_PATH_IDS =
    {
        null,
        new[] { 676, 677, 708, 709 },   // Stage 1
        new[] { 1, 2 },                  // Stage 2 (dark cave floor)
        new[] { 73, 74, 105, 106, 137, 138 },  // Stage 3 (terrain.png 어두운 지대)
        new[] { 130, 131 },              // Stage 4
        new[] { 450, 451 },              // Stage 5
    };

    // ── 스테이지별 mask → 스프라이트 이름 매핑 ────────────────────────
    // (bit0=상, bit1=우, bit2=하, bit3=좌)
    static readonly string[] STAGE1_ROAD_SPRITE_NAMES = new string[16]
    {
        "Stage1_Road_Map_15", // mask  0: 고립
        "Stage1_Road_Map_7",  // mask  1: 상(↑) 끝단
        "Stage1_Road_Map_3",  // mask  2: 우(→) 끝단
        "Stage1_Road_Map_6",  // mask  3: 상+우 꺾기
        "Stage1_Road_Map_1",  // mask  4: 하(↓) 끝단
        "Stage1_Road_Map_13", // mask  5: 상+하 세로직선
        "Stage1_Road_Map_0",  // mask  6: 우+하 꺾기
        "Stage1_Road_Map_11", // mask  7: 상+우+하 T(좌열림)
        "Stage1_Road_Map_5",  // mask  8: 좌(←) 끝단
        "Stage1_Road_Map_8",  // mask  9: 상+좌 꺾기
        "Stage1_Road_Map_10", // mask 10: 좌+우 가로직선
        "Stage1_Road_Map_14", // mask 11: 상+좌+우 T(하열림)
        "Stage1_Road_Map_2",  // mask 12: 하+좌 꺾기
        "Stage1_Road_Map_12", // mask 13: 상+하+좌 T(우열림)
        "Stage1_Road_Map_9",  // mask 14: 우+하+좌 T(상열림)
        "Stage1_Road_Map_4",  // mask 15: 전체(십자)
    };
    static readonly string[] STAGE1_NROAD_SPRITE_NAMES = new string[16]
    {
        "Stage1_NRoad_Map_15", // mask  0: 인접 road 없음 (내부 벽)
        "Stage1_NRoad_Map_7",  // mask  1: 위쪽이 road
        "Stage1_NRoad_Map_3",  // mask  2: 오른쪽이 road
        "Stage1_NRoad_Map_6",  // mask  3: 위+오른쪽 road
        "Stage1_NRoad_Map_1",  // mask  4: 아래가 road
        "Stage1_NRoad_Map_13", // mask  5: 위+아래 road
        "Stage1_NRoad_Map_0",  // mask  6: 오른쪽+아래 road
        "Stage1_NRoad_Map_11", // mask  7: 위+오른쪽+아래 road
        "Stage1_NRoad_Map_5",  // mask  8: 왼쪽이 road
        "Stage1_NRoad_Map_8",  // mask  9: 위+왼쪽 road
        "Stage1_NRoad_Map_10", // mask 10: 왼쪽+오른쪽 road
        "Stage1_NRoad_Map_14", // mask 11: 위+왼쪽+오른쪽 road
        "Stage1_NRoad_Map_2",  // mask 12: 아래+왼쪽 road
        "Stage1_NRoad_Map_12", // mask 13: 위+아래+왼쪽 road
        "Stage1_NRoad_Map_9",  // mask 14: 오른쪽+아래+왼쪽 road
        "Stage1_NRoad_Map_4",  // mask 15: 사방 road
    };
    static readonly string[] STAGE2_ROAD_SPRITE_NAMES = new string[16]
    {
        "Stage2_Road_Map_15", "Stage2_Road_Map_7",  "Stage2_Road_Map_3",
        "Stage2_Road_Map_6",  "Stage2_Road_Map_1",  "Stage2_Road_Map_13",
        "Stage2_Road_Map_0",  "Stage2_Road_Map_11", "Stage2_Road_Map_5",
        "Stage2_Road_Map_8",  "Stage2_Road_Map_10", "Stage2_Road_Map_14",
        "Stage2_Road_Map_2",  "Stage2_Road_Map_12", "Stage2_Road_Map_9",
        "Stage2_Road_Map_4",
    };
    static readonly string[] STAGE2_NROAD_SPRITE_NAMES = new string[16]
    {
        "Stage2_NRoad_Map_15", "Stage2_NRoad_Map_7",  "Stage2_NRoad_Map_3",
        "Stage2_NRoad_Map_6",  "Stage2_NRoad_Map_1",  "Stage2_NRoad_Map_13",
        "Stage2_NRoad_Map_0",  "Stage2_NRoad_Map_11", "Stage2_NRoad_Map_5",
        "Stage2_NRoad_Map_8",  "Stage2_NRoad_Map_10", "Stage2_NRoad_Map_14",
        "Stage2_NRoad_Map_2",  "Stage2_NRoad_Map_12", "Stage2_NRoad_Map_9",
        "Stage2_NRoad_Map_4",
    };
    static readonly string[] STAGE3_ROAD_SPRITE_NAMES = new string[16]
    {
        "Stage3_Road_Map_15", "Stage3_Road_Map_7",  "Stage3_Road_Map_3",
        "Stage3_Road_Map_6",  "Stage3_Road_Map_1",  "Stage3_Road_Map_13",
        "Stage3_Road_Map_0",  "Stage3_Road_Map_11", "Stage3_Road_Map_5",
        "Stage3_Road_Map_8",  "Stage3_Road_Map_10", "Stage3_Road_Map_14",
        "Stage3_Road_Map_2",  "Stage3_Road_Map_12", "Stage3_Road_Map_9",
        "Stage3_Road_Map_4",
    };
    static readonly string[] STAGE3_NROAD_SPRITE_NAMES = new string[16]
    {
        "Stage3_NRoad_Map_15", "Stage3_NRoad_Map_7",  "Stage3_NRoad_Map_3",
        "Stage3_NRoad_Map_6",  "Stage3_NRoad_Map_1",  "Stage3_NRoad_Map_13",
        "Stage3_NRoad_Map_0",  "Stage3_NRoad_Map_11", "Stage3_NRoad_Map_5",
        "Stage3_NRoad_Map_8",  "Stage3_NRoad_Map_10", "Stage3_NRoad_Map_14",
        "Stage3_NRoad_Map_2",  "Stage3_NRoad_Map_12", "Stage3_NRoad_Map_9",
        "Stage3_NRoad_Map_4",
    };

    // ── 장식 타일 ────────────────────────────────────────────────────
    static readonly string[] STAGE_DECOR_TEX =
    {
        null,
        TEX_TERRAIN_ATLAS,  // Stage 1
        TEX_TERRAIN,        // Stage 2
        TEX_TERRAIN,        // Stage 3
        TEX_TERRAIN_ATLAS,  // Stage 4
        TEX_TERRAIN_ATLAS,  // Stage 5
    };

    static readonly int[][] STAGE_DECOR_IDS =
    {
        null,
        new[] { 707, 708, 709, 739, 740, 741 },   // Stage 1
        new[] { 1, 2, 1, 2 },                      // Stage 2
        new[] { 3, 4, 3, 4 },                      // Stage 3
        new[] { 130, 131, 162, 163 },              // Stage 4
        new[] { 450, 451, 482, 483 },              // Stage 5
    };

    // ── 경계 블렌드 색상 ─────────────────────────────────────────────
    static readonly Color[] STAGE_EDGE_COLORS =
    {
        Color.clear,
        new Color(0.18f, 0.50f, 0.13f, 0.74f),   // Stage 1: 초록
        new Color(0.08f, 0.08f, 0.12f, 0.85f),   // Stage 2: 매우 어두운 청회
        new Color(0.55f, 0.14f, 0.04f, 0.80f),   // Stage 3: 어두운 적갈 크러스트
        new Color(0.15f, 0.17f, 0.28f, 0.74f),   // Stage 4
        new Color(0.33f, 0.34f, 0.37f, 0.70f),   // Stage 5
    };

    // ── 카메라 배경 색상 ─────────────────────────────────────────────
    static readonly Color[] STAGE_BACKDROP_COLORS =
    {
        Color.black,
        new Color(0.33f, 0.55f, 0.23f),          // Stage 1: 초원
        new Color(0.03f, 0.03f, 0.05f),          // Stage 2: 거의 완전한 검정 (동굴)
        new Color(0.08f, 0.05f, 0.03f),          // Stage 3: 어두운 적흑 (화산지대)
        new Color(0.10f, 0.11f, 0.18f),          // Stage 4
        new Color(0.23f, 0.24f, 0.27f),          // Stage 5
    };

    // ── PATH 타일 기저 색상 (절차적 길 배경부 색) ──────────────────
    static readonly Color[] STAGE_GROUND_BASE_COLORS =
    {
        Color.black,
        new Color(0.28f, 0.55f, 0.17f),          // Stage 1: 초원
        new Color(0.06f, 0.06f, 0.08f),          // Stage 2: 동굴 어두운 배경
        new Color(0.11f, 0.08f, 0.06f),          // Stage 3: 화산 어두운 배경
        new Color(0.12f, 0.13f, 0.20f),          // Stage 4
        new Color(0.24f, 0.25f, 0.27f),          // Stage 5
    };

    // ── PATH 메인 색상 ────────────────────────────────────────────────
    //   Stage 2: 어두운 동굴 바닥 (짙은 회청)
    //   Stage 3: 어두운 화산암 길 — 용암은 '벽'(WALL)에 배치
    static readonly Color[] STAGE_PATH_MAIN_COLORS =
    {
        Color.black,
        new Color(0.57f, 0.36f, 0.15f),          // Stage 1: 흙길
        new Color(0.20f, 0.20f, 0.24f),          // Stage 2: 어두운 동굴 바닥
        new Color(0.22f, 0.21f, 0.20f),          // Stage 3: 어두운 화산암 길
        new Color(0.25f, 0.24f, 0.33f),          // Stage 4
        new Color(0.48f, 0.47f, 0.43f),          // Stage 5
    };

    // ── PATH 테두리 어두운 색 ─────────────────────────────────────────
    static readonly Color[] STAGE_PATH_EDGE_DARK_COLORS =
    {
        Color.black,
        new Color(0.28f, 0.18f, 0.08f),          // Stage 1
        new Color(0.09f, 0.09f, 0.12f),          // Stage 2: 동굴 바닥 깊은 어둠
        new Color(0.10f, 0.09f, 0.085f),         // Stage 3: 어두운 화산암 테두리
        new Color(0.11f, 0.11f, 0.17f),          // Stage 4
        new Color(0.27f, 0.27f, 0.26f),          // Stage 5
    };

    // ── 캐시 ─────────────────────────────────────────────────────────
    static readonly Dictionary<string, Texture2D> _texCache      = new();
    static readonly Sprite[][] _wallCache          = new Sprite[MAX_STAGE + 1][];
    static readonly Sprite[][] _pathCache          = new Sprite[MAX_STAGE + 1][];
    static readonly Sprite[][] _decorCache         = new Sprite[MAX_STAGE + 1][];
    static readonly Sprite[][] _edgeCache          = new Sprite[MAX_STAGE + 1][];
    static readonly Sprite[][] _connectedPathCache = new Sprite[MAX_STAGE + 1][];
    static readonly Dictionary<int, Sprite> _volcanoBlockedPathCache = new();

    // terrain.png 슬라이스 스프라이트 캐시 (이름 기반)
    static readonly Dictionary<int, Sprite> _terrainSliceCache = new();
    static bool _terrainSlicesLoaded = false;

    // 스테이지별 전용 시트 스프라이트 캐시 (이름 기반, Road+NRoad 공용)
    static readonly Dictionary<string, Sprite> _stageMapCache = new();
    static bool _stage1RoadLoaded, _stage2RoadLoaded, _stage3RoadLoaded;
    static bool _stage1NRoadLoaded, _stage2NRoadLoaded, _stage3NRoadLoaded;

    static Sprite _grassFallback;
    static Sprite _dirtFallback;

    // ────────────────────────────────────────────────────────────────
    //  공개 API
    // ────────────────────────────────────────────────────────────────

    // roadAdjacencyMask: 이웃 중 road인 방향 (bit0=상, bit1=우, bit2=하, bit3=좌)
    public static Sprite GetWallSprite(int stageIndex, int roadAdjacencyMask, int variant)
    {
        int idx = ClampStage(stageIndex);
        roadAdjacencyMask &= 0x0F;
        EnsureStageNRoadLoaded(idx);
        Sprite sp = GetStageNRoadSpriteVariant(idx, roadAdjacencyMask, variant);
        if (sp != null) return sp;
        if (_wallCache[idx] == null)
            _wallCache[idx] = BuildSpriteArray(STAGE_WALL_TEX[idx], STAGE_WALL_IDS[idx]);
        if (_wallCache[idx] != null)
            return _wallCache[idx][Mathf.Abs(variant) % _wallCache[idx].Length];
        return GetFallback(ref _grassFallback, CreateGrassSprite);
    }

    public static Sprite GetWallSprite(int stageIndex, int variant = 0)
        => GetWallSprite(stageIndex, 0, variant);

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

        string[] roadNames = GetStageRoadSpriteNames(idx);
        if (roadNames != null)
        {
            EnsureStageRoadLoaded(idx);
            Sprite sp = GetStageRoadSpriteVariant(idx, connectionMask, variant);
            if (sp != null) return sp;
        }

        if (_connectedPathCache[idx] == null)
        {
            _connectedPathCache[idx] = new Sprite[16];
            for (int i = 0; i < 16; i++)
            {
                _connectedPathCache[idx][i] = (idx == 3)
                    ? CreateVolcanoConnectedPathSprite(i, variant)
                    : (idx == 1)
                    ? CreateOrganicGrassPathSprite(
                        STAGE_GROUND_BASE_COLORS[idx],
                        STAGE_PATH_MAIN_COLORS[idx],
                        STAGE_PATH_EDGE_DARK_COLORS[idx], i)
                    : CreateConnectedPathSprite(
                        STAGE_GROUND_BASE_COLORS[idx],
                        STAGE_PATH_MAIN_COLORS[idx],
                        STAGE_PATH_EDGE_DARK_COLORS[idx],
                        i, idx, variant);
            }
        }
        return _connectedPathCache[idx][connectionMask];
    }

    public static Sprite GetVolcanoBlockedPathSprite(int variant = 0)
        => GetVolcanoBlockedPathSprite(0x0F, variant);

    public static Sprite GetVolcanoBlockedPathSprite(int connectionMask, int variant = 0)
    {
        int key = ((connectionMask & 0x0F) << 8) ^ (Mathf.Abs(variant) % 251);
        if (!_volcanoBlockedPathCache.TryGetValue(key, out var sprite))
        {
            sprite = CreateVolcanoPlatformSprite(connectionMask & 0x0F, variant);
            _volcanoBlockedPathCache[key] = sprite;
        }
        return sprite;
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

    // 하위 호환
    public static Sprite GetGrassSprite(int variant = 0) => GetWallSprite(1, variant);
    public static Sprite GetDirtSprite()                  => GetPathSprite(1, 0);

    // ────────────────────────────────────────────────────────────────
    //  내부 로더
    // ────────────────────────────────────────────────────────────────

    static Texture2D GetOrLoadTex(string path)
    {
        if (_texCache.TryGetValue(path, out var cached)) return cached;
        var tex = Resources.Load<Texture2D>(path);
        if (tex != null) { tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp; }
        else Debug.LogWarning($"[TileTextureGenerator] '{path}' 로드 실패 → 절차적 폴백 사용");
        _texCache[path] = tex;
        return tex;
    }

    static Sprite[] BuildSpriteArray(string texPath, int[] ids)
    {
        if (ids == null || ids.Length == 0) return null;

        if (texPath == TEX_TERRAIN)
        {
            EnsureTerrainSlicesLoaded();
            var arr = new Sprite[ids.Length];
            bool anyFound = false;
            for (int i = 0; i < ids.Length; i++)
            {
                arr[i] = GetTerrainSlice(ids[i]);
                if (arr[i] != null) anyFound = true;
            }
            return anyFound ? arr : null;
        }

        var tex = GetOrLoadTex(texPath);
        if (tex == null) return null;
        var result = new Sprite[ids.Length];
        for (int i = 0; i < ids.Length; i++)
            result[i] = Sprite.Create(tex, TileRect(ids[i], tex.width, tex.height), PIVOT, PPU);
        return result;
    }

    static void EnsureTerrainSlicesLoaded()
    {
        if (_terrainSlicesLoaded) return;
        _terrainSlicesLoaded = true;

        var sprites = Resources.LoadAll<Sprite>(TEX_TERRAIN);
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogWarning("[TileTextureGenerator] terrain.png 슬라이스 로드 실패 → 절차적 폴백 사용");
            return;
        }

        foreach (var s in sprites)
        {
            if (s.name.StartsWith("terrain_") &&
                int.TryParse(s.name.Substring(8), out int id))
            {
                // PPU=32로 재생성해 기존 타일 크기 유지
                _terrainSliceCache[id] = Sprite.Create(s.texture, s.rect, PIVOT, PPU);
            }
        }
    }

    static Sprite GetTerrainSlice(int id)
    {
        return _terrainSliceCache.TryGetValue(id, out var sp) ? sp : null;
    }

    static void EnsureStageRoadLoaded(int stageIdx)
    {
        switch (stageIdx)
        {
            case 1: LoadStageMapSprites(TEX_STAGE1_ROAD,  ref _stage1RoadLoaded); break;
            case 2: LoadStageMapSprites(TEX_STAGE2_ROAD,  ref _stage2RoadLoaded); break;
            case 3: LoadStageMapSprites(TEX_STAGE3_ROAD,  ref _stage3RoadLoaded); break;
        }
    }

    static void EnsureStageNRoadLoaded(int stageIdx)
    {
        switch (stageIdx)
        {
            case 1: LoadStageMapSprites(TEX_STAGE1_NROAD, ref _stage1NRoadLoaded); break;
            case 2: LoadStageMapSprites(TEX_STAGE2_NROAD, ref _stage2NRoadLoaded); break;
            case 3: LoadStageMapSprites(TEX_STAGE3_NROAD, ref _stage3NRoadLoaded); break;
        }
    }

    static void LoadStageMapSprites(string path, ref bool loaded)
    {
        if (loaded) return;
        loaded = true;
        var sprites = Resources.LoadAll<Sprite>(path);
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogWarning($"[TileTextureGenerator] '{path}' 로드 실패 → 폴백");
            return;
        }
        foreach (var s in sprites)
            _stageMapCache[s.name] = Sprite.Create(s.texture, s.rect, PIVOT, PPU);
    }

    static Sprite GetStageMapSprite(string name)
    {
        if (name == null) return null;
        return _stageMapCache.TryGetValue(name, out var sp) ? sp : null;
    }

    static string[] GetStageRoadSpriteNames(int stageIdx) => stageIdx switch
    {
        1 => STAGE1_ROAD_SPRITE_NAMES,
        2 => STAGE2_ROAD_SPRITE_NAMES,
        3 => STAGE3_ROAD_SPRITE_NAMES,
        _ => null,
    };

    static string[] GetStageNRoadSpriteNames(int stageIdx) => stageIdx switch
    {
        1 => STAGE1_NROAD_SPRITE_NAMES,
        2 => STAGE2_NROAD_SPRITE_NAMES,
        3 => STAGE3_NROAD_SPRITE_NAMES,
        _ => null,
    };

    // mask 0-15 → StageN_Road_Map 스프라이트 직접 룩업
    static Sprite GetStageRoadSpriteVariant(int idx, int mask, int variant)
    {
        string[] names = GetStageRoadSpriteNames(idx);
        return names != null ? GetStageMapSprite(names[mask & 0x0F]) : null;
    }

    // mask 0-15 → StageN_NRoad_Map 스프라이트 직접 룩업
    static Sprite GetStageNRoadSpriteVariant(int idx, int roadMask, int variant)
    {
        string[] names = GetStageNRoadSpriteNames(idx);
        return names != null ? GetStageMapSprite(names[roadMask & 0x0F]) : null;
    }

    /// <summary>
    /// 타일 ID → Unity Sprite.Create 용 Rect
    /// 실제 텍스처 크기를 받아 col 수를 자동 계산합니다.
    /// </summary>
    static Rect TileRect(int id, int texW, int texH)
    {
        int cols = Mathf.Max(1, texW / TILE_SZ);
        int col  = id % cols;
        int row  = id / cols;
        float x  = col * TILE_SZ;
        float y  = texH - (row + 1) * TILE_SZ;
        x = Mathf.Clamp(x, 0f, texW - TILE_SZ);
        y = Mathf.Clamp(y, 0f, texH - TILE_SZ);
        return new Rect(x, y, TILE_SZ, TILE_SZ);
    }

    static int   ClampStage(int s) => (s >= 1 && s <= MAX_STAGE) ? s : 1;
    static Sprite GetFallback(ref Sprite cache, System.Func<Sprite> creator)
        => cache != null ? cache : (cache = creator());

    // ────────────────────────────────────────────────────────────────
    //  절차적 길(Path) 타일 생성
    // ────────────────────────────────────────────────────────────────

    static Sprite CreateVolcanoConnectedPathSprite(int mask, int variant)
    {
        const int size = 64;
        const float center = 31.5f;
        const float roadHalf = 18.5f;
        const float edgeHalf = 24.5f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        bool top = (mask & 1) != 0; bool right = (mask & 2) != 0;
        bool bottom = (mask & 4) != 0; bool left = (mask & 8) != 0;
        if (mask == 0) top = right = bottom = left = true;

        Vector2 cpt = new Vector2(center, center);
        Vector2 topPt = new Vector2(center, size + edgeHalf);
        Vector2 rightPt = new Vector2(size + edgeHalf, center);
        Vector2 bottomPt = new Vector2(center, -edgeHalf);
        Vector2 leftPt = new Vector2(-edgeHalf, center);

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            Color lava = SampleVolcanoLavaColor(variant, x, y);

            float dist = Vector2.Distance(p, cpt);
            if (top) dist = Mathf.Min(dist, DistToSeg(p, cpt, topPt));
            if (right) dist = Mathf.Min(dist, DistToSeg(p, cpt, rightPt));
            if (bottom) dist = Mathf.Min(dist, DistToSeg(p, cpt, bottomPt));
            if (left) dist = Mathf.Min(dist, DistToSeg(p, cpt, leftPt));

            float rough = (HashNoise(3, mask + variant, x / 2, y / 2, 223) - 0.5f) * 4.2f
                        + Mathf.Sin((x + mask * 13) * 0.23f) * 0.8f
                        + Mathf.Sin((y + mask * 17) * 0.19f) * 0.7f;

            float inner = roadHalf + rough;
            float outer = edgeHalf + rough * 0.65f;
            Color color = lava;

            if (dist < outer)
            {
                float t = Mathf.InverseLerp(outer, inner, dist);
                Color glow = new Color(0.95f, 0.26f, 0.04f);
                Color rim = Color.Lerp(new Color(0.13f, 0.09f, 0.075f), glow, 0.28f);
                color = Color.Lerp(lava, rim, Mathf.Lerp(0.25f, 0.92f, t));
            }
            if (dist < inner)
            {
                float stone = HashNoise(3, variant, x, y, 239);
                Color dark = Color.Lerp(new Color(0.16f, 0.15f, 0.145f), new Color(0.32f, 0.30f, 0.28f), stone);
                if (HashNoise(3, variant, x / 2, y / 2, 241) > 0.88f)
                    dark = Color.Lerp(dark, new Color(0.52f, 0.48f, 0.42f), 0.20f);
                color = dark;
            }

            tex.SetPixel(x, y, ClampColor(color));
        }
        tex.Apply();
        return SpriteFromTex(tex);
    }

    static Sprite CreateVolcanoPlatformSprite(int mask, int variant)
    {
        const int size = 64;
        const float center = 31.5f;
        const float rockHalf = 22.5f;
        const float edgeHalf = 28.0f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        bool top = (mask & 1) != 0; bool right = (mask & 2) != 0;
        bool bottom = (mask & 4) != 0; bool left = (mask & 8) != 0;
        Vector2 cpt = new Vector2(center, center);
        Vector2 topPt = new Vector2(center, size + edgeHalf);
        Vector2 rightPt = new Vector2(size + edgeHalf, center);
        Vector2 bottomPt = new Vector2(center, -edgeHalf);
        Vector2 leftPt = new Vector2(-edgeHalf, center);

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            Color lava = SampleVolcanoLavaColor(variant + 17, x, y);

            float dist = Vector2.Distance(p, cpt);
            if (top) dist = Mathf.Min(dist, DistToSeg(p, cpt, topPt));
            if (right) dist = Mathf.Min(dist, DistToSeg(p, cpt, rightPt));
            if (bottom) dist = Mathf.Min(dist, DistToSeg(p, cpt, bottomPt));
            if (left) dist = Mathf.Min(dist, DistToSeg(p, cpt, leftPt));

            float rough = (HashNoise(3, variant, x / 2, y / 2, 251) - 0.5f) * 5.5f;
            float inner = rockHalf + rough;
            float outer = edgeHalf + rough * 0.55f;
            Color color = lava;

            if (dist < outer)
            {
                float t = Mathf.InverseLerp(outer, inner, dist);
                Color rim = Color.Lerp(new Color(0.09f, 0.07f, 0.06f), new Color(0.92f, 0.24f, 0.03f), 0.30f);
                color = Color.Lerp(lava, rim, Mathf.Lerp(0.18f, 0.86f, t));
            }
            if (dist < inner)
            {
                Color stone = Color.Lerp(new Color(0.14f, 0.13f, 0.125f), new Color(0.34f, 0.32f, 0.30f),
                    HashNoise(3, variant, x, y, 257));
                color = stone;
            }

            tex.SetPixel(x, y, ClampColor(color));
        }
        tex.Apply();
        return SpriteFromTex(tex);
    }

    static Color SampleVolcanoLavaColor(int variant, int x, int y)
    {
        float broad = HashNoise(3, variant / 7, x / 8, y / 8, 261);
        float vein = Mathf.Abs(Mathf.Sin((x + variant * 2) * 0.16f + y * 0.10f));
        float heat = Mathf.Clamp01(broad * 0.62f + vein * 0.38f);
        Color crust = Color.Lerp(new Color(0.14f, 0.03f, 0.015f), new Color(0.32f, 0.06f, 0.02f), broad);
        Color lava = Color.Lerp(new Color(0.70f, 0.12f, 0.015f), new Color(1.00f, 0.47f, 0.04f), heat);
        return Color.Lerp(crust, lava, Mathf.SmoothStep(0.54f, 0.90f, heat));
    }

    static Sprite CreateConnectedPathSprite(
        Color ground, Color path, Color pathEdge, int mask, int stageIndex, int variant)
    {
        const int size     = 64;
        const int roadHalf = 15;
        const int edgeHalf = 19;
        const int center   = size / 2;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        bool top    = (mask & 1) != 0; bool right  = (mask & 2) != 0;
        bool bottom = (mask & 4) != 0; bool left   = (mask & 8) != 0;
        if (mask == 0) top = right = bottom = left = true;

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            int dx = x - center; int dy = y - center;
            float gn = HashNoise(stageIndex, variant, x, y, 5);
            Color c = Color.Lerp(ground * 0.85f, ground * 1.15f, gn);

            bool inCore =
                Mathf.Abs(dx) <= roadHalf && Mathf.Abs(dy) <= roadHalf;
            bool inConn =
                (top    && Mathf.Abs(dx) <= roadHalf && dy >=  -roadHalf) ||
                (right  && Mathf.Abs(dy) <= roadHalf && dx >=  -roadHalf) ||
                (bottom && Mathf.Abs(dx) <= roadHalf && dy <=   roadHalf) ||
                (left   && Mathf.Abs(dy) <= roadHalf && dx <=   roadHalf);
            bool inEdgeCore =
                Mathf.Abs(dx) <= edgeHalf && Mathf.Abs(dy) <= edgeHalf;
            bool inEdgeConn =
                (top    && Mathf.Abs(dx) <= edgeHalf && dy >= -edgeHalf) ||
                (right  && Mathf.Abs(dy) <= edgeHalf && dx >= -edgeHalf) ||
                (bottom && Mathf.Abs(dx) <= edgeHalf && dy <=  edgeHalf) ||
                (left   && Mathf.Abs(dy) <= edgeHalf && dx <=  edgeHalf);

            if (inEdgeCore || inEdgeConn)
                c = Color.Lerp(c, pathEdge, 0.88f);

            if (inCore || inConn)
            {
                float pn = HashNoise(stageIndex, variant, x, y, 17);
                c = Color.Lerp(path * 0.82f, path * 1.18f, pn);
                if (HashNoise(stageIndex, variant, x / 2, y / 2, 29) > 0.88f)
                    c = Color.Lerp(c, Color.white, 0.10f);
            }

            tex.SetPixel(x, y, ClampColor(c));
        }
        tex.Apply();
        return SpriteFromTex(tex);
    }

    // Stage 1 전용: 유기적 잔디 경계
    static Sprite CreateOrganicGrassPathSprite(Color ground, Color path, Color pathEdge, int mask)
    {
        const int size = 64;
        const float center   = 31.5f;
        const float roadHalf = 13.5f;
        const float edgeHalf = 18f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        bool top    = (mask & 1) != 0; bool right  = (mask & 2) != 0;
        bool bottom = (mask & 4) != 0; bool left   = (mask & 8) != 0;
        if (mask == 0) top = right = bottom = left = true;

        Vector2 cpt      = new Vector2(center, center);
        Vector2 topPt    = new Vector2(center, size + edgeHalf);
        Vector2 rightPt  = new Vector2(size + edgeHalf, center);
        Vector2 bottomPt = new Vector2(center, -edgeHalf);
        Vector2 leftPt   = new Vector2(-edgeHalf, center);

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            Color color = Color.Lerp(ground * 0.90f, ground * 1.10f, HashNoise(1, mask, x, y, 5));

            float dist = Vector2.Distance(p, cpt);
            if (top)    dist = Mathf.Min(dist, DistToSeg(p, cpt, topPt));
            if (right)  dist = Mathf.Min(dist, DistToSeg(p, cpt, rightPt));
            if (bottom) dist = Mathf.Min(dist, DistToSeg(p, cpt, bottomPt));
            if (left)   dist = Mathf.Min(dist, DistToSeg(p, cpt, leftPt));

            float rough = (HashNoise(1, mask, x / 2, y / 2, 23) - 0.5f) * 4.8f
                        + Mathf.Sin((x + mask * 7) * 0.33f) * 0.9f
                        + Mathf.Sin((y + mask * 11) * 0.27f) * 0.7f;

            float oRoad = roadHalf + rough;
            float oEdge = edgeHalf + rough * 0.75f;

            if (dist < oEdge)
            {
                float t = Mathf.InverseLerp(oEdge, oRoad, dist);
                color = Color.Lerp(color, pathEdge, Mathf.Lerp(0.55f, 0.95f, t));
            }
            if (dist < oRoad)
            {
                Color pc = Color.Lerp(path * 0.82f, path * 1.14f, HashNoise(1, mask, x, y, 37));
                float cw = Mathf.Clamp01(1f - dist / (oRoad + 0.01f));
                pc = Color.Lerp(pc, new Color(0.70f, 0.50f, 0.25f), cw * 0.16f);
                if (HashNoise(1, mask, x / 2, y / 2, 47) > 0.91f)
                    pc = Color.Lerp(pc, Color.white, 0.11f);
                color = pc;
            }
            tex.SetPixel(x, y, ClampColor(color));
        }
        tex.Apply();
        return SpriteFromTex(tex);
    }

    static Sprite CreatePathEdgeSprite(Color edgeColor, int mask)
    {
        const int size = 64, edge = 8, fringe = 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color clear = new Color(0, 0, 0, 0);
        Color light = Color.Lerp(edgeColor, Color.white, 0.14f);
        light.a = Mathf.Min(0.55f, edgeColor.a);

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            Color c = clear;
            bool top = (mask & 1) != 0 && y >= size - edge;
            bool right = (mask & 2) != 0 && x >= size - edge;
            bool bottom = (mask & 4) != 0 && y < edge;
            bool left = (mask & 8) != 0 && x < edge;
            if (top || right || bottom || left)
                c = Color.Lerp(edgeColor, light, ((x / 2 + y / 3) % 3 == 0) ? 0.10f : 0f);

            bool iTop = (mask & 1) != 0 && y >= size - edge - fringe && y < size - edge;
            bool iRight = (mask & 2) != 0 && x >= size - edge - fringe && x < size - edge;
            bool iBottom = (mask & 4) != 0 && y >= edge && y < edge + fringe;
            bool iLeft = (mask & 8) != 0 && x >= edge && x < edge + fringe;
            if (iTop || iRight || iBottom || iLeft)
                c = Color.Lerp(c, new Color(0, 0, 0, 0.18f), c.a > 0f ? 0.20f : 1f);

            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return SpriteFromTex(tex);
    }

    // ── 절차적 폴백 ──────────────────────────────────────────────────
    static Sprite CreateGrassSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float ox = Random.Range(0f, 100f), oy = Random.Range(0f, 100f);
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float n = Mathf.PerlinNoise((float)x / size * 4f + ox, (float)y / size * 4f + oy);
            float d = Mathf.PerlinNoise((float)x / size * 12f + ox, (float)y / size * 12f + oy) * 0.3f;
            float t = n * 0.7f + d;
            Color c = t < 0.4f
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
        float ox = Random.Range(0f, 100f), oy = Random.Range(0f, 100f);
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float n = Mathf.PerlinNoise((float)x / size * 5f + ox, (float)y / size * 5f + oy);
            float t = n * 0.6f + Mathf.PerlinNoise((float)x / size * 20f + ox, (float)y / size * 20f + oy) * 0.25f;
            Color c = t < 0.35f
                ? Color.Lerp(new Color(0.38f, 0.23f, 0.08f), new Color(0.55f, 0.36f, 0.14f), t / 0.35f)
                : Color.Lerp(new Color(0.55f, 0.36f, 0.14f), new Color(0.78f, 0.62f, 0.35f), (t - 0.35f) / 0.65f);
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return SpriteFromTex(tex);
    }

    // ── 유틸 ─────────────────────────────────────────────────────────
    static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    static float HashNoise(int stage, int variant, int x, int y, int salt)
    {
        uint h = (uint)(stage * 374761393 + variant * 668265263
                      + x * 2246822519u + y * 3266489917u + salt * 1274126177u);
        h ^= h >> 13; h *= 1274126177u;
        return (h & 0x00FFFFFF) / 16777215f;
    }

    static Color ClampColor(Color c)
    {
        c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g);
        c.b = Mathf.Clamp01(c.b); c.a = 1f; return c;
    }

    static Sprite SpriteFromTex(Texture2D tex)
        => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                         new Vector2(0.5f, 0.5f), tex.width);
}
