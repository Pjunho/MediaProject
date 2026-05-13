using UnityEngine;

/// <summary>
/// 아군 타입별 속도/체력 업그레이드 상태 관리.
/// 코인으로 각 3단계까지 업그레이드 가능하며 스테이지 재시작 시 초기화된다.
/// </summary>
public static class UpgradeSystem
{
    public enum StatType { Speed, Hp }

    static readonly int[]   upgradeCosts       = { 1, 3, 6 };  // 1→2→3단계 비용
    static readonly float   speedBonusPerLevel = 0.10f;         // 단계당 속도 +10%
    static readonly float   hpBonusPerLevel    = 0.15f;         // 단계당 HP  +15%

    const int MAX_LEVEL = 3;
    const int TYPE_COUNT = 6; // Warrior, Archer, Mage, Cleric, Rogue, Paladin

    static readonly int[] speedLevels = new int[TYPE_COUNT];
    static readonly int[] hpLevels    = new int[TYPE_COUNT];

    static int TypeIndex(AllyType t) => (int)t;

    public static int  GetSpeedLevel(AllyType t) => speedLevels[TypeIndex(t)];
    public static int  GetHpLevel   (AllyType t) => hpLevels   [TypeIndex(t)];
    public static int  GetMaxLevel()             => MAX_LEVEL;

    /// <summary>다음 단계 업그레이드 비용. 이미 최대 레벨이면 -1 반환.</summary>
    public static int GetNextCost(AllyType t, StatType stat)
    {
        int level = stat == StatType.Speed ? speedLevels[TypeIndex(t)] : hpLevels[TypeIndex(t)];
        if (level >= MAX_LEVEL) return -1;
        return upgradeCosts[level];
    }

    public static float GetSpeedMultiplier(AllyType t)
        => 1f + speedLevels[TypeIndex(t)] * speedBonusPerLevel;

    public static float GetHpMultiplier(AllyType t)
        => 1f + hpLevels[TypeIndex(t)] * hpBonusPerLevel;

    /// <summary>
    /// 업그레이드 시도. 성공 시 true 및 costSpent에 차감 금액 기록.
    /// 코인 차감은 호출자(GameManager)가 수행한다.
    /// </summary>
    public static bool TryUpgrade(AllyType t, StatType stat, float availableCoins, out int costSpent)
    {
        costSpent = 0;
        int cost = GetNextCost(t, stat);
        if (cost < 0)            return false;
        if (availableCoins < cost) return false;

        if (stat == StatType.Speed) speedLevels[TypeIndex(t)]++;
        else                         hpLevels   [TypeIndex(t)]++;

        costSpent = cost;
        return true;
    }

    /// <summary>스테이지 시작/재시작 시 호출 — 모든 업그레이드 초기화</summary>
    public static void ResetForStage()
    {
        for (int i = 0; i < TYPE_COUNT; i++)
        {
            speedLevels[i] = 0;
            hpLevels   [i] = 0;
        }
    }

    /// <summary>AllyPlacer에서 스폰 직후 호출 — 업그레이드 배율을 ally에 적용</summary>
    public static void ApplyToAlly(AllyBase ally, AllyType allyType)
    {
        float sm = GetSpeedMultiplier(allyType);
        float hm = GetHpMultiplier(allyType);
        if (sm != 1f) ally.moveSpeed *= sm;
        if (hm != 1f)
        {
            ally.maxHp    *= hm;
            ally.currentHp = ally.maxHp;
        }
    }
}
