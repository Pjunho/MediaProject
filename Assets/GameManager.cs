using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

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

    // ── 개별 출전 (Space / Click) ─────────────────────────────────────────
    private bool alliesFullyDeployed = false;

    static readonly float[]  speedSteps  = { 1f, 1.5f, 2f, 3f };
    static readonly string[] speedLabels = { "1x", "1.5x", "2x", "3x" };

    private GameObject pausePanel;
    private GameObject pauseBox;       // 일시정지 패널 내부 박스 (설정 패널 표시 시 숨김)
    private GameObject settingsPanel;
    private Text       settingsVolumeLbl;
    private Text       settingsFsStatLbl;
    private Text       speedBtnTxt;
    private Text       coinTxt;        // HUD 상단 코인 표시

    // ── 보석 가방 버튼 / 팝업 패널 ───────────────────────────────────────
    private GameObject    gemPanelGo;
    private RectTransform gemBagBtnRt;
    private Image         gemBagBtnFill;
    private bool          gemPanelOpen = false;

    // ── 토스트 알림 ──────────────────────────────────────────────────────
    private GameObject toastGo;
    private Text       toastTxt;
    private Coroutine  toastCoroutine;

    // ── 출전 힌트 HUD ─────────────────────────────────────────────────────
    private GameObject deployHintGo;
    private Text       deployHintTxt;

    // ── 웨이브 시스템 ──────────────────────────────────────────────────────
    private StageManager.WaveConfig[] currentWaves;
    private int  currentWaveIndex = -1;
    private int  waveGoalCount    = 0;  // 이번 웨이브에서 골 도달한 수
    private int  waveAllyCount    = 0;  // 이번 웨이브에서 실제 투입된 수
    private int  waveAllyDone     = 0;  // 이번 웨이브에서 완료된 수(골+사망)
    private bool waveInProgress   = false;
    private List<Vector3> currentRoutePath = new List<Vector3>();
    private readonly List<BonusCoinPickup> activeBonusCoins = new List<BonusCoinPickup>();

    struct BonusCoinCandidate
    {
        public Vector3 position;
        public float distanceFromShortestRoute;
    }

    // 웨이브 HUD
    private Text       waveInfoTxt;   // "웨이브 X / Y" 표시
    private Text       starProgressTxt; // 별 진행 상황 (★☆☆ 등)
    private GameObject waveBannerGo;  // 웨이브 사이 배너
    private Text       waveBannerTxt;
    private Text       waveBannerStatsTxt; // 웨이브 클리어 시 통계 서브텍스트

    // ── 스테이지별 위험 요소 ──────────────────────────────────────────────
    private VolcanoHazard volcanoHazard;  // Stage 3: 화산 체력 감소
    private CaveVignette  caveVignette;   // Stage 2: 동굴 화면 비네트

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
    static readonly Color COL_GEM    = new Color(0.18f,0.14f,0.32f);   // 가방 버튼 기본
    static readonly Color COL_GEM_H  = new Color(0.30f,0.22f,0.50f);   // 가방 버튼 호버/열림

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

        InitStageHazards();

        // ── 스테이지 진입 즉시 초기 코인 지급 ─────────────────────────
        // 경로 설계 단계부터 스킬·업그레이드를 살 수 있도록
        // (기존: 첫 경로 확정 후 스타트 버튼 눌러야 지급됐음)
        int stageIdx = StageManager.Instance != null
            ? StageManager.Instance.currentStageIndex : 1;
        currentCoins = StageManager.GetStageConfig(stageIdx).startingCoins;
        UpdateCoinHUD();
        Debug.Log($"[GameManager] 스테이지 진입 — 초기 코인 {currentCoins}개 지급");
    }

    void Start() { }

    /// <summary>
    /// 현재 스테이지에 맞는 환경 위험 요소를 초기화합니다.
    ///   Stage 2 – 동굴 비네트 생성
    ///   Stage 3 – 화산 체력 감소 생성
    /// </summary>
    void InitStageHazards()
    {
        int stage = StageManager.Instance != null ? StageManager.Instance.currentStageIndex : 1;

        if (stage == 2)
        {
            var go = new GameObject("CaveVignette");
            caveVignette = go.AddComponent<CaveVignette>();
        }
        else if (stage == 3)
        {
            var go = new GameObject("VolcanoHazard");
            volcanoHazard = go.AddComponent<VolcanoHazard>();
        }
    }

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
        currentRoutePath = new List<Vector3>(worldPath);

        var placer = AllyPlacer.Instance != null ? AllyPlacer.Instance : FindFirstObjectByType<AllyPlacer>();
        if (placer != null)
        {
            placer.enabled = true;
            placer.InitWithPathAndOrder(worldPath, order);
        }

        if (!gameStarted)
        {
            gameStarted = true;
            // currentCoins는 Awake()에서 이미 지급됨 — 여기서 덮어쓰지 않는다
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
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 mp = mouse.position.ReadValue();

        if (SkillSystem.HandleTargetingInput(mp, mouse))
            return;

        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            TogglePause();

        bool anyBtnClicked = false;
        foreach (var b in btns)
        {
            if (b.rt == null || !b.rt.gameObject.activeInHierarchy) continue;
            bool active = b.pauseOnly ? isPaused : true;
            if (!active) continue;
            bool over = RectTransformUtility.RectangleContainsScreenPoint(b.rt, mp, null);
            if (b.fill != null) b.fill.color = over ? b.h : b.n;
            if (over && mouse.leftButton.wasPressedThisFrame) { b.cb?.Invoke(); anyBtnClicked = true; }
        }

        // 보석 패널이 열려 있고 버튼/패널 바깥을 클릭하면 닫기
        if (gemPanelOpen && mouse.leftButton.wasPressedThisFrame && !anyBtnClicked)
        {
            bool onPanel = gemPanelGo != null && gemPanelGo.activeSelf &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    gemPanelGo.GetComponent<RectTransform>(), mp, null);
            if (!onPanel) CloseGemPanel();
        }

        // Space 또는 UI 외 클릭으로 대기 중인 아군 1명씩 출전
        if (waveInProgress && !alliesFullyDeployed && !isPaused)
        {
            bool spaceDeploy = kb != null && kb.spaceKey.wasPressedThisFrame;
            bool clickDeploy = !anyBtnClicked && mouse.leftButton.wasPressedThisFrame;
            if (spaceDeploy || clickDeploy)
                TriggerDeployNext();
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
        SpawnWaveBonusCoins();

        // Stage 3 화산 체력 감소 시작
        volcanoHazard?.StartDrain();

        UpdateWaveHUD();

        Debug.Log($"[GameManager] ▶ 웨이브 {currentWaveIndex + 1}/{currentWaves.Length} 시작 " +
                  $"(투입: {wave.allyCount}명, 클리어 조건: {wave.goalRequirement}명 이상 골 도달)");

        var enemySpawner = FindFirstObjectByType<EnemyAutoSpawner>();
        enemySpawner?.ActivateAllEnemies();

        var placer = AllyPlacer.Instance != null ? AllyPlacer.Instance : FindFirstObjectByType<AllyPlacer>();
        if (placer != null)
        {
            waveAllyCount = placer.PrepareDeployQueue(wave.allyCount);
        }

        alliesFullyDeployed = (waveAllyCount == 0);

        if (!alliesFullyDeployed)
        {
            // 출전 힌트 표시
            RefreshDeployHint();
            if (deployHintGo != null) deployHintGo.SetActive(true);
        }
        else
        {
            // 투입 인원이 0이면 즉시 웨이브 완료 처리
            CheckWaveCompletion();
        }
    }

    void CheckWaveCompletion()
    {
        if (!waveInProgress) return;
        if (!alliesFullyDeployed) return;   // 아직 대기 중인 아군이 있으면 대기
        if (waveAllyDone < waveAllyCount) return;

        waveInProgress = false;

        // Stage 3 화산 체력 감소 중단
        volcanoHazard?.StopDrain();
        ClearBonusCoins();

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
        ClearBonusCoins();

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

    public void CollectBonusCoin(Vector3 worldPosition)
    {
        currentCoins++;
        UpdateCoinHUD();
        FloatingText.Spawn(worldPosition + Vector3.up * 0.35f, "+1 코인", COL_GOLD);
        Debug.Log($"[GameManager] 보너스 코인 획득 (+1코인, 잔여 {currentCoins})");
    }

    public bool ShouldBlockGameplayInput()
    {
        return isPaused || resultShown || SkillSystem.IsTargeting;
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

        // 코인 표시 (배속 버튼 왼쪽)
        coinTxt = BuildTopLabel(cgo.transform, "coin", "코인: 0",
            new Vector2(440, 320), new Vector2(140, 40));

        // 배속 버튼
        BuildIconBtn(cgo.transform, "speed", speedLabels[0],
            new Vector2(550, 320), new Vector2(80, 44),
            COL_SPEED, COL_SPEED_H, OnSpeedClicked, out speedBtnTxt);

        // 정지 버튼
        BuildIconBtn(cgo.transform, "pause", "II",
            new Vector2(610, 320), new Vector2(44, 44),
            COL_PAUSE, COL_PAUSE_H, OnPauseClicked, out _);

        BuildPausePanel(cgo.transform);
        pausePanel.SetActive(false);

        BuildWaveBanner(cgo.transform);
        waveBannerGo.SetActive(false);

        BuildSettingsPanel(cgo.transform);
        BuildToastUI(cgo.transform);
        BuildGemBagUI(cgo.transform);
        BuildDeployHintUI(cgo.transform);
    }

    // ── 개별 출전 힌트 UI ─────────────────────────────────────────────────

    void BuildDeployHintUI(Transform parent)
    {
        deployHintGo = new GameObject("DeployHint");
        deployHintGo.transform.SetParent(parent, false);
        var bg = deployHintGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);
        SR(deployHintGo.GetComponent<RectTransform>(), new Vector2(0, -305), new Vector2(370, 44));

        var tgo = new GameObject("Txt");
        tgo.transform.SetParent(deployHintGo.transform, false);
        deployHintTxt = tgo.AddComponent<Text>();
        deployHintTxt.text               = "";
        deployHintTxt.color              = new Color(0.98f, 0.92f, 0.40f);
        deployHintTxt.fontSize           = 18;
        deployHintTxt.alignment          = TextAnchor.MiddleCenter;
        deployHintTxt.fontStyle          = FontStyle.Normal;
        deployHintTxt.alignByGeometry    = true;
        deployHintTxt.raycastTarget      = false;
        deployHintTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
        deployHintTxt.verticalOverflow   = VerticalWrapMode.Overflow;
        deployHintTxt.font               = UiPixelFont.Get();
        SR(tgo.GetComponent<RectTransform>(), Vector2.zero, new Vector2(360, 40));

        deployHintGo.SetActive(false);
    }

    void RefreshDeployHint()
    {
        if (deployHintTxt == null) return;
        var placer  = AllyPlacer.Instance != null ? AllyPlacer.Instance : FindFirstObjectByType<AllyPlacer>();
        int pending = placer != null ? placer.PendingDeployCount : 0;
        deployHintTxt.text = $"[Space / Click]  아군 출전  (대기: {pending}명)";
    }

    void TriggerDeployNext()
    {
        var placer = AllyPlacer.Instance != null ? AllyPlacer.Instance : FindFirstObjectByType<AllyPlacer>();
        if (placer == null || !placer.HasPendingDeployments) return;

        placer.DeployNextFromQueue();
        RefreshDeployHint();

        if (!placer.HasPendingDeployments)
        {
            alliesFullyDeployed = true;
            if (deployHintGo != null) deployHintGo.SetActive(false);
            CheckWaveCompletion();
        }
    }

    // ── 가방 버튼 & 보석 팝업 패널 ──────────────────────────────────────────

    void BuildGemBagUI(Transform parent)
    {
        // ── 오른쪽 하단 가방 버튼 ────────────────────────────────────────
        var btnSize = new Vector2(52f, 52f);
        var btnPos  = new Vector2(490f, -310f);

        // 외곽 테두리 shadow
        var bgGo = new GameObject("GemBagBtn"); bgGo.transform.SetParent(parent, false);
        bgGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.50f);
        SR(bgGo.GetComponent<RectTransform>(), btnPos, btnSize + new Vector2(4f, 4f));

        // 채움 (클릭·호버 색상용)
        var fill = new GameObject("Fill"); fill.transform.SetParent(bgGo.transform, false);
        gemBagBtnFill = fill.AddComponent<Image>(); gemBagBtnFill.color = COL_GEM;
        gemBagBtnRt   = fill.GetComponent<RectTransform>();
        SR(gemBagBtnRt, Vector2.zero, btnSize);

        // 아이콘 레이블 (◆ = 보석 기호)
        var lbl = new GameObject("Lbl"); lbl.transform.SetParent(fill.transform, false);
        var lTx = lbl.AddComponent<Text>();
        lTx.text               = "◆";
        lTx.color              = COL_GOLD;
        lTx.fontSize           = 28;
        lTx.alignment          = TextAnchor.MiddleCenter;
        lTx.alignByGeometry    = true;
        lTx.raycastTarget      = false;
        lTx.horizontalOverflow = HorizontalWrapMode.Overflow;
        lTx.verticalOverflow   = VerticalWrapMode.Overflow;
        lTx.font               = UiPixelFont.Get();
        SR(lbl.GetComponent<RectTransform>(), Vector2.zero, btnSize);

        // 버튼 하단 소제목 "보석"
        var sub = new GameObject("Sub"); sub.transform.SetParent(fill.transform, false);
        var sTx = sub.AddComponent<Text>();
        sTx.text               = "보석";
        sTx.color              = new Color(0.80f, 0.75f, 0.95f);
        sTx.fontSize           = 11;
        sTx.alignment          = TextAnchor.LowerCenter;
        sTx.alignByGeometry    = true;
        sTx.raycastTarget      = false;
        sTx.horizontalOverflow = HorizontalWrapMode.Overflow;
        sTx.verticalOverflow   = VerticalWrapMode.Overflow;
        sTx.font               = UiPixelFont.Get();
        SR(sub.GetComponent<RectTransform>(), new Vector2(0f, -4f), btnSize);

        // btns 에 등록 (클릭 = 팝업 토글, 호버 = 색 변환)
        btns.Add(new BtnData
        {
            rt       = gemBagBtnRt,
            fill     = gemBagBtnFill,
            n        = COL_GEM,
            h        = COL_GEM_H,
            cb       = ToggleGemPanel,
            pauseOnly= false
        });

        // ── 보석 팝업 패널 (버튼 위에 열림) ─────────────────────────────
        BuildGemPanel(parent, btnPos, btnSize.y);
    }

    void BuildGemPanel(Transform parent, Vector2 btnPos, float btnH)
    {
        // 활성 보석 수집
        var defs   = GemInventory.GetDefinitions();
        var active = new System.Collections.Generic.List<GemInventory.GemDefinition>();
        foreach (var d in defs)
            if (GemInventory.IsActive(d.stageIndex)) active.Add(d);

        // 패널 크기 계산
        const float ROW_H  = 62f;
        const float HEAD_H = 42f;
        const float PAD_B  = 14f;
        float panelH = HEAD_H + Mathf.Max(1, active.Count) * ROW_H + PAD_B;
        float panelW = 215f;

        // 버튼 바로 위에 정렬
        float panelY = btnPos.y + btnH / 2f + panelH / 2f + 6f;
        var   panelPos = new Vector2(btnPos.x, panelY);

        gemPanelGo = new GameObject("GemPanel"); gemPanelGo.transform.SetParent(parent, false);
        var bg = gemPanelGo.AddComponent<Image>(); bg.color = COL_PANEL;
        SR(gemPanelGo.GetComponent<RectTransform>(), panelPos, new Vector2(panelW, panelH));

        // 패널 테두리
        var border = new GameObject("Border"); border.transform.SetParent(gemPanelGo.transform, false);
        border.AddComponent<Image>().color = new Color(0.60f, 0.48f, 0.90f, 0.30f);
        SR(border.GetComponent<RectTransform>(), Vector2.zero, new Vector2(panelW + 2f, panelH + 2f));
        border.transform.SetAsFirstSibling();

        // 헤더 "◆ 활성 보석"
        float topEdge = panelH / 2f;
        float headerY = topEdge - HEAD_H / 2f;
        CreateTxtIn(gemPanelGo.transform,
            "◆  활성 보석", COL_GOLD,
            new Vector2(0f, headerY), new Vector2(panelW - 12f, HEAD_H - 4f), 17);
        CreateImgIn(gemPanelGo.transform,
            new Color(0.60f, 0.48f, 0.90f, 0.25f),
            new Vector2(0f, topEdge - HEAD_H), new Vector2(panelW - 16f, 1f));

        // 항목 표시
        float rowStart = topEdge - HEAD_H - ROW_H / 2f;

        if (active.Count == 0)
        {
            CreateTxtIn(gemPanelGo.transform,
                "활성화된 보석 없음",
                new Color(0.55f, 0.55f, 0.65f),
                new Vector2(0f, rowStart + (ROW_H - 14f) / 2f - 8f),
                new Vector2(panelW - 16f, ROW_H), 15);
        }
        else
        {
            for (int i = 0; i < active.Count; i++)
            {
                var   gem  = active[i];
                float cy   = rowStart - i * ROW_H;

                // 보석 이름 (원색)
                CreateTxtIn(gemPanelGo.transform,
                    gem.gemName, gem.color,
                    new Vector2(0f, cy + 14f), new Vector2(panelW - 14f, 24f), 16);

                // 효과 설명 (연한 파랑)
                CreateTxtIn(gemPanelGo.transform,
                    gem.effectSummary, new Color(0.80f, 0.88f, 1f),
                    new Vector2(0f, cy - 10f), new Vector2(panelW - 14f, 20f), 13);

                // 구분선
                if (i < active.Count - 1)
                    CreateImgIn(gemPanelGo.transform,
                        new Color(1f, 1f, 1f, 0.10f),
                        new Vector2(0f, cy - ROW_H / 2f + 2f), new Vector2(panelW - 20f, 1f));
            }
        }

        gemPanelGo.SetActive(false);
    }

    void ToggleGemPanel()
    {
        if (gemPanelGo == null) return;
        gemPanelOpen = !gemPanelOpen;
        gemPanelGo.SetActive(gemPanelOpen);
        // 버튼 색상: 열려 있으면 밝게 유지
        if (gemBagBtnFill != null)
            gemBagBtnFill.color = gemPanelOpen ? COL_GEM_H : COL_GEM;
    }

    void CloseGemPanel()
    {
        if (!gemPanelOpen) return;
        gemPanelOpen = false;
        if (gemPanelGo      != null) gemPanelGo.SetActive(false);
        if (gemBagBtnFill   != null) gemBagBtnFill.color = COL_GEM;
    }

    Text BuildTopLabel(Transform parent, string id, string text, Vector2 pos, Vector2 size)
    {
        var bg = new GameObject("TopLabel_" + id);
        bg.transform.SetParent(parent, false);
        bg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        SR(bg.GetComponent<RectTransform>(), pos, size + new Vector2(6, 6));

        var tgo = new GameObject("Txt");
        tgo.transform.SetParent(bg.transform, false);
        var tx = tgo.AddComponent<Text>();
        tx.text               = text;
        tx.color              = Color.white;
        tx.fontSize           = 18;
        tx.alignment          = TextAnchor.MiddleCenter;
        tx.fontStyle          = FontStyle.Normal;
        tx.alignByGeometry    = true;
        tx.raycastTarget      = false;
        tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        tx.verticalOverflow   = VerticalWrapMode.Overflow;
        tx.font               = UiPixelFont.Get();
        SR(tgo.GetComponent<RectTransform>(), Vector2.zero, size + new Vector2(6, 6));
        return tx;
    }

    void BuildWaveBanner(Transform parent)
    {
        waveBannerGo = new GameObject("WaveBanner");
        waveBannerGo.transform.SetParent(parent, false);

        // 반투명 배경 (통계 텍스트 공간 확보)
        var bg = waveBannerGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.70f);
        SR(waveBannerGo.GetComponent<RectTransform>(), new Vector2(0, 60), new Vector2(520, 110));

        // 주 텍스트 (클리어 메시지)
        var tgo = new GameObject("BannerTxt");
        tgo.transform.SetParent(waveBannerGo.transform, false);
        waveBannerTxt = tgo.AddComponent<Text>();
        waveBannerTxt.text               = "";
        waveBannerTxt.color              = new Color(0.3f, 1f, 0.4f);
        waveBannerTxt.fontSize           = 32;
        waveBannerTxt.alignment          = TextAnchor.MiddleCenter;
        waveBannerTxt.fontStyle          = FontStyle.Normal;
        waveBannerTxt.alignByGeometry    = true;
        waveBannerTxt.raycastTarget      = false;
        waveBannerTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
        waveBannerTxt.verticalOverflow   = VerticalWrapMode.Overflow;
        waveBannerTxt.font               = UiPixelFont.Get();
        SR(tgo.GetComponent<RectTransform>(), new Vector2(0, 26), new Vector2(510, 44));

        // 통계 서브텍스트 (통과 인원 / 획득 코인)
        var sgo = new GameObject("BannerStatsTxt");
        sgo.transform.SetParent(waveBannerGo.transform, false);
        waveBannerStatsTxt = sgo.AddComponent<Text>();
        waveBannerStatsTxt.text               = "";
        waveBannerStatsTxt.color              = new Color(0.85f, 0.90f, 1f, 0.92f);
        waveBannerStatsTxt.fontSize           = 17;
        waveBannerStatsTxt.alignment          = TextAnchor.MiddleCenter;
        waveBannerStatsTxt.alignByGeometry    = true;
        waveBannerStatsTxt.raycastTarget      = false;
        waveBannerStatsTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
        waveBannerStatsTxt.verticalOverflow   = VerticalWrapMode.Overflow;
        waveBannerStatsTxt.font               = UiPixelFont.Get();
        SR(sgo.GetComponent<RectTransform>(), new Vector2(0, -24), new Vector2(504, 34));
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

        RegPanelBtn(box.transform, "resume",   "▶ 재개",  new Vector2(-130,-130), new Vector2(90,80), COL_GREEN, COL_GREEN_H, OnResume);
        RegPanelBtn(box.transform, "settings", "⚙ 설정",  new Vector2(0,   -130), new Vector2(90,80), COL_BLUE,  COL_BLUE_H,  OnSettings);
        RegPanelBtn(box.transform, "exit",     "✕ 퇴장",  new Vector2(130, -130), new Vector2(90,80), COL_RED,   COL_RED_H,   OnExit);
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

        UpdateStarProgressHUD();
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

        CreateTxtIn(settingsPanel.transform, "⚙ 설정", COL_GOLD, new Vector2(0, 173), new Vector2(380, 46), 30);
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
        SR(toastGo.GetComponent<RectTransform>(), new Vector2(0, -200), new Vector2(310, 48));

        var tgo = new GameObject("Txt");
        tgo.transform.SetParent(toastGo.transform, false);
        toastTxt = tgo.AddComponent<Text>();
        toastTxt.fontSize           = 20;
        toastTxt.fontStyle          = FontStyle.Normal;
        toastTxt.alignment          = TextAnchor.MiddleCenter;
        toastTxt.alignByGeometry    = true;
        toastTxt.raycastTarget      = false;
        toastTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
        toastTxt.verticalOverflow   = VerticalWrapMode.Overflow;
        toastTxt.font               = UiPixelFont.Get();
        SR(tgo.GetComponent<RectTransform>(), Vector2.zero, new Vector2(300, 44));
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

        if (waveBannerGo  != null) waveBannerGo.SetActive(false);
        if (deployHintGo  != null) deployHintGo.SetActive(false);
        ClearBonusCoins();

        int stageIdx = StageManager.Instance != null ? StageManager.Instance.currentStageIndex : 1;
        int stars    = StageManager.Instance != null ? StageManager.Instance.CalcStars(clearedWaveCount) : 0;
        StageManager.SaveStars(stageIdx, stars);
        if (stars >= 3)
            GemInventory.UnlockForStageClear(stageIdx);

        if (pausePanel != null) pausePanel.SetActive(false);
        ShowResult(stars);
    }

    void UpdateCoinHUD()
    {
        if (coinTxt != null)
            coinTxt.text = $"코인: {currentCoins}";
    }

    void SpawnWaveBonusCoins()
    {
        ClearBonusCoins();

        int coinCount = RollBonusCoinCount();
        if (coinCount <= 0 || currentRoutePath == null || currentRoutePath.Count < 3)
            return;

        var positions = PickBonusCoinPositions(coinCount);
        for (int i = 0; i < positions.Count; i++)
        {
            var go = new GameObject("BonusCoin");
            var coin = go.AddComponent<BonusCoinPickup>();
            coin.Init(positions[i]);
            activeBonusCoins.Add(coin);
        }
    }

    int RollBonusCoinCount()
    {
        float r = Random.value;
        if (r < 0.96f) return 0;
        if (r < 0.99f) return 1;
        return 2;
    }

    List<Vector3> PickBonusCoinPositions(int count)
    {
        var result = new List<Vector3>(count);
        var candidates = new List<BonusCoinCandidate>();
        Vector3 start = currentRoutePath[0];
        Vector3 goal = currentRoutePath[currentRoutePath.Count - 1];

        for (int i = 1; i < currentRoutePath.Count - 1; i++)
        {
            Vector3 pos = currentRoutePath[i];
            candidates.Add(new BonusCoinCandidate
            {
                position = new Vector3(pos.x, pos.y, -1.25f),
                distanceFromShortestRoute = DistanceToSegment(pos, start, goal)
            });
        }

        candidates.Sort((a, b) => b.distanceFromShortestRoute.CompareTo(a.distanceFromShortestRoute));

        for (int i = 0; i < candidates.Count && result.Count < count; i++)
        {
            bool farEnough = true;
            for (int j = 0; j < result.Count; j++)
            {
                if (Vector2.Distance(candidates[i].position, result[j]) < 1.2f)
                {
                    farEnough = false;
                    break;
                }
            }
            if (farEnough) result.Add(candidates[i].position);
        }

        for (int i = 0; i < candidates.Count && result.Count < count; i++)
        {
            if (!result.Contains(candidates[i].position))
                result.Add(candidates[i].position);
        }

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

    void ClearBonusCoins()
    {
        for (int i = activeBonusCoins.Count - 1; i >= 0; i--)
        {
            if (activeBonusCoins[i] != null)
                Destroy(activeBonusCoins[i].gameObject);
        }
        activeBonusCoins.Clear();
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
        SR(go.GetComponent<RectTransform>(), pos, size + new Vector2(3,3));
        var inner = new GameObject("Fill"); inner.transform.SetParent(go.transform, false);
        var fi = inner.AddComponent<Image>(); fi.color = n;
        SR(inner.GetComponent<RectTransform>(), Vector2.zero, size);
        var btn = inner.AddComponent<Button>();
        btn.targetGraphic = fi;
        btn.onClick.AddListener(() => cb?.Invoke());
        var tg = new GameObject("Lbl"); tg.transform.SetParent(inner.transform, false);
        var tx = tg.AddComponent<Text>(); tx.text = label; tx.color = Color.white; tx.fontSize = 20;
        tx.alignment          = TextAnchor.MiddleCenter; tx.fontStyle = FontStyle.Normal;
        tx.alignByGeometry    = true;
        tx.raycastTarget      = false;
        tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        tx.verticalOverflow   = VerticalWrapMode.Overflow;
        tx.font               = UiPixelFont.Get();
        SR(tg.GetComponent<RectTransform>(), Vector2.zero, size);
        lTxt = tx;
        btns.Add(new BtnData { rt=inner.GetComponent<RectTransform>(), fill=fi, n=n, h=h, cb=cb, pauseOnly=false });
        return fi;
    }

    void RegPanelBtn(Transform p, string id, string label, Vector2 pos, Vector2 size, Color n, Color h, System.Action cb)
    {
        var go = new GameObject("PBtn_"+id); go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = new Color(1f,1f,1f,0.10f);
        SR(go.GetComponent<RectTransform>(), pos, size + new Vector2(3,3));
        var inner = new GameObject("Fill"); inner.transform.SetParent(go.transform, false);
        var fi = inner.AddComponent<Image>(); fi.color = n;
        SR(inner.GetComponent<RectTransform>(), Vector2.zero, size);
        var btn = inner.AddComponent<Button>();
        btn.targetGraphic = fi;
        btn.onClick.AddListener(() => cb?.Invoke());
        var tg = new GameObject("Lbl"); tg.transform.SetParent(inner.transform, false);
        var tx = tg.AddComponent<Text>(); tx.text = label; tx.color = Color.white; tx.fontSize = 22;
        tx.alignment          = TextAnchor.MiddleCenter; tx.fontStyle = FontStyle.Normal;
        tx.alignByGeometry    = true;
        tx.raycastTarget      = false;
        tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        tx.verticalOverflow   = VerticalWrapMode.Overflow;
        tx.font               = UiPixelFont.Get();
        SR(tg.GetComponent<RectTransform>(), Vector2.zero, size);
        btns.Add(new BtnData { rt=inner.GetComponent<RectTransform>(), fill=fi, n=n, h=h, cb=cb, pauseOnly=true });
    }

    void RegResultBtn(Transform p, string id, string label, Vector2 pos, Vector2 size, Color n, Color h, System.Action cb)
    {
        var go = new GameObject("RBtn_"+id); go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = new Color(1f,1f,1f,0.12f);
        SR(go.GetComponent<RectTransform>(), pos, size + new Vector2(3,3));
        var inner = new GameObject("Fill"); inner.transform.SetParent(go.transform, false);
        var fi = inner.AddComponent<Image>(); fi.color = n;
        SR(inner.GetComponent<RectTransform>(), Vector2.zero, size);
        var btn = inner.AddComponent<Button>();
        btn.targetGraphic = fi;
        btn.onClick.AddListener(() => cb?.Invoke());
        var tg = new GameObject("Lbl"); tg.transform.SetParent(inner.transform, false);
        var tx = tg.AddComponent<Text>(); tx.text = label; tx.color = Color.white; tx.fontSize = 22;
        tx.alignment          = TextAnchor.MiddleCenter; tx.fontStyle = FontStyle.Normal;
        tx.alignByGeometry    = true;
        tx.raycastTarget      = false;
        tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        tx.verticalOverflow   = VerticalWrapMode.Overflow;
        tx.font               = UiPixelFont.Get();
        SR(tg.GetComponent<RectTransform>(), Vector2.zero, size);
        btns.Add(new BtnData { rt=inner.GetComponent<RectTransform>(), fill=fi, n=n, h=h, cb=cb, pauseOnly=false });
    }

    // ── RectTransform 공통 헬퍼 (앵커·피벗 = 중앙(0.5,0.5) 고정) ────
    static void SR(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
    }

    void CreateImgIn(Transform p, Color c, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Img"); go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = c;
        SR(go.GetComponent<RectTransform>(), pos, size);
    }

    Text CreateTxtIn(Transform p, string s, Color c, Vector2 pos, Vector2 size, int fs)
    {
        var go = new GameObject("Txt"); go.transform.SetParent(p, false);
        var tx = go.AddComponent<Text>(); tx.text = s; tx.color = c; tx.fontSize = fs;
        tx.alignment          = TextAnchor.MiddleCenter;
        tx.fontStyle          = FontStyle.Normal;
        tx.alignByGeometry    = true;
        tx.raycastTarget      = false;
        tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        tx.verticalOverflow   = VerticalWrapMode.Overflow;
        tx.font               = UiPixelFont.Get();
        SR(go.GetComponent<RectTransform>(), pos, size);
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
