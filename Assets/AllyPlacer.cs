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

    [Header("Spawn Settings")]
    public float spawnInterval = 1.5f;  // 아군 사이 출전 간격 (초) — 현재 동시 출전이므로 간격용

    private List<Vector3>   pathPositions = new List<Vector3>();
    private List<AllyType>  deployOrder   = new List<AllyType>();

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
    /// 현재 웨이브에서 투입할 count명을 스폰한다.
    /// 선택한 출전 명단의 앞쪽부터 count명만 사용한다.
    /// </summary>
    /// <returns>실제 투입된 인원 수</returns>
    public int DeployWave(int count)
    {
        if (pathPositions == null || pathPositions.Count < 2)
        {
            Debug.LogError("[AllyPlacer] 경로 미등록 상태에서 DeployWave 호출!");
            return 0;
        }

        int deployed = 0;
        int spawnCount = Mathf.Min(Mathf.Max(0, count), deployOrder.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            DeployAlly(deployOrder[i], deployed);
            deployed++;
        }

        Debug.Log($"[AllyPlacer] 웨이브 투입 완료 — {deployed}명 (요청값: {count})");
        return deployed;
    }

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

        AddHpBar(go, ally);

        ally.OnReachedGoal += a =>
        {
            Debug.Log($"[AllyPlacer] ✅ {a.allyName} 도달!");
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

    // ── HP 바 생성 ────────────────────────────────────────────────────────
    void AddHpBar(GameObject parent, AllyBase ally)
    {
        var barBg = new GameObject("HpBarBg");
        barBg.transform.SetParent(parent.transform, false);
        barBg.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        var bgSr = barBg.AddComponent<SpriteRenderer>();
        bgSr.sprite = MakeSq(Color.black);
        bgSr.sortingOrder = 11;
        barBg.transform.localScale = new Vector3(0.8f, 0.1f, 1f);

        var barFill = new GameObject("HpBarFill");
        barFill.transform.SetParent(barBg.transform, false);
        barFill.transform.localPosition = Vector3.zero;
        var fillSr = barFill.AddComponent<SpriteRenderer>();
        fillSr.sprite = MakeSq(Color.green);
        fillSr.sortingOrder = 12;
        barFill.transform.localScale = Vector3.one;

        var hpBar = parent.AddComponent<HpBar>();
        hpBar.ally   = ally;
        hpBar.fillTf = barFill.transform;
        hpBar.fillSr = fillSr;
    }

    Sprite MakeSq(Color c)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, c);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
