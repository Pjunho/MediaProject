using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 화면 우측에 표시되는 스킬 HUD.
/// - 잠금  : 회색 원 오버레이 + "LOCK"
/// - 해금+준비 : 풀컬러 아이콘
/// - 쿨다운 중 : 회색 Radial360 (fillClockwise=false, Origin=Top → 12시→시계방향으로 복원)
///              + 중앙 남은 초 숫자
/// - 쿨다운 완료 : 황금빛 3회 펄싱
/// </summary>
public class SkillCooldownHUD : MonoBehaviour
{
    public static SkillCooldownHUD Instance { get; private set; }

    // ── 레이아웃 상수 ─────────────────────────────────────────────────────
    const float ICON  = 62f;
    const float GAP   = 8f;
    const float PAD   = 10f;
    const float KEY_H = 15f;

    // ── 슬롯 정보 ────────────────────────────────────────────────────────
    class SlotUI
    {
        public Image    icon;
        public Image    radial;
        public Image    lockOverlay;
        public Text     cdTxt;
        public Text     keyTxt;
        public Image    glowImg;
        public AllyType allyType;
        public bool     wasOnCd;
    }

    readonly List<SlotUI> slots = new();

    // ── 라이프사이클 ──────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 빌드 ─────────────────────────────────────────────────────────────

    /// <summary>아군 순서가 확정될 때 GameManager가 호출. 슬롯을 재구성한다.</summary>
    public void Build(List<AllyType> order)
    {
        if (order == null || order.Count == 0) return;

        // 기존 자식 전부 제거
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
        slots.Clear();

        int   n      = order.Count;
        float slotH  = ICON + KEY_H + 4f;
        float panelH = PAD * 2f + 14f + GAP + n * slotH + (n - 1) * GAP;
        float panelW = PAD * 2f + ICON;

        // 패널 자신의 RectTransform 크기 재설정
        var selfRt = (RectTransform)transform;
        selfRt.sizeDelta = new Vector2(panelW, panelH);

        // ── 배경 ─────────────────────────────────────────────────────────
        CreateImg("Bg", transform, Vector2.zero, Vector2.one, Color.clear)
            .GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.10f, 0.90f);

        // 배경 이미지 교체 (둥근 사각형)
        var bgImg = transform.Find("Bg").GetComponent<Image>();
        bgImg.sprite = RoundedRectSprite(8);
        bgImg.type   = Image.Type.Sliced;

        // ── 금색 테두리 ───────────────────────────────────────────────────
        var borderImg = CreateImg("Border", transform, Vector2.zero, Vector2.one,
                                  new Color(1f, 0.85f, 0.22f, 0.22f)).GetComponent<Image>();
        borderImg.sprite = RoundedRectSprite(8);
        borderImg.type   = Image.Type.Sliced;

        // ── 제목 ──────────────────────────────────────────────────────────
        var titleGo = MkRect("Title", transform);
        var titleRt = (RectTransform)titleGo.transform;
        titleRt.anchorMin        = new Vector2(0f, 1f);
        titleRt.anchorMax        = new Vector2(1f, 1f);
        titleRt.pivot            = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -PAD);
        titleRt.sizeDelta        = new Vector2(0f, 14f);
        var titleTx = titleGo.AddComponent<Text>();
        titleTx.font      = BFont();
        titleTx.fontSize  = 10;
        titleTx.fontStyle = FontStyle.Bold;
        titleTx.alignment = TextAnchor.MiddleCenter;
        titleTx.color     = new Color(1f, 0.85f, 0.22f, 0.90f);
        titleTx.text      = "SKILL";

        // ── 슬롯 ─────────────────────────────────────────────────────────
        for (int i = 0; i < n; i++)
        {
            float yFromTop = -(PAD + 14f + GAP + i * (slotH + GAP) + slotH * 0.5f);
            slots.Add(BuildSlot(order[i], i + 1, yFromTop, slotH));
        }
    }

    // ── 개별 슬롯 ────────────────────────────────────────────────────────

    SlotUI BuildSlot(AllyType type, int keyNum, float yFromTop, float slotH)
    {
        // 슬롯 루트
        var root = MkRect($"Slot{keyNum}", transform);
        var rootRt = (RectTransform)root.transform;
        rootRt.anchorMin        = new Vector2(0.5f, 1f);
        rootRt.anchorMax        = new Vector2(0.5f, 1f);
        rootRt.pivot            = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = new Vector2(0f, yFromTop);
        rootRt.sizeDelta        = new Vector2(ICON, slotH);

        // ── 아이콘 영역 (상단 ICON × ICON) ───────────────────────────────
        var area = MkRect("IconArea", root.transform);
        var areaRt = (RectTransform)area.transform;
        areaRt.anchorMin        = new Vector2(0f, 1f);
        areaRt.anchorMax        = new Vector2(1f, 1f);
        areaRt.pivot            = new Vector2(0.5f, 1f);
        areaRt.anchoredPosition = Vector2.zero;
        areaRt.sizeDelta        = new Vector2(0f, ICON);

        // 원형 배경
        var bgImg = CreateImg("Bg", area.transform, Vector2.zero, Vector2.one,
                              new Color(0.08f, 0.10f, 0.18f, 1f)).GetComponent<Image>();
        bgImg.sprite = CircleSprite(32);

        // 스킬 아이콘
        var iconGo = MkRect("Icon", area.transform);
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin = new Vector2(0.10f, 0.10f);
        iconRt.anchorMax = new Vector2(0.90f, 0.90f);
        iconRt.offsetMin = Vector2.zero;
        iconRt.offsetMax = Vector2.zero;
        var iconImg = iconGo.AddComponent<Image>();
        iconImg.sprite         = LoadIcon(type);
        iconImg.preserveAspect = true;

        // ── 쿨다운 Radial360 오버레이 ─────────────────────────────────────
        // fillClockwise=false + Origin=Top
        // → fillAmount 감소 시 12시(Top)부터 시계방향으로 아이콘 색이 복원됨
        var radImg = CreateImg("Radial", area.transform, Vector2.zero, Vector2.one,
                               new Color(0f, 0f, 0f, 0.78f)).GetComponent<Image>();
        radImg.sprite        = CircleSprite(128);
        radImg.type          = Image.Type.Filled;
        radImg.fillMethod    = Image.FillMethod.Radial360;
        radImg.fillOrigin    = (int)Image.Origin360.Top;
        radImg.fillClockwise = false;
        radImg.fillAmount    = 0f;
        radImg.gameObject.SetActive(false);

        // 쿨다운 초 텍스트
        var cdGo = MkRect("CdTxt", area.transform);
        var cdRt = (RectTransform)cdGo.transform;
        cdRt.anchorMin = Vector2.zero; cdRt.anchorMax = Vector2.one;
        cdRt.offsetMin = Vector2.zero; cdRt.offsetMax = Vector2.zero;
        var cdTx = cdGo.AddComponent<Text>();
        cdTx.font      = BFont();
        cdTx.fontSize  = 20;
        cdTx.fontStyle = FontStyle.Bold;
        cdTx.color     = Color.white;
        cdTx.alignment = TextAnchor.MiddleCenter;
        cdGo.SetActive(false);

        // ── 잠금 오버레이 ──────────────────────────────────────────────────
        var lockImg = CreateImg("Lock", area.transform, Vector2.zero, Vector2.one,
                                new Color(0f, 0f, 0f, 0.65f)).GetComponent<Image>();
        lockImg.sprite = CircleSprite(32);
        // LOCK 텍스트
        var lockTxtGo = MkRect("LockTxt", lockImg.transform);
        var ltRt = (RectTransform)lockTxtGo.transform;
        ltRt.anchorMin = Vector2.zero; ltRt.anchorMax = Vector2.one;
        ltRt.offsetMin = Vector2.zero; ltRt.offsetMax = Vector2.zero;
        var lockTx = lockTxtGo.AddComponent<Text>();
        lockTx.font      = BFont();
        lockTx.fontSize  = 12;
        lockTx.fontStyle = FontStyle.Bold;
        lockTx.color     = new Color(1f, 1f, 1f, 0.60f);
        lockTx.alignment = TextAnchor.MiddleCenter;
        lockTx.text      = "LOCK";
        lockImg.gameObject.SetActive(false);

        // ── 완료 반짝임 ────────────────────────────────────────────────────
        var glowGo = MkRect("Glow", area.transform);
        var glowRt = (RectTransform)glowGo.transform;
        glowRt.anchorMin = new Vector2(-0.18f, -0.18f);
        glowRt.anchorMax = new Vector2(1.18f,  1.18f);
        glowRt.offsetMin = Vector2.zero; glowRt.offsetMax = Vector2.zero;
        var glowImg = glowGo.AddComponent<Image>();
        glowImg.sprite = CircleSprite(96);
        glowImg.color  = Color.clear;
        glowGo.SetActive(false);

        // ── 키 힌트 "[1]" ─────────────────────────────────────────────────
        var keyGo = MkRect("Key", root.transform);
        var keyRt = (RectTransform)keyGo.transform;
        keyRt.anchorMin        = new Vector2(0f, 0f);
        keyRt.anchorMax        = new Vector2(1f, 0f);
        keyRt.pivot            = new Vector2(0.5f, 0f);
        keyRt.anchoredPosition = Vector2.zero;
        keyRt.sizeDelta        = new Vector2(0f, KEY_H + 4f);
        var keyTx = keyGo.AddComponent<Text>();
        keyTx.font      = BFont();
        keyTx.fontSize  = 10;
        keyTx.fontStyle = FontStyle.Bold;
        keyTx.color     = new Color(1f, 1f, 1f, 0.60f);
        keyTx.alignment = TextAnchor.MiddleCenter;
        keyTx.text      = AllyTypeToKorean(type);

        return new SlotUI
        {
            icon        = iconImg,
            radial      = radImg,
            lockOverlay = lockImg,
            cdTxt       = cdTx,
            keyTxt      = keyTx,
            glowImg     = glowImg,
            allyType    = type,
            wasOnCd     = false
        };
    }

    // ── 업데이트 ──────────────────────────────────────────────────────────

    void Update()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var ui   = slots[i];
            bool  unlocked  = SkillSystem.IsUnlocked(ui.allyType);
            float remaining = SkillSystem.GetCooldownRemaining(ui.allyType);
            float total     = SkillSystem.GetCooldownTotal(ui.allyType);
            bool  onCd      = remaining > 0.05f;

            // 잠금 오버레이
            ui.lockOverlay.gameObject.SetActive(!unlocked);

            // 쿨다운 Radial
            ui.radial.gameObject.SetActive(unlocked && onCd);
            if (unlocked && onCd && total > 0f)
                ui.radial.fillAmount = remaining / total;

            // 남은 초 텍스트
            ui.cdTxt.gameObject.SetActive(unlocked && onCd);
            if (unlocked && onCd)
                ui.cdTxt.text = Mathf.CeilToInt(remaining).ToString();

            // 쿨다운 완료 감지
            if (ui.wasOnCd && !onCd && unlocked)
                StartCoroutine(ReadyFlash(ui.glowImg));

            ui.wasOnCd = onCd;
        }
    }

    // ── 반짝임 ───────────────────────────────────────────────────────────

    IEnumerator ReadyFlash(Image img)
    {
        if (img == null) yield break;
        var rt = (RectTransform)img.transform;
        img.gameObject.SetActive(true);
        for (int p = 0; p < 3; p++)
        {
            float t = 0f;
            while (t < 0.22f)
            {
                t  += Time.unscaledDeltaTime;
                float s = Mathf.Sin(t / 0.22f * Mathf.PI);
                img.color     = new Color(1f, 0.92f, 0.25f, s * 0.90f);
                rt.localScale = Vector3.one * (1f + s * 0.20f);
                yield return null;
            }
        }
        img.color     = Color.clear;
        rt.localScale = Vector3.one;
        img.gameObject.SetActive(false);
    }

    // ── 유틸리티 ──────────────────────────────────────────────────────────

    /// <summary>RectTransform을 가진 빈 GameObject를 생성하고 parent에 붙인다.</summary>
    static GameObject MkRect(string name, Transform parent)
    {
        // typeof(RectTransform)을 생성자에 전달 → Transform 없이 RectTransform으로 시작
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>Full-stretch Image를 생성한다.</summary>
    static GameObject CreateImg(string name, Transform parent,
                                Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var go = MkRect(name, parent);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    static string AllyTypeToKorean(AllyType type) => type switch
    {
        AllyType.Warrior => "전사",
        AllyType.Archer  => "궁수",
        AllyType.Mage    => "마법사",
        AllyType.Cleric  => "성직자",
        AllyType.Rogue   => "도적",
        AllyType.Paladin => "성기사",
        _                => type.ToString()
    };

    static Sprite LoadIcon(AllyType type)
    {
        Sprite spr = SkillSystem.GetIconSprite(type);
        return spr != null ? spr : AllyVisualGenerator.CreatePortraitSprite(type);
    }

    static Font BFont() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    static Sprite CircleSprite(int size)
    {
        var pixels = new Color[size * size];
        float r = size * 0.5f - 0.5f, c = size * 0.5f - 0.5f;
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float dx = x - c, dy = y - c;
            float a  = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.8f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels(pixels); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static Sprite RoundedRectSprite(int cornerR)
    {
        int sz = cornerR * 2 + 4;
        var pixels = new Color[sz * sz];
        float cx = (sz - 1) * 0.5f, cy = (sz - 1) * 0.5f;
        for (int x = 0; x < sz; x++)
        for (int y = 0; y < sz; y++)
        {
            float ax = Mathf.Max(Mathf.Abs(x - cx) - (cx - cornerR), 0f);
            float ay = Mathf.Max(Mathf.Abs(y - cy) - (cy - cornerR), 0f);
            float a  = Mathf.Clamp01(cornerR - Mathf.Sqrt(ax * ax + ay * ay) + 0.5f);
            pixels[y * sz + x] = new Color(1f, 1f, 1f, a);
        }
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels(pixels); tex.Apply();
        int b = cornerR;
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz,
                             0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }
}
