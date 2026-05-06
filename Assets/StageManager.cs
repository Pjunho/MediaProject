using UnityEngine;

/// <summary>
/// 씬 간 스테이지/웨이브 정보 전달용 싱글턴
/// DontDestroyOnLoad로 유지
/// </summary>
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }
    const int IMPLEMENTED_STAGE_COUNT = 3;

    public struct StageConfig
    {
        public int stageIndex;
        public string stageName;
        public int sniperCount;
        public int spearmanCount;
        public int brawlerCount;
        public int allySlots;
        public int startWaveAllyCount;
        /// <summary>스테이지 시작 시 지급되는 기본 코인 수</summary>
        public int startingCoins;
        /// <summary>1~5웨이브용 기본 맵 가로 타일 수 (홀수 권장)</summary>
        public int mapWidth;
        /// <summary>1~5웨이브용 기본 맵 세로 타일 수 (홀수 권장)</summary>
        public int mapHeight;
        /// <summary>1~5웨이브용 추가 통로 생성 비율 0~1</summary>
        public float extraPassageRate;
    }

    /// <summary>웨이브 하나의 실제 진행 설정</summary>
    public struct WaveConfig
    {
        public int waveNumber;
        public int allyCount;
        public int goalRequirement;
        public int sniperCount;
        public int spearmanCount;
        public int brawlerCount;
        public int mapWidth;
        public int mapHeight;
        public float extraPassageRate;
        public float minDistFromPath;
        public float minEnemySpacing;
        public int minNearRouteSpawns;
        public int maxNearRouteSpawns;
        public float nearRouteRatio;
    }

    [Header("현재 스테이지")]
    public int currentStageIndex = 1; // 1~3
    public int currentWaveNumber = 1; // 1~15
    public System.Collections.Generic.List<AllyType> selectedAllies = new();

    static readonly StageConfig[] stageConfigs = new StageConfig[]
    {
        default,
        new StageConfig
        {
            stageIndex = 1, stageName = "초원의 전투",
            sniperCount = 2, spearmanCount = 2, brawlerCount = 6,
            allySlots = 4, startWaveAllyCount = 4, startingCoins = 3,
            mapWidth = 15, mapHeight = 11,
            extraPassageRate = 0.02f
        },
        new StageConfig
        {
            stageIndex = 2, stageName = "어둠의 동굴",
            sniperCount = 4, spearmanCount = 4, brawlerCount = 4,
            allySlots = 5, startWaveAllyCount = 5, startingCoins = 5,
            mapWidth = 17, mapHeight = 13,
            extraPassageRate = 0.06f
        },
        new StageConfig
        {
            stageIndex = 3, stageName = "화산의 심판",
            sniperCount = 5, spearmanCount = 4, brawlerCount = 3,
            allySlots = 6, startWaveAllyCount = 6, startingCoins = 7,
            mapWidth = 19, mapHeight = 15,
            extraPassageRate = 0.10f
        },
        new StageConfig
        {
            stageIndex = 4, stageName = "어둠의 미궁",
            sniperCount = 6, spearmanCount = 5, brawlerCount = 4,
            allySlots = 6, startWaveAllyCount = 6, startingCoins = 10,
            mapWidth = 21, mapHeight = 15,
            extraPassageRate = 0.16f
        },
        new StageConfig
        {
            stageIndex = 5, stageName = "최후의 요새",
            sniperCount = 7, spearmanCount = 6, brawlerCount = 5,
            allySlots = 6, startWaveAllyCount = 6, startingCoins = 12,
            mapWidth = 23, mapHeight = 17,
            extraPassageRate = 0.22f
        },
    };

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        currentStageIndex = Mathf.Clamp(currentStageIndex, 1, GetStageCount());
        selectedAllies = NormalizeSelectedAllies(selectedAllies, currentStageIndex);
        currentWaveNumber = 1;
    }

    public StageConfig GetCurrentStageConfig() => GetStageConfig(currentStageIndex);
    public WaveConfig GetCurrentWaveConfig() => GetWaveConfig(currentStageIndex, currentWaveNumber);

    public void SetCurrentWaveNumber(int waveNumber)
    {
        currentWaveNumber = Mathf.Clamp(waveNumber, 1, 15);
    }

    public static StageConfig GetStageConfig(int stageIndex)
    {
        if (stageIndex < 1 || stageIndex > GetStageCount())
            return stageConfigs[1];
        return stageConfigs[stageIndex];
    }

    public static int GetStageCount() => Mathf.Min(IMPLEMENTED_STAGE_COUNT, stageConfigs.Length - 1);

    public static int GetWavePhase(int waveNumber)
    {
        if (waveNumber <= 5) return 0;
        if (waveNumber <= 10) return 1;
        return 2;
    }

    public static WaveConfig[] GetWaves(int stageIndex)
    {
        var waves = new WaveConfig[15];
        for (int i = 0; i < waves.Length; i++)
            waves[i] = GetWaveConfig(stageIndex, i + 1);
        return waves;
    }

    public static WaveConfig GetWaveConfig(int stageIndex, int waveNumber)
    {
        var cfg = GetStageConfig(stageIndex);
        waveNumber = Mathf.Clamp(waveNumber, 1, 15);

        int phase = GetWavePhase(waveNumber);
        int waveInPhase = (waveNumber - 1) % 5;
        int allyCount = Mathf.Min(cfg.allySlots, cfg.startWaveAllyCount + phase);

        int mapWidth = EnsureOdd(cfg.mapWidth + phase * 4);
        int mapHeight = EnsureOdd(cfg.mapHeight + phase * 2);
        float extraPassageRate = Mathf.Clamp01(cfg.extraPassageRate + phase * 0.08f + waveInPhase * 0.015f);

        int lateTotal = cfg.sniperCount + cfg.spearmanCount + cfg.brawlerCount;
        float phaseScale = phase switch
        {
            0 => 0.45f,
            1 => 0.70f,
            _ => 1.00f
        };
        int totalEnemies = Mathf.Max(3, Mathf.RoundToInt(lateTotal * phaseScale) + waveInPhase / 2);

        int minNearRouteSpawns = phase switch
        {
            0 => waveInPhase < 2 ? 1 : 2,
            1 => waveInPhase < 2 ? 3 : 4,
            _ => waveInPhase < 2 ? 5 : 6
        };

        float nearRouteRatio = Mathf.Clamp01((phase switch
        {
            0 => 0.08f,
            1 => 0.28f,
            _ => 0.55f
        }) + (stageIndex - 1) * 0.05f + waveInPhase * 0.025f);

        int maxNearRouteSpawns = Mathf.Max(minNearRouteSpawns,
            Mathf.RoundToInt(totalEnemies * nearRouteRatio));
        minNearRouteSpawns = Mathf.Min(minNearRouteSpawns, totalEnemies);
        maxNearRouteSpawns = Mathf.Clamp(maxNearRouteSpawns, minNearRouteSpawns, totalEnemies);

        float minDistFromPath = Mathf.Max(0.70f, (phase switch
        {
            0 => 2.80f,
            1 => 1.90f,
            _ => 1.10f
        }) - (stageIndex - 1) * 0.18f - waveInPhase * 0.08f);

        float minEnemySpacing = Mathf.Max(1.35f, (phase switch
        {
            0 => 3.00f,
            1 => 2.35f,
            _ => 1.85f
        }) - (stageIndex - 1) * 0.12f - waveInPhase * 0.05f);

        int[] counts = SplitEnemyCounts(cfg, totalEnemies);
        int goalRequirement = Mathf.Clamp((phase + 1) + Mathf.Max(0, (stageIndex - 1) / 2), 1, allyCount);

        return new WaveConfig
        {
            waveNumber = waveNumber,
            allyCount = allyCount,
            goalRequirement = goalRequirement,
            sniperCount = counts[0],
            spearmanCount = counts[1],
            brawlerCount = counts[2],
            mapWidth = mapWidth,
            mapHeight = mapHeight,
            extraPassageRate = extraPassageRate,
            minDistFromPath = minDistFromPath,
            minEnemySpacing = minEnemySpacing,
            minNearRouteSpawns = minNearRouteSpawns,
            maxNearRouteSpawns = maxNearRouteSpawns,
            nearRouteRatio = nearRouteRatio
        };
    }

    static int[] SplitEnemyCounts(StageConfig cfg, int totalEnemies)
    {
        float totalWeight = cfg.sniperCount + cfg.spearmanCount + cfg.brawlerCount;
        if (totalWeight <= 0f)
            return new[] { 1, 1, Mathf.Max(1, totalEnemies - 2) };

        float sniperExact = totalEnemies * (cfg.sniperCount / totalWeight);
        float spearmanExact = totalEnemies * (cfg.spearmanCount / totalWeight);

        int sniper = Mathf.Clamp(Mathf.RoundToInt(sniperExact), 0, totalEnemies);
        int spearman = Mathf.Clamp(Mathf.RoundToInt(spearmanExact), 0, totalEnemies - sniper);
        int brawler = Mathf.Max(0, totalEnemies - sniper - spearman);
        int minBrawler = GetMinimumBrawlerCount(totalEnemies);
        if (brawler < minBrawler)
        {
            int need = minBrawler - brawler;
            while (need > 0 && (sniper > 0 || spearman > 0))
            {
                if (sniper >= spearman && sniper > 0) sniper--;
                else if (spearman > 0) spearman--;
                else sniper--;

                brawler++;
                need--;
            }
        }

        if (cfg.brawlerCount >= cfg.sniperCount && cfg.brawlerCount >= cfg.spearmanCount && brawler == 0)
        {
            if (sniper > spearman && sniper > 0) sniper--;
            else if (spearman > 0) spearman--;
            brawler = 1;
        }

        return new[] { sniper, spearman, brawler };
    }

    static int GetMinimumBrawlerCount(int totalEnemies)
    {
        if (totalEnemies <= 3) return 1;
        if (totalEnemies <= 5) return 2;
        if (totalEnemies <= 8) return 3;
        return Mathf.CeilToInt(totalEnemies * 0.35f);
    }

    static int EnsureOdd(int value) => value % 2 == 0 ? value + 1 : value;

    static int[] GetStarThresholds(int stageIndex) => new[] { 5, 10, 15 };

    public static int GetEnemyTotalCount(int stageIndex)
    {
        var waves = GetWaves(stageIndex);
        int total = 0;
        for (int i = 0; i < waves.Length; i++)
            total = Mathf.Max(total, waves[i].sniperCount + waves[i].spearmanCount + waves[i].brawlerCount);
        return total;
    }

    public static bool IsStageUnlocked(int stageIndex)
    {
        if (stageIndex < 1 || stageIndex > GetStageCount()) return false;
        if (stageIndex <= 2) return true;
        return GetSavedStars(stageIndex - 1) > 0;
    }

    public static System.Collections.Generic.List<AllyType> GetDefaultSelectedAllies(int stageIndex)
    {
        int slots = GetStageConfig(stageIndex).allySlots;
        AllyType[] cycle = { AllyType.Warrior, AllyType.Archer, AllyType.Mage, AllyType.Cleric, AllyType.Rogue, AllyType.Paladin };
        var result = new System.Collections.Generic.List<AllyType>(slots);
        for (int i = 0; i < slots; i++)
            result.Add(cycle[i % cycle.Length]);
        return result;
    }

    public void SetSelectedAllies(System.Collections.Generic.List<AllyType> allies)
    {
        selectedAllies = NormalizeSelectedAllies(allies, currentStageIndex);
    }

    public void SetSelectedAlliesForStage(System.Collections.Generic.List<AllyType> allies, int stageIndex)
    {
        selectedAllies = NormalizeSelectedAllies(allies, stageIndex);
    }

    public static System.Collections.Generic.List<AllyType> NormalizeSelectedAllies(
        System.Collections.Generic.List<AllyType> allies,
        int stageIndex)
    {
        int slots = GetStageConfig(stageIndex).allySlots;
        var defaults = GetDefaultSelectedAllies(stageIndex);
        var result = new System.Collections.Generic.List<AllyType>(slots);

        if (allies != null)
        {
            for (int i = 0; i < allies.Count && result.Count < slots; i++)
                result.Add(allies[i]);
        }

        for (int i = result.Count; i < slots; i++)
            result.Add(defaults[i]);

        return result;
    }

    public int CalcStars(int clearedWaves)
    {
        int[] t = GetStarThresholds(currentStageIndex);
        if (clearedWaves <= 0) return 0;
        if (clearedWaves >= t[2]) return 3;
        if (clearedWaves >= t[1]) return 2;
        if (clearedWaves >= t[0]) return 1;
        return 0;
    }

    public string GetStarConditionText()
    {
        int[] t = GetStarThresholds(currentStageIndex);
        var w = GetWaves(currentStageIndex);
        return $"★ {t[0]}웨이브 클리어\n★★ {t[1]}웨이브 클리어\n★★★ {t[2]}웨이브 클리어\n[총 {w.Length}웨이브]";
    }

    public static string GetStarConditionTextForStage(int stageIndex)
    {
        int[] t = GetStarThresholds(stageIndex);
        return $"{t[0]}웨이브  ★☆☆\n{t[1]}웨이브  ★★☆\n{t[2]}웨이브  ★★★";
    }

    public static int GetSavedStars(int stageIndex)
        => PlayerPrefs.GetInt($"Stage_{stageIndex}_Stars", 0);

    public static void SaveStars(int stageIndex, int stars)
    {
        int prev = GetSavedStars(stageIndex);
        if (stars > prev)
            PlayerPrefs.SetInt($"Stage_{stageIndex}_Stars", stars);
        PlayerPrefs.Save();
    }

    public void LoadStage(int stageIndex)
    {
        currentStageIndex = Mathf.Clamp(stageIndex, 1, GetStageCount());
        currentWaveNumber = 1;
        selectedAllies = NormalizeSelectedAllies(selectedAllies, currentStageIndex);
        UnityEngine.SceneManagement.SceneManager.LoadScene("MediaProject",
            UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}
