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
            effectSummary = "이동 속도 +15% 예정",
            color = new Color(0.30f, 0.82f, 0.38f)
        },
        new GemDefinition
        {
            stageIndex = 2,
            gemName = "사막의 보석",
            effectSummary = "원거리 대응 강화 예정",
            color = new Color(0.95f, 0.72f, 0.24f)
        },
        new GemDefinition
        {
            stageIndex = 3,
            gemName = "화산의 보석",
            effectSummary = "공격 버프 계열 예정",
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

    static string BuildActiveKey(int stageIndex) => $"Gem_{stageIndex}_Active";
    static string BuildUnlockKey(int stageIndex) => $"Gem_{stageIndex}_Unlocked";
}
