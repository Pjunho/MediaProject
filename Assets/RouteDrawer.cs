using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 경로 그리기 단계 관리
/// - START/GOAL 마커 표시
/// - 흙길 하이라이트
/// - 드래그로 흙 경로를 따라 선 그리기
/// - 경로 위 아무 지점 클릭 → 그 지점부터 재수정
/// - Ctrl+Z → 드래그 세션 단위 되돌리기
/// - GOAL 도달 시 Start 버튼 활성화
/// - Start 버튼 클릭 → 게임 시작
/// </summary>
public class RouteDrawer : MonoBehaviour
{
    public static void CreateForWavePlanning()
    {
        if (FindFirstObjectByType<RouteDrawer>() != null) return;

        var go = new GameObject("RouteDrawer");
        go.AddComponent<RouteDrawer>();
    }

    // ── 참조 ───────────────────────────────────────────────────────
    private Map              map;
    private AllyPlacer       allyPlacer;
    private EnemyAutoSpawner enemySpawner;

    // ── 경로 상태 ──────────────────────────────────────────────────
    private List<Vector2Int>       drawnPath        = new List<Vector2Int>();
    private Vector2Int             startTile;
    private Vector2Int             goalTile;
    private bool                   isDragging       = false;
    private bool                   pathComplete     = false;
    private bool                   gameStarted      = false;
    private int                    dragSessionStart  = 0;
    private List<Vector2Int>       dragSnapshot      = null; // 드래그 시작 시점 스냅샷
    private List<List<Vector2Int>> undoHistory          = new List<List<Vector2Int>>(); // Ctrl+Z용 스냅샷 스택
    private bool                   keyboardSessionActive = false; // 키보드 세션 진행 중 여부

    // ── 키보드 반복 입력 ───────────────────────────────────────────
    private Vector2Int heldDir       = Vector2Int.zero;
    private float      keyHoldTime   = 0f;
    private float      keyRepeatTimer = 0f;
    const float KEY_INITIAL_DELAY = 0.35f; // 첫 반복까지 대기 시간
    const float KEY_MAX_INTERVAL  = 0.13f; // 처음 반복 간격
    const float KEY_MIN_INTERVAL  = 0.04f; // 최고 속도 반복 간격
    const float KEY_ACCEL_TIME    = 1.8f;  // 최고 속도까지 걸리는 시간

    // ── 비주얼 ────────────────────────────────────────────────────
    private LineRenderer      pathLine;
    private GameObject        pulseIndicator;
    private GameObject        startMarkerGo;
    private GameObject        goalMarkerGo;
    private GameObject        dirtHighlightParent;
    private List<GameObject>  arrowPool   = new List<GameObject>(); // 방향 화살표 풀
    private Sprite            arrowSprite;

    // ── HUD ───────────────────────────────────────────────────────
    private Canvas         hudCanvas;
    private Image          startBtnFill;
    private Text           instrTxt;
    private AllyOrderPanel allyOrderPanel;

    // ── 색상 ───────────────────────────────────────────────────────
    static readonly Color COL_LINE     = new Color(1.0f, 0.85f, 0.20f, 0.90f);
    static readonly Color COL_DONE     = new Color(0.2f, 1.00f, 0.30f, 0.95f);
    static readonly Color COL_BTN_OFF  = new Color(0.22f, 0.22f, 0.28f, 0.85f);
    static readonly Color COL_BTN_ON   = new Color(0.12f, 0.65f, 0.22f, 1.00f);
    static readonly Color COL_BTN_HOV  = new Color(0.18f, 0.88f, 0.32f, 1.00f);
    static readonly Color COL_HIGHLIGHT = new Color(1.0f, 0.85f, 0.20f, 0.18f);
    private bool hasInitialized = false;

    // ── 라이프사이클 ───────────────────────────────────────────────
    void Awake()
    {
        TryInit();
    }

    void Start()
    {
        TryInit();
    }

    void TryInit()
    {
        if (hasInitialized) return;

        map          = FindFirstObjectByType<Map>();
        allyPlacer   = FindFirstObjectByType<AllyPlacer>();
        enemySpawner = FindFirstObjectByType<EnemyAutoSpawner>();

        if (map == null) return;
        if (!map.IsGenerated)
            map.GenerateMap();

        if (allyPlacer != null) allyPlacer.enabled = false;

        if (!map.IsGenerated)
            return;

        hasInitialized = true;

        startTile = map.pathWaypoints[0];
        goalTile  = map.pathWaypoints[map.pathWaypoints.Length - 1];

        drawnPath.Add(startTile);
        dragSessionStart = drawnPath.Count;
        dragSnapshot     = new List<Vector2Int>(drawnPath);

        SetupDirtHighlights();
        SetupPathLine();
        SetupMarkers();
        SetupHUD();
        UpdatePathLine();

        StartCoroutine(RespawnEnemiesAfterUiReady());
    }

    IEnumerator RespawnEnemiesAfterUiReady()
    {
        yield return null;
        if (enemySpawner == null) enemySpawner = FindFirstObjectByType<EnemyAutoSpawner>();
        enemySpawner?.RespawnForCurrentWave();
    }

    void Update()
    {
        if (!hasInitialized)
        {
            TryInit();
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 mp = mouse.position.ReadValue();

        // ── Start 버튼은 게임 시작 전 언제든지 클릭 가능 (일시정지 중에도) ──
        if (!gameStarted && pathComplete && startBtnFill != null)
        {
            var ir   = startBtnFill.GetComponent<RectTransform>();
            bool over = RectTransformUtility.RectangleContainsScreenPoint(ir, mp, null);
            if (!IsInPulseCoroutine)
                startBtnFill.color = over ? COL_BTN_HOV : COL_BTN_ON;
            if (over && mouse.leftButton.wasPressedThisFrame)
            {
                OnStartGame();
                return;
            }
        }

        if (GameManager.Instance != null && GameManager.Instance.ShouldBlockGameplayInput())
            return;

        // ── 경로 그리기 (게임 시작 전까지 언제든지 수정 가능) ──────────
        // 아군 순서 패널 위에서는 경로 그리기 무시
        bool mouseOnAllyPanel = allyOrderPanel != null && allyOrderPanel.IsMouseOverPanel(mp);
        bool mouseOnEnemy = EnemyInspector.Instance != null && EnemyInspector.Instance.IsMouseOverEnemy(mp);

        if (!gameStarted && !mouseOnAllyPanel && !mouseOnEnemy)
        {
            Vector3 mw = Camera.main.ScreenToWorldPoint(new Vector3(mp.x, mp.y, 10f));

            if (mouse.leftButton.wasPressedThisFrame)
            {
                // 마우스 드래그 시작 전 키보드 세션 commit
                if (keyboardSessionActive)
                {
                    if (drawnPath.Count != dragSessionStart)
                        undoHistory.Add(dragSnapshot);
                    keyboardSessionActive = false;
                }

                // 경로 위 가장 가까운 타일 찾기 (끝점뿐 아니라 어디든 클릭 가능)
                int clickedIdx = -1;
                float minDist  = map.tileSize * 1.5f;
                for (int i = 0; i < drawnPath.Count; i++)
                {
                    float d = Vector2.Distance(mw, map.GetWorldPosition(drawnPath[i].x, drawnPath[i].y));
                    if (d < minDist) { minDist = d; clickedIdx = i; }
                }

                if (clickedIdx >= 0)
                {
                    // 중간 지점 클릭 → 해당 지점 이후 경로 제거 후 드래그 시작
                    if (clickedIdx < drawnPath.Count - 1)
                    {
                        // 제거 전 스냅샷을 undo에 저장
                        undoHistory.Add(new List<Vector2Int>(drawnPath));

                        drawnPath.RemoveRange(clickedIdx + 1, drawnPath.Count - clickedIdx - 1);

                        bool wasComplete2 = pathComplete;
                        bool nowComplete2 = drawnPath[drawnPath.Count - 1] == goalTile;
                        if (wasComplete2 && !nowComplete2) OnPathIncomplete();

                        UpdatePathLine();
                        UpdatePulse();
                    }

                    isDragging       = true;
                    dragSessionStart = drawnPath.Count;
                    dragSnapshot     = new List<Vector2Int>(drawnPath);
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame && isDragging)
            {
                isDragging = false;
                // 드래그로 경로가 실제로 바뀌었을 때만 undo에 기록
                if (drawnPath.Count != dragSessionStart)
                    undoHistory.Add(dragSnapshot);
            }

            if (isDragging)
            {
                bool wasComplete = pathComplete;
                ExtendPath(mw);
                UpdatePathLine();
                UpdatePulse();

                bool isNowComplete = drawnPath.Count > 0 && drawnPath[drawnPath.Count - 1] == goalTile;
                if (isNowComplete && !wasComplete)   OnPathComplete();
                else if (!isNowComplete && wasComplete) OnPathIncomplete();
            }
        }

        // ── 키보드 방향키 / WASD 경로 그리기 (꾹 누를수록 가속) ────────
        if (!gameStarted && !isDragging)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                // 현재 눌린 방향 감지
                Vector2Int dir = Vector2Int.zero;
                if      (kb.upArrowKey.isPressed    || kb.wKey.isPressed) dir = Vector2Int.up;
                else if (kb.downArrowKey.isPressed  || kb.sKey.isPressed) dir = Vector2Int.down;
                else if (kb.leftArrowKey.isPressed  || kb.aKey.isPressed) dir = Vector2Int.left;
                else if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) dir = Vector2Int.right;

                // 새로 누른 키 감지 (방향 전환 포함)
                bool justPressed = dir != Vector2Int.zero && (
                    kb.upArrowKey.wasPressedThisFrame    || kb.wKey.wasPressedThisFrame    ||
                    kb.downArrowKey.wasPressedThisFrame  || kb.sKey.wasPressedThisFrame    ||
                    kb.leftArrowKey.wasPressedThisFrame  || kb.aKey.wasPressedThisFrame    ||
                    kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame);

                bool shouldMove = false;

                if (dir == Vector2Int.zero)
                {
                    // 아무 키도 안 눌림 → 타이머 리셋
                    heldDir       = Vector2Int.zero;
                    keyHoldTime   = 0f;
                    keyRepeatTimer = 0f;
                }
                else if (justPressed || dir != heldDir)
                {
                    // 새 방향키 입력 → 즉시 한 칸, 반복 타이머 초기화
                    heldDir        = dir;
                    keyHoldTime    = 0f;
                    keyRepeatTimer = KEY_INITIAL_DELAY;
                    shouldMove     = true;
                }
                else
                {
                    // 꾹 누르는 중 → 시간 누적, 가속
                    keyHoldTime    += Time.unscaledDeltaTime;
                    keyRepeatTimer -= Time.unscaledDeltaTime;
                    if (keyRepeatTimer <= 0f)
                    {
                        float t = Mathf.Clamp01(keyHoldTime / KEY_ACCEL_TIME);
                        keyRepeatTimer = Mathf.Lerp(KEY_MAX_INTERVAL, KEY_MIN_INTERVAL, t);
                        shouldMove = true;
                    }
                }

                if (shouldMove && drawnPath.Count > 0)
                {
                    // 첫 키 입력 시 세션 시작 스냅샷 저장
                    if (!keyboardSessionActive)
                    {
                        dragSnapshot          = new List<Vector2Int>(drawnPath);
                        keyboardSessionActive = true;
                    }

                    Vector2Int next = drawnPath[drawnPath.Count - 1] + dir;
                    next.x = Mathf.Clamp(next.x, 0, map.mapWidth  - 1);
                    next.y = Mathf.Clamp(next.y, 0, map.mapHeight - 1);

                    bool wasComplete = pathComplete;
                    ExtendPathByTile(next);
                    UpdatePathLine();
                    UpdatePulse();

                    bool isNowComplete = drawnPath.Count > 0 && drawnPath[drawnPath.Count - 1] == goalTile;
                    if (isNowComplete && !wasComplete)    OnPathComplete();
                    else if (!isNowComplete && wasComplete) OnPathIncomplete();
                }

                // 스페이스바 → 경로 완성 시 게임 시작, 미완성 시 commit
                if (kb.spaceKey.wasPressedThisFrame && drawnPath.Count > 0)
                {
                    if (pathComplete)
                    {
                        // 경로가 완성된 상태 → 게임 시작 (Start 버튼과 동일)
                        OnStartGame();
                        return;
                    }

                    // 경로 미완성 → 기존 commit 동작 (이전 길 교차 허용 세션 시작)
                    if (keyboardSessionActive && drawnPath.Count != dragSessionStart)
                        undoHistory.Add(dragSnapshot);

                    keyboardSessionActive = false;
                    heldDir               = Vector2Int.zero;
                    keyHoldTime           = 0f;
                    keyRepeatTimer        = 0f;
                    dragSessionStart      = drawnPath.Count;
                    dragSnapshot          = new List<Vector2Int>(drawnPath);
                    Debug.Log($"[RouteDrawer] [Space] commit: 경로 {drawnPath.Count}타일");
                }
            }
        }

        // ── Ctrl+Z: 마지막 세션 되돌리기 ─────────────────────────────
        if (!gameStarted && !isDragging && !keyboardSessionActive)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.ctrlKey.isPressed && kb.zKey.wasPressedThisFrame
                && undoHistory.Count > 0)
            {
                var snapshot = undoHistory[undoHistory.Count - 1];
                undoHistory.RemoveAt(undoHistory.Count - 1);

                bool wasComplete = pathComplete;
                drawnPath        = new List<Vector2Int>(snapshot);
                dragSessionStart = drawnPath.Count;

                UpdatePathLine();
                UpdatePulse();

                bool isNowComplete = drawnPath.Count > 0 && drawnPath[drawnPath.Count - 1] == goalTile;
                if (wasComplete && !isNowComplete)    OnPathIncomplete();
                else if (!wasComplete && isNowComplete) OnPathComplete();

                Debug.Log($"[RouteDrawer] ↩ Ctrl+Z: 경로 {drawnPath.Count}타일로 복원");
            }
        }

    }

    bool IsInPulseCoroutine = false;

    // ── 경로 확장 ──────────────────────────────────────────────────
    void ExtendPath(Vector3 worldPos) => ExtendPathByTile(WorldToTile(worldPos));

    void ExtendPathByTile(Vector2Int nearest)
    {
        if (drawnPath.Count == 0) return;
        if (nearest == drawnPath[drawnPath.Count - 1]) return;

        // 현재 세션 내 이미 방문한 타일 → 해당 지점까지 backtrack
        // dragSessionStart - 1 = commit 타일 자체도 포함 (스페이스바 고정 지점까지 되돌아갈 수 있음)
        for (int i = drawnPath.Count - 2; i >= dragSessionStart - 1; i--)
        {
            if (drawnPath[i] == nearest)
            {
                drawnPath.RemoveRange(i + 1, drawnPath.Count - i - 1);
                return;
            }
        }

        // 인접 + 흙 타일이면 추가 (commit된 이전 경로 교차 허용)
        Vector2Int last = drawnPath[drawnPath.Count - 1];
        bool isAdjacent = Mathf.Abs(nearest.x - last.x) + Mathf.Abs(nearest.y - last.y) == 1;
        bool isDirt     = map.GetTileType(nearest.x, nearest.y) == Map.TileType.Dirt;

        if (isAdjacent && isDirt)
            drawnPath.Add(nearest);
    }

    Vector2Int WorldToTile(Vector3 worldPos)
    {
        float halfW = map.mapWidth  * map.tileSize / 2f;
        float halfH = map.mapHeight * map.tileSize / 2f;
        int tx = Mathf.RoundToInt((worldPos.x + halfW - map.tileSize * 0.5f) / map.tileSize);
        int ty = Mathf.RoundToInt((worldPos.y + halfH - map.tileSize * 0.5f) / map.tileSize);
        tx = Mathf.Clamp(tx, 0, map.mapWidth  - 1);
        ty = Mathf.Clamp(ty, 0, map.mapHeight - 1);
        return new Vector2Int(tx, ty);
    }

    // ── 경로 완성 ──────────────────────────────────────────────────
    void OnPathComplete()
    {
        pathComplete = true;

        pathLine.startColor     = COL_DONE;
        pathLine.endColor       = COL_DONE;
        pathLine.material.color = COL_DONE;
        SetArrowColors(COL_DONE);

        if (pulseIndicator != null) pulseIndicator.SetActive(false);

        if (instrTxt != null)
            instrTxt.text = "경로 완성! ▶ Start 버튼 또는 Space로 시작  |  경로 위 클릭 → 수정  |  Ctrl+Z → 되돌리기";

        if (startBtnFill != null)
        {
            startBtnFill.color = COL_BTN_ON;
            StartCoroutine(PulseStartBtn());
        }

        Debug.Log("[RouteDrawer] ✅ 경로 완성!");
    }

    // ── 경로 미완성으로 복원 ───────────────────────────────────────
    void OnPathIncomplete()
    {
        pathComplete = false;

        pathLine.startColor     = COL_LINE;
        pathLine.endColor       = COL_LINE;
        pathLine.material.color = COL_LINE;
        SetArrowColors(COL_LINE);

        if (pulseIndicator != null)
        {
            pulseIndicator.SetActive(true);
            StopCoroutine(nameof(PulseIndicator));
            StartCoroutine(PulseIndicator());
        }

        if (startBtnFill != null) startBtnFill.color = COL_BTN_OFF;

        if (instrTxt != null)
            instrTxt.text = "흙길을 드래그하여 GOAL까지 이어주세요  |  경로 위 클릭 → 그 지점부터 수정  |  Ctrl+Z → 되돌리기";
    }

    IEnumerator PulseStartBtn()
    {
        IsInPulseCoroutine = true;
        while (pathComplete && startBtnFill != null)
        {
            float t = Mathf.PingPong(Time.unscaledTime * 2.5f, 1f);
            startBtnFill.color = Color.Lerp(COL_BTN_ON, COL_BTN_HOV, t);
            yield return null;
        }
        IsInPulseCoroutine = false;
    }

    // ── 게임 시작 ──────────────────────────────────────────────────
    void OnStartGame()
    {
        gameStarted = true;

        var worldPath = new List<Vector3>();
        foreach (var tile in drawnPath)
        {
            Vector3 wp = map.GetWorldPosition(tile.x, tile.y);
            wp.z = -1f;
            worldPath.Add(wp);
        }

        var order = allyOrderPanel != null
            ? allyOrderPanel.GetAllyOrder()
            : new System.Collections.Generic.List<AllyType>
                { AllyType.Warrior, AllyType.Archer, AllyType.Mage, AllyType.Cleric };

        int stageIndex = StageManager.Instance != null ? StageManager.Instance.currentStageIndex : 1;
        order = StageManager.NormalizeSelectedAllies(order, stageIndex);

        GameManager.Instance?.ConfirmRouteAndStartWave(worldPath, order);

        if (hudCanvas != null)           Destroy(hudCanvas.gameObject);
        if (startMarkerGo != null)       Destroy(startMarkerGo);
        if (goalMarkerGo != null)        Destroy(goalMarkerGo);
        if (pathLine != null)            Destroy(pathLine.gameObject);
        if (pulseIndicator != null)      Destroy(pulseIndicator);
        if (dirtHighlightParent != null) Destroy(dirtHighlightParent);
        foreach (var go in arrowPool)    if (go != null) Destroy(go);
        arrowPool.Clear();

        Destroy(this);
    }

    // ── 비주얼 업데이트 ────────────────────────────────────────────
    void UpdatePathLine()
    {
        if (pathLine == null || drawnPath.Count == 0) return;
        pathLine.positionCount = drawnPath.Count;
        for (int i = 0; i < drawnPath.Count; i++)
        {
            Vector3 wp = map.GetWorldPosition(drawnPath[i].x, drawnPath[i].y);
            wp.z = -2f;
            pathLine.SetPosition(i, wp);
        }
        UpdateArrows();
    }

    // ── 방향 화살표 ────────────────────────────────────────────────
    void UpdateArrows()
    {
        if (arrowSprite == null) return;
        int needed = Mathf.Max(0, drawnPath.Count - 1);

        // 풀 확장
        while (arrowPool.Count < needed)
        {
            var go = new GameObject("PathArrow");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = arrowSprite;
            sr.sortingOrder = 10;
            go.SetActive(false);
            arrowPool.Add(go);
        }

        Color arrowColor = pathComplete ? COL_DONE : COL_LINE;
        float size       = map.tileSize * 0.36f;

        for (int i = 0; i < arrowPool.Count; i++)
        {
            if (i < needed)
            {
                Vector3 from = map.GetWorldPosition(drawnPath[i].x,   drawnPath[i].y);
                Vector3 to   = map.GetWorldPosition(drawnPath[i+1].x, drawnPath[i+1].y);
                Vector3 mid  = Vector3.Lerp(from, to, 0.5f);
                mid.z = -4f;

                Vector2 d = (to - from).normalized;
                float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;

                var go = arrowPool[i];
                go.transform.position   = mid;
                go.transform.rotation   = Quaternion.Euler(0f, 0f, angle);
                go.transform.localScale = Vector3.one * size;
                go.GetComponent<SpriteRenderer>().color = arrowColor;
                go.SetActive(true);
            }
            else
            {
                arrowPool[i].SetActive(false);
            }
        }
    }

    void SetArrowColors(Color c)
    {
        foreach (var go in arrowPool)
            if (go != null && go.activeSelf)
                go.GetComponent<SpriteRenderer>().color = c;
    }

    void UpdatePulse()
    {
        if (pulseIndicator == null || drawnPath.Count == 0) return;
        Vector3 pos = map.GetWorldPosition(
            drawnPath[drawnPath.Count - 1].x,
            drawnPath[drawnPath.Count - 1].y);
        pos.z = -3f;
        pulseIndicator.transform.position = pos;
    }

    // ── 비주얼 설정 ────────────────────────────────────────────────
    void SetupDirtHighlights()
    {
        dirtHighlightParent = new GameObject("DirtHighlights");
        var spr = MakeSoftHighlightSprite(32);

        for (int x = 0; x < map.mapWidth; x++)
        for (int y = 0; y < map.mapHeight; y++)
        {
            if (map.GetTileType(x, y) != Map.TileType.Dirt) continue;

            var go = new GameObject($"H_{x}_{y}");
            go.transform.SetParent(dirtHighlightParent.transform);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = spr;
            sr.color        = COL_HIGHLIGHT;
            sr.sortingOrder = 2;
            Vector3 pos = map.GetWorldPosition(x, y);
            pos.z = -0.5f;
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * map.tileSize * 1.05f;
        }

        StartCoroutine(PulseDirtHighlights());
    }

    IEnumerator PulseDirtHighlights()
    {
        while (dirtHighlightParent != null && !gameStarted)
        {
            float t     = Mathf.PingPong(Time.unscaledTime * 0.9f, 1f);
            float alpha = Mathf.Lerp(0.06f, 0.16f, t);
            Color c     = new Color(COL_HIGHLIGHT.r, COL_HIGHLIGHT.g, COL_HIGHLIGHT.b, alpha);

            foreach (Transform child in dirtHighlightParent.transform)
            {
                var sr = child.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = c;
            }
            yield return null;
        }
    }

    void SetupPathLine()
    {
        arrowSprite = MakeArrowSprite();

        var go   = new GameObject("DrawnPathLine");
        pathLine = go.AddComponent<LineRenderer>();
        pathLine.useWorldSpace     = true;
        pathLine.widthMultiplier   = 0.22f;
        pathLine.sortingOrder      = 8;
        pathLine.numCapVertices    = 5;
        pathLine.numCornerVertices = 5;
        pathLine.startColor        = COL_LINE;
        pathLine.endColor          = COL_LINE;
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = COL_LINE;
        pathLine.material = mat;
    }

    void SetupMarkers()
    {
        Vector3 startWorld = map.GetWorldPosition(startTile.x, startTile.y);
        startMarkerGo = CreateWorldBadge("START", startWorld, new Color(0.15f, 0.75f, 0.25f));

        Vector3 goalWorld = map.GetWorldPosition(goalTile.x, goalTile.y);
        goalMarkerGo = CreateWorldBadge("GOAL", goalWorld, new Color(0.95f, 0.25f, 0.25f));

        pulseIndicator = new GameObject("PulseIndicator");
        var sr = pulseIndicator.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite(32);
        sr.color  = new Color(1f, 0.9f, 0.2f, 0.85f);
        sr.sortingOrder = 12;
        pulseIndicator.transform.position   = new Vector3(startWorld.x, startWorld.y, -3f);
        pulseIndicator.transform.localScale = Vector3.one * 0.6f;
        StartCoroutine(PulseIndicator());
    }

    IEnumerator PulseIndicator()
    {
        while (pulseIndicator != null && !pathComplete)
        {
            float t = Mathf.PingPong(Time.unscaledTime * 2.2f, 1f);
            pulseIndicator.transform.localScale = Vector3.one * (0.5f + t * 0.25f);
            yield return null;
        }
    }

    GameObject CreateWorldBadge(string label, Vector3 worldPos, Color color)
    {
        var bg   = new GameObject("Badge_" + label);
        var bgSr = bg.AddComponent<SpriteRenderer>();
        bgSr.sprite       = MakeRoundedRect(80, 30);
        bgSr.color        = new Color(0f, 0f, 0f, 0.75f);
        bgSr.sortingOrder = 10;
        bg.transform.position   = new Vector3(worldPos.x, worldPos.y + 0.75f, -3f);
        bg.transform.localScale = Vector3.one * 0.025f;

        var canvasGo = new GameObject("BadgeCanvas_" + label);
        var wc = canvasGo.AddComponent<Canvas>();
        wc.renderMode   = RenderMode.WorldSpace;
        wc.sortingOrder = 11;
        canvasGo.transform.position   = new Vector3(worldPos.x, worldPos.y + 0.75f, -3.1f);
        canvasGo.transform.localScale = Vector3.one * 0.018f;

        var tgo = new GameObject("Txt");
        tgo.transform.SetParent(canvasGo.transform, false);
        var tx = tgo.AddComponent<Text>();
        tx.text      = label;
        tx.color     = color;
        tx.fontSize  = 32;
        tx.fontStyle = FontStyle.Bold;
        tx.alignment = TextAnchor.MiddleCenter;
        tx.font      = UiPixelFont.Get();
        tgo.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 50);

        var parent = new GameObject("MarkerGroup_" + label);
        bg.transform.SetParent(parent.transform, true);
        canvasGo.transform.SetParent(parent.transform, true);
        return parent;
    }

    void SetupHUD()
    {
        var cgo   = new GameObject("RouteHUDCanvas");
        hudCanvas = cgo.AddComponent<Canvas>();
        hudCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 60;
        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280, 720);
        sc.matchWidthOrHeight  = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        CreateUIImg(cgo.transform, new Color(0f,0f,0f,0.55f), new Vector2(0, -325), new Vector2(820, 44));

        instrTxt = CreateUITxt(cgo.transform,
            "흙길을 드래그하여 GOAL까지 이어주세요  |  경로 위 클릭 → 그 지점부터 수정  |  Ctrl+Z → 되돌리기",
            new Color(1f, 1f, 0.85f), new Vector2(0, -325), new Vector2(820, 44), 18);

        CreateUIImg(cgo.transform, new Color(0f,0f,0f,0.45f), new Vector2(-548, 318), new Vector2(126, 52));

        var fillGo = new GameObject("StartBtnFill");
        fillGo.transform.SetParent(cgo.transform, false);
        startBtnFill       = fillGo.AddComponent<Image>();
        startBtnFill.color = COL_BTN_OFF;
        var frt = fillGo.GetComponent<RectTransform>();
        frt.anchoredPosition = new Vector2(-548, 318);
        frt.sizeDelta        = new Vector2(126, 52);

        var lgo = new GameObject("StartLbl");
        lgo.transform.SetParent(fillGo.transform, false);
        var ltx = lgo.AddComponent<Text>();
        ltx.text      = "▶  Start";
        ltx.color     = new Color(1f, 1f, 1f, 0.55f);
        ltx.fontSize  = 22;
        ltx.fontStyle = FontStyle.Bold;
        ltx.alignment = TextAnchor.MiddleCenter;
        ltx.font      = UiPixelFont.Get();
        var lrt = lgo.GetComponent<RectTransform>();
        lrt.anchoredPosition = Vector2.zero;
        lrt.sizeDelta        = new Vector2(126, 52);

        StartCoroutine(UpdateBtnLabelColor(ltx));

        // ── 아군 출전 순서 패널 생성 (화면 왼쪽) ──────────────────────
        var panelHolder = new GameObject("AllyOrderPanel", typeof(RectTransform));
        panelHolder.transform.SetParent(cgo.transform, false);
        var holderRt = (RectTransform)panelHolder.transform;
        holderRt.anchorMin = Vector2.zero;
        holderRt.anchorMax = Vector2.one;
        holderRt.offsetMin = Vector2.zero;
        holderRt.offsetMax = Vector2.zero;
        allyOrderPanel = panelHolder.AddComponent<AllyOrderPanel>();
        var selectedAllies = StageManager.Instance != null
            ? StageManager.Instance.selectedAllies
            : null;
        allyOrderPanel.Initialize(hudCanvas, selectedAllies);
    }

    IEnumerator UpdateBtnLabelColor(Text lbl)
    {
        while (lbl != null)
        {
            lbl.color = pathComplete
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(1f, 1f, 1f, 0.45f);
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }

    // ── UI 헬퍼 ───────────────────────────────────────────────────
    void CreateUIImg(Transform p, Color c, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Img"); go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = c;
        var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    Text CreateUITxt(Transform p, string s, Color c, Vector2 pos, Vector2 size, int fs)
    {
        var go = new GameObject("Txt"); go.transform.SetParent(p, false);
        var tx = go.AddComponent<Text>(); tx.text = s; tx.color = c; tx.fontSize = fs;
        tx.alignment = TextAnchor.MiddleCenter; tx.fontStyle = FontStyle.Bold;
        tx.font = UiPixelFont.Get();
        var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = size;
        return tx;
    }

    // ── 스프라이트 헬퍼 ───────────────────────────────────────────
    Sprite MakeSquareSprite()
    {
        var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        for (int x = 0; x < 4; x++)
        for (int y = 0; y < 4; y++)
            t.SetPixel(x, y, Color.white);
        t.Apply();
        return Sprite.Create(t, new Rect(0,0,4,4), Vector2.one*0.5f, 4);
    }

    Sprite MakeSoftHighlightSprite(int res)
    {
        var t = new Texture2D(res, res, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;
        float c = (res - 1) * 0.5f;
        float inner = res * 0.25f;
        float outer = res * 0.47f;

        for (int x = 0; x < res; x++)
        for (int y = 0; y < res; y++)
        {
            float dx = x - c;
            float dy = y - c;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = d <= inner ? 1f : Mathf.Clamp01((outer - d) / (outer - inner));
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }

        t.Apply();
        return Sprite.Create(t, new Rect(0,0,res,res), Vector2.one*0.5f, res);
    }

    Sprite MakeCircleSprite(int res)
    {
        var t = new Texture2D(res, res, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;
        int c = res / 2;
        for (int x = 0; x < res; x++)
        for (int y = 0; y < res; y++)
        {
            float d     = Mathf.Sqrt((x-c)*(x-c) + (y-c)*(y-c));
            float inner = c - 5f;
            float outer = c - 1f;
            float a = d > outer ? 0f : (d < inner ? 1f : (outer - d) / (outer - inner));
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply();
        return Sprite.Create(t, new Rect(0,0,res,res), Vector2.one*0.5f, res);
    }

    Sprite MakeArrowSprite()
    {
        // 위쪽을 가리키는 삼각형 화살표 (y=0 밑변, y=top 꼭짓점)
        int size = 24;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var clear = new Color[size * size];
        t.SetPixels(clear);

        int mid   = size / 2;
        int baseY = 2;
        int tipY  = size - 3;

        for (int y = baseY; y <= tipY; y++)
        {
            float progress = (float)(y - baseY) / (tipY - baseY); // 0=밑변, 1=꼭짓점
            int halfW = Mathf.RoundToInt((1f - progress) * (mid - 1));
            for (int x = mid - halfW; x <= mid + halfW; x++)
                t.SetPixel(x, y, Color.white);
        }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
    }

    Sprite MakeRoundedRect(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            t.SetPixel(x, y, Color.white);
        t.Apply();
        return Sprite.Create(t, new Rect(0,0,w,h), Vector2.one*0.5f, w);
    }
}
