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

    private Sprite grassSprite;
    private Sprite dirtSprite;

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

        grassSprite = TileTextureGenerator.GetGrassSprite();
        dirtSprite  = TileTextureGenerator.GetDirtSprite();

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
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Vector3  pos  = GetWorldPosition(x, y);
                TileType type = tileMap[x, y];
                GameObject prefab = (type == TileType.Dirt) ? dirtTilePrefab : grassTilePrefab;

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

                    if (type == TileType.Dirt)
                    {
                        sr.sprite = dirtSprite;
                        float tint = Random.Range(0.92f, 1.05f);
                        sr.color = new Color(tint, tint, tint, 1f);
                    }
                    else
                    {
                        sr.sprite = grassSprite;
                        // 벽 타일에 살짝 다크 틴트 적용해 구분감 강조
                        float tint = Random.Range(0.72f, 0.88f);
                        sr.color = new Color(tint, tint, tint, 1f);
                    }
                }

                tile.name = $"Tile_{x}_{y}";
                tileObjects[x, y] = tile;
            }
        }

        FitCameraToMap();
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
