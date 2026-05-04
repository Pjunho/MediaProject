using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 스테이지 선택 화면
/// - 카드 5장을 캔버스에 직접 배치 (Mask 없음)
/// - 좌우 드래그 + 관성(모멘텀) 스크롤
/// - 블라인더 패널로 화면 밖 카드를 시각적으로 가림
/// - ◀ / ▶ 화살표: 끝에 도달 시 자동 숨김
/// </summary>
public class StageSelect : MonoBehaviour
{
    // ── 색상 상수 ─────────────────────────────────────────────────────
    static readonly Color C_BG       = new Color(0.06f, 0.08f, 0.15f);
    static readonly Color C_TITLE    = new Color(1.00f, 0.85f, 0.20f);
    static readonly Color C_LOCKED   = new Color(0.25f, 0.25f, 0.30f);
    static readonly Color C_LOCKED_H = new Color(0.35f, 0.35f, 0.42f);
    static readonly Color C_BACK     = new Color(0.30f, 0.15f, 0.15f);
    static readonly Color C_BACK_H   = new Color(0.50f, 0.22f, 0.22f);

    // 스테이지별 색상
    static readonly Color[] C_STAGE   = { default,
        new Color(0.15f,0.50f,0.20f), new Color(0.20f,0.35f,0.60f),
        new Color(0.60f,0.30f,0.10f), new Color(0.35f,0.12f,0.55f),
        new Color(0.55f,0.08f,0.08f)
    };
    static readonly Color[] C_STAGE_H = { default,
        new Color(0.22f,0.68f,0.28f), new Color(0.28f,0.48f,0.80f),
        new Color(0.80f,0.42f,0.15f), new Color(0.50f,0.18f,0.75f),
        new Color(0.75f,0.12f,0.12f)
    };
    static readonly Color[] C_CARD_BG = { default,
        new Color(0.08f,0.20f,0.08f), new Color(0.10f,0.14f,0.28f),
        new Color(0.24f,0.10f,0.04f), new Color(0.14f,0.06f,0.24f),
        new Color(0.24f,0.04f,0.04f)
    };

    // ── 스크롤 상수 ───────────────────────────────────────────────────
    // 카드 간격 290px, 중심 기준 배치
    // si=1 → -580, si=2 → -290, si=3 → 0, si=4 → +290, si=5 → +580
    // scrollX: 양수 = 카드가 우측 이동 (작은 번호 보임), 음수 = 카드가 좌측 이동 (큰 번호 보임)
    const float MAX_SCROLL_X =  290f;   // 왼쪽 끝: 1·2·3 스테이지 표시
    const float MIN_SCROLL_X = -290f;   // 오른쪽 끝: 3·4·5 스테이지 표시
    const float CARD_SPACING =  290f;
    const float CARD_Y       =   20f;
    const float FRICTION     =    0.88f;  // 관성 감속 계수 (1프레임당)
    const float MIN_VELOCITY =    2f;     // 이 이하이면 관성 정지

    static float CardBaseX(int si) => (si - 3) * CARD_SPACING;

    float scrollX        = MAX_SCROLL_X;
    float scrollVelocity = 0f;

    bool    isDragging      = false;
    bool    scrollConsumed  = false;
    Vector2 dragStartMouse;
    float   dragStartScrollX;
    float   prevDragScrollX;  // 전 프레임 scrollX (속도 계산용)

    readonly System.Collections.Generic.List<RectTransform> cardRects = new();

    // ── 버튼 데이터 ───────────────────────────────────────────────────
    struct BtnData
    {
        public string id;
        public RectTransform rt;
        public Image fill;
        public Color n, h;
        public System.Action cb;
        public bool inScrollArea;
    }
    readonly System.Collections.Generic.List<BtnData> btns = new();

    // 뷰포트 경계 RT (스크롤 영역 내 버튼 클릭 필터용)
    RectTransform viewportRt;
    Canvas        mainCanvas;

    // 화살표
    GameObject arrowLeft, arrowRight;

    // ── 아군 상세 정보 ────────────────────────────────────────────────
    struct SkillInfo { public string name, desc; public Color iconColor; }
    struct AllyDetailData
    {
        public string    allyName, role;
        public float     hp, speed;
        public SkillInfo skill;
    }

    static readonly System.Collections.Generic.Dictionary<AllyType, AllyDetailData> AllyDetails =
        new System.Collections.Generic.Dictionary<AllyType, AllyDetailData>
    {
        { AllyType.Warrior, new AllyDetailData {
            allyName="검객", role="근접 전투형",
            hp=180f, speed=3.2f,
            skill=new SkillInfo{ name="방패 밀치기",
                desc="전방의 적을 강하게 밀쳐내어\n이동 경로를 확보한다.",
                iconColor=new Color(0.85f,0.25f,0.20f) }
        }},
        { AllyType.Archer, new AllyDetailData {
            allyName="궁수", role="중거리 지원형",
            hp=140f, speed=3.8f,
            skill=new SkillInfo{ name="쾌속 이동",
                desc="빠른 발걸음으로 이동 속도가\n25% 증가합니다.",
                iconColor=new Color(0.25f,0.75f,0.30f) }
        }},
        { AllyType.Mage, new AllyDetailData {
            allyName="마법사", role="원거리 광역형",
            hp=110f, speed=4.2f,
            skill=new SkillInfo{ name="생명력 강화",
                desc="마법 에너지로 몸을 강화하여\n최대 HP가 30% 증가합니다.",
                iconColor=new Color(0.95f,0.45f,0.10f) }
        }},
        { AllyType.Cleric, new AllyDetailData {
            allyName="성직자", role="근접 지원형",
            hp=200f, speed=2.8f,
            skill=new SkillInfo{ name="치유 기도",
                desc="신성한 기도로 매 초\n최대 HP의 3%를 회복합니다.",
                iconColor=new Color(0.90f,0.85f,0.30f) }
        }},
        { AllyType.Rogue, new AllyDetailData {
            allyName="도적", role="민첩 기습형",
            hp=90f, speed=4.8f,
            skill=new SkillInfo{ name="그림자 걸음",
                desc="어둠 속에 몸을 숨기며\n이동 속도가 40% 증가합니다.",
                iconColor=new Color(0.60f,0.20f,0.80f) }
        }},
        { AllyType.Paladin, new AllyDetailData {
            allyName="성기사", role="중장 방어형",
            hp=280f, speed=1.8f,
            skill=new SkillInfo{ name="성전사의 서약",
                desc="신성한 서약으로 몸을 강화하여\n최대 HP가 2배가 됩니다.",
                iconColor=new Color(0.90f,0.75f,0.10f) }
        }},
    };

    // 상세 팝업
    GameObject detailPopup;
    Image      detailPortraitImg;
    Text       detailNameTxt, detailRoleTxt, detailStatTxt;
    Image      detailSkillIconImg;
    Text       detailSkillNameTxt;

    // 스킬 설명 오버레이 (꾹 누르는 동안 표시)
    GameObject detailSkillDescOverlay;
    Text       detailSkillDescTxt;
    RectTransform detailSkillBtnRt;   // 스킬 버튼 히트박스
    float      skillHoldTimer  = 0f;
    const float HOLD_THRESHOLD = 0.0f;  // 누르는 즉시 표시
    bool       skillDescShown  = false;

    // 우클릭 감지용: 선택 가능 아군 버튼별 AllyType 저장
    readonly System.Collections.Generic.List<(RectTransform rt, AllyType type)> availRightClickTargets = new();

    // ── 토스트 알림 ───────────────────────────────────────────────────
    GameObject toastGo;
    Text       toastTxt;
    System.Collections.IEnumerator toastCoroutine;

    // ── 준비 패널 ─────────────────────────────────────────────────────
    GameObject prepPanel;
    Text prepTitleTxt, prepSlotTxt;
    // 선택 가능 아군: 6종 표시 (선택 시 어둡게)
    static readonly AllyType[] AVAIL_TYPES = { AllyType.Warrior, AllyType.Archer, AllyType.Mage, AllyType.Cleric, AllyType.Rogue, AllyType.Paladin };
    readonly int[] availSelectedCount = new int[6]; // 각 타입이 선택된 횟수

    readonly System.Collections.Generic.List<AllyType>   prepSelected   = new();
    readonly System.Collections.Generic.List<AllyType>   prepAvailable  = new(); // 하위 호환용 (사용 안 함)
    readonly System.Collections.Generic.List<GameObject> prepSlotItems        = new();
    readonly System.Collections.Generic.List<Text>       prepSlotLabels       = new();
    readonly System.Collections.Generic.List<Image>      prepSlotPortraits    = new();
    readonly System.Collections.Generic.List<GameObject> prepAvailItems  = new();
    readonly System.Collections.Generic.List<Text>       prepAvailLabels = new();
    readonly System.Collections.Generic.List<Image>      prepAvailPortraits = new();
    int prepStageIndex = -1;

    // ── 라이프사이클 ──────────────────────────────────────────────────
    void Start()
    {
        Debug.Log("[StageSelect] Start()");
        if (StageManager.Instance == null)
            new GameObject("StageManager").AddComponent<StageManager>();

        SetupCamera();
        DrawStars();
        BuildUI();
        Debug.Log("[StageSelect] BuildUI 완료");
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 mp = mouse.position.ReadValue();

        HandleScrollDrag(mouse, mp);
        ApplyInertia();
        HandleButtonHover(mouse, mp);
        HandleRightClick(mouse, mp);
        HandleSkillHold(mouse, mp);
    }

    // ── 모멘텀 스크롤 ─────────────────────────────────────────────────
    void HandleScrollDrag(Mouse mouse, Vector2 mp)
    {
        if (prepPanel != null && prepPanel.activeSelf)
        { isDragging = false; scrollVelocity = 0f; return; }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            isDragging       = true;
            scrollConsumed   = false;
            scrollVelocity   = 0f;
            dragStartMouse   = mp;
            dragStartScrollX = scrollX;
            prevDragScrollX  = scrollX;
        }

        if (isDragging && mouse.leftButton.isPressed)
        {
            float delta = mp.x - dragStartMouse.x;
            if (Mathf.Abs(delta) > 5f) scrollConsumed = true;

            float prevX = scrollX;
            scrollX = Mathf.Clamp(dragStartScrollX + delta * 0.70f, MIN_SCROLL_X, MAX_SCROLL_X);

            // 속도 = 이번 프레임 이동량 / deltaTime (픽셀/초)
            if (Time.deltaTime > 0f)
                scrollVelocity = (scrollX - prevDragScrollX) / Time.deltaTime;
            prevDragScrollX = scrollX;

            ApplyScrollToCards();
            UpdateArrows();
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            isDragging    = false;
            // scrollConsumed는 버튼 클릭 필터용으로 다음 프레임에 리셋
        }
    }

    void ApplyInertia()
    {
        if (isDragging) return;
        if (Mathf.Abs(scrollVelocity) < MIN_VELOCITY) { scrollVelocity = 0f; return; }

        scrollX = Mathf.Clamp(scrollX + scrollVelocity * Time.deltaTime, MIN_SCROLL_X, MAX_SCROLL_X);
        scrollVelocity *= Mathf.Pow(FRICTION, Time.deltaTime * 60f); // 프레임레이트 무관 감속

        if (scrollX <= MIN_SCROLL_X || scrollX >= MAX_SCROLL_X)
            scrollVelocity = 0f;

        ApplyScrollToCards();
        UpdateArrows();
    }

    void ApplyScrollToCards()
    {
        for (int i = 0; i < cardRects.Count; i++)
        {
            if (cardRects[i] == null) continue;
            SR(cardRects[i], new Vector2(CardBaseX(i + 1) + scrollX, CARD_Y), cardRects[i].sizeDelta);
        }
    }

    void UpdateArrows()
    {
        if (arrowLeft  != null) arrowLeft.SetActive (scrollX < MAX_SCROLL_X - 8f);
        if (arrowRight != null) arrowRight.SetActive(scrollX > MIN_SCROLL_X + 8f);
    }

    void HandleRightClick(Mouse mouse, Vector2 mp)
    {
        // 팝업이 열려 있으면 좌·우클릭 모두로 닫기
        if (detailPopup != null && detailPopup.activeSelf)
        {
            if (mouse.rightButton.wasPressedThisFrame || mouse.leftButton.wasPressedThisFrame)
            {
                detailPopup.SetActive(false);
                scrollConsumed = true; // 팝업 닫을 때 하위 버튼 클릭 방지
            }
            return;
        }

        // 우클릭만 상세 팝업 열기
        if (!mouse.rightButton.wasPressedThisFrame) return;
        if (prepPanel == null || !prepPanel.activeSelf) return;

        foreach (var (rt, type) in availRightClickTargets)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, mp, null))
            {
                ShowAllyDetail(type);
                return;
            }
        }
    }

    void HandleSkillHold(Mouse mouse, Vector2 mp)
    {
        if (detailPopup == null || !detailPopup.activeSelf) return;
        if (detailSkillBtnRt == null || detailSkillDescOverlay == null) return;

        bool overSkill = RectTransformUtility.RectangleContainsScreenPoint(detailSkillBtnRt, mp, null);

        if (overSkill != skillDescShown)
        {
            skillDescShown = overSkill;
            detailSkillDescOverlay.SetActive(overSkill);
        }
    }

    void ShowAllyDetail(AllyType type)
    {
        if (!AllyDetails.TryGetValue(type, out var d)) return;

        detailPortraitImg.sprite  = AllyVisualGenerator.CreatePortraitSprite(type);
        detailPortraitImg.color   = Color.white;
        detailNameTxt.text        = d.allyName;
        detailRoleTxt.text        = d.role;
        detailStatTxt.text        = $"HP  {d.hp}     이동속도  {d.speed}";
        detailSkillIconImg.color  = d.skill.iconColor;
        detailSkillNameTxt.text   = d.skill.name;
        detailSkillDescTxt.text   = d.skill.desc;
        if (detailSkillDescOverlay != null) detailSkillDescOverlay.SetActive(false);
        skillDescShown = false;
        detailPopup.SetActive(true);
    }

    void HandleButtonHover(Mouse mouse, Vector2 mp)
    {
        // 드래그 직후 버튼 클릭 방지: wasReleasedThisFrame에도 scrollConsumed를 유지하고
        // 다음 프레임(Press 없음)에서 리셋
        if (!mouse.leftButton.isPressed && !mouse.leftButton.wasPressedThisFrame)
            scrollConsumed = false;

        foreach (var b in btns)
        {
            if (b.rt == null || !b.rt.gameObject.activeInHierarchy) continue;

            // 스크롤 영역 버튼은 뷰포트 안에 있을 때만 반응
            if (b.inScrollArea && viewportRt != null)
            {
                Camera cam = mainCanvas != null && mainCanvas.renderMode == RenderMode.ScreenSpaceCamera
                    ? mainCanvas.worldCamera : null;
                if (!RectTransformUtility.RectangleContainsScreenPoint(viewportRt, mp, cam))
                {
                    if (b.fill != null) b.fill.color = b.n;
                    continue;
                }
            }

            Camera hoverCam = mainCanvas != null && mainCanvas.renderMode == RenderMode.ScreenSpaceCamera
                ? mainCanvas.worldCamera : null;
            bool over = GetStrictButtonHit(b, mp, hoverCam);
            if (b.fill != null) b.fill.color = over ? b.h : b.n;
            if (over && mouse.leftButton.wasPressedThisFrame && !scrollConsumed)
                b.cb?.Invoke();
        }
    }

    bool GetStrictButtonHit(BtnData b, Vector2 screenPoint, Camera cam)
    {
        float insetX = 4f;
        float insetY = 4f;

        if (b.id == "prep_reset")
            insetY = 6f;
        else if (b.id.StartsWith("slot_"))
            insetX = insetY = 6f;
        else if (b.id.StartsWith("avail_"))
            insetX = insetY = 5f;
        else if (b.id.StartsWith("play_") || b.id == "prep_back" || b.id == "prep_start" || b.id == "back")
            insetX = insetY = 5f;

        return ContainsScreenPointInset(b.rt, screenPoint, cam, insetX, insetY);
    }

    static bool ContainsScreenPointInset(RectTransform rt, Vector2 screenPoint, Camera cam, float insetX, float insetY)
    {
        if (rt == null) return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, cam, out var local))
            return false;

        Rect rect = rt.rect;
        rect.xMin += insetX;
        rect.xMax -= insetX;
        rect.yMin += insetY;
        rect.yMax -= insetY;
        return rect.Contains(local);
    }

    // ── RectTransform 헬퍼 (앵커·피벗을 항상 중앙(0.5,0.5)으로 고정) ──
    static void SR(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
    }

    // ── 카메라 ────────────────────────────────────────────────────────
    void SetupCamera()
    {
        Camera cam = FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            var g = new GameObject("Main Camera"); g.tag = "MainCamera";
            cam = g.AddComponent<Camera>();
        }
        cam.tag              = "MainCamera";
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = C_BG;
        cam.orthographic     = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0, 0, -10f);
    }

    // ── 배경 별빛 ─────────────────────────────────────────────────────
    void DrawStars()
    {
        var rng = new System.Random(42);
        for (int i = 0; i < 60; i++)
        {
            float x = (float)(rng.NextDouble() * 24 - 12);
            float y = (float)(rng.NextDouble() * 10 - 5);
            float s = (float)(rng.NextDouble() * 0.06f + 0.02f);
            float a = (float)(rng.NextDouble() * 0.5f + 0.2f);
            var go = new GameObject($"Star{i}");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeCircSprite(8);
            sr.color  = new Color(1f, 1f, 1f, a);
            sr.sortingOrder = -20;
            go.transform.position   = new Vector3(x, y, 5f);
            go.transform.localScale = Vector3.one * s;
        }
    }

    // ── UI 전체 구성 ──────────────────────────────────────────────────
    void BuildUI()
    {
        // ── 캔버스 ──────────────────────────────────────────────────
        var cgo = new GameObject("StageCanvas");
        mainCanvas = cgo.AddComponent<Canvas>();
        mainCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        mainCanvas.sortingOrder = 10;
        var csc = cgo.AddComponent<CanvasScaler>();
        csc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        csc.referenceResolution = new Vector2(1280, 720);
        csc.matchWidthOrHeight  = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        Transform cv = cgo.transform;

        // ── 전체 배경 ────────────────────────────────────────────────
        MkImg(cv, C_BG, Vector2.zero, new Vector2(1400, 800));

        // ── 제목 ─────────────────────────────────────────────────────
        MkImg(cv, new Color(0f, 0f, 0f, 0.45f), new Vector2(0, 300), new Vector2(720, 64));
        MkTxt(cv, "스테이지 선택", C_TITLE, new Vector2(0, 300), new Vector2(720, 64), 38);
        MkImg(cv, new Color(1f, 0.85f, 0.2f, 0.55f), new Vector2(0, 265), new Vector2(720, 2));

        // ── 뷰포트 경계 오브젝트 (시각 없음, 버튼 클릭 범위 판단용) ─
        var vpGo = new GameObject("Viewport");
        vpGo.transform.SetParent(cv, false);
        // RectTransform을 Image가 아닌 일반 오브젝트에 직접 추가하므로
        // 반드시 anchorMin/Max를 명시 설정
        var vpImg = vpGo.AddComponent<Image>();  // Image → auto-adds RectTransform
        vpImg.color = new Color(0, 0, 0, 0);     // 완전 투명 (시각 없음)
        viewportRt  = vpGo.GetComponent<RectTransform>();
        SR(viewportRt, new Vector2(0f, CARD_Y + 10f), new Vector2(950f, 380f));

        // ── 스테이지 카드 ─────────────────────────────────────────────
        string[] terrains     = { "", "평원 지대", "사막 지대", "화산 지대", "어둠 지대", "최종 요새" };
        string[] difficulties = { "", "쉬움", "보통", "어려움", "매우 어려움", "극한" };
        var cardSize = new Vector2(280f, 350f);

        cardRects.Clear();
        int stageCount = StageManager.GetStageCount();
        for (int si = 1; si <= stageCount; si++)
        {
            int    cap    = si;
            bool   locked = !StageManager.IsStageUnlocked(si);
            int    saved  = StageManager.GetSavedStars(si);
            string cond    = locked ? "" : StageManager.GetStarConditionTextForStage(si);
            string desc    = $"{terrains[si]}\n15웨이브  |  난이도 {difficulties[si]}";

            float initX = CardBaseX(si) + scrollX;

            var crt = BuildCard(cv, new Vector2(initX, CARD_Y), cardSize,
                si, locked, saved, desc, cond,
                locked ? C_LOCKED   : C_STAGE[si],
                locked ? C_LOCKED_H : C_STAGE_H[si],
                locked ? () => OnLockedStage(cap) : () => OpenPrep(cap));

            cardRects.Add(crt);
        }

        // ── 블라인더 (카드보다 나중에 추가 → 위에 렌더링 → 오버플로우 가림) ─
        // 화면 좌우 끝(-640~-455, +455~+640)을 배경색으로 덮어 카드가 보이지 않게
        MkImg(cv, C_BG, new Vector2(-554f, CARD_Y + 10f), new Vector2(180f, 400f));
        MkImg(cv, C_BG, new Vector2( 554f, CARD_Y + 10f), new Vector2(180f, 400f));

        // ── 화살표 ───────────────────────────────────────────────────
        arrowLeft  = MakeArrow(cv, "<", new Vector2(-462f, CARD_Y));
        arrowRight = MakeArrow(cv, ">", new Vector2( 462f, CARD_Y));
        UpdateArrows(); // 초기: 왼쪽 끝 → ◀ 숨김

        // ── 안내 텍스트 ───────────────────────────────────────────────
        MkTxt(cv, "< 드래그하여 스테이지 탐색 >",
              new Color(1f, 1f, 1f, 0.30f), new Vector2(0f, -175f), new Vector2(620f, 32f), 16);

        // ── 뒤로 버튼 ─────────────────────────────────────────────────
        RegBtn(cv, "back", "  뒤로",
               new Vector2(-510f, -310f), new Vector2(180f, 46f),
               C_BACK, C_BACK_H, OnBack, false);

        // ── 준비 패널 (최상위 렌더) ───────────────────────────────────
        BuildPrepPanel(cv);

        // ── 토스트 알림 ───────────────────────────────────────────────
        BuildToastUI(cv);
    }

    // ── 화살표 생성 ───────────────────────────────────────────────────
    GameObject MakeArrow(Transform parent, string sym, Vector2 pos)
    {
        var go   = new GameObject("Arrow_" + sym);
        go.transform.SetParent(parent, false);
        var bg   = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);
        SR(go.GetComponent<RectTransform>(), pos + new Vector2(0f, 10f), new Vector2(42f, 380f));
        MkTxt(go.transform,
              sym == "<" ? "◀" : "▶",
              new Color(1f, 1f, 1f, 0.90f),
              Vector2.zero, new Vector2(42f, 380f), 28);
        return go;
    }

    // ── 스테이지 카드 생성 ────────────────────────────────────────────
    RectTransform BuildCard(Transform parent, Vector2 pos, Vector2 size,
        int si, bool locked, int savedStars,
        string desc, string condText,
        Color btnCol, Color btnHov,
        System.Action cb)
    {
        float hy    = size.y * 0.5f;
        float cw    = size.x * 0.88f;
        float scale = size.y / 400f;

        // ─ 카드 배경 ─
        var card = new GameObject($"Card_{si}");
        card.transform.SetParent(parent, false);
        card.AddComponent<Image>().color = locked
            ? new Color(0.12f, 0.12f, 0.16f)
            : C_CARD_BG[si];
        var crt = card.GetComponent<RectTransform>();
        SR(crt, pos, size);

        // 테두리
        MkImgChild(card.transform, new Color(1f, 1f, 1f, locked ? 0.06f : 0.20f),
            Vector2.zero, size + new Vector2(4, 4));

        // 스테이지 이름
        float numY = hy * 0.62f;
        MkTxtChild(card.transform, StageManager.GetStageConfig(si).stageName,
            locked ? new Color(0.40f, 0.40f, 0.40f) : C_STAGE[si],
            new Vector2(0, numY), new Vector2(cw, hy * 0.40f),
            Mathf.Max(28, (int)(42 * scale)));

        // 구분선
        MkImgChild(card.transform, new Color(1f, 1f, 1f, locked ? 0.07f : 0.20f),
            new Vector2(0, hy * 0.40f), new Vector2(cw, 2));

        // 설명
        MkTxtChild(card.transform, desc,
            locked ? new Color(0.38f, 0.38f, 0.38f) : new Color(0.80f, 0.88f, 0.80f),
            new Vector2(0, -hy * 0.02f), new Vector2(cw, hy * 0.42f),
            Mathf.Max(16, (int)(24 * scale)));

        // 클리어 조건
        MkTxtChild(card.transform, condText,
            locked ? new Color(0.32f, 0.32f, 0.32f) : new Color(1f, 0.85f, 0.2f, 0.80f),
            new Vector2(0, -hy * 0.48f), new Vector2(cw, hy * 0.24f),
            Mathf.Max(12, (int)(16 * scale)));

        // 별
        string starStr = locked ? "" : StarStr(savedStars);
        MkTxtChild(card.transform, starStr,
            locked ? new Color(0.35f, 0.35f, 0.35f) : new Color(1f, 0.85f, 0.15f),
            new Vector2(0, -hy * 0.69f), new Vector2(cw, hy * 0.28f),
            Mathf.Max(16, (int)(28 * scale)));

        // 시작 버튼 (inScrollArea=true)
        float btnH = Mathf.Max(32f, size.y * 0.12f);
        RegBtn(card.transform, $"play_{si}",
               locked ? "조건 보기" : "▶  시  작",
               new Vector2(0, -hy * 0.875f), new Vector2(size.x * 0.75f, btnH),
               btnCol, btnHov, cb, true);

        return crt;
    }

    string StarStr(int s) => s switch
    { 1 => "★ ☆ ☆", 2 => "★ ★ ☆", 3 => "★ ★ ★", _ => "☆ ☆ ☆" };

    // ── 준비 패널 ─────────────────────────────────────────────────────
    // 레이아웃: 패널 860×540 중앙 배치
    //   상단: 제목 + 출전 인원 안내
    //   좌측(x<0): 선택 가능 아군 — 초상화만 3열 그리드
    //   우측(x>0): 선택된 출전 순서 — 초상화 + 이름 세로 목록
    //   하단: 버튼 3개
    void BuildPrepPanel(Transform parent)
    {
        // ── 루트 ────────────────────────────────────────────────────
        prepPanel = new GameObject("PrepPanel");
        prepPanel.transform.SetParent(parent, false);
        prepPanel.AddComponent<Image>().color = new Color(0,0,0,0);
        SR(prepPanel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1400, 800));

        Transform pp = prepPanel.transform;

        // 전체 어두운 오버레이
        MkImgChild(pp, new Color(0f,0f,0f,0.80f), Vector2.zero, new Vector2(1400,800));

        // 패널 본체 860×660
        MkImgChild(pp, new Color(0.06f,0.08f,0.14f,0.98f), new Vector2(0,10), new Vector2(860,660));
        MkImgChild(pp, new Color(1f,1f,1f,0.12f),           new Vector2(0,10), new Vector2(864,664));

        // ── 상단: 제목 ───────────────────────────────────────────────
        prepTitleTxt = MkTxtR(pp, "전투 준비", C_TITLE, new Vector2(0, 315), new Vector2(820, 46), 32);
        MkImgChild(pp, new Color(1f,0.85f,0.2f,0.45f), new Vector2(0, 290), new Vector2(820, 2));
        prepSlotTxt = MkTxtR(pp, "", new Color(0.80f,0.90f,1f,0.90f), new Vector2(0, 268), new Vector2(820, 30), 18);

        // ── 중앙 수직 구분선 ─────────────────────────────────────────
        MkImgChild(pp, new Color(1f,1f,1f,0.12f), new Vector2(0, 20), new Vector2(2, 480));

        // ── 좌측 헤더 "선택 가능 아군" ──────────────────────────────
        MkTxtChild(pp, "선택 가능 아군", new Color(0.90f,0.94f,1f),
                   new Vector2(-215, 245), new Vector2(400, 30), 19);
        MkTxtChild(pp, "클릭하여 추가", new Color(1f,1f,1f,0.35f),
                   new Vector2(-215, 218), new Vector2(400, 24), 14);

        // ── 좌측: 선택 가능 아군 초상화 그리드 (3열 × 2행, 최대 6) ─
        // 열: -325, -215, -105 / 행: 140, 30
        float[] availColX = { -325f, -215f, -105f };
        float[] availRowY = {  140f,   30f };

        for (int i = 0; i < 6; i++)
        {
            int cap = i;
            float cx = availColX[i % 3];
            float cy = availRowY[i / 3];

            // 아이템 루트 (90×90)
            var item = new GameObject($"Avail_{i}");
            item.transform.SetParent(pp, false);
            item.AddComponent<Image>().color = new Color(0,0,0,0);
            var itemRt = item.GetComponent<RectTransform>();
            SR(itemRt, new Vector2(cx, cy), new Vector2(92, 92));
            prepAvailItems.Add(item);
            availRightClickTargets.Add((itemRt, AllyType.Warrior)); // 실제 타입은 RefreshPrep에서 갱신

            // 배경
            MkImgChild(item.transform, new Color(0.10f,0.12f,0.18f,0.90f), Vector2.zero, new Vector2(90, 90));
            // 테두리
            MkImgChild(item.transform, new Color(1f,1f,1f,0.15f), Vector2.zero, new Vector2(92, 92));

            // 초상화
            var pgo     = new GameObject("Portrait");
            pgo.transform.SetParent(item.transform, false);
            var portrait = pgo.AddComponent<Image>();
            SR(pgo.GetComponent<RectTransform>(), Vector2.zero, new Vector2(80, 80));
            prepAvailPortraits.Add(portrait);

            // 이름 라벨 (빈 텍스트 — portrait-only 모드, RefreshPrep에서 갱신)
            var lbl = MkTxtR(item.transform, "", new Color(0,0,0,0), Vector2.zero, new Vector2(90,90), 1);
            prepAvailLabels.Add(lbl);

            // 클릭 버튼 (아이템 전체 영역)
            RegBtn(item.transform, $"avail_{i}", "",
                   Vector2.zero, new Vector2(90, 90),
                   new Color(0f,0f,0f,0.01f), new Color(1f,1f,1f,0.22f),
                   () => SelectPrepAlly(cap), false);
        }

        // ── 우측 헤더 "선택된 출전 순서" ────────────────────────────
        MkTxtChild(pp, "선택된 출전 순서", new Color(0.90f,0.94f,1f),
                   new Vector2(215, 245), new Vector2(400, 30), 19);
        MkTxtChild(pp, "클릭하여 제거", new Color(1f,1f,1f,0.35f),
                   new Vector2(215, 218), new Vector2(400, 24), 14);

        // ── 우측: 선택된 슬롯 목록 ───────────────────────────────────
        // 슬롯 높이 70px, 간격 2px → step=72
        // 좌측 그리드 top = 140+45 = 185 → 첫 슬롯 center = 185-35 = 150
        for (int i = 0; i < 6; i++)
        {
            int cap = i;
            float sy = 150f - i * 72f;   // top: 150+35=185 ✓

            var slot = new GameObject($"Slot_{i+1}");
            slot.transform.SetParent(pp, false);
            slot.AddComponent<Image>().color = new Color(0,0,0,0);
            SR(slot.GetComponent<RectTransform>(), new Vector2(215f, sy), new Vector2(380f, 70f));
            prepSlotItems.Add(slot);

            // 슬롯 배경
            MkImgChild(slot.transform, new Color(0.08f,0.10f,0.16f,0.85f), Vector2.zero, new Vector2(380, 70));
            MkImgChild(slot.transform, new Color(1f,1f,1f,0.10f),           Vector2.zero, new Vector2(382, 72));

            // 초상화
            var pgo     = new GameObject("Portrait");
            pgo.transform.SetParent(slot.transform, false);
            var portrait = pgo.AddComponent<Image>();
            SR(pgo.GetComponent<RectTransform>(), new Vector2(-155f, 0f), new Vector2(56, 56));
            prepSlotPortraits.Add(portrait);

            // 이름 라벨 (초상화 오른쪽)
            var lbl = MkTxtR(slot.transform, $"{i+1}. 비어 있음",
                             new Color(1f,1f,1f,0.38f),
                             new Vector2(55f, 0f), new Vector2(240f, 66f), 18);
            lbl.alignment = TextAnchor.MiddleLeft;
            prepSlotLabels.Add(lbl);

            // 클릭 버튼 (슬롯 전체)
            RegBtn(slot.transform, $"slot_{i}", "",
                   Vector2.zero, new Vector2(380, 70),
                   new Color(0f,0f,0f,0.01f), new Color(1f,1f,1f,0.08f),
                   () => RemovePrepAllyAt(cap), false);
        }

        // ── 하단 버튼 ────────────────────────────────────────────────
        RegBtn(pp, "prep_back",  "  돌아가기",
               new Vector2(-220, -295), new Vector2(190, 48), C_BACK,     C_BACK_H,    ClosePrep,  false);
        RegBtn(pp, "prep_reset", "초기화",
               new Vector2(   0, -295), new Vector2(150, 48), C_LOCKED,   C_LOCKED_H,  ResetPrep,  false);
        RegBtn(pp, "prep_start", "▶  출전",
               new Vector2( 220, -295), new Vector2(190, 48), C_STAGE[1], C_STAGE_H[1],ConfirmPrep,false);

        prepPanel.SetActive(false);

        // ── 상세 팝업 빌드 (PrepPanel의 자식 → 항상 최상위 렌더) ────
        BuildDetailPopup(pp);
    }

    void BuildDetailPopup(Transform parent)
    {
        // 팝업 크기 300×460
        detailPopup = new GameObject("DetailPopup");
        detailPopup.transform.SetParent(parent, false);
        detailPopup.AddComponent<Image>().color = new Color(0,0,0,0);
        SR(detailPopup.GetComponent<RectTransform>(), new Vector2(215f, 10f), new Vector2(300f, 460f));

        Transform dp = detailPopup.transform;

        // 배경
        MkImgChild(dp, new Color(0.04f,0.06f,0.12f,0.98f), Vector2.zero, new Vector2(300,460));
        MkImgChild(dp, new Color(1f,1f,1f,0.18f),           Vector2.zero, new Vector2(304,464));

        // ── 초상화 (top: 230→130, 크기 100×100) ─────────────────────
        var pgo = new GameObject("Portrait");
        pgo.transform.SetParent(dp, false);
        detailPortraitImg = pgo.AddComponent<Image>();
        SR(pgo.GetComponent<RectTransform>(), new Vector2(0f, 155f), new Vector2(100f, 100f));

        // ── 이름 / 역할 ──────────────────────────────────────────────
        detailNameTxt = MkTxtR(dp, "", C_TITLE,
            new Vector2(0f, 88f), new Vector2(270f, 36f), 24);
        detailRoleTxt = MkTxtR(dp, "", new Color(0.75f,0.85f,1f,0.80f),
            new Vector2(0f, 60f), new Vector2(270f, 26f), 15);

        // 구분선
        MkImgChild(dp, new Color(1f,1f,1f,0.18f), new Vector2(0f, 42f), new Vector2(268f, 2f));

        // ── 스탯 ─────────────────────────────────────────────────────
        detailStatTxt = MkTxtR(dp, "", new Color(0.88f,0.95f,1f),
            new Vector2(0f, 22f), new Vector2(270f, 28f), 15);

        // 구분선
        MkImgChild(dp, new Color(1f,1f,1f,0.10f), new Vector2(0f, 4f), new Vector2(268f, 2f));

        // ── 스킬 헤더 ─────────────────────────────────────────────────
        MkTxtChild(dp, "스킬", new Color(1f,0.85f,0.2f,0.75f),
                   new Vector2(0f, -14f), new Vector2(268f, 22f), 13);

        // ── 스킬 아이콘 (중앙, 80×80) ────────────────────────────────
        var iconGo = new GameObject("SkillIcon");
        iconGo.transform.SetParent(dp, false);
        detailSkillIconImg = iconGo.AddComponent<Image>();
        SR(iconGo.GetComponent<RectTransform>(), new Vector2(0f, -75f), new Vector2(80f, 80f));
        MkImgChild(dp, new Color(1f,1f,1f,0.20f), new Vector2(0f, -75f), new Vector2(84f, 84f));

        // ── 호버 힌트 (아이콘 왼쪽, 창 안) ──────────────────────────
        MkTxtChild(dp, "커서를 올리면\n설명 표시", new Color(1f,1f,1f,0.30f),
                   new Vector2(-100f, -75f), new Vector2(90f, 40f), 12);

        // ── 스킬 이름 (아이콘 바로 밑) ───────────────────────────────
        detailSkillNameTxt = MkTxtR(dp, "", new Color(1f,0.92f,0.50f),
            new Vector2(0f, -128f), new Vector2(268f, 28f), 17);

        // ── 닫기 힌트 (창 맨 밑 중앙) ────────────────────────────────
        MkTxtChild(dp, "우클릭 / 좌클릭으로 닫기", new Color(1f,1f,1f,0.22f),
                   new Vector2(0f, -210f), new Vector2(270f, 22f), 12);

        // ── 스킬 히트박스 (아이콘 영역) ──────────────────────────────
        var btnGo = new GameObject("SkillHitbox");
        btnGo.transform.SetParent(dp, false);
        btnGo.AddComponent<Image>().color = new Color(0,0,0,0);
        detailSkillBtnRt = btnGo.GetComponent<RectTransform>();
        SR(detailSkillBtnRt, new Vector2(0f, -75f), new Vector2(84f, 84f));

        // ── 설명 오버레이 (호버 시 아이콘 아래에 표시) ───────────────
        detailSkillDescOverlay = new GameObject("SkillDescOverlay");
        detailSkillDescOverlay.transform.SetParent(dp, false);
        detailSkillDescOverlay.AddComponent<Image>().color = new Color(0,0,0,0);
        SR(detailSkillDescOverlay.GetComponent<RectTransform>(), new Vector2(0f, -165f), new Vector2(268f, 70f));

        Transform ov = detailSkillDescOverlay.transform;
        MkImgChild(ov, new Color(0.02f,0.04f,0.10f,0.96f), Vector2.zero, new Vector2(268, 70));
        MkImgChild(ov, new Color(1f,0.85f,0.2f,0.30f),     Vector2.zero, new Vector2(272, 74));
        detailSkillDescTxt = MkTxtR(ov, "", new Color(0.88f,0.92f,0.98f),
            Vector2.zero, new Vector2(250f, 64f), 15);
        detailSkillDescTxt.lineSpacing = 1.3f;

        detailSkillDescOverlay.SetActive(false);
        detailPopup.SetActive(false);
    }

    // ── 준비 패널 로직 ────────────────────────────────────────────────
    void OpenPrep(int si)
    {
        prepStageIndex = si;
        prepPanel.SetActive(true);
        var cfg = StageManager.GetStageConfig(si);
        int midCount = Mathf.Min(cfg.allySlots, cfg.startWaveAllyCount + 1);
        int lateCount = Mathf.Min(cfg.allySlots, cfg.startWaveAllyCount + 2);
        prepTitleTxt.text = $"전투 준비 — STAGE {si}  {cfg.stageName}";
        int maxPickable = Mathf.Min(cfg.allySlots, AVAIL_TYPES.Length);
        prepSlotTxt.text  = $"편성 {maxPickable}명 선택  |  1~5웨이브 {cfg.startWaveAllyCount}명 / 6~10웨이브 {midCount}명 / 11~15웨이브 {lateCount}명";
        ResetPrep();
        RefreshPrep();
    }

    void ClosePrep()
    {
        if (detailPopup != null) detailPopup.SetActive(false);
        prepPanel.SetActive(false);
        prepStageIndex = -1;
    }

    void ResetPrep()
    {
        if (prepStageIndex <= 0) return;
        prepSelected.Clear();
        System.Array.Clear(availSelectedCount, 0, availSelectedCount.Length);
        RefreshPrep();
    }

    void SelectPrepAlly(int idx)
    {
        if (prepStageIndex <= 0 || idx < 0 || idx >= AVAIL_TYPES.Length) return;
        if (prepSelected.Count >= StageManager.GetStageConfig(prepStageIndex).allySlots) return;
        if (availSelectedCount[idx] > 0) return;
        prepSelected.Add(AVAIL_TYPES[idx]);
        availSelectedCount[idx]++;
        RefreshPrep();
    }

    void RemovePrepAllyAt(int slotIdx)
    {
        if (prepStageIndex <= 0 || slotIdx < 0 || slotIdx >= prepSelected.Count) return;
        AllyType removed = prepSelected[slotIdx];
        int typeIdx = System.Array.IndexOf(AVAIL_TYPES, removed);
        if (typeIdx >= 0) availSelectedCount[typeIdx] = Mathf.Max(0, availSelectedCount[typeIdx] - 1);
        prepSelected.RemoveAt(slotIdx);
        RefreshPrep();
    }

    void RefreshPrep()
    {
        int slots = prepStageIndex > 0 ? StageManager.GetStageConfig(prepStageIndex).allySlots : 0;

        // ── 우측: 선택된 슬롯 ────────────────────────────────────────
        for (int i = 0; i < prepSlotLabels.Count; i++)
        {
            bool active = i < slots;
            prepSlotItems[i].SetActive(active);
            if (!active) continue;
            if (i < prepSelected.Count)
            {
                prepSlotPortraits[i].sprite = AllyVisualGenerator.CreatePortraitSprite(prepSelected[i]);
                prepSlotPortraits[i].color  = Color.white;
                prepSlotLabels[i].text      = $"{i+1}. {AllyLabel(prepSelected[i])}";
                prepSlotLabels[i].color     = Color.white;
            }
            else
            {
                prepSlotPortraits[i].sprite = null;
                prepSlotPortraits[i].color  = new Color(1,1,1,0);
                prepSlotLabels[i].text      = $"{i+1}. 비어 있음";
                prepSlotLabels[i].color     = new Color(1,1,1,0.42f);
            }
        }

        // ── 좌측: 선택 가능 아군 (이미 선택된 타입은 어둡게) ──
        for (int i = 0; i < prepAvailItems.Count; i++)
        {
            bool active = i < AVAIL_TYPES.Length;
            prepAvailItems[i].SetActive(active);
            if (!active) continue;

            AllyType ally   = AVAIL_TYPES[i];
            bool dimmed     = availSelectedCount[i] > 0;

            prepAvailPortraits[i].sprite = AllyVisualGenerator.CreatePortraitSprite(ally);
            prepAvailPortraits[i].color  = dimmed
                ? new Color(0.28f, 0.28f, 0.28f, 1f)
                : Color.white;

            // 우클릭 타겟 타입 고정
            if (i < availRightClickTargets.Count)
                availRightClickTargets[i] = (availRightClickTargets[i].rt, ally);
        }
    }

    void ConfirmPrep()
    {
        if (prepStageIndex <= 0) return;
        int slots = StageManager.GetStageConfig(prepStageIndex).allySlots;
        if (prepSelected.Count != slots)
        {
            prepSlotTxt.text = $"아군을 {slots}명 모두 선택하세요  ({prepSelected.Count}/{slots}명)";
            return;
        }
        StageManager.Instance?.SetSelectedAlliesForStage(prepSelected, prepStageIndex);
        if (StageManager.Instance != null) StageManager.Instance.LoadStage(prepStageIndex);
        else SceneManager.LoadScene("MediaProject", LoadSceneMode.Single);
    }

    string AllyLabel(AllyType t) => t switch
    { AllyType.Warrior=>"전사", AllyType.Archer=>"궁수",
      AllyType.Mage=>"마법사", AllyType.Cleric=>"성직자",
      AllyType.Rogue=>"도적", AllyType.Paladin=>"성기사", _=>"아군" };

    void OnLockedStage(int si)
    {
        bool prevUnlocked = StageManager.IsStageUnlocked(si - 1);
        string msg = prevUnlocked
            ? $"STAGE {si - 1}을 별 1개 이상으로 클리어하면 해금됩니다."
            : $"STAGE {si - 1}을 먼저 클리어해야 합니다.";
        ShowToast(msg, new Color(1f, 0.7f, 0.3f));
    }

    void BuildToastUI(Transform parent)
    {
        toastGo = new GameObject("Toast");
        toastGo.transform.SetParent(parent, false);
        var bg = toastGo.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.10f, 0.92f);
        var rt = toastGo.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0, -270);
        rt.sizeDelta = new Vector2(440, 48);

        var tgo = new GameObject("Txt");
        tgo.transform.SetParent(toastGo.transform, false);
        toastTxt = tgo.AddComponent<Text>();
        toastTxt.fontSize  = 19;
        toastTxt.fontStyle = FontStyle.Bold;
        toastTxt.alignment = TextAnchor.MiddleCenter;
        toastTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var tRt = tgo.GetComponent<RectTransform>();
        tRt.anchoredPosition = Vector2.zero;
        tRt.sizeDelta        = new Vector2(430, 44);
        toastGo.SetActive(false);
    }

    void ShowToast(string message, Color color)
    {
        if (toastGo == null) return;
        if (toastCoroutine != null) StopCoroutine(toastCoroutine);
        toastCoroutine = ToastCoroutine(message, color);
        StartCoroutine(toastCoroutine);
    }

    System.Collections.IEnumerator ToastCoroutine(string message, Color baseColor)
    {
        if (toastTxt == null || toastGo == null) yield break;
        toastTxt.text  = message;
        toastTxt.color = baseColor;
        toastGo.SetActive(true);

        float elapsed   = 0f;
        float duration  = 2.2f;
        float fadeStart = 1.4f;

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

    void OnBack()        => SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);

    // ── 버튼 등록 ─────────────────────────────────────────────────────
    void RegBtn(Transform p, string id, string label,
                Vector2 pos, Vector2 size,
                Color n, Color h, System.Action cb, bool inScroll)
    {
        var outer = new GameObject("Btn_" + id);
        outer.transform.SetParent(p, false);
        outer.AddComponent<Image>().color = new Color(1,1,1,0.12f);
        SR(outer.GetComponent<RectTransform>(), pos, size + new Vector2(3,3));

        var inner = new GameObject("Fill");
        inner.transform.SetParent(outer.transform, false);
        var fi = inner.AddComponent<Image>(); fi.color = n;
        SR(inner.GetComponent<RectTransform>(), Vector2.zero, size);

        if (!string.IsNullOrEmpty(label))
            MkTxt(inner.transform, label, Color.white, Vector2.zero, size, 21);

        btns.Add(new BtnData { id=id, rt=inner.GetComponent<RectTransform>(), fill=fi,
                               n=n, h=h, cb=cb, inScrollArea=inScroll });
    }

    // ── UI 헬퍼 (모두 SR() 사용 → anchorMin=Max=pivot=(0.5,0.5)) ────
    void MkImg(Transform p, Color c, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Img");
        go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = c;
        SR(go.GetComponent<RectTransform>(), pos, size);
    }

    // 자식 오브젝트용 (anchoredPosition이 부모 카드 내 로컬 좌표)
    void MkImgChild(Transform p, Color c, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Img");
        go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = c;
        SR(go.GetComponent<RectTransform>(), pos, size);
    }

    void MkTxt(Transform p, string s, Color c, Vector2 pos, Vector2 size, int fs)
    {
        var go = new GameObject("Txt");
        go.transform.SetParent(p, false);
        var tx = go.AddComponent<Text>();
        tx.text = s; tx.color = c; tx.fontSize = fs;
        tx.alignment = TextAnchor.MiddleCenter;
        tx.fontStyle = FontStyle.Bold;
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        SR(go.GetComponent<RectTransform>(), pos, size);
    }

    void MkTxtChild(Transform p, string s, Color c, Vector2 pos, Vector2 size, int fs)
    {
        var go = new GameObject("Txt");
        go.transform.SetParent(p, false);
        var tx = go.AddComponent<Text>();
        tx.text = s; tx.color = c; tx.fontSize = fs;
        tx.alignment = TextAnchor.MiddleCenter;
        tx.fontStyle = FontStyle.Bold;
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        SR(go.GetComponent<RectTransform>(), pos, size);
    }

    Text MkTxtR(Transform p, string s, Color c, Vector2 pos, Vector2 size, int fs)
    {
        var go = new GameObject("Txt");
        go.transform.SetParent(p, false);
        var tx = go.AddComponent<Text>();
        tx.text = s; tx.color = c; tx.fontSize = fs;
        tx.alignment = TextAnchor.MiddleCenter;
        tx.fontStyle = FontStyle.Bold;
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        SR(go.GetComponent<RectTransform>(), pos, size);
        return tx;
    }

    // ── 스프라이트 유틸 ───────────────────────────────────────────────
    Sprite MakeCircSprite(int res)
    {
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float c = res * 0.5f;
        for (int x = 0; x < res; x++)
        for (int y = 0; y < res; y++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - d / c)));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), Vector2.one * 0.5f, res);
    }
}
