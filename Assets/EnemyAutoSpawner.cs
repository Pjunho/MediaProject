using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 스테이지·웨이브에 따라 적을 자동 배치하는 컴포넌트.
///
/// ▶ 배치 우선순위 (타워 이스케이프 스타일)
///   - 원거리 적 (저격수·창병): 최단 경로 선분 근처에 먼저 배치
///     → 플레이어의 경로 선택을 압박하는 핵심 장애물
///   - 근접 적 (근접병): 경로에서 먼 곳에 배치
///     → 넓은 영역 커버, 길목 이탈 시 위협
/// </summary>
public class EnemyAutoSpawner : MonoBehaviour
{
    // ── 내부 데이터 ──────────────────────────────────────────────
    struct SpawnCandidate
    {
        public Vector2Int tile;
        public Vector3    world;
        public float      distanceFromRoute;
        public bool       preferredPlatform;
    }

    [Header("References")]
    public Map map;

    [Header("Spawn Settings")]
    public int   sniperCount   = 2;
    public int   spearmanCount = 3;
    public int   brawlerCount  = 4;

    [Tooltip("이 거리 미만 = '경로 근처' 타일로 분류")]
    public float minDistFromPath = 1.9f;

    [Tooltip("적끼리 최소 간격")]
    public float minEnemySpacing = 2.35f;

    // ── 상태 ─────────────────────────────────────────────────────
    readonly List<EnemyBase>        spawnedEnemies     = new();
    readonly List<Vector3>          reservedPositions  = new();

    // ── 라이프사이클 ──────────────────────────────────────────────
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
            Debug.LogWarning("[EnemyAutoSpawner] 맵 생성 완료 전 타임아웃 — 적 배치 취소");
            yield break;
        }

        RespawnForCurrentWave();
    }

    // ── 스테이지/웨이브 설정 읽기 ────────────────────────────────
    void ApplyStageConfig()
    {
        if (StageManager.Instance == null) return;

        // 웨이브별로 적 수·거리 파라미터가 달라지므로 WaveConfig 우선 사용
        var waveCfg = StageManager.Instance.GetCurrentWaveConfig();
        sniperCount     = waveCfg.sniperCount;
        spearmanCount   = waveCfg.spearmanCount;
        brawlerCount    = waveCfg.brawlerCount;
        minDistFromPath = waveCfg.minDistFromPath;
        minEnemySpacing = waveCfg.minEnemySpacing;
        // nearRouteRatio·minNearRouteSpawns·maxNearRouteSpawns 는
        // 새 풀 분리 방식에서 사용하지 않으므로 무시
    }

    // ── 공개 API ─────────────────────────────────────────────────

    /// <summary>웨이브 전환 시 기존 적 제거 후 재배치</summary>
    public void RespawnForCurrentWave()
    {
        if (map == null) map = FindFirstObjectByType<Map>();
        if (map == null || !map.IsGenerated) return;
        ClearSpawnedEnemies();
        ApplyStageConfig();
        SpawnAll();
    }

    /// <summary>RouteDrawer가 게임 시작 시 호출 — 모든 적 활성화</summary>
    public void ActivateAllEnemies()
    {
        foreach (var e in spawnedEnemies)
            if (e != null) e.Activate();
        Debug.Log($"[EnemyAutoSpawner] {spawnedEnemies.Count}명 적 활성화!");
    }

    // ── 핵심 배치 로직 ────────────────────────────────────────────

    void SpawnAll()
    {
        reservedPositions.Clear();

        var allCandidates = GetValidGrassTiles();
        if (allCandidates.Count == 0)
        {
            Debug.LogWarning("[EnemyAutoSpawner] 배치 가능한 타일이 없습니다.");
            return;
        }

        var nearPool = new List<SpawnCandidate>();
        var farPool  = new List<SpawnCandidate>();
        foreach (var c in allCandidates)
        {
            if (c.distanceFromRoute < minDistFromPath) nearPool.Add(c);
            else                                        farPool.Add(c);
        }
        nearPool.Sort(CompareNearCandidates);
        farPool.Sort(CompareFarCandidates);

        int stage = StageManager.Instance?.currentStageIndex ?? 1;
        var (sniperType, spearmanType, brawlerType) = GetStageEnemyTypes(stage);

        // 원거리 적: 경로 근처 우선
        SpawnEnemyType(nearPool, farPool,  sniperCount,   "저격수",  sniperType);
        SpawnEnemyType(nearPool, farPool,  spearmanCount, "창병",    spearmanType);
        // 근접 적: 경로 먼 곳 우선
        SpawnEnemyType(farPool,  nearPool, brawlerCount,  "근접병",  brawlerType);

        Debug.Log($"[EnemyAutoSpawner] Stage {stage} 배치 완료 — {spawnedEnemies.Count}명 " +
                  $"({sniperType.Name} {sniperCount} / {spearmanType.Name} {spearmanCount} / {brawlerType.Name} {brawlerCount})");
    }

    /// <summary>스테이지 인덱스 → (저격수 타입, 창병 타입, 근접병 타입)</summary>
    static (System.Type sniper, System.Type spearman, System.Type brawler) GetStageEnemyTypes(int stage)
        => stage switch
        {
            1 => (typeof(GrassSniper),    typeof(GrassSpearman),   typeof(GrassBrawler)),
            2 => (typeof(DesertSniper),   typeof(DesertSpearman),  typeof(DesertBrawler)),
            3 => (typeof(VolcanoSniper),  typeof(VolcanoSpearman), typeof(EnemyBrawler)),
            4 => (typeof(ShadowSniper),   typeof(ShadowSpearman),  typeof(ShadowBrawler)),
            5 => (typeof(FortressSniper), typeof(FortressSpearman),typeof(FortressBrawler)),
            _ => (typeof(EnemySniper),    typeof(EnemySpearman),   typeof(EnemyBrawler))
        };

    /// <summary>primary → secondary 순서로 배치. 타입은 런타임에 결정.</summary>
    void SpawnEnemyType(
        List<SpawnCandidate> primary,
        List<SpawnCandidate> secondary,
        int count, string label, System.Type enemyType)
    {
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            if (!TryTakeFrom(primary, out Vector3 pos) &&
                !TryTakeFrom(secondary, out pos))
            {
                Debug.LogWarning(
                    $"[EnemyAutoSpawner] {label} 배치 위치 부족 — {spawned}/{count}명만 배치됨");
                break;
            }

            pos.z = -1f;
            var go = new GameObject(label);
            go.transform.position = pos;
            go.AddComponent<SpriteRenderer>().sortingOrder = 10;

            var enemy = (EnemyBase)go.AddComponent(enemyType);
            enemy.PlaceInactive(pos);

            spawnedEnemies.Add(enemy);
            spawned++;
        }
    }

    /// <summary>
    /// 후보 리스트 앞쪽부터 간격 조건을 만족하는 첫 자리를 확보.
    /// 확보된 자리 주변의 후보는 즉시 제거해 중복 배치 방지.
    /// </summary>
    bool TryTakeFrom(List<SpawnCandidate> candidates, out Vector3 pos)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            Vector3 candidatePos = candidates[i].world;
            if (!HasEnoughSpacing(candidatePos)) continue;

            pos = candidatePos;
            candidates.RemoveAt(i);
            RemoveNearbyCandidates(candidates, candidatePos);
            reservedPositions.Add(candidatePos);
            return true;
        }
        pos = default;
        return false;
    }

    // ── 유틸리티 ─────────────────────────────────────────────────

    void ClearSpawnedEnemies()
    {
        foreach (var e in spawnedEnemies)
            if (e != null) Destroy(e.gameObject);
        spawnedEnemies.Clear();
        reservedPositions.Clear();
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
            if (Vector2.Distance(candidates[i].world, center) < minEnemySpacing)
                candidates.RemoveAt(i);
    }

    /// <summary>
    /// 맵의 모든 풀(Grass) 타일을 후보로 수집.
    /// 경로 선분(start→goal)까지의 거리를 계산해 far→near 순으로 정렬.
    /// </summary>
    List<SpawnCandidate> GetValidGrassTiles()
    {
        var result = new List<SpawnCandidate>();
        if (map == null || map.pathWaypoints == null || map.pathWaypoints.Length < 2)
            return result;

        Vector3 start = map.GetWorldPosition(
            map.pathWaypoints[0].x, map.pathWaypoints[0].y);
        Vector3 goal  = map.GetWorldPosition(
            map.pathWaypoints[map.pathWaypoints.Length - 1].x,
            map.pathWaypoints[map.pathWaypoints.Length - 1].y);

        for (int x = 0; x < map.mapWidth; x++)
        for (int y = 0; y < map.mapHeight; y++)
        {
            if (map.GetTileType(x, y) != Map.TileType.Grass) continue;
            Vector3 wp = map.GetWorldPosition(x, y);

            result.Add(new SpawnCandidate
            {
                tile              = new Vector2Int(x, y),
                world             = wp,
                distanceFromRoute = DistanceToSegment(wp, start, goal),
                preferredPlatform = map.IsEnemyPlatformTile(x, y)
            });
        }

        // 기본 정렬: far→near (farPool의 초기 순서)
        result.Sort((a, b) =>
        {
            int pref = b.preferredPlatform.CompareTo(a.preferredPlatform);
            if (pref != 0) return pref;
            int cmp = b.distanceFromRoute.CompareTo(a.distanceFromRoute);
            return cmp != 0 ? cmp : Random.Range(-1, 2);
        });

        return result;
    }

    int CompareNearCandidates(SpawnCandidate a, SpawnCandidate b)
    {
        int pref = b.preferredPlatform.CompareTo(a.preferredPlatform);
        if (pref != 0) return pref;
        int cmp = a.distanceFromRoute.CompareTo(b.distanceFromRoute);
        return cmp != 0 ? cmp : Random.Range(-1, 2);
    }

    int CompareFarCandidates(SpawnCandidate a, SpawnCandidate b)
    {
        int pref = b.preferredPlatform.CompareTo(a.preferredPlatform);
        if (pref != 0) return pref;
        int cmp = b.distanceFromRoute.CompareTo(a.distanceFromRoute);
        return cmp != 0 ? cmp : Random.Range(-1, 2);
    }

    /// <summary>점 p에서 선분 a-b까지의 최단 거리</summary>
    float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector2 ap = new Vector2(point.x - a.x, point.y - a.y);
        Vector2 ab = new Vector2(b.x - a.x,     b.y - a.y);
        float abLenSq = ab.sqrMagnitude;
        if (abLenSq <= Mathf.Epsilon) return ap.magnitude;

        float   t       = Mathf.Clamp01(Vector2.Dot(ap, ab) / abLenSq);
        Vector2 closest = new Vector2(a.x, a.y) + ab * t;
        return Vector2.Distance(new Vector2(point.x, point.y), closest);
    }
}
