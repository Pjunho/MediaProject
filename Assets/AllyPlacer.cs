using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 웨이브 단위로 아군을 스폰하는 컴포넌트.
/// RouteDrawer가 InitWithPathAndOrder()로 경로·순서를 등록하고,
/// GameManager가 DeployWave()를 호출해 웨이브별로 투입한다.
/// </summary>
public class AllyPlacer : MonoBehaviour
{
    public static AllyPlacer Instance { get; private set; }

    [Header("References")]
    public Map map;

    private List<Vector3>   pathPositions = new List<Vector3>();
    private List<AllyType>  deployOrder   = new List<AllyType>();

    // ── 개별 출전 대기열 (Space / Click 기반) ─────────────────────────────
    private Queue<AllyType> deployQueue  = new Queue<AllyType>();
    private int             deployedSoFar = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (map == null) map = FindFirstObjectByType<Map>();
    }

    /// <summary>
    /// RouteDrawer가 경로 확정 후 호출. 경로와 출전 순서를 저장만 하고 즉시 투입하지 않는다.
    /// 실제 투입은 GameManager가 웨이브마다 DeployWave()를 호출해 수행한다.
    /// </summary>
    public void InitWithPathAndOrder(List<Vector3> path, List<AllyType> order)
    {
        if (path == null || path.Count < 2)
        {
            Debug.LogError("[AllyPlacer] 경로 부족!");
            return;
        }

        pathPositions = path;
        deployOrder   = new List<AllyType>(order);

        Debug.Log($"[AllyPlacer] 경로·출전 순서 등록 완료 — 총 {order.Count}명");
    }

    /// <summary>
    /// Space / Click 기반 개별 출전용 — 출전 대기열을 준비한다.
    /// 실제 출전은 DeployNextFromQueue() 호출마다 1명씩 이뤄진다.
    /// </summary>
    /// <returns>대기열에 올라간 총 인원 수</returns>
    public int PrepareDeployQueue(int count)
    {
        deployQueue.Clear();
        deployedSoFar = 0;
        int n = Mathf.Min(Mathf.Max(0, count), deployOrder.Count);
        for (int i = 0; i < n; i++)
            deployQueue.Enqueue(deployOrder[i]);
        Debug.Log($"[AllyPlacer] 출전 대기열 준비 — 총 {n}명 대기");
        return n;
    }

    /// <summary>대기열 선두의 아군 1명을 출전시킨다. 대기열이 비었으면 false.</summary>
    public bool DeployNextFromQueue()
    {
        if (deployQueue.Count == 0) return false;
        DeployAlly(deployQueue.Dequeue(), deployedSoFar);
        deployedSoFar++;
        return true;
    }

    public bool HasPendingDeployments => deployQueue.Count > 0;
    public int  PendingDeployCount    => deployQueue.Count;

    /// <summary>현재 대기열의 아군 타입 목록을 순서대로 반환 (읽기 전용)</summary>
    public AllyType[] PeekDeployQueue() => deployQueue.ToArray();

    // ── 아군 1명 스폰 ────────────────────────────────────────────────────
    void DeployAlly(AllyType type, int waveSlotIndex)
    {
        Vector3 spawnPos = pathPositions[0];
        spawnPos.z = -1f;
        spawnPos += Vector3.left * 0.35f * waveSlotIndex; // 같은 웨이브 내 슬롯 간격

        var go = new GameObject();
        go.transform.position = spawnPos;

        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = Vector3.zero;

        AllyVisualGenerator.BuildCharacterVisual(type, visual.transform, 10);

        AllyBase ally = type switch
        {
            AllyType.Warrior => go.AddComponent<Warrior>(),
            AllyType.Archer  => go.AddComponent<Archer>(),
            AllyType.Mage    => go.AddComponent<Mage>(),
            AllyType.Cleric  => go.AddComponent<Cleric>(),
            AllyType.Rogue   => go.AddComponent<Rogue>(),
            AllyType.Paladin => go.AddComponent<Paladin>(),
            _                => go.AddComponent<Warrior>()
        };

        go.name = ally.allyName;

        // 보석 효과 적용
        float speedMult = GemInventory.GetSpeedMultiplier();
        float hpMult    = GemInventory.GetHpMultiplier();
        if (speedMult != 1f) ally.moveSpeed *= speedMult;
        if (hpMult != 1f)
        {
            ally.maxHp    *= hpMult;
            ally.currentHp = ally.maxHp;
        }

        // 업그레이드 효과 적용 (스킬보다 먼저 기반 스탯에 적용)
        UpgradeSystem.ApplyToAlly(ally, type);

        // 스킬 효과 적용
        SkillSystem.ApplyToAlly(ally, type);

        ally.OnReachedGoal += a =>
        {
            Debug.Log($"[AllyPlacer] ✅ {a.allyName} 도달!");
            FloatingText.Spawn(
                a.transform.position + Vector3.up * 0.5f,
                "+1 코인",
                new Color(1f, 0.85f, 0.2f));
            GameManager.Instance?.ReportGoal();
        };
        ally.OnDied += a =>
        {
            Debug.Log($"[AllyPlacer] ❌ {a.allyName} 사망.");
            GameManager.Instance?.ReportDead();
        };

        ally.SetWaypoints(pathPositions);
        Debug.Log($"[AllyPlacer] 🚀 {ally.allyName} 출전! (웨이브 슬롯 {waveSlotIndex})");
    }

}
