using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public bool IsPaused     => isPaused;
    public bool IsGameStarted => gameStarted;

    public int goalCount = 0;
    public int deadCount = 0;

    private bool isPaused       = false;
    private bool resultShown    = false;
    private bool gameStarted    = false;
    private int  speedIndex     = 0;
    private int  currentCoins   = 0;
    private int  clearedWaveCount = 0;

    static readonly float[]  speedSteps  = { 1f, 1.5f, 2f, 3f };
    static readonly string[] speedLabels = { "1x", "1.5x", "2x", "3x" };

    private GameObject pausePanel;
    private GameObject pauseBox;       // 일시정지 패널 내부 박스 (설정 패널 표시 시 숨김)
    private GameObject settingsPanel;
    private Text       settingsVolumeLbl;
    private Text       settingsFsStatLbl;
    private Text       speedBtnTxt;
    private Text       goalCountTxt;   // 일시정지 패널 내 골 카운트
    private Text       coinTxt;        // HUD 상단 코인 표시

    // ── 토스트 알림 ──────────────────────────────────────────────────────
    private GameObject toastGo;
    private Text       toastTxt;
    private Coroutine  toastCoroutine;

    // ── 웨이브 시스템 ──────────────────────────────────────────────────────
    private StageManager.WaveConfig[] currentWaves;
    private int  currentWaveIndex = -1;
    private int  waveGoalCount    = 0;  // 이번 웨이브에서 골 도달한 수
    private int  waveAllyCount    = 0;  // 이번 웨이브에서 실제 투입된 수
    private int  waveAllyDone     = 0;  // 이번 웨이브에서 완료된 수(골+사망)
    private bool waveInProgress   = false;

    // 웨이브 HUD
    private Text       waveInfoTxt;   // "웨이브 X / Y" 표시
    private Text       mainGoalTxt;   // HUD 상단 골 카운트 표시
    private Text       starProgressTxt; // 별 진행 상황 (★☆☆ 등)
    private GameObject waveBannerGo;  // 웨이브 사이 배너
    private Text       waveBannerTxt;
    private Text       waveBannerStatsTxt; // 웨이브 클리어 시 통계 서브텍스트

    struct BtnData { public RectTransform rt; public Image fill; public Color n, h; public System.Action cb; public bool pauseOnly; }
    System.Collections.Generic.List<BtnData> btns = new();

    static readonly Color COL_GOLD   = new Color(1f,  0.85f, 0.20f);
    static readonly Color COL_PANEL  = new Color(0.06f,0.08f,0.14f, 0.95f);
    static readonly Color COL_GREEN  = new Color(0.15f,0.55f,0.25f);
    static readonly Color COL_GREEN_H= new Color(0.22f,0.72f,0.35f);
    static readonly Color COL_BLUE   = new Color(0.20f,0.30f,0.55f);
    static readonly Color COL_BLUE_H = new Color(0.30f,0.45f,0.75f);
    static readonly Color COL_RED    = new Color(0.55f,0.12f,0.12f);
    static readonly Color COL_RED_H  = new Color(0.75f,0.18f,0.18f);
    static readonly Color COL_SPEED  = new Color(0.18f,0.22f,0.32f);
    static readonly Color COL_SPEED_H= new Color(0.28f,0.35f,0.50f);
    static readonly Color COL_PAUSE  = new Color(0.14f,0.18f,0.28f);
    static readonly Color COL_PAUSE_H= new Color(0.24f,0.30f,0.45f);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Time.timeScale = 1f;
        speedIndex = 0;
        SettingsManager.Apply();
        EnsureEventSystem();
        BuildHUD();
        if (pausePanel != null) pausePanel.SetActive(false);
        if (waveBannerGo != null) waveBannerGo.SetActive(false);
    }

    void Start() { }

    /// <summary>RouteDrawer가 경로 확정 후 호출 — 해당 경로로 다음 웨이브 시작</summary>
    public void ConfirmRouteAndStartWave(System.Collections.Generic.List<Vector3> worldPath,
                                         System.Collections.Generic.List<AllyType> order)
    {
        if (worldPath == null || worldPath.Count < 2) return;
        if (waveInProgress) return;

        int stageIdx = StageManager.Instance != null ? StageManager.Instance.currentStageIndex : 1;
        order = StageManager.NormalizeSelectedAllies(order, stageIdx);

        if (StageManager.Instance != null)
            StageManager.Instance.SetSelectedAlliesForStage(order, stageIdx);

        var placer = AllyPlacer.Instance != null ? AllyPlacer.Instance : FindFirstObjectByType<AllyPlacer>();
        if (placer != null)
        {
            placer.enabled = true;
            placer.InitWithPathAndOrder(worldPath, order);
        }

        if (!gameStarted)
        {
            gameStarted = true;
            currentCoins      = StageManager.GetStageConfig(stageIdx).startingCoins;
            currentWaves      = StageManager.GetWaves(stageIdx);
            currentWaveIndex  = 0;
            clearedWaveCount  = 0;
            SkillSystem.ResetForStage();
            UpgradeSystem.ResetForStage();
            StageManager.Instance?.SetCurrentWaveNumber(1);
            UpdateCoinHUD();

            var enemySpawner = FindFirstObjectByType<EnemyAutoSpawner>();
            if (enemySpawner != null)
                enemySpawner.RespawnForCurrentWave();

            Debug.Log($"[GameManager] 게임 시작! (총 {currentWaves.Length}웨이브)");
        }

        StartCurrentWave();
    }

    void Update()
    {
        if (resultShown) return;

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            TogglePause();

        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 mp = mouse.position.ReadValue();

        foreach (var b in btns)
        {
            if (b.rt == null || !b.rt.gameObject.activeInHierarchy) continue;
            bool active = b.pauseOnly ? isPaused : true;
            if (!active) continue;
            bool over = RectTransformUtility.RectangleContainsScreenPoint(b.rt, mp, null);
            if (b.fill != null) b.fill.color = over ? b.h : b.n;
            if (over && mouse.leftButton.wasPressedThisFrame) b.cb?.Invoke();
        }
    }

    void OnDestroy() => Time.timeScale = 1f;

    // ── 웨이브 진행 ────────────────────────────────────────────────────────

    void StartCurrentWave()
    {
        if (currentWaves == null || currentWaveIndex < 0 || currentWaveIndex >= currentWaves.Length)
            return;

        StageManager.Instance?.SetCurrentWaveNumber(currentWaveIndex + 1);

        var wave = currentWaves[currentWaveIndex];
        waveGoalCount = 0;
        waveAllyDone  = 0;
        waveAllyCount = 0;
        waveInProgress = true;

        UpdateWaveHUD();

        Debug.Log($"[GameManager] ▶ 웨이브 {currentWaveIndex + 1}/{currentWaves.Length} 시작 " +
                  $"(투입: {wave.allyCount}명, 클리어 조건: {wave.goalRequirement}명 이상 골 도달)");

        var enemySpawner = FindFirstObjectByType<EnemyAutoSpawner>();
        enemySpawner?.ActivateAllEnemies();

        var placer = AllyPlacer.Instance != null ? AllyPlacer.Instance : FindFirstObjectByType<AllyPlacer>();
        if (placer != null)
        {
            waveAllyCount = placer.DeployWave(wave.allyCount);
        }

        // 투입 인원이 0이면 즉시 웨이브 완료 처리
        if (waveAllyCount == 0)
            CheckWaveCompletion();
    }

    void CheckWaveCompletion()
    {
        if (!waveInProgress) return;
        if (waveAllyDone < waveAllyCount) return;

        waveInProgress = false;

        var wave = currentWaves[currentWaveIndex];
        bool wavePassed = (wave.goalRequirement <= 0) || (waveGoalCount >= wave.goalRequirement);

        if (!wavePassed)
        {
            Debug.Log($"[GameManager] ✗ 웨이브 {currentWaveIndex + 1} 실패 " +
                      $"(골 도달: {waveGoalCount}/{wave.goalRequirement})");
            EndStage();
        }
        else if (currentWaveIndex + 1 >= currentWaves.Length)
        {
            clearedWaveCount++;
            Debug.Log($"[GameManager] ✓ 최종 웨이브 {currentWaveIndex + 1} 클리어! " +
                      $"(총 클리어 웨이브: {clearedWaveCount})");
            EndStage();
        }
        else
        {
            clearedWaveCount++;
            Debug.Log($"[GameManager] ✓ 웨이브 {currentWaveIndex + 1} 클리어! " +
                      $"(골 도달: {waveGoalCount}/{wave.goalRequirement})");
            int clearedWaveNumber = currentWaveIndex + 1;
            currentWaveIndex++;
            StartCoroutine(WaveClearTransition(clearedWaveNumber));
        }
    }

    IEnumerator WaveClearTransition(int clearedWaveNumber)
    {
        string stats = $"통과 {waveGoalCount} / {waveAllyCount}명  •  코인 +{waveGoalCount}  •  다음 경로를 설정하세요";
        ShowWaveBanner($"웨이브 {clearedWaveNumber} 클리어!", stats);
        yield return new WaitForSecondsRealtime(2.2f);
        HideWaveBanner();
        PrepareWaveEnvironment(currentWaveIndex);
        RouteDrawer.CreateForWavePlanning();
    }

    void PrepareWaveEnvironment(int waveIndex)
    {
        if (currentWaves == null || waveIndex < 0 || waveIndex >= currentWaves.Length)
            return;

        int waveNumber = waveIndex + 1;
        StageManager.Instance?.SetCurrentWaveNumber(waveNumber);

        var map = FindFirstObjectByType<Map>();
        if (map != null)
            map.GenerateMap();

        var enemySpawner = FindFirstObjectByType<EnemyAutoSpawner>();
        if (enemySpawner != null)
            enemySpawner.RespawnForCurrentWave();

        var allies = FindObjectsByType<AllyBase>(FindObjectsSortMode.None);
        for (int i = 0; i < allies.Length; i++)
        {
            if (allies[i] == null) continue;
            Destroy(allies[i].gameObject);
        }

        UpdateWaveHUD();
        Debug.Log($"[GameManager] 다음 웨이브 준비 완료 — 웨이브 {waveNumber}");
    }

    void ShowWaveBanner(string text, string stats = "")
    {
        if (waveBannerTxt      != null) waveBannerTxt.text      = text;
        if (waveBannerStatsTxt != null) waveBannerStatsTxt.text = stats;
        if (waveBannerGo       != null) waveBannerGo.SetActive(true);
    }

    void HideWaveBanner()
    {
        if (waveBannerGo != null) waveBannerGo.SetActive(false);
    }

    // ── 보고 콜백 ──────────────────────────────────────────────────────────

    public void ReportGoal()
    {
        goalCount++;
        waveGoalCount++;
        waveAllyDone++;
        currentCoins++;
        UpdateGoalCount();
        UpdateCoinHUD();
        UpdateWaveHUD();
        Debug.Log($"[GameManager] 골 도달 — 총: {goalCount}명, 이번 웨이브: {waveGoalCount}명 (+1코인, 잔여 {currentCoins})");
        CheckWaveCompletion();
    }

    public void ReportDead()
    {
        deadCount++;
        waveAllyDone++;
        CheckWaveCompletion();
    }

    public bool ShouldBlockGameplayInput()
    {
        return isPaused || resultShown;
    }

    // ── HUD 구성 ──────────────────────────────────────────────────────────

    void BuildHUD()
    {
        if (GameObject.Find("HUDCanvas") != null) return;

        var cgo = new GameObject("HUDCanvas");
        var cv  = cgo.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 50;
        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280, 720);
        sc.matchWidthOrHeight  = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        // 웨이브 정보 (상단 좌측)
        waveInfoTxt = BuildTopLabel(cgo.transform, "wave", "웨이브 - / -",
            new Vector2(-400, 320), new Vector2(200, 40));

        // 별 진행 표시 (웨이브 텍스트 아래)
        starProgressTxt = BuildTopLabel(cgo.transform, "star", "☆ ☆ ☆",
            new Vector2(-400, 276), new Vector2(200, 34));
        if (starProgressTxt != null)
        {
            starProgressTxt.color    = COL_GOLD;
            starProgressTxt.fontSize = 14;
        }

        // 골 카운트 (상단 중앙)
        mainGoalTxt = BuildTopLabel(cgo.transform, "goal", "골: 0명",
            new Vector2(-180, 320), new Vector2(160, 40));

        // 코인 표시 (배속 버튼 왼쪽)
        coinTxt = BuildTopLabel(cgo.transform, "coin", "코인: 0",
            new Vector2(440, 320), new Vector2(130, 40));

        // 배속 버튼
        BuildIconBtn(cgo.transform, "speed", speedLabels[0],
            new Vector2(550, 320), new Vector2(80, 44),
            COL_SPEED, COL_SPEED_H, OnSpeedClicked, out speedBtnTxt);

        // 정지 버튼
        BuildIconBtn(cgo.transform, "pause", "⏸",
            new Vector2(610, 320), new Vector2(44, 44),
            COL_PAUSE, COL_PAUSE_H, OnPauseClicked, out _);

        BuildPausePanel(cgo.transform);
        pausePanel.SetActive(false);

        BuildWaveBanner(cgo.transform);
        waveBannerGo.SetActive(false);

        BuildSettingsPanel(cgo.transform);
        BuildToastUI(cgo.transform);
    }

    Text BuildTopLabel(Transform parent, string id, string text, Vector2 pos, Vector2 size)
    {
        var bg = new GameObject("TopLabel_" + id);
        bg.transform.SetParent(parent, false);
        bg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchoredPosition = pos;
        bgRt.sizeDelta        = size + new Vector2(6, 6);

        var tgo = new GameObject("Txt");
        tgo.transform.SetParent(bg.transform, false);
        var tx = tgo.AddComponent<Text>();
        tx.text      = text;
        tx.color     = Color.white;
        tx.fontSize  = 18;
        tx.alignment = TextAnchor.MiddleCenter;
        tx.fontStyle = FontStyle.Bold;
        tx.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var tRt = tgo.GetComponent<RectTransform>();
        tRt.anchoredPosition = Vector2.zero;
        tRt.sizeDelta        = size;
        return tx;
    }

    void BuildWaveBanner(Transform parent)
    {
        waveBannerGo = new GameObject("WaveBanner");
        waveBannerGo.transform.SetParent(parent, false);

        // 반투명 배경 (통계 텍스트 공간 확보)
        var bg = waveBannerGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.70f);
        var bgRt = waveBannerGo.GetComponent<RectTransform>();
        bgRt.anchoredPosition = new Vector2(0, 60);
        bgRt.sizeDelta        = new Vector2(520, 110);

        // 주 텍스트 (클리어 메시지)
        var tgo = new GameObject("BannerTxt");
        tgo.transform.SetParent(waveBannerGo.transform, false);
        waveBannerTxt = tgo.AddComponent<Text>();
        waveBannerTxt.text      = "";
        waveBannerTxt.color     = new Color(0.3f, 1f, 0.4f);
        waveBannerTxt.fontSize  = 32;
        waveBannerTxt.alignment = TextAnchor.MiddleCenter;
        waveBannerTxt.fontStyle = FontStyle.Bold;
        waveBannerTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var tRt = tgo.GetComponent<RectTransform>();
        tRt.anchoredPosition = new Vector2(0, 26);
        tRt.sizeDelta        = new Vector2(510, 44);

        // 통계 서브텍스트 (통과 인원 / 획득 코인)
        var sgo = new GameObject("BannerStatsTxt");
        sgo.transform.SetParent(waveBannerGo.transform, false);
        waveBannerStatsTxt = sgo.AddComponent<Text>();
        waveBannerStatsTxt.text      = "";
        waveBannerStatsTxt.color     = new Color(0.85f, 0.90f, 1f, 0.92f);
        waveBannerStatsTxt.fontSize  = 17;
        waveBannerStatsTxt.alignment = TextAnchor.MiddleCenter;
        waveBannerStatsTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var sRt = sgo.GetComponent<RectTransform>();
        sRt.anchoredPosition = new Vector2(0, -24);
        sRt.sizeDelta        = new Vector2(504, 34);
    }

    void BuildPausePanel(Transform parent)
    {
        pausePanel = new GameObject("PausePanel");
        pausePanel.transform.SetParent(parent, false);

        var overlay = new GameObject("Overlay"); overlay.transform.SetParent(pausePanel.transform, false);
        overlay.AddComponent<Image>().color = new Color(0f,0f,0f,0.65f);
        var ort = overlay.GetComponent<RectTransform>(); ort.anchoredPosition = Vector2.zero; ort.sizeDelta = new Vector2(1920,1080);

        var box = new GameObject("Box"); box.transform.SetParent(pausePanel.transform, false);
        pauseBox = box;
        box.AddComponent<Image>().color = COL_PANEL;
        var brt = box.GetComponent<RectTransform>(); brt.anchoredPosition = new Vector2(0,40); brt.sizeDelta = new Vector2(460,340);

        var border = new GameObject("Border"); border.transform.SetParent(box.transform, false);
        border.AddComponent<Image>().color = new Color(1f,1f,1f,0.15f);
        var bord = border.GetComponent<RectTransform>(); bord.anchoredPosition = Vector2.zero; bord.sizeDelta = new Vector2(464,344);
        border.transform.SetAsFirstSibling();

        CreateTxtIn(box.transform, "⏸  일시정지", new Color(0.8f,0.85f,1f), new Vector2(0,130), new Vector2(420,50), 30);
        CreateImgIn(box.transform, new Color(1f,1f,1f,0.15f), new Vector2(0,100), new Vector2(400,1));

        string condText = StageManager.Instance != null
            ? StageManager.Instance.GetStarConditionText()
            : "★ 1명 통과\n★★ 2명 통과\n★★★ 3명 통과";
        CreateTxtIn(box.transform, condText, COL_GOLD, new Vector2(0,30), new Vector2(400,90), 20);

        goalCountTxt = CreateTxtIn(box.transform, $"현재 통과: {goalCount}명",
            new Color(0.85f,0.9f,0.85f), new Vector2(0,-40), new Vector2(400,35), 20);

        CreateImgIn(box.transform, new Color(1f,1f,1f,0.15f), new Vector2(0,-70), new Vector2(400,1));

        RegPanelBtn(box.transform, "resume",   "▶\n재개",  new Vector2(-130,-130), new Vector2(90,80), COL_GREEN, COL_GREEN_H, OnResume);
        RegPanelBtn(box.transform, "settings", "⚙\n설정",  new Vector2(0,   -130), new Vector2(90,80), COL_BLUE,  COL_BLUE_H,  OnSettings);
        RegPanelBtn(box.transform, "exit",     "🚪\n퇴장", new Vector2(130, -130), new Vector2(90,80), COL_RED,   COL_RED_H,   OnExit);
    }

    void UpdateWaveHUD()
    {
        if (waveInfoTxt != null && currentWaves != null)
        {
            string waveStr = currentWaveIndex >= 0
                ? $"웨이브 {currentWaveIndex + 1} / {currentWaves.Length}"
                : $"웨이브 - / {currentWaves.Length}";
            waveInfoTxt.text = waveStr;
        }

        if (mainGoalTxt != null)
            mainGoalTxt.text = $"골: {goalCount}명";

        UpdateStarProgressHUD();
        UpdateGoalCount();
        UpdateCoinHUD();
    }

    void UpdateStarProgressHUD()
    {
        if (starProgressTxt == null) return;

        int stageIdx = StageManager.Instance != null ? StageManager.Instance.currentStageIndex : 1;
        int currentStars = StageManager.Instance != null
            ? StageManager.Instance.CalcStars(clearedWaveCount)
            : 0;

        // 다음 별까지 남은 웨이브 수 계산
        int[] thresholds = { 5, 10, 15 };
        string nextHint = "";
        if (currentStars < 3)
        {
            int nextThreshold = thresholds[currentStars];
            int remaining = nextThreshold - clearedWaveCount;
            if (remaining > 0)
                nextHint = $" (다음★까지 {remaining}웨이브)";
        }

        starProgressTxt.text = BuildStarStr(currentStars) + nextHint;
    }

    // ── 일시정지 / 속도 ───────────────────────────────────────────────────

    void OnSpeedClicked()
    {
        if (isPaused) return;
        speedIndex = (speedIndex + 1) % speedSteps.Length;
        Time.timeScale = isPaused ? 0f : speedSteps[speedIndex];
        if (speedBtnTxt != null) speedBtnTxt.text = speedLabels[speedIndex];
    }

    void OnPauseClicked() => TogglePause();

    void TogglePause() => SetPaused(!isPaused);

    void SetPaused(bool paused)
    {
        if (resultShown) return;

        isPaused = paused;
        if (pausePanel != null) pausePanel.SetActive(isPaused);
        Time.timeScale = isPaused ? 0f : speedSteps[speedIndex];
        UpdateGoalCount();
    }

    void OnResume()    { SetPaused(false); }
    void OnSettings()
    {
        if (settingsVolumeLbl != null) settingsVolumeLbl.text = VolumePct();
        if (settingsFsStatLbl != null) settingsFsStatLbl.text = FsText();
        if (pauseBox != null) pauseBox.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }
    void OnExit()      { Time.timeScale = 1f; isPaused = false; EndStage(); }

    void BuildSettingsPanel(Transform parent)
    {
        settingsPanel = new GameObject("SettingsPanel");
        settingsPanel.transform.SetParent(parent, false);

        CreateImgIn(settingsPanel.transform, new Color(0f, 0f, 0f, 0.82f), Vector2.zero, new Vector2(1920, 1080));
        CreateImgIn(settingsPanel.transform, COL_PANEL, new Vector2(0, 40), new Vector2(460, 320));
        CreateImgIn(settingsPanel.transform, new Color(1f, 1f, 1f, 0.12f), new Vector2(0, 40), new Vector2(464, 324));

        CreateTxtIn(settingsPanel.transform, "⚙  설  정", COL_GOLD, new Vector2(0, 173), new Vector2(380, 46), 30);
        CreateImgIn(settingsPanel.transform, new Color(1f, 1f, 1f, 0.15f), new Vector2(0, 147), new Vector2(420, 2));

        // 볼륨 행
        CreateTxtIn(settingsPanel.transform, "마스터 볼륨", new Color(0.85f, 0.88f, 0.94f), new Vector2(-90, 100), new Vector2(175, 32), 20);
        settingsVolumeLbl = CreateTxtIn(settingsPanel.transform, VolumePct(), COL_GOLD, new Vector2(115, 100), new Vector2(110, 32), 20);
        RegPanelBtn(settingsPanel.transform, "gm_vol_dn", "−", new Vector2(-38, 60), new Vector2(50, 40), COL_BLUE, COL_BLUE_H, () => GmAdjustVolume(-0.1f));
        RegPanelBtn(settingsPanel.transform, "gm_vol_up", "+", new Vector2(38,  60), new Vector2(50, 40), COL_BLUE, COL_BLUE_H, () => GmAdjustVolume(+0.1f));

        // 전체화면 행
        CreateTxtIn(settingsPanel.transform, "전체화면", new Color(0.85f, 0.88f, 0.94f), new Vector2(-90, 10), new Vector2(175, 32), 20);
        settingsFsStatLbl = CreateTxtIn(settingsPanel.transform, FsText(), COL_GOLD, new Vector2(115, 10), new Vector2(110, 32), 20);
        RegPanelBtn(settingsPanel.transform, "gm_fs", "전환", new Vector2(0, -30), new Vector2(110, 40), COL_BLUE, COL_BLUE_H, GmToggleFs);

        CreateImgIn(settingsPanel.transform, new Color(1f, 1f, 1f, 0.15f), new Vector2(0, -65), new Vector2(420, 2));
        RegPanelBtn(settingsPanel.transform, "gm_set_close", "← 뒤로", new Vector2(0, -95), new Vector2(170, 46), COL_RED, COL_RED_H, CloseSettingsPanel);

        settingsPanel.SetActive(false);
    }

    void BuildToastUI(Transform parent)
    {
        toastGo = new GameObject("Toast");
        toastGo.transform.SetParent(parent, false);
        var bg = toastGo.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.10f, 0.90f);
        var rt = toastGo.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0, -200);
        rt.sizeDelta = new Vector2(310, 48);

        var tgo = new GameObject("Txt");
        tgo.transform.SetParent(toastGo.transform, false);
        toastTxt = tgo.AddComponent<Text>();
        toastTxt.fontSize  = 20;
        toastTxt.fontStyle = FontStyle.Bold;
        toastTxt.alignment = TextAnchor.MiddleCenter;
        toastTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var tRt = tgo.GetComponent<RectTransform>();
        tRt.anchoredPosition = Vector2.zero;
        tRt.sizeDelta        = new Vector2(300, 44);
        toastGo.SetActive(false);
    }

    public void ShowToast(string message, Color color)
    {
        if (toastGo == null) return;
        if (toastCoroutine != null) StopCoroutine(toastCoroutine);
        toastCoroutine = StartCoroutine(ToastCoroutine(message, color));
    }

    IEnumerator ToastCoroutine(string message, Color baseColor)
    {
        if (toastTxt == null || toastGo == null) yield break;
        toastTxt.text  = message;
        toastTxt.color = baseColor;
        toastGo.SetActive(true);

        float elapsed   = 0f;
        float duration  = 1.8f;
        float fadeStart = 1.1f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = elapsed > fadeStart
                ? 1f - (elapsed - fadeStart) / (duration - fadeStart)
                : 1f;
            toastTxt.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }

        toastGo.SetActive(false);
        toastCoroutine = null;
    }

    void GmAdjustVolume(float delta)
    {
        SettingsManager.Volume = SettingsManager.Volume + delta;
        if (settingsVolumeLbl != null) settingsVolumeLbl.text = VolumePct();
    }

    void GmToggleFs()
    {
        SettingsManager.IsFullscreen = !SettingsManager.IsFullscreen;
        if (settingsFsStatLbl != null) settingsFsStatLbl.text = FsText();
    }

    void CloseSettingsPanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pauseBox != null) pauseBox.SetActive(true);
    }

    string VolumePct() => $"{Mathf.RoundToInt(SettingsManager.Volume * 100)}%";
    string FsText()    => SettingsManager.IsFullscreen ? "ON" : "OFF";

    /// <summary>AllyOrderPanel 등에서 코인으로 스킬 해금 시 호출</summary>
    public bool TryUnlockSkill(AllyType allyType)
    {
        if (SkillSystem.TryUnlock(allyType, currentCoins, out int cost))
        {
            currentCoins -= cost;
            UpdateCoinHUD();
            Debug.Log($"[GameManager] 스킬 해금: {allyType} (-{cost}코인, 잔여 {currentCoins})");
            return true;
        }
        if (!SkillSystem.IsUnlocked(allyType))
            ShowToast("코인이 부족합니다!", new Color(1f, 0.35f, 0.35f));
        return false;
    }

    public int GetCurrentCoins() => currentCoins;

    /// <summary>AllyOrderPanel에서 속도/체력 업그레이드 요청 시 호출</summary>
    public bool TryUpgrade(AllyType allyType, UpgradeSystem.StatType stat)
    {
        if (UpgradeSystem.TryUpgrade(allyType, stat, currentCoins, out int cost))
        {
            currentCoins -= cost;
            UpdateCoinHUD();
            Debug.Log($"[GameManager] 업그레이드: {allyType} {stat} (-{cost}코인, 잔여 {currentCoins})");
            return true;
        }
        if (UpgradeSystem.GetNextCost(allyType, stat) < 0)
            ShowToast("이미 최고 레벨입니다!", new Color(1f, 0.85f, 0.2f));
        else
            ShowToast("코인이 부족합니다!", new Color(1f, 0.35f, 0.35f));
        return false;
    }

    // ── 스테이지 종료 ────────────────────────────────────────────────────

    public void EndStage()
    {
        if (resultShown) return;
        resultShown = true;
        waveInProgress = false;
        Time.timeScale = 1f;

        if (waveBannerGo != null) waveBannerGo.SetActive(false);

        int stageIdx = StageManager.Instance != null ? StageManager.Instance.currentStageIndex : 1;
        int stars    = StageManager.Instance != null ? StageManager.Instance.CalcStars(clearedWaveCount) : 0;
        StageManager.SaveStars(stageIdx, stars);
        if (stars >= 3)
            GemInventory.UnlockForStageClear(stageIdx);

        if (pausePanel != null) pausePanel.SetActive(false);
        ShowResult(stars);
    }

    void UpdateGoalCount()
    {
        if (goalCountTxt != null) goalCountTxt.text = $"현재 통과: {goalCount}명";
    }

    void UpdateCoinHUD()
    {
        if (coinTxt != null)
            coinTxt.text = $"코인: {currentCoins}";
    }

    // ── 결과 화면 ────────────────────────────────────────────────────────

    void ShowResult(int stars)
    {
        var cgo = new GameObject("ResultCanvas");
        var cv  = cgo.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 200;
        var sc  = cgo.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280,720); sc.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        CreateImgIn(cgo.transform, new Color(0f,0f,0f,0.78f), Vector2.zero, new Vector2(1920,1080));

        bool win = stars > 0;
        Color panelCol = win ? new Color(0.04f,0.14f,0.06f) : new Color(0.14f,0.04f,0.04f);
        CreateImgIn(cgo.transform, panelCol, new Vector2(0,30), new Vector2(520,440));
        CreateImgIn(cgo.transform, new Color(1f,1f,1f,0.13f), new Vector2(0,30), new Vector2(524,444));

        string titleStr = win ? "스테이지 클리어!" : "스테이지 실패";
        Color  titleCol = win ? new Color(0.3f,1f,0.4f) : new Color(1f,0.3f,0.3f);
        CreateTxtIn(cgo.transform, titleStr, titleCol, new Vector2(0,230), new Vector2(500,70), 44);
        CreateTxtIn(cgo.transform, BuildStarStr(stars), COL_GOLD, new Vector2(0,150), new Vector2(420,70), 56);

        // 웨이브 진행 정보
        int totalWaves = currentWaves != null ? currentWaves.Length : 0;
        CreateTxtIn(cgo.transform, $"웨이브 {clearedWaveCount} / {totalWaves} 클리어",
            new Color(0.75f, 0.85f, 1f), new Vector2(0, 70), new Vector2(460, 40), 22);

        int stageIdx = StageManager.Instance != null ? StageManager.Instance.currentStageIndex : 1;
        int prevBest = StageManager.GetSavedStars(stageIdx);
        CreateTxtIn(cgo.transform, $"통과: {goalCount}명    사망: {deadCount}명\n최고 기록: {BuildStarStr(prevBest)}",
            new Color(0.85f,0.85f,0.85f), new Vector2(0,10), new Vector2(460,68), 20);

        string cond = StageManager.Instance != null ? StageManager.Instance.GetStarConditionText() : "";
        CreateTxtIn(cgo.transform, cond, new Color(1f,0.85f,0.2f,0.55f), new Vector2(0,-75), new Vector2(400,90), 17);

        RegResultBtn(cgo.transform, "retry",  "↺ 다시하기",     new Vector2(-120,-185), new Vector2(200,52), new Color(0.2f,0.35f,0.6f), new Color(0.3f,0.5f,0.8f), OnRetry);
        RegResultBtn(cgo.transform, "select", "≡ 스테이지 선택", new Vector2(120,-185),  new Vector2(220,52), new Color(0.28f,0.28f,0.28f), new Color(0.48f,0.48f,0.48f), OnStageSelect);

        if (win && stageIdx < StageManager.GetStageCount())
        {
            int nextIdx = stageIdx + 1;
            string nextName = StageManager.GetStageConfig(nextIdx).stageName;
            RegResultBtn(cgo.transform, "next",
                $"▶  STAGE {nextIdx}  {nextName}",
                new Vector2(0, -250), new Vector2(360, 52),
                new Color(0.12f, 0.42f, 0.22f), new Color(0.18f, 0.58f, 0.30f),
                () => OnNextStage(nextIdx));
        }
    }

    string BuildStarStr(int s) => s switch { 1=>"★ ☆ ☆", 2=>"★ ★ ☆", 3=>"★ ★ ★", _=>"☆ ☆ ☆" };
    void OnRetry()       => SceneManager.LoadScene("MediaProject", LoadSceneMode.Single);
    void OnStageSelect() => SceneManager.LoadScene("StageSelect",  LoadSceneMode.Single);
    void OnNextStage(int nextStageIdx)
    {
        if (StageManager.Instance != null)
            StageManager.Instance.currentStageIndex = nextStageIdx;
        Time.timeScale = 1f;
        SceneManager.LoadScene("StageSelect", LoadSceneMode.Single);
    }

    // ── UI 헬퍼 ──────────────────────────────────────────────────────────

    Image BuildIconBtn(Transform p, string id, string label, Vector2 pos, Vector2 size, Color n, Color h, System.Action cb, out Text lTxt)
    {
        var go = new GameObject("IconBtn_" + id); go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = new Color(0f,0f,0f,0.45f);
        var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = size + new Vector2(3,3);
        var inner = new GameObject("Fill"); inner.transform.SetParent(go.transform, false);
        var fi = inner.AddComponent<Image>(); fi.color = n;
        var ir = inner.GetComponent<RectTransform>(); ir.anchoredPosition = Vector2.zero; ir.sizeDelta = size;
        var btn = inner.AddComponent<Button>();
        btn.targetGraphic = fi;
        btn.onClick.AddListener(() => cb?.Invoke());
        var tg = new GameObject("Lbl"); tg.transform.SetParent(inner.transform, false);
        var tx = tg.AddComponent<Text>(); tx.text = label; tx.color = Color.white; tx.fontSize = 20;
        tx.alignment = TextAnchor.MiddleCenter; tx.fontStyle = FontStyle.Bold;
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var tr = tg.GetComponent<RectTransform>(); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = size;
        lTxt = tx;
        btns.Add(new BtnData { rt=ir, fill=fi, n=n, h=h, cb=cb, pauseOnly=false });
        return fi;
    }

    void RegPanelBtn(Transform p, string id, string label, Vector2 pos, Vector2 size, Color n, Color h, System.Action cb)
    {
        var go = new GameObject("PBtn_"+id); go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = new Color(1f,1f,1f,0.10f);
        var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = size+new Vector2(3,3);
        var inner = new GameObject("Fill"); inner.transform.SetParent(go.transform, false);
        var fi = inner.AddComponent<Image>(); fi.color = n;
        var ir = inner.GetComponent<RectTransform>(); ir.anchoredPosition = Vector2.zero; ir.sizeDelta = size;
        var btn = inner.AddComponent<Button>();
        btn.targetGraphic = fi;
        btn.onClick.AddListener(() => cb?.Invoke());
        var tg = new GameObject("Lbl"); tg.transform.SetParent(inner.transform, false);
        var tx = tg.AddComponent<Text>(); tx.text = label; tx.color = Color.white; tx.fontSize = 22;
        tx.alignment = TextAnchor.MiddleCenter; tx.fontStyle = FontStyle.Bold;
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var tr = tg.GetComponent<RectTransform>(); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = size;
        btns.Add(new BtnData { rt=ir, fill=fi, n=n, h=h, cb=cb, pauseOnly=true });
    }

    void RegResultBtn(Transform p, string id, string label, Vector2 pos, Vector2 size, Color n, Color h, System.Action cb)
    {
        var go = new GameObject("RBtn_"+id); go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = new Color(1f,1f,1f,0.12f);
        var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = size+new Vector2(3,3);
        var inner = new GameObject("Fill"); inner.transform.SetParent(go.transform, false);
        var fi = inner.AddComponent<Image>(); fi.color = n;
        var ir = inner.GetComponent<RectTransform>(); ir.anchoredPosition = Vector2.zero; ir.sizeDelta = size;
        var btn = inner.AddComponent<Button>();
        btn.targetGraphic = fi;
        btn.onClick.AddListener(() => cb?.Invoke());
        var tg = new GameObject("Lbl"); tg.transform.SetParent(inner.transform, false);
        var tx = tg.AddComponent<Text>(); tx.text = label; tx.color = Color.white; tx.fontSize = 22;
        tx.alignment = TextAnchor.MiddleCenter; tx.fontStyle = FontStyle.Bold;
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var tr = tg.GetComponent<RectTransform>(); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = size;
        btns.Add(new BtnData { rt=ir, fill=fi, n=n, h=h, cb=cb, pauseOnly=false });
    }

    void CreateImgIn(Transform p, Color c, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Img"); go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = c;
        var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    Text CreateTxtIn(Transform p, string s, Color c, Vector2 pos, Vector2 size, int fs)
    {
        var go = new GameObject("Txt"); go.transform.SetParent(p, false);
        var tx = go.AddComponent<Text>(); tx.text = s; tx.color = c; tx.fontSize = fs;
        tx.alignment = TextAnchor.MiddleCenter; tx.fontStyle = FontStyle.Bold;
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = size;
        return tx;
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }
}
