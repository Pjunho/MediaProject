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
    private bool          gemPanelOpen  = false;

    // 탭 상태
    private GameObject gemTabGemsContent;
    private GameObject gemTabItemsContent;
    private Image      gemTabGemsFill;
    private Image      gemTabItemsFill;
    private int        gemTabIndex       = 0;   // 0=보석, 1=아이템
    private int        gemTabGemsBtnIdx  = -1;  // btns 리스트 인덱스
    private int        gemTabItemsBtnIdx = -1;
    private int        pressedButtonIndex = -1;
    private bool       pressedWorldDeploy;
    private bool       pressedGemPanelOutside;

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
    private List<AllyType> currentSkillOrder = new List<AllyType>();
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
    static readonly Color COL_GEM      = new Color(0.18f,0.14f,0.32f);  // 가방 버튼 기본
    static readonly Color COL_GEM_H   = new Color(0.30f,0.22f,0.50f);  // 가방 버튼 호버/열림
    static readonly Color COL_TAB_ACT  = new Color(0.28f,0.20f,0.48f); // 활성 탭
    static readonly Color COL_TAB_IDLE = new Color(0.10f,0.07f,0.18f); // 비활성 탭

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
        SkillSystem.ResetForStage();
        UpgradeSystem.ResetForStage();
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
    public bool ConfirmRouteAndStartWave(System.Collections.Generic.List<Vector3> worldPath,
                                         System.Collections.Generic.List<AllyType> order)
    {
        if (worldPath == null || worldPath.Count < 2)
        {
            ShowToast("경로를 먼저 완성해야 합니다!", new Color(1f, 0.65f, 0.25f));
            Debug.LogWarning("[GameManager] 시작 실패: 경로가 부족합니다.");
            return false;
        }
        if (waveInProgress)
        {
            ShowToast("이미 웨이브가 진행 중입니다!", new Color(1f, 0.65f, 0.25f));
            Debug.LogWarning("[GameManager] 시작 실패: 이미 웨이브가 진행 중입니다.");
            return false;
        }

        int stageIdx = StageManager.Instance != null ? StageManager.Instance.currentStageIndex : 1;
        order = StageManager.NormalizeSelectedAllies(order, stageIdx);
        currentSkillOrder = new List<AllyType>(order);

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
            StageManager.Instance?.SetCurrentWaveNumber(1);
            UpdateCoinHUD();
            // ※ RespawnForCurrentWave()는 여기서 호출하지 않는다.
            //   적은 씬 로드 직후 EnemyAutoSpawner.InitializeAfterMapReady()에서 이미
            //   배치되어 있으므로, 여기서 다시 호출하면 위치가 재랜덤화되어
            //   "준비 단계와 실제 이동 단계의 적 위치가 달라지는" 버그가 발생한다.
            Debug.Log($"[GameManager] 게임 시작! (총 {currentWaves.Length}웨이브)");
        }

        StartCurrentWave();
        return true;
    }

    void Update()
    {
        if (resultShown) return;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (TryHandleSkillHotkey(kb))
            return;

        if (mouse != null && SkillSystem.HandleTargetingInput(mouse.position.ReadValue(), mouse))
            return;

        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            TogglePause();

        if (mouse == null) return;
        Vector2 mp = mouse.position.ReadValue();
        bool pointerOnGemPanel = IsPointerOnOpenGemPanel(mp);
        bool gemPanelWasOpen = gemPanelOpen;

        bool anyBtnClicked = false;
        int hoveredButtonIndex = -1;
        for (int i = 0; i < btns.Count; i++)
        {
            var b = btns[i];
            if (b.rt == null || !b.rt.gameObject.activeInHierarchy) continue;
            bool gemUiButton = IsGemUiButton(b.rt);
            if (gemPanelWasOpen && !gemUiButton)
            {
                if (b.fill != null) b.fill.color = b.n;
                continue;
            }

            bool active = b.pauseOnly ? isPaused : true;
            if (!active) continue;
            bool over = RectTransformUtility.RectangleContainsScreenPoint(b.rt, mp, null);
            if (b.fill != null) b.fill.color = over ? b.h : b.n;
            if (over)
                hoveredButtonIndex = i;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressedButtonIndex = hoveredButtonIndex;
            anyBtnClicked = pressedButtonIndex >= 0;
            pressedGemPanelOutside = gemPanelOpen && !anyBtnClicked && !pointerOnGemPanel;
            pressedWorldDeploy = !anyBtnClicked && !gemPanelWasOpen && !pointerOnGemPanel;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (pressedButtonIndex >= 0 && pressedButtonIndex == hoveredButtonIndex && pressedButtonIndex < btns.Count)
            {
                var b = btns[pressedButtonIndex];
                if (b.rt != null && b.rt.gameObject.activeInHierarchy)
                    b.cb?.Invoke();
                anyBtnClicked = true;
            }
            else if (pressedGemPanelOutside && gemPanelOpen && !pointerOnGemPanel)
            {
                CloseGemPanel();
            }

            pressedButtonIndex = -1;
            pressedGemPanelOutside = false;
        }

        // Space 또는 UI 외 클릭으로 대기 중인 아군 1명씩 출전
        if (waveInProgress && !alliesFullyDeployed && !isPaused)
        {
            bool spaceDeploy = kb != null && kb.spaceKey.wasPressedThisFrame;
            bool clickDeploy = !anyBtnClicked && pressedWorldDeploy && !pointerOnGemPanel && mouse.leftButton.wasReleasedThisFrame;
            if (spaceDeploy || clickDeploy)
                TriggerDeployNext();
        }

        if (mouse.leftButton.wasReleasedThisFrame)
            pressedWorldDeploy = false;
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

    bool TryHandleSkillHotkey(Keyboard keyboard)
    {
        if (keyboard == null || isPaused || resultShown) return false;

        // ── 조준 모드 중: 궁수 슬롯과 같은 번호 키를 다시 누르면 취소 ──
        if (SkillSystem.IsTargeting)
        {
            for (int i = 0; i < 6; i++)
            {
                if (!WasNumberPressed(keyboard, i + 1)) continue;
                // 해당 슬롯이 궁수이면 취소, 아니면 그냥 소비
                if (i < currentSkillOrder.Count && currentSkillOrder[i] == AllyType.Archer)
                {
                    SkillSystem.CancelTargeting();
                    ShowToast("마비 화살 조준 취소", new Color(0.8f, 0.8f, 0.8f));
                }
                return true; // 조준 중엔 다른 숫자 키도 일반 스킬 발동 차단
            }
            return false;
        }

        // ── 일반 스킬 발동 ────────────────────────────────────────────────
        for (int i = 0; i < 6; i++)
        {
            if (!WasNumberPressed(keyboard, i + 1)) continue;
            if (!gameStarted || currentSkillOrder == null || currentSkillOrder.Count == 0)
            {
                ShowToast("전투 시작 후 사용할 수 있습니다!", new Color(1f, 0.65f, 0.25f));
                return true;
            }
            if (i >= currentSkillOrder.Count)
            {
                ShowToast("해당 순서의 아군이 없습니다!", new Color(1f, 0.65f, 0.25f));
                return true;
            }

            SkillSystem.ActivateSkill(currentSkillOrder[i]);
            return true;
        }

        return false;
    }

    bool WasNumberPressed(Keyboard keyboard, int number)
    {
        return number switch
        {
            1 => keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame,
            2 => keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame,
            3 => keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame,
            4 => keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame,
            5 => keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame,
            6 => keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame,
            _ => false
        };
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

        // 웨이브 정보 (상단 좌측) — 스테이지 총 웨이브 수를 미리 표시
        {
            int si = StageManager.Instance != null ? StageManager.Instance.currentStageIndex : 1;
            int totalWaves = StageManager.GetWaves(si).Length;
            waveInfoTxt = BuildTopLabel(cgo.transform, "wave", $"웨이브 1 / {totalWaves}",
                new Vector2(-400, 320), new Vector2(200, 40));
        }

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
        // ── 오른쪽 하단 가방 아이콘 ─────────────────────────────────────
        var btnSize = new Vector2(72f, 72f);
        var btnPos  = new Vector2(458f, -322f); // 투명 여백을 포함한 원본 아이콘의 실제 그림 아래선을 RouteDrawer 하단 안내 박스에 맞춤

        var iconGo = new GameObject("GemBagIcon");
        iconGo.transform.SetParent(parent, false);
        var iconImg = iconGo.AddComponent<Image>();
        gemBagBtnFill = iconImg;
        gemBagBtnRt   = iconGo.GetComponent<RectTransform>();

        var bagSprite = LoadIconSprite("bag_icon");
        if (bagSprite != null)
        {
            iconImg.sprite         = bagSprite;
            iconImg.color          = Color.white;
            iconImg.preserveAspect = true;
        }
        else
        {
            // 스프라이트 없을 때 텍스트 폴백
            iconImg.color = new Color(0f, 0f, 0f, 0f);
            var fb = new GameObject("FbLbl"); fb.transform.SetParent(iconGo.transform, false);
            var fbTx = fb.AddComponent<Text>();
            fbTx.text = "◆"; fbTx.color = COL_GOLD; fbTx.fontSize = 38;
            fbTx.alignment = TextAnchor.MiddleCenter; fbTx.alignByGeometry = true;
            fbTx.raycastTarget = false;
            fbTx.horizontalOverflow = HorizontalWrapMode.Overflow;
            fbTx.verticalOverflow   = VerticalWrapMode.Overflow;
            fbTx.font = UiPixelFont.Get();
            SR(fb.GetComponent<RectTransform>(), Vector2.zero, btnSize);
        }
        SR(gemBagBtnRt, btnPos, btnSize);

        // btns 에 등록 (클릭 = 팝업 토글, 호버 = 아이콘 밝기만 살짝 강조)
        btns.Add(new BtnData
        {
            rt       = gemBagBtnRt,
            fill     = gemBagBtnFill,
            n        = Color.white,
            h        = new Color(1f, 0.95f, 0.72f, 1f),
            cb       = ToggleGemPanel,
            pauseOnly= false
        });

        // ── 가방 팝업 패널 (버튼 위에 열림) ─────────────────────────────
        BuildGemPanel(parent, btnPos, btnSize.y);
    }

    void BuildGemPanel(Transform parent, Vector2 btnPos, float btnH)
    {
        var defs = GemInventory.GetDefinitions();

        const float TAB_H  = 38f;   // 탭 행 높이
        const float ROW_H  = 68f;   // 보석 행 높이
        const float PAD_V  = 12f;   // 세로 패딩

        float contentH = Mathf.Max(3, defs.Length) * ROW_H + PAD_V * 2f;
        float panelH   = TAB_H + 2f + contentH;   // 탭 + 구분선 + 콘텐츠
        float panelW   = 254f;

        // 버튼 바로 위에 정렬
        float panelY   = btnPos.y + btnH / 2f + panelH / 2f + 6f;
        var   panelPos = new Vector2(btnPos.x, panelY);

        gemPanelGo = new GameObject("GemPanel");
        gemPanelGo.transform.SetParent(parent, false);
        gemPanelGo.AddComponent<Image>().color = COL_PANEL;
        SR(gemPanelGo.GetComponent<RectTransform>(), panelPos, new Vector2(panelW, panelH));

        // 외곽 테두리
        var border = new GameObject("Border"); border.transform.SetParent(gemPanelGo.transform, false);
        border.AddComponent<Image>().color = new Color(0.60f, 0.48f, 0.90f, 0.30f);
        SR(border.GetComponent<RectTransform>(), Vector2.zero, new Vector2(panelW + 2f, panelH + 2f));
        border.transform.SetAsFirstSibling();

        float topEdge = panelH / 2f;
        float halfW   = panelW / 2f;

        // ── 탭 버튼 행 ────────────────────────────────────────────────────
        float tabRowY = topEdge - TAB_H / 2f;

        // 탭 배경 (어두운 바)
        var tabBg = new GameObject("TabBg"); tabBg.transform.SetParent(gemPanelGo.transform, false);
        tabBg.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.09f, 1f);
        SR(tabBg.GetComponent<RectTransform>(), new Vector2(0f, tabRowY), new Vector2(panelW, TAB_H));

        // ── 탭 0: "보석" ─────────────────────────────────────────────────
        var t0Shell = new GameObject("TabGems"); t0Shell.transform.SetParent(gemPanelGo.transform, false);
        t0Shell.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        SR(t0Shell.GetComponent<RectTransform>(), new Vector2(-halfW / 2f, tabRowY), new Vector2(halfW, TAB_H));

        var t0Fill = new GameObject("Fill"); t0Fill.transform.SetParent(t0Shell.transform, false);
        gemTabGemsFill = t0Fill.AddComponent<Image>();
        gemTabGemsFill.color = COL_TAB_ACT; // 기본 선택
        SR(t0Fill.GetComponent<RectTransform>(), Vector2.zero, new Vector2(halfW, TAB_H));

        var t0TxtGo = new GameObject("Lbl"); t0TxtGo.transform.SetParent(t0Fill.transform, false);
        var t0Tx = t0TxtGo.AddComponent<Text>();
        t0Tx.text = "◆ 보석"; t0Tx.color = COL_GOLD; t0Tx.fontSize = 15;
        t0Tx.alignment = TextAnchor.MiddleCenter; t0Tx.alignByGeometry = true;
        t0Tx.raycastTarget = false;
        t0Tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        t0Tx.verticalOverflow   = VerticalWrapMode.Overflow;
        t0Tx.font = UiPixelFont.Get();
        SR(t0TxtGo.GetComponent<RectTransform>(), Vector2.zero, new Vector2(halfW - 4f, TAB_H));

        btns.Add(new BtnData
        {
            rt       = t0Fill.GetComponent<RectTransform>(),
            fill     = gemTabGemsFill,
            n        = COL_TAB_ACT,
            h        = new Color(0.38f, 0.28f, 0.62f),
            cb       = () => SwitchGemTab(0),
            pauseOnly= false
        });
        gemTabGemsBtnIdx = btns.Count - 1;

        // 탭 사이 세로 구분선
        CreateImgIn(gemPanelGo.transform, new Color(0.60f, 0.48f, 0.90f, 0.25f),
            new Vector2(0f, tabRowY), new Vector2(1f, TAB_H - 8f));

        // ── 탭 1: "아이템" ────────────────────────────────────────────────
        var t1Shell = new GameObject("TabItems"); t1Shell.transform.SetParent(gemPanelGo.transform, false);
        t1Shell.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        SR(t1Shell.GetComponent<RectTransform>(), new Vector2(halfW / 2f, tabRowY), new Vector2(halfW, TAB_H));

        var t1Fill = new GameObject("Fill"); t1Fill.transform.SetParent(t1Shell.transform, false);
        gemTabItemsFill = t1Fill.AddComponent<Image>();
        gemTabItemsFill.color = COL_TAB_IDLE;
        SR(t1Fill.GetComponent<RectTransform>(), Vector2.zero, new Vector2(halfW, TAB_H));

        var t1TxtGo = new GameObject("Lbl"); t1TxtGo.transform.SetParent(t1Fill.transform, false);
        var t1Tx = t1TxtGo.AddComponent<Text>();
        t1Tx.text = "☆ 아이템"; t1Tx.color = new Color(0.70f, 0.70f, 0.82f); t1Tx.fontSize = 15;
        t1Tx.alignment = TextAnchor.MiddleCenter; t1Tx.alignByGeometry = true;
        t1Tx.raycastTarget = false;
        t1Tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        t1Tx.verticalOverflow   = VerticalWrapMode.Overflow;
        t1Tx.font = UiPixelFont.Get();
        SR(t1TxtGo.GetComponent<RectTransform>(), Vector2.zero, new Vector2(halfW - 4f, TAB_H));

        btns.Add(new BtnData
        {
            rt       = t1Fill.GetComponent<RectTransform>(),
            fill     = gemTabItemsFill,
            n        = COL_TAB_IDLE,
            h        = new Color(0.22f, 0.18f, 0.36f),
            cb       = () => SwitchGemTab(1),
            pauseOnly= false
        });
        gemTabItemsBtnIdx = btns.Count - 1;

        // 탭 아래 구분선
        CreateImgIn(gemPanelGo.transform, new Color(0.60f, 0.48f, 0.90f, 0.40f),
            new Vector2(0f, topEdge - TAB_H - 1f), new Vector2(panelW, 2f));

        // ── 콘텐츠 영역 공통 위치 계산 ────────────────────────────────────
        float contentTopEdge = topEdge - TAB_H - 2f;
        float contentCenterY = contentTopEdge - contentH / 2f;

        // ── 보석 탭 콘텐츠 ────────────────────────────────────────────────
        gemTabGemsContent = new GameObject("GemsContent", typeof(RectTransform));
        gemTabGemsContent.transform.SetParent(gemPanelGo.transform, false);
        SR(gemTabGemsContent.GetComponent<RectTransform>(),
            new Vector2(0f, contentCenterY),
            new Vector2(panelW, contentH));

        // ── 아이템 탭 콘텐츠 ──────────────────────────────────────────────
        gemTabItemsContent = new GameObject("ItemsContent", typeof(RectTransform));
        gemTabItemsContent.transform.SetParent(gemPanelGo.transform, false);
        SR(gemTabItemsContent.GetComponent<RectTransform>(),
            new Vector2(0f, contentCenterY),
            new Vector2(panelW, contentH));

        RefreshGemBagContents();
        gemTabItemsContent.SetActive(false); // 기본: 보석 탭 표시
        gemPanelGo.SetActive(false);
    }

    /// <summary>가방 팝업 탭 전환 (0=보석, 1=아이템)</summary>
    void SwitchGemTab(int tabIndex)
    {
        gemTabIndex = tabIndex;

        if (gemTabGemsContent  != null) gemTabGemsContent.SetActive(tabIndex == 0);
        if (gemTabItemsContent != null) gemTabItemsContent.SetActive(tabIndex == 1);

        // btns 리스트의 n 색상을 업데이트해 hover-off 시에도 올바른 색이 유지되도록
        if (gemTabGemsBtnIdx >= 0 && gemTabGemsBtnIdx < btns.Count)
        {
            var b = btns[gemTabGemsBtnIdx];
            b.n = (tabIndex == 0) ? COL_TAB_ACT : COL_TAB_IDLE;
            if (gemTabGemsFill != null) gemTabGemsFill.color = b.n;
            btns[gemTabGemsBtnIdx] = b;
        }
        if (gemTabItemsBtnIdx >= 0 && gemTabItemsBtnIdx < btns.Count)
        {
            var b = btns[gemTabItemsBtnIdx];
            b.n = (tabIndex == 1) ? COL_TAB_ACT : COL_TAB_IDLE;
            if (gemTabItemsFill != null) gemTabItemsFill.color = b.n;
            btns[gemTabItemsBtnIdx] = b;
        }
    }

    void ToggleGemPanel()
    {
        if (gemPanelGo == null) return;
        gemPanelOpen = !gemPanelOpen;
        gemPanelGo.SetActive(gemPanelOpen);
        if (gemPanelOpen)
        {
            RefreshGemBagContents();
            SwitchGemTab(0);
        }
        // 버튼 색상: 열려 있으면 밝게 유지
        if (gemBagBtnFill != null)
            gemBagBtnFill.color = gemPanelOpen ? new Color(1f, 0.95f, 0.72f, 1f) : Color.white;
    }

    void CloseGemPanel()
    {
        if (!gemPanelOpen) return;
        gemPanelOpen = false;
        if (gemPanelGo      != null) gemPanelGo.SetActive(false);
        if (gemBagBtnFill   != null) gemBagBtnFill.color = Color.white;
    }

    bool IsPointerOnOpenGemPanel(Vector2 screenPoint)
    {
        if (!gemPanelOpen || gemPanelGo == null || !gemPanelGo.activeSelf)
            return false;

        var rt = gemPanelGo.GetComponent<RectTransform>();
        return rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, null);
    }

    bool IsGemUiButton(RectTransform rt)
    {
        if (rt == null)
            return false;

        if (rt == gemBagBtnRt)
            return true;

        return gemPanelGo != null && rt.transform.IsChildOf(gemPanelGo.transform);
    }

    void RefreshGemBagContents()
    {
        if (gemTabGemsContent == null || gemTabItemsContent == null)
            return;

        ClearChildren(gemTabGemsContent.transform);
        ClearChildren(gemTabItemsContent.transform);

        var gemsRt  = gemTabGemsContent.GetComponent<RectTransform>();
        var itemsRt = gemTabItemsContent.GetComponent<RectTransform>();
        float panelW   = gemsRt.rect.width;
        float contentH = gemsRt.rect.height;
        const float ROW_H = 68f;
        const float PAD_V = 12f;

        var activeGems = new List<GemInventory.GemDefinition>();
        foreach (var gem in GemInventory.GetDefinitions())
        {
            if (GemInventory.IsUnlocked(gem.stageIndex) && GemInventory.IsActive(gem.stageIndex))
                activeGems.Add(gem);
        }

        if (activeGems.Count == 0)
        {
            CreateTxtIn(gemTabGemsContent.transform, "활성화된 보석 없음",
                new Color(0.58f, 0.58f, 0.68f),
                Vector2.zero, new Vector2(panelW - 28f, 44f), 16);
        }
        else
        {
            float rowStartY = contentH / 2f - PAD_V - ROW_H / 2f;
            for (int i = 0; i < activeGems.Count; i++)
            {
                var gem = activeGems[i];
                float cy = rowStartY - i * ROW_H;

                CreateImgIn(gemTabGemsContent.transform,
                    (i % 2 == 0) ? new Color(1f, 1f, 1f, 0.035f) : new Color(0f, 0f, 0f, 0.12f),
                    new Vector2(0f, cy), new Vector2(panelW - 16f, ROW_H - 8f));

                var iconShell = CreateImgIn(gemTabGemsContent.transform, new Color(0f, 0f, 0f, 0.28f),
                    new Vector2(-95f, cy + 2f), new Vector2(48f, 48f));
                var iconGo = new GameObject("GemIcon");
                iconGo.transform.SetParent(iconShell.transform, false);
                var icon = iconGo.AddComponent<Image>();
                icon.sprite = LoadIconSprite(GetGemIconResourceName(gem.stageIndex));
                icon.color = icon.sprite != null ? Color.white : gem.color;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                SR(iconGo.GetComponent<RectTransform>(), Vector2.zero, new Vector2(42f, 42f));

                var name = CreateTxtIn(gemTabGemsContent.transform, gem.gemName, gem.color,
                    new Vector2(25f, cy + 15f), new Vector2(panelW - 86f, 22f), 15);
                name.alignment = TextAnchor.MiddleLeft;
                var effect = CreateTxtIn(gemTabGemsContent.transform, gem.effectSummary,
                    new Color(0.80f, 0.88f, 1f),
                    new Vector2(25f, cy - 12f), new Vector2(panelW - 86f, 28f), 12);
                effect.alignment = TextAnchor.MiddleLeft;
                effect.horizontalOverflow = HorizontalWrapMode.Wrap;

                if (i < activeGems.Count - 1)
                    CreateImgIn(gemTabGemsContent.transform, new Color(0.60f, 0.48f, 0.90f, 0.18f),
                        new Vector2(0f, cy - ROW_H / 2f + 2f), new Vector2(panelW - 20f, 1f));
            }
        }

        CreateTxtIn(gemTabItemsContent.transform, "아이템 없음",
            new Color(0.58f, 0.58f, 0.68f),
            Vector2.zero, new Vector2(itemsRt.rect.width - 28f, 44f), 16);
    }

    void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    Sprite LoadIconSprite(string iconName)
    {
        var sprite = Resources.Load<Sprite>($"Icon/{iconName}");
        if (sprite != null)
            return sprite;

        var sprites = Resources.LoadAll<Sprite>($"Icon/{iconName}");
        return sprites != null && sprites.Length > 0 ? sprites[0] : null;
    }

    string GetGemIconResourceName(int stageIndex) => stageIndex switch
    {
        2 => "dark_crystal",
        3 => "volcano_crystal",
        _ => "green_crystal"
    };

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
        if (waveInfoTxt != null)
        {
            int si         = StageManager.Instance != null ? StageManager.Instance.currentStageIndex : 1;
            int totalWaves = currentWaves?.Length ?? StageManager.GetWaves(si).Length;
            int displayNum = currentWaveIndex >= 0 ? currentWaveIndex + 1 : 1;
            waveInfoTxt.text = $"웨이브 {displayNum} / {totalWaves}";
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

    GameObject CreateImgIn(Transform p, Color c, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Img"); go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = c;
        SR(go.GetComponent<RectTransform>(), pos, size);
        return go;
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
