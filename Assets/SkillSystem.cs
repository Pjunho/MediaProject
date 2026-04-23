using UnityEngine;

/// <summary>
/// 아군 타입별 스킬 데이터 및 해금 상태 관리.
/// 코인으로 해금 가능하며 스테이지 재시작 시 초기화된다.
/// </summary>
public static class SkillSystem
{
    public struct SkillData
    {
        public AllyType allyType;
        public string   skillName;
        public string   description;
        public int      cost;
    }

    static readonly SkillData[] skills = new SkillData[]
    {
        new SkillData
        {
            allyType    = AllyType.Warrior,
            skillName   = "방패 밀치기",
            description = "방어막을 펼쳐 첫 번째\n피해를 완전히 무효화합니다.",
            cost        = 3
        },
        new SkillData
        {
            allyType    = AllyType.Archer,
            skillName   = "산탄 화살",
            description = "빠른 발걸음으로 적을 혼란시켜\n이동 속도가 25% 증가합니다.",
            cost        = 2
        },
        new SkillData
        {
            allyType    = AllyType.Mage,
            skillName   = "화염구",
            description = "마법 에너지로 몸을 강화하여\n최대 HP가 30% 증가합니다.",
            cost        = 3
        },
        new SkillData
        {
            allyType    = AllyType.Cleric,
            skillName   = "치유 기도",
            description = "신성한 기도로 매 초\n최대 HP의 3%를 회복합니다.",
            cost        = 4
        }
    };

    static readonly bool[] unlockedSkills = new bool[4];

    public static SkillData[] GetAllSkills() => skills;

    public static SkillData GetSkillForAlly(AllyType allyType)
    {
        for (int i = 0; i < skills.Length; i++)
            if (skills[i].allyType == allyType) return skills[i];
        return skills[0];
    }

    public static bool IsUnlocked(AllyType allyType)
    {
        for (int i = 0; i < skills.Length; i++)
            if (skills[i].allyType == allyType) return unlockedSkills[i];
        return false;
    }

    /// <summary>스테이지 시작/재시작 시 호출 — 모든 스킬 잠금 상태로 초기화</summary>
    public static void ResetForStage()
    {
        for (int i = 0; i < unlockedSkills.Length; i++)
            unlockedSkills[i] = false;
    }

    /// <summary>
    /// 스킬 해금 시도. 성공 시 true 반환 및 costSpent에 차감 코인 수 기록.
    /// 코인 차감은 호출자(GameManager)가 직접 수행한다.
    /// </summary>
    public static bool TryUnlock(AllyType allyType, int availableCoins, out int costSpent)
    {
        costSpent = 0;
        for (int i = 0; i < skills.Length; i++)
        {
            if (skills[i].allyType != allyType) continue;
            if (unlockedSkills[i])              return false;
            if (availableCoins < skills[i].cost) return false;
            unlockedSkills[i] = true;
            costSpent         = skills[i].cost;
            return true;
        }
        return false;
    }

    /// <summary>AllyPlacer에서 스폰 직후 호출 — 해금된 스킬 효과를 ally에 적용</summary>
    public static void ApplyToAlly(AllyBase ally, AllyType allyType)
    {
        if (!IsUnlocked(allyType)) return;

        switch (allyType)
        {
            case AllyType.Warrior:
                ally.EnableSkillShield();
                break;
            case AllyType.Archer:
                ally.moveSpeed *= 1.25f;
                break;
            case AllyType.Mage:
                ally.maxHp     *= 1.30f;
                ally.currentHp  = ally.maxHp;
                break;
            case AllyType.Cleric:
                ally.EnableSkillRegen();
                break;
        }
    }
}
