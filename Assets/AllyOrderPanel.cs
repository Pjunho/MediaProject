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
    const float DETAIL_W   = 220f;
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
    private RectTransform    detailHpUpgradeBtnRt;
    private RectTransform    detailSpeedUpgradeBtnRt;
    private Text             detailHpUpgradeTxt;
    private Text             detailSpeedUpgradeTxt;
    private UpgradeTierButtonIcon detailHpUpgradeGraphic;
    private UpgradeTierButtonIcon detailSpeedUpgradeGraphic;
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
    enum PressAction { None, Toggle, Skill, HpUpgrade, SpeedUpgrade }
    private PressAction   pressedAction = PressAction.None;

    // ── 색상 ─────────────────────────────────────────────────────────────
    static readonly Color COL_PANEL   = new Color(0.05f, 0.06f, 0.12f, 0.90f);
    static readonly Color COL_BORDER  = new Color(1.0f,  0.85f, 0.20f, 0.35f);
    static readonly Color COL_WARRIOR = new Color(0.15f, 0.25f, 0.55f, 0.93f);
    static readonly Color COL_ARCHER  = new Color(0.12f, 0.42f, 0.18f, 0.93f);
    static readonly Color COL_MAGE    = new Color(0.12f, 0.18f, 0.62f, 0.93f);
    static readonly Color COL_CLERIC  = new Color(0.62f, 0.58f, 0.28f, 0.93f);
    static readonly Color COL_ROGUE   = new Color(0.32f, 0.10f, 0.48f, 0.93f);  // 보라 (nja1 닌자)
    static readonly Color COL_PALADIN = new Color(0.50f, 0.12f, 0.12f, 0.93f);  // 진홍 (knt1 기사)
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
        AllyType.Rogue   => "도적",
        AllyType.Paladin => "성기사",
        _                => "?"
    };

    static Color GetCardColor(AllyType t) => t switch
    {
        AllyType.Warrior => COL_WARRIOR,
        AllyType.Archer  => COL_ARCHER,
        AllyType.Mage    => COL_MAGE,
        AllyType.Cleric  => COL_CLERIC,
        AllyType.Rogue   => COL_ROGUE,
        AllyType.Paladin => COL_PALADIN,
        _                => Color.gray
    };

    static float GetHp(AllyType t) => t switch
    {
        AllyType.Warrior => 180f,
        AllyType.Archer  => 140f,
        AllyType.Mage    => 110f,
        AllyType.Cleric  => 200f,
        AllyType.Rogue   =>  90f,
        AllyType.Paladin => 280f,
        _                => 0f
    };

    static float GetSpeed(AllyType t) => t switch
    {
        AllyType.Warrior => 1.92f,
        AllyType.Archer  => 2.28f,
        AllyType.Mage    => 2.52f,
        AllyType.Cleric  => 1.68f,
        AllyType.Rogue   => 2.88f,
        AllyType.Paladin => 1.08f,
        _                => 0f
    };

    static string GetSkillName(AllyType t) => SkillSystem.GetSkillForAlly(t).skillName;

    static string GetSkillDesc(AllyType t)
    {
        var skill = SkillSystem.GetSkillForAlly(t);
        return skill.description;
    }

    static Color GetSkillColor(AllyType t) => t switch
    {
        AllyType.Warrior => new Color(0.85f, 0.25f, 0.20f),
        AllyType.Archer  => new Color(0.25f, 0.75f, 0.30f),
        AllyType.Mage    => new Color(0.95f, 0.45f, 0.10f),
        AllyType.Cleric  => new Color(0.90f, 0.85f, 0.30f),
        AllyType.Rogue   => new Color(0.60f, 0.20f, 0.80f),
        AllyType.Paladin => new Color(0.90f, 0.75f, 0.10f),
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
        tTx.text = "[ 출전 순서 ]"; tTx.fontSize = 13; tTx.fontStyle = FontStyle.Normal;
        tTx.font = BuiltinFont(); tTx.color = COL_TITLE; tTx.alignment = TextAnchor.MiddleCenter;

        // 힌트
        var hint = MakeUIRect("Hint", bg.transform);
        var hRt  = hint.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0f, 0f); hRt.anchorMax = new Vector2(1f, 0f);
        hRt.pivot     = new Vector2(0.5f, 0f);
        hRt.anchoredPosition = new Vector2(0f, 4f); hRt.sizeDelta = new Vector2(0f, 14f);
        var hTx = hint.AddComponent<Text>();
        hTx.text = "드래그 변경 | 1~6 스킬"; hTx.fontSize = 9;
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

        MakeLabel("DetailTitle", detail.transform, "선택 정보", 12, FontStyle.Normal,
            COL_TITLE, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.87f), new Vector2(1f, 0.98f),
            new Vector2(6f, 0f), new Vector2(-6f, 0f));

        detailNameText = CreateTextLabel("DetailName", detail.transform, string.Empty, 18, FontStyle.Normal,
            Color.white, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.62f), new Vector2(1f, 0.84f),
            new Vector2(8f, 0f), new Vector2(-8f, 0f));

        detailHpText = CreateTextLabel("DetailHp", detail.transform, string.Empty, 14, FontStyle.Normal,
            new Color(0.92f, 0.94f, 0.98f, 1f), TextAnchor.MiddleLeft,
            new Vector2(0f, 0.38f), new Vector2(0.62f, 0.56f),
            new Vector2(12f, 0f), new Vector2(0f, 0f));

        detailHpUpgradeBtnRt = BuildUpgradeBtn("HpUpgradeBtn", detail.transform,
            new Vector2(0.62f, 0.38f), new Vector2(1f, 0.56f),
            out detailHpUpgradeTxt, out detailHpUpgradeGraphic);

        detailSpeedText = CreateTextLabel("DetailSpeed", detail.transform, string.Empty, 14, FontStyle.Normal,
            new Color(0.92f, 0.94f, 0.98f, 1f), TextAnchor.MiddleLeft,
            new Vector2(0f, 0.20f), new Vector2(0.62f, 0.38f),
            new Vector2(12f, 0f), new Vector2(0f, 0f));

        detailSpeedUpgradeBtnRt = BuildUpgradeBtn("SpeedUpgradeBtn", detail.transform,
            new Vector2(0.62f, 0.20f), new Vector2(1f, 0.38f),
            out detailSpeedUpgradeTxt, out detailSpeedUpgradeGraphic);

        MakeLabel("SkillTitle", detail.transform, "스킬", 12, FontStyle.Normal,
            COL_TITLE, TextAnchor.MiddleLeft,
            new Vector2(0f, 0.10f), new Vector2(1f, 0.20f),
            new Vector2(12f, 0f), new Vector2(-12f, 0f));

        var skillBtn = MakeUIRect("SkillButton", detail.transform);
        detailSkillBtnRt = skillBtn.GetComponent<RectTransform>();
        detailSkillBtnRt.anchorMin = new Vector2(0f, 0.01f);
        detailSkillBtnRt.anchorMax = new Vector2(1f, 0.18f);
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
        detailSkillLockText.fontStyle = FontStyle.Normal;
        detailSkillLockText.color = Color.white;
        detailSkillLockText.alignment = TextAnchor.MiddleCenter;
        detailSkillLockText.text = "🔒";

        detailSkillNameText = CreateTextLabel("SkillName", skillBtn.transform, string.Empty, 12, FontStyle.Normal,
            new Color(1f, 0.92f, 0.50f, 1f), TextAnchor.MiddleLeft,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(58f, 0f), new Vector2(-8f, 0f));
        detailSkillNameText.horizontalOverflow = HorizontalWrapMode.Wrap;

        var overlay = MakeUIRect("SkillDescOverlay", detail.transform);
        detailSkillDescOverlay = overlay;
        var ovRt = overlay.GetComponent<RectTransform>();
        ovRt.anchorMin = new Vector2(0f, 0f);
        ovRt.anchorMax = new Vector2(1f, 0f);
        ovRt.pivot = new Vector2(0.5f, 0f);
        ovRt.anchoredPosition = new Vector2(0f, -108f);
        ovRt.sizeDelta = new Vector2(-16f, 110f);
        overlay.AddComponent<Image>().color = new Color(0.02f, 0.04f, 0.10f, 0.97f);

        detailSkillDescText = CreateTextLabel("SkillDesc", overlay.transform, string.Empty, 12, FontStyle.Normal,
            new Color(0.88f, 0.92f, 0.98f, 1f), TextAnchor.MiddleLeft,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(12f, 8f), new Vector2(-12f, -8f));
        detailSkillDescText.horizontalOverflow = HorizontalWrapMode.Wrap;
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
        toggleBtnLabel.fontStyle = FontStyle.Normal;
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
        numTx.text = $"{cardIdx + 1}"; numTx.fontSize = 17; numTx.fontStyle = FontStyle.Normal;
        numTx.font = BuiltinFont(); numTx.color = new Color(1f, 1f, 1f, 0.45f);
        numTx.alignment = TextAnchor.UpperLeft;
        numLabels[cardIdx] = numTx;

        // 초상화 — 캐릭터를 딱 담을 정도의 정사각 영역(카드 좌측 40%)
        var portGo = MakeUIRect("Portrait", card.transform);
        var portRt = portGo.GetComponent<RectTransform>();
        portRt.anchorMin = new Vector2(0.04f, 0.05f); portRt.anchorMax = new Vector2(0.44f, 0.95f);
        portRt.offsetMin = new Vector2(2f, 2f);        portRt.offsetMax = new Vector2(-2f, -2f);
        var portImg = portGo.AddComponent<Image>();
        portImg.sprite = GetAllyPortraitSprite(type);
        portImg.type   = Image.Type.Simple;
        portImg.preserveAspect = true;

        // 이름 — 오른쪽 빈 공간에 크게
        MakeLabel("Name", card.transform, GetAllyName(type), 17, FontStyle.Bold,
            Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.45f, 0.05f), new Vector2(0.98f, 0.95f),
            new Vector2(0f, 0f),       new Vector2(-4f, 0f));

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
            pressedAction = PressAction.Toggle;
            return;
        }

        if (mouse.leftButton.wasReleasedThisFrame && pressedAction == PressAction.Toggle)
        {
            if (IsOverToggleButton(mp))
                ToggleCollapsed();
            pressedAction = PressAction.None;
            return;
        }

        HandleDetailSkillHover(mp);

        if (!isCollapsed && mouse.leftButton.wasPressedThisFrame)
        {
            pressedAction = GetPressActionAt(mp);
            if (pressedAction != PressAction.None)
                return;

            TryBeginDrag(mp);
        }

        if (!isCollapsed && mouse.leftButton.wasReleasedThisFrame && pressedAction != PressAction.None)
        {
            var releaseAction = GetPressActionAt(mp);
            if (releaseAction == pressedAction)
                ExecutePressAction(releaseAction, mp);
            pressedAction = PressAction.None;
            return;
        }

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

    bool TryHandleSkillUnlock(Vector2 screenPos)
    {
        if (detailSkillBtnRt == null || !detailExpanded || isCollapsed) return false;
        if (selectedCard < 0 || selectedCard >= cardToSlot.Length) return false;
        if (!RectTransformUtility.RectangleContainsScreenPoint(detailSkillBtnRt, screenPos, null))
            return false;

        AllyType type = allyOrder[cardToSlot[selectedCard]];
        if (SkillSystem.IsUnlocked(type))
        {
            SkillSystem.ActivateSkill(type);
            return true;
        }

        if (GameManager.Instance != null && GameManager.Instance.TryUnlockSkill(type))
            RefreshDetailPanel();

        return true;
    }

    PressAction GetPressActionAt(Vector2 screenPos)
    {
        if (detailExpanded && !isCollapsed)
        {
            if (detailSkillBtnRt != null &&
                RectTransformUtility.RectangleContainsScreenPoint(detailSkillBtnRt, screenPos, null))
                return PressAction.Skill;

            if (detailHpUpgradeBtnRt != null &&
                RectTransformUtility.RectangleContainsScreenPoint(detailHpUpgradeBtnRt, screenPos, null))
                return PressAction.HpUpgrade;

            if (detailSpeedUpgradeBtnRt != null &&
                RectTransformUtility.RectangleContainsScreenPoint(detailSpeedUpgradeBtnRt, screenPos, null))
                return PressAction.SpeedUpgrade;
        }

        return PressAction.None;
    }

    void ExecutePressAction(PressAction action, Vector2 screenPos)
    {
        switch (action)
        {
            case PressAction.Skill:
                TryHandleSkillUnlock(screenPos);
                break;
            case PressAction.HpUpgrade:
            case PressAction.SpeedUpgrade:
                TryHandleUpgrade(screenPos);
                break;
        }
    }

    bool TryHandleUpgrade(Vector2 screenPos)
    {
        if (!detailExpanded || isCollapsed) return false;
        if (selectedCard < 0 || selectedCard >= cardToSlot.Length) return false;

        AllyType type = allyOrder[cardToSlot[selectedCard]];
        bool handled = false;

        if (detailHpUpgradeBtnRt != null &&
            RectTransformUtility.RectangleContainsScreenPoint(detailHpUpgradeBtnRt, screenPos, null))
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.TryUpgrade(type, UpgradeSystem.StatType.Hp))
                RefreshDetailPanel();
            handled = true;
        }
        else if (detailSpeedUpgradeBtnRt != null &&
            RectTransformUtility.RectangleContainsScreenPoint(detailSpeedUpgradeBtnRt, screenPos, null))
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.TryUpgrade(type, UpgradeSystem.StatType.Speed))
                RefreshDetailPanel();
            handled = true;
        }

        return handled;
    }

    void RefreshUpgradeButtons(AllyType type)
    {
        RefreshOneStat(type, UpgradeSystem.StatType.Hp,    detailHpUpgradeBtnRt,    detailHpUpgradeTxt,    detailHpUpgradeGraphic);
        RefreshOneStat(type, UpgradeSystem.StatType.Speed, detailSpeedUpgradeBtnRt, detailSpeedUpgradeTxt, detailSpeedUpgradeGraphic);
    }

    void RefreshOneStat(AllyType type, UpgradeSystem.StatType stat,
        RectTransform btnRt, Text btnTxt, UpgradeTierButtonIcon tierGraphic)
    {
        if (btnRt == null || btnTxt == null) return;
        int cost = UpgradeSystem.GetNextCost(type, stat);
        int level = stat == UpgradeSystem.StatType.Hp
            ? UpgradeSystem.GetHpLevel(type)
            : UpgradeSystem.GetSpeedLevel(type);

        if (tierGraphic != null)
            tierGraphic.SetLevel(level);

        if (cost < 0)
        {
            btnTxt.text  = "MAX";
            btnTxt.color = new Color(1f, 0.82f, 0.12f, 1f);
        }
        else
        {
            btnTxt.text  = $"▲ {cost}c";
            btnTxt.color = new Color(0.96f, 0.86f, 0.48f, 1f);
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

        AllyType type    = allyOrder[cardToSlot[selectedCard]];
        bool     unlocked = SkillSystem.IsUnlocked(type);

        if (detailNameText != null) detailNameText.text = GetAllyName(type);

        // 실효 스탯 계산 (업그레이드 + 보석 + 스킬 효과 반영)
        float baseHp    = GetHp(type);
        float baseSpeed = GetSpeed(type);
        float effHp     = baseHp    * UpgradeSystem.GetHpMultiplier(type) * GemInventory.GetHpMultiplier();
        float effSpeed  = baseSpeed * UpgradeSystem.GetSpeedMultiplier(type) * GemInventory.GetSpeedMultiplier();
        var  boostedCol = new Color(0.45f, 1f, 0.55f);
        var  normalCol  = new Color(0.92f, 0.94f, 0.98f, 1f);
        bool hpUp       = effHp    > baseHp    + 0.5f;
        bool spdUp      = effSpeed > baseSpeed + 0.01f;
        if (detailHpText != null)
        {
            detailHpText.text  = hpUp  ? $"HP  {effHp:0}  ↑" : $"HP  {baseHp:0}";
            detailHpText.color = hpUp  ? boostedCol : normalCol;
        }
        if (detailSpeedText != null)
        {
            detailSpeedText.text  = spdUp ? $"속도  {effSpeed:0.0}  ↑" : $"속도  {baseSpeed:0.0}";
            detailSpeedText.color = spdUp ? boostedCol : normalCol;
        }
        if (detailSkillIconImg != null)
        {
            var skillIcon = SkillSystem.GetIconSprite(type);
            detailSkillIconImg.sprite = skillIcon;
            detailSkillIconImg.color = skillIcon != null ? Color.white : GetSkillColor(type);
            detailSkillIconImg.preserveAspect = true;
        }
        var  skillData = SkillSystem.GetSkillForAlly(type);
        if (detailSkillNameText != null)
            detailSkillNameText.text = unlocked
                ? $"{GetSkillName(type)}  [발동]"
                : $"{GetSkillName(type)}  [{skillData.cost}코인]";
        if (detailSkillDescText != null) detailSkillDescText.text = GetSkillDesc(type);
        if (detailSkillLockText != null) detailSkillLockText.text = unlocked ? "🔓" : "🔒";

        RefreshUpgradeButtons(type);
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

    RectTransform BuildUpgradeBtn(string id, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, out Text label, out UpgradeTierButtonIcon tierGraphic)
    {
        var go = MakeUIRect(id, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(0f,  3f);
        rt.offsetMax = new Vector2(-6f, -3f);

        var tgo = new GameObject("Lbl", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        var tx = tgo.AddComponent<Text>();
        tx.text      = "▲ 1c";
        tx.fontSize  = 12;
        tx.fontStyle = FontStyle.Normal;
        tx.font      = BuiltinFont();
        tx.color     = new Color(0.96f, 0.86f, 0.48f, 1f);
        tx.alignment = TextAnchor.MiddleRight;
        tx.alignByGeometry = true;
        tx.raycastTarget = false;
        var trt = tgo.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = new Vector2(0f, 0f);
        trt.offsetMax = new Vector2(-46f, 0f);

        var icon = MakeUIRect("TierButton", go.transform);
        var iconRt = icon.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(1f, 0.5f);
        iconRt.anchorMax = new Vector2(1f, 0.5f);
        iconRt.pivot = new Vector2(1f, 0.5f);
        iconRt.anchoredPosition = Vector2.zero;
        iconRt.sizeDelta = new Vector2(38f, 38f);
        var iconImage = icon.AddComponent<Image>();
        iconImage.raycastTarget = true;
        tierGraphic = icon.AddComponent<UpgradeTierButtonIcon>();

        label = tx;
        return iconRt;
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
        tx.text               = text; tx.font = BuiltinFont(); tx.fontSize = fontSize;
        tx.fontStyle          = style; tx.color = color; tx.alignment = alignment;
        tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        tx.verticalOverflow   = VerticalWrapMode.Overflow;
        tx.alignByGeometry    = true;
        tx.raycastTarget      = false;
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
        tx.text               = text; tx.font = BuiltinFont(); tx.fontSize = fontSize;
        tx.fontStyle          = style; tx.color = color; tx.alignment = alignment;
        tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        tx.verticalOverflow   = VerticalWrapMode.Overflow;
        tx.alignByGeometry    = true;
        tx.raycastTarget      = false;
        return tx;
    }

    Font BuiltinFont() => UiPixelFont.Get();

    Sprite GetAllyPortraitSprite(AllyType t) =>
        AllyVisualGenerator.CreatePortraitSprite(t);
}

class UpgradeTierButtonIcon : MonoBehaviour
{
    const int Size = 48;

    static readonly Color32 EmptyColor = new Color32(72, 77, 86, 245);
    static readonly Color32 FillColor  = new Color32(255, 210, 31, 255);
    static readonly Color32 EdgeColor  = new Color32(14, 16, 22, 255);
    static readonly Color32 LineColor  = new Color32(7, 8, 11, 255);
    static readonly Color32 ShineColor = new Color32(255, 248, 150, 45);
    static readonly Sprite[] CachedSprites = new Sprite[4];

    Image img;

    public void SetLevel(int newLevel)
    {
        newLevel = Mathf.Clamp(newLevel, 0, 3);
        if (img == null)
            img = GetComponent<Image>();
        if (img != null)
            img.sprite = GetSprite(newLevel);
    }

    void Awake()
    {
        SetLevel(0);
    }

    static Sprite GetSprite(int level)
    {
        if (CachedSprites[level] != null)
            return CachedSprites[level];

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.name = $"upgrade_tier_{level}";
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[Size * Size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, 0);

        FillRect(pixels, 2, 2, Size - 3, Size - 3, EdgeColor);
        FillRect(pixels, 5, 5, Size - 6, Size - 6, EmptyColor);

        if (level >= 1) FillPolygon(pixels, Segment(0), FillColor);
        if (level >= 2) FillPolygon(pixels, Segment(1), FillColor);
        if (level >= 3) FillPolygon(pixels, Segment(2), FillColor);

        if (level > 0)
            FillRect(pixels, 7, 6, Size - 8, 8, ShineColor);

        DrawLine(pixels, Pt(0.10f, 0.34f), Pt(0.90f, 0.44f), 2, LineColor);
        DrawLine(pixels, Pt(0.10f, 0.63f), Pt(0.90f, 0.73f), 2, LineColor);
        DrawBorder(pixels);

        tex.SetPixels32(pixels);
        tex.Apply();

        CachedSprites[level] = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        return CachedSprites[level];
    }

    static Vector2Int[] Segment(int segment)
    {
        if (segment == 0)
        {
            return new[]
            {
                Pt(0.10f, 0.10f), Pt(0.90f, 0.10f),
                Pt(0.90f, 0.40f), Pt(0.10f, 0.30f)
            };
        }
        if (segment == 1)
        {
            return new[]
            {
                Pt(0.10f, 0.34f), Pt(0.90f, 0.44f),
                Pt(0.90f, 0.69f), Pt(0.10f, 0.59f)
            };
        }
        return new[]
        {
            Pt(0.10f, 0.63f), Pt(0.90f, 0.73f),
            Pt(0.90f, 0.90f), Pt(0.10f, 0.90f)
        };
    }

    static Vector2Int Pt(float x, float y)
    {
        return new Vector2Int(
            Mathf.RoundToInt(Mathf.Lerp(0, Size - 1, x)),
            Mathf.RoundToInt(Mathf.Lerp(0, Size - 1, y)));
    }

    static void FillRect(Color32[] pixels, int x0, int y0, int x1, int y1, Color32 color)
    {
        for (int y = Mathf.Max(0, y0); y <= Mathf.Min(Size - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(Size - 1, x1); x++)
                pixels[y * Size + x] = color;
    }

    static void FillPolygon(Color32[] pixels, Vector2Int[] points, Color32 color)
    {
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                if (ContainsPoint(points, x + 0.5f, y + 0.5f))
                    pixels[y * Size + x] = color;
            }
        }
    }

    static bool ContainsPoint(Vector2Int[] points, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            if (((points[i].y > y) != (points[j].y > y)) &&
                (x < (points[j].x - points[i].x) * (y - points[i].y) / (float)(points[j].y - points[i].y) + points[i].x))
                inside = !inside;
        }
        return inside;
    }

    static void DrawLine(Color32[] pixels, Vector2Int a, Vector2Int b, int thickness, Color32 color)
    {
        int steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0f : i / (float)steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t));
            FillRect(pixels, x - thickness, y - thickness, x + thickness, y + thickness, color);
        }
    }

    static void DrawBorder(Color32[] pixels)
    {
        FillRect(pixels, 2, 2, Size - 3, 4, LineColor);
        FillRect(pixels, 2, Size - 5, Size - 3, Size - 3, LineColor);
        FillRect(pixels, 2, 2, 4, Size - 3, LineColor);
        FillRect(pixels, Size - 5, 2, Size - 3, Size - 3, LineColor);
    }
}
