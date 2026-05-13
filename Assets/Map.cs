using UnityEngine;
using System.Collections.Generic;

public class Map : MonoBehaviour
{
    [Header("Map Settings")]
    [Tooltip("홀수여야 합니다 (예: 21, 23, 25)")]
    public int mapWidth  = 21;
    [Tooltip("홀수여야 합니다 (예: 13, 15, 17)")]
    public int mapHeight = 13;
    public float tileSize = 1f;

    [Header("Tile Prefabs (선택 - 비워두면 절차적 텍스처 사용)")]
    public GameObject grassTilePrefab;
    public GameObject dirtTilePrefab;

    [Header("Path Waypoints (자동 설정됨 - 직접 수정 불필요)")]
    public Vector2Int[] pathWaypoints;

    // ── 내부 상태 ──────────────────────────────────────────────
    private int[,]        maze;        // 0=길, 1=벽
    private TileType[,]   tileMap;
    private GameObject[,] tileObjects;

    /// <summary>Stage 3 어두운 바위섬 플랫폼 타일 위치 목록 (적 배치에 활용)</summary>
    public IReadOnlyList<Vector2Int> VolcanoPlatforms => _volcanoPlatforms;
    private readonly List<Vector2Int> _volcanoPlatforms = new();

    public bool IsGenerated => tileMap != null &&
                               tileMap.GetLength(0) > 0 &&
                               tileMap.GetLength(1) > 0 &&
                               pathWaypoints != null &&
                               pathWaypoints.Length >= 2;

    // 타일 타입: Grass=벽, Dirt=길
    public enum TileType { Grass, Dirt }

    // ── 상수 ───────────────────────────────────────────────────
    const int ROAD = 0;
    const int WALL = 1;

    void Start()
    {
        if (!IsGenerated)
            GenerateMap();
    }

    // ──────────────────────────────────────────────────────────
    //  공개 진입점
    // ──────────────────────────────────────────────────────────
    public void GenerateMap()
    {
        // ── 현재 웨이브 설정에서 맵 크기 자동 적용 ───────────────
        if (StageManager.Instance != null)
        {
            var waveCfg = StageManager.Instance.GetCurrentWaveConfig();
            mapWidth  = waveCfg.mapWidth;
            mapHeight = waveCfg.mapHeight;
        }

        // 홀수 보정 (짝수 크기는 미로 생성 불가)
        int w = mapWidth  % 2 == 0 ? mapWidth  - 1 : mapWidth;
        int h = mapHeight % 2 == 0 ? mapHeight - 1 : mapHeight;

        // 기존 맵 오브젝트 제거
        const string holderName = "Generated Map Holder";
        Transform existing = transform.Find(holderName);
        if (existing != null) DestroyImmediate(existing.gameObject);

        Transform mapHolder = new GameObject(holderName).transform;
        mapHolder.parent        = transform;
        mapHolder.localPosition = Vector3.zero;

        // ── 1. 미로 생성 ──────────────────────────────────────
        maze    = GenerateMaze(w, h);

        // ── 1-b. 스테이지 복잡도에 따라 추가 통로 뚫기 ──────────
        float extraRate = StageManager.Instance != null
            ? StageManager.Instance.GetCurrentWaveConfig().extraPassageRate
            : 0f;
        if (extraRate > 0f)
            CarveExtraPassages(maze, w, h, extraRate);

        tileMap = new TileType[w, h];

        // ── 2. 출발/도착 지점 뚫기 ───────────────────────────
        int[] oddRows = GetOddIndices(h);

        int startRow = oddRows[Random.Range(0, oddRows.Length)];
        int goalRow  = oddRows[Random.Range(0, oddRows.Length)];

        maze[0,     startRow] = ROAD;   // 왼쪽 벽 구멍 (출발)
        maze[w - 1, goalRow]  = ROAD;   // 오른쪽 벽 구멍 (도착)

        // RouteDrawer 가 사용할 웨이포인트 자동 설정
        pathWaypoints = new Vector2Int[]
        {
            new Vector2Int(0,     startRow),
            new Vector2Int(w - 1, goalRow)
        };

        // ── 3. 실제 사용 크기로 필드 먼저 동기화 ─────────────────
        // GetWorldPosition()이 mapWidth/mapHeight를 참조하므로
        // 타일 배치 전에 반드시 업데이트해야 좌표가 일치함
        mapWidth  = w;
        mapHeight = h;

        // ── 4. 타일 맵 변환 (미로 → TileType) ─────────────────
        tileObjects = new GameObject[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                tileMap[x, y] = (maze[x, y] == ROAD) ? TileType.Dirt : TileType.Grass;

        // ── 5. 타일 오브젝트 생성 ─────────────────────────────
        // 스테이지 인덱스에 따라 테마 타일 선택
        int stageIdx = StageManager.Instance != null
            ? StageManager.Instance.currentStageIndex : 1;
        _volcanoPlatforms.Clear();

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Vector3  pos  = GetWorldPosition(x, y);
                TileType type = tileMap[x, y];
                bool useStageSpriteSheets = stageIdx >= 1 && stageIdx <= 3;
                GameObject prefab = useStageSpriteSheets
                    ? null
                    : (type == TileType.Dirt) ? dirtTilePrefab : grassTilePrefab;

                GameObject tile;
                if (prefab != null)
                {
                    tile = Instantiate(prefab, pos, Quaternion.identity, mapHolder);
                }
                else
                {
                    tile = new GameObject($"Tile_{x}_{y}");
                    tile.transform.position   = pos;
                    tile.transform.parent     = mapHolder;
                    tile.transform.localScale = Vector3.one * tileSize;

                    var sr = tile.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = 0;
                    sr.color        = Color.white;   // LPC 원본 팔레트 그대로

                    int variant = StableVariant(stageIdx, x, y);
                    if (type == TileType.Dirt)
                    {
                        int roadTileIndex = GetRoadTileIndex(x, y, variant);
                        sr.sprite = TileTextureGenerator.GetRoadSprite(stageIdx, roadTileIndex, variant);
                    }
                    else
                    {
                        if (IsVolcanoBlockedPathTile(stageIdx, x, y))
                            _volcanoPlatforms.Add(new Vector2Int(x, y));

                        int nRoadTileIndex = GetNRoadTileIndex(variant);
                        sr.sprite = TileTextureGenerator.GetWallSprite(stageIdx, nRoadTileIndex, variant);
                    }
                }

                tile.name = $"Tile_{x}_{y}";
                tileObjects[x, y] = tile;
            }
        }

        BuildThemeDetails(mapHolder, stageIdx);
        FitCameraToMap();
    }

    void BuildThemeDetails(Transform mapHolder, int stageIdx)
    {
        if (Camera.main != null)
            Camera.main.backgroundColor = TileTextureGenerator.GetBackdropColor(stageIdx);

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                GameObject tile = tileObjects[x, y];
                if (tile == null) continue;

                TileType type = tileMap[x, y];
                // 길 타일에는 장식 없음 (깔끔한 룩 유지)
                if (type == TileType.Grass)
                {
                    if (stageIdx >= 1 && stageIdx <= 3)
                    {
                        continue;
                    }

                    if (IsVolcanoBlockedPathTile(stageIdx, x, y))
                    {
                        continue;
                    }

                    int pathNeighbors = CountPathNeighbors(x, y);
                    float decorChance = GetDecorChance(stageIdx, pathNeighbors > 0);
                    if (Hash01(stageIdx, x, y, 19) < decorChance)
                        AddGroundDecor(tile.transform, stageIdx, x, y, pathNeighbors > 0);
                }
            }
        }
    }

    /// <summary>
    /// Stage 3 바위섬 플랫폼 타일 아래 용암 균열 빛 오버레이 추가.
    /// 단색 주황색 정사각형을 타일 크기보다 약간 크게 배치해
    /// 바위 가장자리에 주황 빛이 비치는 효과를 만듦.
    /// </summary>
    void AddPlatformLavaGlow(Transform parent, int x, int y)
    {
        // 이웃이 플랫폼이 아닌 방향 = 용암과 접한 변 → 그 방향에 빛을 강조
        int stageIdx = 3;
        float glowAlpha = Mathf.Lerp(0.55f, 0.80f, Hash01(stageIdx, x, y, 77));
        var glowColor = new Color(0.95f, 0.38f, 0.05f, glowAlpha);

        var glowGo = new GameObject("LavaGlow");
        glowGo.transform.SetParent(parent, false);
        glowGo.transform.localPosition = Vector3.zero;
        glowGo.transform.localScale    = Vector3.one * 1.22f; // 약간 크게 → 테두리 빛

        var sr = glowGo.AddComponent<SpriteRenderer>();
        sr.sprite       = MakeSolidSprite(glowColor);
        sr.sortingOrder = -1; // 타일 스프라이트(0) 아래
        sr.color        = glowColor;
    }

    /// <summary>단색 1×1 픽셀 텍스처에서 스프라이트 생성 (빛 오버레이용)</summary>
    static Sprite _solidWhiteSprite;
    static Sprite MakeSolidSprite(Color color)
    {
        if (_solidWhiteSprite == null)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _solidWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
        return _solidWhiteSprite;
    }

    void AddGroundDecor(Transform parent, int stageIdx, int x, int y, bool nearPath)
    {
        Sprite sprite = TileTextureGenerator.GetDecorSprite(stageIdx, StableVariant(stageIdx + 11, x, y));
        if (sprite == null) return;

        float sx = Mathf.Lerp(-0.18f, 0.18f, Hash01(stageIdx, x, y, 31));
        float sy = Mathf.Lerp(-0.18f, 0.18f, Hash01(stageIdx, x, y, 37));
        float scale = Mathf.Lerp(0.46f, nearPath ? 0.70f : 0.92f, Hash01(stageIdx, x, y, 43));
        float rot = Mathf.Round(Hash01(stageIdx, x, y, 47) * 3f) * 90f;

        AddSpriteChild(
            parent,
            "ThemeDecor",
            sprite,
            new Vector3(sx, sy, 0f),
            Vector3.one * (tileSize * scale),
            Quaternion.Euler(0f, 0f, rot),
            2,
            Color.white);
    }

    void AddPathDetail(Transform parent, int stageIdx, int x, int y)
    {
        Sprite sprite = TileTextureGenerator.GetDecorSprite(stageIdx, StableVariant(stageIdx + 23, x, y));
        if (sprite == null) return;

        Color tint = Color.white;
        tint.a = 0.34f;

        AddSpriteChild(
            parent,
            "PathDetail",
            sprite,
            new Vector3(
                Mathf.Lerp(-0.22f, 0.22f, Hash01(stageIdx, x, y, 53)),
                Mathf.Lerp(-0.22f, 0.22f, Hash01(stageIdx, x, y, 59)),
                0f),
            Vector3.one * (tileSize * Mathf.Lerp(0.28f, 0.48f, Hash01(stageIdx, x, y, 61))),
            Quaternion.Euler(0f, 0f, Mathf.Round(Hash01(stageIdx, x, y, 67) * 3f) * 90f),
            2,
            tint);
    }

    float GetDecorChance(int stageIdx, bool nearPath)
    {
        // 통일감을 위해 장식 밀도 낮춤: 이전 대비 약 1/3 수준
        if (stageIdx == 1)
            return nearPath ? 0.018f : 0.008f;
        return nearPath ? 0.025f : 0.012f;
    }

    void AddSpriteChild(
        Transform parent,
        string name,
        Sprite sprite,
        Vector3 localPosition,
        Vector3 localScale,
        Quaternion localRotation,
        int sortingOrder,
        Color color)
    {
        if (sprite == null) return;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.transform.localRotation = localRotation;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;
        sr.color = color;
    }

    int GetPathEdgeMask(int x, int y)
    {
        int mask = 0;
        if (GetTileType(x, y + 1) == TileType.Grass) mask |= 1;
        if (GetTileType(x + 1, y) == TileType.Grass) mask |= 2;
        if (GetTileType(x, y - 1) == TileType.Grass) mask |= 4;
        if (GetTileType(x - 1, y) == TileType.Grass) mask |= 8;
        return mask;
    }

    int GetPathConnectionMask(int x, int y)
    {
        int mask = 0;
        if (GetTileType(x, y + 1) == TileType.Dirt) mask |= 1;
        if (GetTileType(x + 1, y) == TileType.Dirt || x == mapWidth - 1) mask |= 2;
        if (GetTileType(x, y - 1) == TileType.Dirt) mask |= 4;
        if (GetTileType(x - 1, y) == TileType.Dirt || x == 0) mask |= 8;
        return mask;
    }

    int CountPathNeighbors(int x, int y)
    {
        int count = 0;
        if (GetTileType(x, y + 1) == TileType.Dirt) count++;
        if (GetTileType(x + 1, y) == TileType.Dirt) count++;
        if (GetTileType(x, y - 1) == TileType.Dirt) count++;
        if (GetTileType(x - 1, y) == TileType.Dirt) count++;
        return count;
    }

    int GetRoadTileIndex(int x, int y, int variant)
    {
        if (CountNonRoadSides(x, y) >= 3)
            return 10;

        int[] candidates = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 13, 14, 15, 16, 17 };
        return candidates[Mathf.Abs(variant) % candidates.Length];
    }

    int GetNRoadTileIndex(int variant)
    {
        int[] candidates = { 10, 15, 16, 17 };
        return candidates[Mathf.Abs(variant) % candidates.Length];
    }

    int CountNonRoadSides(int x, int y)
    {
        int count = 0;
        if (GetTileType(x, y + 1) != TileType.Dirt) count++;
        if (GetTileType(x + 1, y) != TileType.Dirt) count++;
        if (GetTileType(x, y - 1) != TileType.Dirt) count++;
        if (GetTileType(x - 1, y) != TileType.Dirt) count++;
        return count;
    }

    int StableVariant(int stageIdx, int x, int y)
    {
        int h = stageIdx * 73856093 ^ x * 19349663 ^ y * 83492791;
        h ^= h >> 13;
        return Mathf.Abs(h);
    }

    float Hash01(int stageIdx, int x, int y, int salt)
    {
        uint h = (uint)(stageIdx * 374761393 + x * 668265263 + y * 2246822519u + salt * 3266489917u);
        h = (h ^ (h >> 13)) * 1274126177u;
        return (h & 0x00FFFFFF) / 16777215f;
    }

    // ──────────────────────────────────────────────────────────
    //  추가 통로 뚫기 — Binary Tree 미로에 루프(분기)를 추가해 복잡도 상승
    //  짝수-홀수 좌표에 있는 '연결 벽'을 rate 확률로 개방한다.
    //  ┌─────────────────────────────────────────────────────┐
    //  │  x짝·y홀 → 좌우 셀 사이 수평 벽                    │
    //  │  x홀·y짝 → 상하 셀 사이 수직 벽                    │
    //  └─────────────────────────────────────────────────────┘
    // ──────────────────────────────────────────────────────────
    void CarveExtraPassages(int[,] m, int w, int h, float rate)
    {
        for (int y = 1; y < h - 1; y++)
        for (int x = 1; x < w - 1; x++)
        {
            if (m[x, y] != WALL) continue;
            if (Random.value >= rate) continue;

            // 수평 연결벽: 짝수 x, 홀수 y — 좌우 두 경로 셀 사이
            if (x % 2 == 0 && y % 2 == 1
                && m[x - 1, y] == ROAD && m[x + 1, y] == ROAD)
            {
                m[x, y] = ROAD;
            }
            // 수직 연결벽: 홀수 x, 짝수 y — 상하 두 경로 셀 사이
            else if (x % 2 == 1 && y % 2 == 0
                && m[x, y - 1] == ROAD && m[x, y + 1] == ROAD)
            {
                m[x, y] = ROAD;
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    //  Binary Tree 미로 생성 알고리즘
    //  - 2D 배열 (0=길, 1=벽)
    //  - 모든 홀수 좌표 셀을 길로 만들고
    //    각 셀에서 오른쪽 또는 위쪽 방향으로 무작위로 벽을 제거
    // ──────────────────────────────────────────────────────────
    int[,] GenerateMaze(int w, int h)
    {
        int[,] m = new int[w, h];

        // 초기화: 전부 벽
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                m[x, y] = WALL;

        // 홀수 좌표 셀을 순회하며 Binary Tree 규칙 적용
        for (int y = 1; y < h - 1; y += 2)
        {
            for (int x = 1; x < w - 1; x += 2)
            {
                m[x, y] = ROAD;   // 현재 셀을 길로

                bool canRight = (x + 2 <= w - 2);
                bool canUp    = (y + 2 <= h - 2);

                if (canRight && canUp)
                {
                    if (Random.Range(0, 2) == 0)
                        m[x + 1, y] = ROAD;   // 오른쪽 벽 제거
                    else
                        m[x, y + 1] = ROAD;   // 위쪽 벽 제거
                }
                else if (canRight)
                {
                    m[x + 1, y] = ROAD;
                }
                else if (canUp)
                {
                    m[x, y + 1] = ROAD;
                }
                // 최상단-최우단 코너는 아무것도 안 함
            }
        }

        return m;
    }

    // 1 이상 (h-2) 이하인 홀수 인덱스 배열 반환
    int[] GetOddIndices(int size)
    {
        var list = new List<int>();
        for (int i = 1; i < size - 1; i += 2)
            list.Add(i);
        return list.ToArray();
    }

    // ──────────────────────────────────────────────────────────
    //  공개 유틸리티
    // ──────────────────────────────────────────────────────────
    public Vector3 GetWorldPosition(int x, int y)
    {
        float wx = x * tileSize - (mapWidth  * tileSize / 2f) + tileSize / 2f;
        float wy = y * tileSize - (mapHeight * tileSize / 2f) + tileSize / 2f;
        return new Vector3(wx, wy, 0f);
    }

    public List<Vector3> GetPathWorldPositions()
    {
        var positions = new List<Vector3>();
        if (pathWaypoints == null) return positions;
        foreach (var wp in pathWaypoints)
            positions.Add(GetWorldPosition(wp.x, wp.y));
        return positions;
    }

    public TileType GetTileType(int x, int y)
    {
        if (tileMap == null) return TileType.Grass;
        if (!IsInBounds(new Vector2Int(x, y))) return TileType.Grass;
        return tileMap[x, y];
    }

    public bool IsEnemyPlatformTile(int x, int y)
    {
        int stageIdx = StageManager.Instance != null
            ? StageManager.Instance.currentStageIndex : 1;
        return IsVolcanoBlockedPathTile(stageIdx, x, y);
    }

    /// <summary>
    /// Stage 3 화산 전용 — 벽 타일이지만 시각적으로 어두운 화산암처럼 보이는 "플랫폼" 타일 여부.
    ///
    /// ▶ 생성 원리 (2단계 클러스터)
    ///   1차 레이어 : 걷기 가능한 경로와 직접 맞닿은 벽 타일의 ~62% → "바위섬 씨앗"
    ///   2차 레이어 : 씨앗 타일과 맞닿은(대각선 제외) 벽 타일의 ~58% → 클러스터 확장
    ///
    /// 이 방식으로 경로를 따라 2~4칸짜리 어두운 바위섬이 자연스럽게 형성됨.
    /// 경로와 전혀 무관한 깊은 용암 지대에는 플랫폼이 생성되지 않음.
    /// </summary>
    bool IsVolcanoBlockedPathTile(int stageIdx, int x, int y)
    {
        if (stageIdx != 3) return false;
        if (GetTileType(x, y) != TileType.Grass) return false;
        if (x <= 0 || x >= mapWidth - 1 || y <= 0 || y >= mapHeight - 1) return false;

        // ── 1차 레이어: 경로 직접 인접 타일 ──────────────────────────
        if (CountPathNeighbors(x, y) > 0)
            return Hash01(stageIdx, x, y, 101) < 0.62f;

        // ── 2차 레이어: 1차 씨앗 타일에 맞닿은 타일 (클러스터 확장) ──
        if (IsSeedPlatform(stageIdx, x - 1, y) ||
            IsSeedPlatform(stageIdx, x + 1, y) ||
            IsSeedPlatform(stageIdx, x,     y - 1) ||
            IsSeedPlatform(stageIdx, x,     y + 1))
        {
            return Hash01(stageIdx, x, y, 109) < 0.58f;
        }

        // 경로와 무관한 깊은 용암 지대엔 플랫폼 없음
        return false;
    }

    /// <summary>재귀 없이 "1차 씨앗" 조건(경로 인접 + 해시)만 확인</summary>
    bool IsSeedPlatform(int stageIdx, int x, int y)
    {
        if (!IsInBounds(new Vector2Int(x, y))) return false;
        if (GetTileType(x, y) != TileType.Grass) return false;
        if (x <= 0 || x >= mapWidth - 1 || y <= 0 || y >= mapHeight - 1) return false;
        return CountPathNeighbors(x, y) > 0 && Hash01(stageIdx, x, y, 101) < 0.62f;
    }

    int GetVolcanoPlatformConnectionMask(int x, int y)
    {
        int stageIdx = 3;
        int mask = 0;
        if (IsVolcanoBlockedPathTile(stageIdx, x, y + 1)) mask |= 1;
        if (IsVolcanoBlockedPathTile(stageIdx, x + 1, y)) mask |= 2;
        if (IsVolcanoBlockedPathTile(stageIdx, x, y - 1)) mask |= 4;
        if (IsVolcanoBlockedPathTile(stageIdx, x - 1, y)) mask |= 8;
        return mask;
    }

    bool IsInBounds(Vector2Int tile) =>
        tile.x >= 0 && tile.x < mapWidth && tile.y >= 0 && tile.y < mapHeight;

    /// <summary>맵이 화면을 꽉 채우도록 카메라 설정</summary>
    void FitCameraToMap()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        cam.transform.position = new Vector3(0, 0, -10f);
        cam.orthographic       = true;

        float mapW = mapWidth  * tileSize;
        float mapH = mapHeight * tileSize;

        float screenAspect = (float)Screen.width / Screen.height;
        float mapAspect    = mapW / mapH;

        if (screenAspect >= mapAspect)
            cam.orthographicSize = mapH / 2f;
        else
            cam.orthographicSize = (mapW / screenAspect) / 2f;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (pathWaypoints == null || pathWaypoints.Length < 2) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < pathWaypoints.Length - 1; i++)
        {
            Vector3 f = GetWorldPosition(pathWaypoints[i].x,     pathWaypoints[i].y);
            Vector3 t = GetWorldPosition(pathWaypoints[i + 1].x, pathWaypoints[i + 1].y);
            Gizmos.DrawLine(f, t);
            Gizmos.DrawSphere(f, 0.2f);
        }
        var last = pathWaypoints[pathWaypoints.Length - 1];
        Gizmos.DrawSphere(GetWorldPosition(last.x, last.y), 0.2f);
    }
#endif
}
