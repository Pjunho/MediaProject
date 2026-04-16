using UnityEngine;
using System.Collections.Generic;

public class EnemyAutoSpawner : MonoBehaviour
{
    struct SpawnCandidate
    {
        public Vector2Int tile;
        public Vector3 world;
        public float distanceFromRoute;
    }

    [Header("References")]
    public Map map;

    [Header("Spawn Settings")]
    public int sniperCount   = 2;
    public int spearmanCount = 3;
    public int brawlerCount  = 4;

    [Tooltip("경로 타일로부터 최소 거리")]
    public float minDistFromPath = 1.5f;

    [Tooltip("적끼리 너무 붙지 않게 하는 최소 거리")]
    public float minEnemySpacing = 2.25f;

    // 배치된 적 목록 (게임 시작 시 활성화)
    private List<EnemyBase> spawnedEnemies = new List<EnemyBase>();
    private readonly List<Vector3> reservedPositions = new List<Vector3>();
    private int minNearRouteSpawns = 0;
    private int maxNearRouteSpawns = 0;
    private int nearRouteSpawned = 0;

    void Start()
    {
        if (map == null) map = FindFirstObjectByType<Map>();
        StartCoroutine(InitializeAfterMapReady());
    }

    System.Collections.IEnumerator InitializeAfterMapReady()
    {
        float timeout = Time.realtimeSinceStartup + 2f;
        while ((map == null || !map.IsGenerated) &&
               Time.realtimeSinceStartup < timeout)
        {
            if (map == null) map = FindFirstObjectByType<Map>();
            yield return null;
        }

        if (map == null || !map.IsGenerated)
        {
            Debug.LogWarning("[EnemyAutoSpawner] 맵 생성 완료 전에는 적을 배치할 수 없습니다.");
            yield break;
        }

        RespawnForCurrentWave();
    }

    void ApplyStageConfig()
    {
        if (StageManager.Instance == null) return;

        var waveCfg = StageManager.Instance.GetCurrentWaveConfig();
        sniperCount   = waveCfg.sniperCount;
        spearmanCount = waveCfg.spearmanCount;
        brawlerCount  = waveCfg.brawlerCount;
        minDistFromPath = waveCfg.minDistFromPath;
        minEnemySpacing = waveCfg.minEnemySpacing;
        minNearRouteSpawns = waveCfg.minNearRouteSpawns;

        int totalEnemies = sniperCount + spearmanCount + brawlerCount;
        maxNearRouteSpawns = Mathf.Clamp(waveCfg.maxNearRouteSpawns, minNearRouteSpawns, totalEnemies);
    }

    public void RespawnForCurrentWave()
    {
        if (map == null) map = FindFirstObjectByType<Map>();
        if (map == null || !map.IsGenerated) return;
        ClearSpawnedEnemies();
        ApplyStageConfig();
        SpawnAll();
    }

    void SpawnAll()
    {
        reservedPositions.Clear();
        nearRouteSpawned = 0;

        var candidates = GetValidGrassTiles();
        if (candidates.Count == 0) { Debug.LogWarning("[EnemyAutoSpawner] 배치 가능 타일 없음"); return; }

        // 근접 적은 항상 최단 루트 상(경로 근처)에서 먼저 배치한다.
        SpawnType<EnemyBrawler>  (candidates, brawlerCount,  "근접병", true);
        SpawnType<EnemySniper>   (candidates, sniperCount,   "저격수", false);
        SpawnType<EnemySpearman> (candidates, spearmanCount, "창병", false);

        Debug.Log($"[EnemyAutoSpawner] {spawnedEnemies.Count}명 배치 완료 (비활성 대기 중)");
    }

    void ClearSpawnedEnemies()
    {
        for (int i = 0; i < spawnedEnemies.Count; i++)
        {
            if (spawnedEnemies[i] == null) continue;
            spawnedEnemies[i].gameObject.SetActive(false);
            Destroy(spawnedEnemies[i].gameObject);
        }
        spawnedEnemies.Clear();
        reservedPositions.Clear();
    }

    void SpawnType<T>(List<SpawnCandidate> candidates, int count, string name, bool forceNearRoute) where T : EnemyBase
    {
        int spawned = 0;
        for (int attempt = 0; attempt < count; attempt++)
        {
            Vector3 pos;
            bool took = forceNearRoute
                ? TryTakeSpawnPosition(candidates, true, out pos)
                : TryTakeSpawnPosition(candidates, out pos);

            if (!took)
            {
                Debug.LogWarning($"[EnemyAutoSpawner] {name} 배치 위치가 부족해 {spawned}/{count}명만 배치했습니다.");
                break;
            }

            pos.z = -1f;

            var go = new GameObject(name);
            go.transform.position = pos;
            go.AddComponent<SpriteRenderer>().sortingOrder = 10;

            var enemy = go.AddComponent<T>();

            // ★ 비활성 배치 (isPlaced=false → 공격 안 함)
            enemy.PlaceInactive(pos);
            enemy.ShowRange();

            spawnedEnemies.Add(enemy);
            spawned++;
        }
    }

    /// <summary>RouteDrawer가 게임 시작 시 호출 - 모든 적 활성화</summary>
    public void ActivateAllEnemies()
    {
        foreach (var e in spawnedEnemies)
            if (e != null) e.Activate();
        Debug.Log($"[EnemyAutoSpawner] {spawnedEnemies.Count}명 적 활성화!");
    }

    bool TryTakeSpawnPosition(List<SpawnCandidate> candidates, out Vector3 pos)
    {
        if (nearRouteSpawned < minNearRouteSpawns && TryTakeSpawnPosition(candidates, true, out pos))
            return true;

        if (TryTakeSpawnPosition(candidates, false, out pos))
            return true;

        if (nearRouteSpawned < maxNearRouteSpawns && TryTakeSpawnPosition(candidates, true, out pos))
            return true;

        pos = default;
        return false;
    }

    bool TryTakeSpawnPosition(List<SpawnCandidate> candidates, bool allowNearRoute, out Vector3 pos)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            bool isNearRoute = candidate.distanceFromRoute < minDistFromPath;
            if (isNearRoute && !allowNearRoute) continue;
            if (!isNearRoute && allowNearRoute) continue;

            Vector3 candidatePos = candidate.world;
            if (!HasEnoughSpacing(candidatePos)) continue;

            pos = candidatePos;
            candidates.RemoveAt(i);
            RemoveNearbyCandidates(candidates, candidatePos);
            reservedPositions.Add(candidatePos);
            if (isNearRoute) nearRouteSpawned++;
            return true;
        }

        pos = default;
        return false;
    }

    bool HasEnoughSpacing(Vector3 pos)
    {
        foreach (var used in reservedPositions)
            if (Vector2.Distance(used, pos) < minEnemySpacing)
                return false;
        return true;
    }

    void RemoveNearbyCandidates(List<SpawnCandidate> candidates, Vector3 center)
    {
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (Vector2.Distance(candidates[i].world, center) < minEnemySpacing)
                candidates.RemoveAt(i);
        }
    }

    List<SpawnCandidate> GetValidGrassTiles()
    {
        var result = new List<SpawnCandidate>();
        if (map == null || map.pathWaypoints == null || map.pathWaypoints.Length < 2)
            return result;

        Vector3 start = map.GetWorldPosition(map.pathWaypoints[0].x, map.pathWaypoints[0].y);
        Vector3 goal = map.GetWorldPosition(
            map.pathWaypoints[map.pathWaypoints.Length - 1].x,
            map.pathWaypoints[map.pathWaypoints.Length - 1].y);

        for (int x = 0; x < map.mapWidth; x++)
        for (int y = 0; y < map.mapHeight; y++)
        {
            if (map.GetTileType(x, y) != Map.TileType.Grass) continue;
            Vector3 wp = map.GetWorldPosition(x, y);
            float routeDist = DistanceToSegment(wp, start, goal);

            result.Add(new SpawnCandidate
            {
                tile = new Vector2Int(x, y),
                world = wp,
                distanceFromRoute = routeDist
            });
        }

        result.Sort((a, b) =>
        {
            int distCmp = b.distanceFromRoute.CompareTo(a.distanceFromRoute);
            if (distCmp != 0) return distCmp;
            return Random.Range(-1, 2);
        });

        return result;
    }

    float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector2 ap = new Vector2(point.x - a.x, point.y - a.y);
        Vector2 ab = new Vector2(b.x - a.x, b.y - a.y);
        float abLenSq = ab.sqrMagnitude;
        if (abLenSq <= Mathf.Epsilon) return ap.magnitude;

        float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / abLenSq);
        Vector2 closest = new Vector2(a.x, a.y) + ab * t;
        return Vector2.Distance(new Vector2(point.x, point.y), closest);
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        { int j = Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); }
    }
}
