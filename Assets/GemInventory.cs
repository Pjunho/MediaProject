using UnityEngine;

public static class GemInventory
{
    public struct GemDefinition
    {
        public int stageIndex;
        public string gemName;
        public string effectSummary;
        public Color color;
    }

    static readonly GemDefinition[] gemDefinitions =
    {
        new GemDefinition
        {
            stageIndex = 1,
            gemName = "초원의 보석",
            effectSummary = "아군 이동 속도 +15%",
            color = new Color(0.30f, 0.82f, 0.38f)
        },
        new GemDefinition
        {
            stageIndex = 2,
            gemName = "사막의 보석",
            effectSummary = "아군 최대 HP +20%",
            color = new Color(0.95f, 0.72f, 0.24f)
        },
        new GemDefinition
        {
            stageIndex = 3,
            gemName = "화산의 보석",
            effectSummary = "이동 속도 +10%, HP +10%",
            color = new Color(0.95f, 0.34f, 0.24f)
        }
    };

    public static GemDefinition[] GetDefinitions() => gemDefinitions;

    public static bool IsUnlocked(int stageIndex)
    {
        return PlayerPrefs.GetInt(BuildUnlockKey(stageIndex), 0) == 1;
    }

    public static bool IsActive(int stageIndex)
    {
        if (!IsUnlocked(stageIndex))
            return false;

        string key = BuildActiveKey(stageIndex);
        if (!PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
            return true;
        }

        return PlayerPrefs.GetInt(key, 1) == 1;
    }

    public static void SetActive(int stageIndex, bool active)
    {
        if (!IsUnlocked(stageIndex))
            return;

        PlayerPrefs.SetInt(BuildActiveKey(stageIndex), active ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void UnlockForStageClear(int stageIndex)
    {
        PlayerPrefs.SetInt(BuildUnlockKey(stageIndex), 1);

        string activeKey = BuildActiveKey(stageIndex);
        if (!PlayerPrefs.HasKey(activeKey))
            PlayerPrefs.SetInt(activeKey, 1);

        PlayerPrefs.Save();
    }

    /// <summary>현재 활성화된 보석들을 기반으로 아군 이동 속도 배율을 반환</summary>
    public static float GetSpeedMultiplier()
    {
        float mult = 1f;
        if (IsActive(1)) mult += 0.15f;  // 초원의 보석: 속도 +15%
        if (IsActive(3)) mult += 0.10f;  // 화산의 보석: 속도 +10%
        return mult;
    }

    /// <summary>현재 활성화된 보석들을 기반으로 아군 최대 HP 배율을 반환</summary>
    public static float GetHpMultiplier()
    {
        float mult = 1f;
        if (IsActive(2)) mult += 0.20f;  // 사막의 보석: HP +20%
        if (IsActive(3)) mult += 0.10f;  // 화산의 보석: HP +10%
        return mult;
    }

    static string BuildActiveKey(int stageIndex) => $"Gem_{stageIndex}_Active";
    static string BuildUnlockKey(int stageIndex) => $"Gem_{stageIndex}_Unlocked";
}
