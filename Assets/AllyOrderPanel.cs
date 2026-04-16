using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// 경로 그리기 단계에서 화면 왼쪽에 표시되는 아군 출전 순서 패널.
/// 초상화를 위아래로 드래그하면, 절반을 넘는 순간 상대 카드가 부드럽게 자리를 비켜줌.
/// Start 버튼을 누르면 이 순서대로 아군이 자동 출전함.
/// </summary>
public class AllyOrderPanel : MonoBehaviour
{
    // ── 레이아웃 상수 ────────────────────────────────────────────────────
    const float CARD_W     = 155f;
    const float CARD_H     = 90f;
    const float CARD_GAP   = 6f;
    const float LEFT_OFF   = 5f;
    const float INNER_PAD  = 10f;
    const float DETAIL_W   = 146f;
    const float TOGGLE_W   = 24f;
    const float TOGGLE_H   = 64f;
    const float TOGGLE_GAP = 4f;
    const float PANEL_X    = LEFT_OFF + TOGGLE_W + TOGGLE_GAP;
    const float LERP_SPEED = 16f;   // 밀려나는 카드의 이동 속도
    const float CLICK_DRAG_THRESHOLD = 8f;
    const float PANEL_EXTRA_H = 30f + 16f + 20f;

    // ── 출전 순서 데이터 ─────────────────────────────────────────────────
    private List<AllyType> allyOrder = new List<AllyType>
    {
        AllyType.Warrior, AllyType.Archer, AllyType.Mage, AllyType.Cleric
    };

    // ── UI 참조 ──────────────────────────────────────────────────────────
    private Canvas           parentCanvas;
    private List<GameObject> cardGos = new List<GameObject>();
    private float[]          slotY;          // 각 슬롯의 기준 Y 위치 (고정)
    private RectTransform    panelBgRt;
    private RectTransform    contentRootRt;
    private RectTransform    detailPanelRt;
    private RectTransform    toggleBtnRt;
    private Text             toggleBtnLabel;
    private Text             detailNameText;
    private Text             detailHpText;
    private Text             detailSpeedText;
    private Image            detailSkillIconImg;
    private Text             detailSkillNameText;
    private Text             detailSkillLockText;
    private RectTransform    detailSkillBtnRt;
    private GameObject       detailSkillDescOverlay;
    private Text             detailSkillDescText;
    private bool             isCollapsed;
    private bool             detailExpanded;
    private int              selectedCard = -1;
    private float            panelHeight;
    private bool             skillDescShown;

    // ── 카드↔슬롯 매핑 (드래그 중 실시간 갱신) ──────────────────────────
    private int[]   cardToSlot;   // cardToSlot[cardIdx] = 현재 논리 슬롯
    private int[]   slotToCard;   // slotToCard[slot]    = 해당 슬롯의 카드 인덱스
    private float[] cardTargetY;  // 각 카드의 목표 Y (Lerp 대상)
    private Text[]  numLabels;    // 순서 번호 텍스트 참조 (슬롯 바뀔 때 갱신)

    // ── 드래그 상태 ──────────────────────────────────────────────────────
    private int           draggingCard = -1;   // 드래그 중인 카드 인덱스
    private int           draggingSlot = -1;   // 드래그 카드의 현재 논리 슬롯
    private RectTransform draggingRt;
    private Vector2       dragStartScrn;       // 드래그 시작 마우스 위치 (스크린)
    private float         dragStartY;          // 드래그 시작 카드 Y (캔버스)
    private int           pressedCard = -1;
    private bool          dragMoved;

    // ── 색상 ─────────────────────────────────────────────────────────────
    static readonly Color COL_PANEL   = new Color(0.05f, 0.06f, 0.12f, 0.90f);
    static readonly Color COL_BORDER  = new Color(1.0f,  0.85f, 0.20f, 0.35f);
    static readonly Color COL_WARRIOR = new Color(0.15f, 0.25f, 0.55f, 0.93f);
    static readonly Color COL_ARCHER  = new Color(0.12f, 0.42f, 0.18f, 0.93f);
    static readonly Color COL_MAGE    = new Color(0.12f, 0.18f, 0.62f, 0.93f);
    static readonly Color COL_CLERIC  = new Color(0.62f, 0.58f, 0.28f, 0.93f);
    static readonly Color COL_TITLE   = new Color(1.0f,  0.85f, 0.20f, 1.0f);
    static readonly Color COL_DRAG    = new Color(1.0f,  0.92f, 0.30f, 0.95f);
    static readonly Color COL_TOGGLE  = new Color(0.07f, 0.09f, 0.16f, 0.96f);
    static readonly Color COL_TOGGLE_HOVER = new Color(0.16f, 0.20f, 0.32f, 0.98f);

    // ── 캐릭터 데이터 ────────────────────────────────────────────────────
    static string GetAllyName(AllyType t) => t switch
    {
        AllyType.Warrior => "전사",
        AllyType.Archer  => "궁수",
        AllyType.Mage    => "마법사",
        AllyType.Cleric  => "성직자",
        _                => "?"
    };

    static string[] GetSkills(AllyType t) => t switch
    {
        AllyType.Warrior => new[] { "검술 돌파  (HP 180)", "중속 전진  (속도 3.2)" },
        AllyType.Archer  => new[] { "원거리 지원 (HP 140)", "유연 기동  (속도 3.8)" },
        AllyType.Mage    => new[] { "마법 공세  (HP 110)", "쾌속 이동  (속도 4.2)" },
        AllyType.Cleric  => new[] { "신성 방어  (HP 200)", "안정 전진  (속도 2.8)" },
        _                => new[] { "-", "-" }
    };

    static Color GetCardColor(AllyType t) => t switch
    {
        AllyType.Warrior => COL_WARRIOR,
        AllyType.Archer  => COL_ARCHER,
        AllyType.Mage    => COL_MAGE,
        AllyType.Cleric  => COL_CLERIC,
        _                => Color.gray
    };

    static float GetHp(AllyType t) => t switch
    {
        AllyType.Warrior => 180f,
        AllyType.Archer  => 140f,
        AllyType.Mage    => 110f,
        AllyType.Cleric  => 200f,
        _                => 0f
    };

    static float GetSpeed(AllyType t) => t switch
    {
        AllyType.Warrior => 3.2f,
        AllyType.Archer  => 3.8f,
        AllyType.Mage    => 4.2f,
        AllyType.Cleric  => 2.8f,
        _                => 0f
    };

    static string GetSkillName(AllyType t) => t switch
    {
        AllyType.Warrior => "방패 밀치기",
        AllyType.Archer  => "산탄 화살",
        AllyType.Mage    => "화염구",
        AllyType.Cleric  => "치유 기도",
        _                => "잠금 스킬"
    };

    static string GetSkillDesc(AllyType t) => t switch
    {
        AllyType.Warrior => "전방의 적을 강하게 밀쳐내어\n이동 경로를 확보합니다.",
        AllyType.Archer  => "전방 부채꼴 범위에\n화살을 동시에 발사합니다.",
        AllyType.Mage    => "착탄 지점 주변에\n광역 마법 피해를 줍니다.",
        AllyType.Cleric  => "주변 아군을 일정 시간마다\n회복시키는 지원 스킬입니다.",
        _                => "아직 잠겨 있는 스킬입니다."
    };

    static Color GetSkillColor(AllyType t) => t switch
    {
        AllyType.Warrior => new Color(0.85f, 0.25f, 0.20f),
        AllyType.Archer  => new Color(0.25f, 0.75f, 0.30f),
        AllyType.Mage    => new Color(0.95f, 0.45f, 0.10f),
        AllyType.Cleric  => new Color(0.90f, 0.85f, 0.30f),
        _                => new Color(0.55f, 0.55f, 0.55f)
    };

    // ── 공개 API ─────────────────────────────────────────────────────────
    public List<AllyType> GetAllyOrder() => new List<AllyType>(allyOrder);

    public bool IsDragging => draggingCard >= 0;

    public bool IsMouseOverPanel(Vector2 screenPos)
    {
        if (toggleBtnRt != null &&
            RectTransformUtility.RectangleContainsScreenPoint(toggleBtnRt, screenPos, null))
            return true;

        if (panelBgRt != null && panelBgRt.gameObject.activeSelf &&
            RectTransformUtility.RectangleContainsScreenPoint(panelBgRt, screenPos, null))
            return true;

        foreach (var go in cardGos)
        {
            if (go == null || !go.activeInHierarchy) continue;
            var rt = go.GetComponent<RectTransform>();
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null))
                return true;
        }
        return false;
    }

    // ── 초기화 ───────────────────────────────────────────────────────────
    public void Initialize(Canvas canvas, List<AllyType> initialOrder = null)
    {
        parentCanvas = canvas;
        int stageIndex = StageManager.Instance != null ? StageManager.Instance.currentStageIndex : 1;
        allyOrder = StageManager.NormalizeSelectedAllies(initialOrder, stageIndex);

        int   n      = allyOrder.Count;
        float totalH = n * CARD_H + (n - 1) * CARD_GAP;

        slotY = new float[n];
        for (int i = 0; i < n; i++)
            slotY[i] = totalH / 2f - i * (CARD_H + CARD_GAP) - CARD_H / 2f;

        BuildBackground(totalH);
        InitCards();
        ApplyCollapsedState();
    }

    // ── 패널 배경 생성 ────────────────────────────────────────────────────
    void BuildBackground(float totalH)
    {
        float panelW = CARD_W + INNER_PAD * 2;
        float panelH = totalH + PANEL_EXTRA_H;
        panelHeight = panelH;

        var bg   = MakeUIRect("AllyPanelBg", transform);
        panelBgRt = bg.GetComponent<RectTransform>();
        panelBgRt.anchorMin        = new Vector2(0f, 0.5f);
        panelBgRt.anchorMax        = new Vector2(0f, 0.5f);
        panelBgRt.pivot            = new Vector2(0f, 0.5f);
        panelBgRt.anchoredPosition = new Vector2(PANEL_X, 0f);
        panelBgRt.sizeDelta        = new Vector2(panelW, panelH);
        bg.AddComponent<Image>().color = COL_PANEL;

        // 좌측 강조선
        var border = MakeUIRect("Border", bg.transform);
        var bRt    = border.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0f, 0f); bRt.anchorMax = new Vector2(0f, 1f);
        bRt.pivot     = new Vector2(0f, 0.5f);
        bRt.offsetMin = new Vector2(0f, 0f); bRt.offsetMax = new Vector2(3f, 0f);
        border.AddComponent<Image>().color = COL_BORDER;

        // 제목
        var title = MakeUIRect("Title", bg.transform);
        var tRt   = title.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f); tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot     = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -3f); tRt.sizeDelta = new Vector2(0f, 26f);
        var tTx = title.AddComponent<Text>();
        tTx.text = "[ 출전 순서 ]"; tTx.fontSize = 13; tTx.fontStyle = FontStyle.Bold;
        tTx.font = BuiltinFont(); tTx.color = COL_TITLE; tTx.alignment = TextAnchor.MiddleCenter;

        // 힌트
        var hint = MakeUIRect("Hint", bg.transform);
        var hRt  = hint.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0f, 0f); hRt.anchorMax = new Vector2(1f, 0f);
        hRt.pivot     = new Vector2(0.5f, 0f);
        hRt.anchoredPosition = new Vector2(0f, 4f); hRt.sizeDelta = new Vector2(0f, 14f);
        var hTx = hint.AddComponent<Text>();
        hTx.text = "드래그로 순서 변경"; hTx.fontSize = 9;
        hTx.font = BuiltinFont(); hTx.color = new Color(0.6f, 0.6f, 0.6f, 0.75f);
        hTx.alignment = TextAnchor.MiddleCenter;

        var contentRoot = MakeUIRect("CardRoot", transform);
        contentRootRt = contentRoot.GetComponent<RectTransform>();
        contentRootRt.anchorMin        = new Vector2(0f, 0.5f);
        contentRootRt.anchorMax        = new Vector2(0f, 0.5f);
        contentRootRt.pivot            = new Vector2(0f, 0.5f);
        contentRootRt.anchoredPosition = Vector2.zero;
        contentRootRt.sizeDelta        = Vector2.zero;

        BuildDetailPanel(bg.transform);
        BuildToggleButton();
        RefreshPanelLayout();
    }

    void BuildDetailPanel(Transform parent)
    {
        var detail = MakeUIRect("DetailPanel", parent);
        detailPanelRt = detail.GetComponent<RectTransform>();
        detailPanelRt.anchorMin = new Vector2(1f, 0f);
        detailPanelRt.anchorMax = new Vector2(1f, 1f);
        detailPanelRt.pivot = new Vector2(1f, 0.5f);
        detailPanelRt.offsetMin = new Vector2(-INNER_PAD - DETAIL_W, 18f);
        detailPanelRt.offsetMax = new Vector2(-INNER_PAD, -26f);
        detail.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

        MakeLabel("DetailTitle", detail.transform, "선택 정보", 12, FontStyle.Bold,
            COL_TITLE, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.87f), new Vector2(1f, 0.98f),
            new Vector2(6f, 0f), new Vector2(-6f, 0f));

        detailNameText = CreateTextLabel("DetailName", detail.transform, string.Empty, 18, FontStyle.Bold,
            Color.white, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.62f), new Vector2(1f, 0.84f),
            new Vector2(8f, 0f), new Vector2(-8f, 0f));

        detailHpText = CreateTextLabel("DetailHp", detail.transform, string.Empty, 14, FontStyle.Normal,
            new Color(0.92f, 0.94f, 0.98f, 1f), TextAnchor.MiddleLeft,
            new Vector2(0f, 0.38f), new Vector2(1f, 0.56f),
            new Vector2(12f, 0f), new Vector2(-12f, 0f));

        detailSpeedText = CreateTextLabel("DetailSpeed", detail.transform, string.Empty, 14, FontStyle.Normal,
            new Color(0.92f, 0.94f, 0.98f, 1f), TextAnchor.MiddleLeft,
            new Vector2(0f, 0.20f), new Vector2(1f, 0.38f),
            new Vector2(12f, 0f), new Vector2(-12f, 0f));

        MakeLabel("SkillTitle", detail.transform, "스킬", 12, FontStyle.Bold,
            COL_TITLE, TextAnchor.MiddleLeft,
            new Vector2(0f, 0.10f), new Vector2(1f, 0.20f),
            new Vector2(12f, 0f), new Vector2(-12f, 0f));

        var skillBtn = MakeUIRect("SkillButton", detail.transform);
        detailSkillBtnRt = skillBtn.GetComponent<RectTransform>();
        detailSkillBtnRt.anchorMin = new Vector2(0f, 0.01f);
        detailSkillBtnRt.anchorMax = new Vector2(1f, 0.14f);
        detailSkillBtnRt.offsetMin = new Vector2(10f, 0f);
        detailSkillBtnRt.offsetMax = new Vector2(-10f, 0f);
        skillBtn.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.18f, 0.92f);

        var iconGo = MakeUIRect("SkillIcon", skillBtn.transform);
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(10f, 0f);
        iconRt.sizeDelta = new Vector2(38f, 38f);
        detailSkillIconImg = iconGo.AddComponent<Image>();

        var lockGo = MakeUIRect("SkillLock", skillBtn.transform);
        var lockRt = lockGo.GetComponent<RectTransform>();
        lockRt.anchorMin = new Vector2(0f, 0.5f);
        lockRt.anchorMax = new Vector2(0f, 0.5f);
        lockRt.pivot = new Vector2(0f, 0.5f);
        lockRt.anchoredPosition = new Vector2(18f, 0f);
        lockRt.sizeDelta = new Vector2(38f, 38f);
        detailSkillLockText = lockGo.AddComponent<Text>();
        detailSkillLockText.font = BuiltinFont();
        detailSkillLockText.fontSize = 20;
        detailSkillLockText.fontStyle = FontStyle.Bold;
        detailSkillLockText.color = Color.white;
        detailSkillLockText.alignment = TextAnchor.MiddleCenter;
        detailSkillLockText.text = "🔒";

        detailSkillNameText = CreateTextLabel("SkillName", skillBtn.transform, string.Empty, 12, FontStyle.Bold,
            new Color(1f, 0.92f, 0.50f, 1f), TextAnchor.MiddleLeft,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(58f, 0f), new Vector2(-8f, 0f));

        var overlay = MakeUIRect("SkillDescOverlay", detail.transform);
        detailSkillDescOverlay = overlay;
        var ovRt = overlay.GetComponent<RectTransform>();
        ovRt.anchorMin = new Vector2(0f, 0f);
        ovRt.anchorMax = new Vector2(1f, 0f);
        ovRt.pivot = new Vector2(0.5f, 0f);
        ovRt.anchoredPosition = new Vector2(0f, -78f);
        ovRt.sizeDelta = new Vector2(-16f, 80f);
        overlay.AddComponent<Image>().color = new Color(0.02f, 0.04f, 0.10f, 0.97f);

        detailSkillDescText = CreateTextLabel("SkillDesc", overlay.transform, string.Empty, 11, FontStyle.Normal,
            new Color(0.88f, 0.92f, 0.98f, 1f), TextAnchor.MiddleLeft,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(10f, 6f), new Vector2(-10f, -6f));
        detailSkillDescOverlay.SetActive(false);
    }

    void BuildToggleButton()
    {
        var toggle = MakeUIRect("ToggleButton", transform);
        toggleBtnRt = toggle.GetComponent<RectTransform>();
        toggleBtnRt.anchorMin        = new Vector2(0f, 0.5f);
        toggleBtnRt.anchorMax        = new Vector2(0f, 0.5f);
        toggleBtnRt.pivot            = new Vector2(0f, 0.5f);
        toggleBtnRt.anchoredPosition = new Vector2(LEFT_OFF, 0f);
        toggleBtnRt.sizeDelta        = new Vector2(TOGGLE_W, TOGGLE_H);
        toggle.AddComponent<Image>().color = COL_TOGGLE;

        var label = MakeUIRect("Label", toggle.transform);
        var labelRt = label.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        toggleBtnLabel = label.AddComponent<Text>();
        toggleBtnLabel.font = BuiltinFont();
        toggleBtnLabel.fontSize = 20;
        toggleBtnLabel.fontStyle = FontStyle.Bold;
        toggleBtnLabel.color = Color.white;
        toggleBtnLabel.alignment = TextAnchor.MiddleCenter;
    }

    // ── 카드 초기 생성 (한 번만) ─────────────────────────────────────────
    void InitCards()
    {
        int n = allyOrder.Count;
        cardGos     = new List<GameObject>(n);
        cardToSlot  = new int[n];
        slotToCard  = new int[n];
        cardTargetY = new float[n];
        numLabels   = new Text[n];

        for (int i = 0; i < n; i++)
        {
            cardGos.Add(CreateCard(i));
            cardToSlot[i]  = i;
            slotToCard[i]  = i;
            cardTargetY[i] = slotY[i];
        }

        RefreshCardHighlights();
        RefreshDetailPanel();
    }

    // ── 카드 1개 생성 ─────────────────────────────────────────────────────
    GameObject CreateCard(int cardIdx)
    {
        AllyType type = allyOrder[cardIdx];

        var card   = MakeUIRect($"AllyCard_{cardIdx}", contentRootRt);
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin        = new Vector2(0f, 0.5f);
        cardRt.anchorMax        = new Vector2(0f, 0.5f);
        cardRt.pivot            = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = new Vector2(PANEL_X + INNER_PAD + CARD_W * 0.5f, slotY[cardIdx]);
        cardRt.sizeDelta        = new Vector2(CARD_W, CARD_H);
        card.AddComponent<Image>().color = GetCardColor(type);

        // 좌측 강조선
        var stripe = MakeUIRect("Stripe", card.transform);
        var sRt    = stripe.GetComponent<RectTransform>();
        sRt.anchorMin = Vector2.zero; sRt.anchorMax = new Vector2(0f, 1f);
        sRt.pivot     = new Vector2(0f, 0.5f);
        sRt.offsetMin = new Vector2(0f, 0f); sRt.offsetMax = new Vector2(4f, 0f);
        stripe.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.25f);

        // 순서 번호 (참조 저장해 두어 슬롯 바뀔 때 갱신)
        var numGo = MakeUIRect("Num", card.transform);
        var numRt = numGo.GetComponent<RectTransform>();
        numRt.anchorMin = new Vector2(0f, 0.72f); numRt.anchorMax = new Vector2(0.3f, 1f);
        numRt.offsetMin = new Vector2(6f, -2f);   numRt.offsetMax = new Vector2(0f, -2f);
        var numTx = numGo.AddComponent<Text>();
        numTx.text = $"{cardIdx + 1}"; numTx.fontSize = 17; numTx.fontStyle = FontStyle.Bold;
        numTx.font = BuiltinFont(); numTx.color = new Color(1f, 1f, 1f, 0.45f);
        numTx.alignment = TextAnchor.UpperLeft;
        numLabels[cardIdx] = numTx;

        // 초상화
        var portGo = MakeUIRect("Portrait", card.transform);
        var portRt = portGo.GetComponent<RectTransform>();
        portRt.anchorMin = new Vector2(0.03f, 0.33f); portRt.anchorMax = new Vector2(0.58f, 0.98f);
        portRt.offsetMin = new Vector2(2f, 2f);        portRt.offsetMax = new Vector2(-1f, -1f);
        var portImg = portGo.AddComponent<Image>();
        portImg.sprite = GetAllyPortraitSprite(type);
        portImg.type   = Image.Type.Simple;
        portImg.preserveAspect = true;

        // 이름
        MakeLabel("Name", card.transform, GetAllyName(type), 13, FontStyle.Bold,
            Color.white, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.25f), new Vector2(1f, 0.40f),
            new Vector2(4f, 0f),    new Vector2(-4f, 0f));

        // 스킬 2개
        var skills = GetSkills(type);
        float[] yMins = { 0.15f, 0.01f };
        float[] yMaxs = { 0.26f, 0.12f };
        for (int k = 0; k < 2; k++)
            MakeLabel($"Skill{k}", card.transform, $"• {skills[k]}", 9, FontStyle.Normal,
                new Color(0.87f, 0.87f, 0.87f, 1f), TextAnchor.MiddleLeft,
                new Vector2(0.03f, yMins[k]), new Vector2(0.97f, yMaxs[k]),
                Vector2.zero, Vector2.zero);

        return card;
    }

    // ── 메인 루프 ────────────────────────────────────────────────────────
    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.ShouldBlockGameplayInput())
            return;

        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 mp = mouse.position.ReadValue();

        UpdateToggleVisual(mp);

        if (mouse.leftButton.wasPressedThisFrame && IsOverToggleButton(mp))
        {
            ToggleCollapsed();
            return;
        }

        HandleDetailSkillHover(mp);

        if (!isCollapsed && mouse.leftButton.wasPressedThisFrame)
            TryBeginDrag(mp);

        if (draggingCard >= 0)
        {
            if (mouse.leftButton.isPressed) UpdateDrag(mp);
            else                            EndDrag();
        }

        // 드래그되지 않는 카드들을 목표 위치로 부드럽게 이동
        LerpNonDraggedCards();
    }

    void HandleDetailSkillHover(Vector2 screenPos)
    {
        if (detailSkillBtnRt == null || detailSkillDescOverlay == null || !detailExpanded || isCollapsed)
        {
            if (detailSkillDescOverlay != null) detailSkillDescOverlay.SetActive(false);
            skillDescShown = false;
            return;
        }

        bool show = detailPanelRt != null && detailPanelRt.gameObject.activeSelf &&
                    RectTransformUtility.RectangleContainsScreenPoint(detailSkillBtnRt, screenPos, null);
        if (show != skillDescShown)
        {
            skillDescShown = show;
            detailSkillDescOverlay.SetActive(show);
        }
    }

    void UpdateToggleVisual(Vector2 mp)
    {
        if (toggleBtnRt == null) return;

        var img = toggleBtnRt.GetComponent<Image>();
        if (img != null)
            img.color = IsOverToggleButton(mp) ? COL_TOGGLE_HOVER : COL_TOGGLE;

        if (toggleBtnLabel != null)
            toggleBtnLabel.text = isCollapsed ? "▶" : "◀";
    }

    bool IsOverToggleButton(Vector2 mp) =>
        toggleBtnRt != null &&
        RectTransformUtility.RectangleContainsScreenPoint(toggleBtnRt, mp, null);

    void ToggleCollapsed()
    {
        if (draggingCard >= 0)
            EndDrag();

        isCollapsed = !isCollapsed;
        ApplyCollapsedState();
    }

    void ApplyCollapsedState()
    {
        if (panelBgRt != null)
            panelBgRt.gameObject.SetActive(!isCollapsed);

        if (contentRootRt != null)
            contentRootRt.gameObject.SetActive(!isCollapsed);

        if (detailPanelRt != null)
            detailPanelRt.gameObject.SetActive(detailExpanded && !isCollapsed);

        if (toggleBtnLabel != null)
            toggleBtnLabel.text = isCollapsed ? "▶" : "◀";

        RefreshPanelLayout();
    }

    // ── 드래그 시작 ──────────────────────────────────────────────────────
    void TryBeginDrag(Vector2 mp)
    {
        for (int c = 0; c < cardGos.Count; c++)
        {
            if (cardGos[c] == null) continue;
            var rt = cardGos[c].GetComponent<RectTransform>();
            if (!RectTransformUtility.RectangleContainsScreenPoint(rt, mp, null)) continue;

            pressedCard = c;
            dragMoved = false;
            draggingCard  = c;
            draggingSlot  = cardToSlot[c];
            draggingRt    = rt;
            dragStartScrn = mp;
            dragStartY    = rt.anchoredPosition.y;
            cardGos[c].transform.SetAsLastSibling();   // 드래그 카드를 최상단으로
            break;
        }
    }

    // ── 드래그 중 ────────────────────────────────────────────────────────
    void UpdateDrag(Vector2 mp)
    {
        if (draggingRt == null) return;

        // 마우스 이동량을 캔버스 단위로 변환해 Y 위치 갱신
        float dy   = (mp.y - dragStartScrn.y) / parentCanvas.scaleFactor;
        float newY = dragStartY + dy;

        // 패널 범위 안으로 클램프
        newY = Mathf.Clamp(newY,
            slotY[slotY.Length - 1] - CARD_H * 0.6f,
            slotY[0]                + CARD_H * 0.6f);

        if (Mathf.Abs(newY - dragStartY) > CLICK_DRAG_THRESHOLD)
            dragMoved = true;

        draggingRt.anchoredPosition = new Vector2(draggingRt.anchoredPosition.x, newY);
        draggingRt.GetComponent<Image>().color = COL_DRAG;

        // ── 절반 지점을 넘으면 상대 카드를 즉시 밀어냄 ──
        if (draggingSlot > 0)
        {
            float mid = (slotY[draggingSlot] + slotY[draggingSlot - 1]) * 0.5f;
            if (newY > mid)
                PerformLiveSwap(draggingSlot - 1);
        }
        if (draggingSlot < allyOrder.Count - 1)
        {
            float mid = (slotY[draggingSlot] + slotY[draggingSlot + 1]) * 0.5f;
            if (newY < mid)
                PerformLiveSwap(draggingSlot + 1);
        }
    }

    // ── 실시간 슬롯 교체: 상대 카드가 자리를 비켜줌 ─────────────────────
    void PerformLiveSwap(int targetSlot)
    {
        int neighborCard = slotToCard[targetSlot];
        int curSlot      = draggingSlot;

        // 이웃 카드 → 드래그 카드의 원래 슬롯으로 Lerp 이동
        cardTargetY[neighborCard] = slotY[curSlot];
        cardToSlot[neighborCard]  = curSlot;
        slotToCard[curSlot]       = neighborCard;

        // 드래그 카드의 논리 슬롯 갱신
        cardToSlot[draggingCard] = targetSlot;
        slotToCard[targetSlot]   = draggingCard;
        draggingSlot             = targetSlot;

        // allyOrder 갱신
        (allyOrder[curSlot], allyOrder[targetSlot]) = (allyOrder[targetSlot], allyOrder[curSlot]);

        // 순서 번호 텍스트 갱신
        RefreshNumLabels();
        RefreshCardHighlights();
        RefreshDetailPanel();
    }

    // ── 드래그 종료 ──────────────────────────────────────────────────────
    void EndDrag()
    {
        if (draggingCard < 0) return;

        // 드래그 카드도 현재 논리 슬롯으로 부드럽게 복귀
        cardTargetY[draggingCard] = slotY[draggingSlot];

        // 색상 복원
        var img = cardGos[draggingCard]?.GetComponent<Image>();
        if (img != null) img.color = GetCardColor(allyOrder[draggingSlot]);

        if (!dragMoved && pressedCard >= 0)
        {
            if (selectedCard == pressedCard && detailExpanded)
            {
                detailExpanded = false;
            }
            else
            {
                selectedCard = pressedCard;
                detailExpanded = true;
            }
            RefreshDetailPanel();
        }

        draggingCard = -1;
        draggingSlot = -1;
        draggingRt   = null;
        pressedCard = -1;
        RefreshCardHighlights();
    }

    // ── 비드래그 카드 Lerp 이동 (드래그 카드 포함, 해제 후) ──────────────
    void LerpNonDraggedCards()
    {
        for (int c = 0; c < cardGos.Count; c++)
        {
            if (c == draggingCard || cardGos[c] == null) continue;

            var   rt  = cardGos[c].GetComponent<RectTransform>();
            float cur = rt.anchoredPosition.y;
            float tgt = cardTargetY[c];

            if (Mathf.Abs(cur - tgt) < 0.15f)
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, tgt);
            else
                rt.anchoredPosition = new Vector2(
                    rt.anchoredPosition.x,
                    Mathf.Lerp(cur, tgt, Time.deltaTime * LERP_SPEED));
        }
    }

    // ── 순서 번호 텍스트 갱신 ────────────────────────────────────────────
    void RefreshNumLabels()
    {
        for (int c = 0; c < numLabels.Length; c++)
            if (numLabels[c] != null)
                numLabels[c].text = $"{cardToSlot[c] + 1}";
    }

    void RefreshCardHighlights()
    {
        for (int c = 0; c < cardGos.Count; c++)
        {
            if (cardGos[c] == null) continue;
            var image = cardGos[c].GetComponent<Image>();
            if (image == null) continue;

            AllyType type = allyOrder[cardToSlot[c]];
            Color baseColor = GetCardColor(type);
            image.color = c == selectedCard
                ? Color.Lerp(baseColor, Color.white, 0.18f)
                : baseColor;
        }
    }

    void RefreshDetailPanel()
    {
        if (selectedCard < 0 || selectedCard >= cardToSlot.Length)
        {
            if (detailPanelRt != null)
                detailPanelRt.gameObject.SetActive(false);
            if (detailSkillDescOverlay != null)
                detailSkillDescOverlay.SetActive(false);
            RefreshPanelLayout();
            return;
        }

        AllyType type = allyOrder[cardToSlot[selectedCard]];
        if (detailNameText != null) detailNameText.text = GetAllyName(type);
        if (detailHpText != null) detailHpText.text = $"HP  {GetHp(type):0}";
        if (detailSpeedText != null) detailSpeedText.text = $"속도  {GetSpeed(type):0.0}";
        if (detailSkillIconImg != null) detailSkillIconImg.color = GetSkillColor(type);
        if (detailSkillNameText != null) detailSkillNameText.text = $"{GetSkillName(type)}  [잠금]";
        if (detailSkillDescText != null) detailSkillDescText.text = GetSkillDesc(type);
        if (detailSkillLockText != null) detailSkillLockText.text = "🔒";
        skillDescShown = false;
        if (detailSkillDescOverlay != null) detailSkillDescOverlay.SetActive(false);
        if (detailPanelRt != null)
            detailPanelRt.gameObject.SetActive(detailExpanded && !isCollapsed);
        RefreshPanelLayout();
    }

    void RefreshPanelLayout()
    {
        if (panelBgRt != null)
        {
            float width = detailExpanded
                ? CARD_W + DETAIL_W + INNER_PAD * 3
                : CARD_W + INNER_PAD * 2;
            panelBgRt.sizeDelta = new Vector2(width, panelHeight);
        }

        if (detailPanelRt != null)
            detailPanelRt.gameObject.SetActive(detailExpanded && !isCollapsed);
    }

    // ── UI 헬퍼 ──────────────────────────────────────────────────────────
    GameObject MakeUIRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    void MakeLabel(string name, Transform parent,
                   string text, int fontSize, FontStyle style,
                   Color color, TextAnchor alignment,
                   Vector2 anchorMin, Vector2 anchorMax,
                   Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = MakeUIRect(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        var tx = go.AddComponent<Text>();
        tx.text = text; tx.font = BuiltinFont(); tx.fontSize = fontSize;
        tx.fontStyle = style; tx.color = color; tx.alignment = alignment;
    }

    Text CreateTextLabel(string name, Transform parent,
                         string text, int fontSize, FontStyle style,
                         Color color, TextAnchor alignment,
                         Vector2 anchorMin, Vector2 anchorMax,
                         Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = MakeUIRect(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        var tx = go.AddComponent<Text>();
        tx.text = text; tx.font = BuiltinFont(); tx.fontSize = fontSize;
        tx.fontStyle = style; tx.color = color; tx.alignment = alignment;
        return tx;
    }

    Font BuiltinFont() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    Sprite GetAllyPortraitSprite(AllyType t) =>
        AllyVisualGenerator.CreatePortraitSprite(t);
}
