using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 화면 우측에 표시되는 스킬 HUD.
/// - 잠금: 회색 반투명 오버레이 + "LOCK"
/// - 해금+준비: 풀컬러 아이콘
/// - 쿨다운 중: 회색 Radial360 오버레이(12시→시계방향으로 색 복원) + 남은 초 숫자
/// - 쿨다운 완료: 황금빛 반짝임 이펙트
/// </summary>
public class SkillCooldownHUD : MonoBehaviour
{
    public static SkillCooldownHUD Instance { get; private set; }

    // ── 레이아웃 상수 ────────────────────────────────────────────────────
    const float ICON  = 64f;   // 아이콘 한 변 크기
    const float GAP   = 8f;    // 슬롯 간 여백
    const float PAD   = 10f;   // 패널 내부 여백
    const float KEY_H = 16f;   // 키 힌트 텍스트 높이

    // ── 슬롯 데이터 ─────────────────────────────────────────────────────
    class SlotUI
    {
        public Image    icon;          // 스킬 아이콘
        public Image    radial;        // 쿨다운 Radial360 오버레이
        public Image    lockOverlay;   // 잠금 오버레이
        public Text     cdTxt;         // 남은 쿨다운 초 텍스트
        public Text     keyTxt;        // "[1]" 키 힌트
        public Image    glowImg;       // 쿨다운 완료 반짝임
        public AllyType allyType;
        public bool     wasOnCd;       // 이전 프레임 쿨다운 여부 (완료 감지)
    }

    readonly List<SlotUI> slots = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
    void OnDestroy() { if (Instance == this) Instance = null; }

    // ── 초기화 (경로 확정 시 GameManager가 호출) ────────────────────────

    public void Build(List<AllyType> order)
    {
        // 기존 자식 제거
        foreach (Transform c in transform) Destroy(c.gameObject);
        slots.Clear();

        int   n      = order.Count;
        float slotH  = ICON + KEY_H + 4f;
        float panelH = PAD * 2f + 14f + GAP + n * slotH + (n - 1) * GAP;
        float panelW = PAD * 2f + ICON;

        // 자신의 RectTransform 크기 조정
        var selfRt = GetComponent<RectTransform>();
        if (selfRt != null) selfRt.sizeDelta = new Vector2(panelW, panelH);

        // 배경
        var bgGo  = Mk("Bg", transform);
        Stretch(bgGo);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color  = new Color(0.04f, 0.05f, 0.10f, 0.88f);
        bgImg.sprite = MakeRoundedRectSprite(8);
        bgImg.type   = Image.Type.Sliced;

        // 금색 테두리
        var borderGo = Mk("Border", transform);
        Stretch(borderGo);
        var borderImg    = borderGo.AddComponent<Image>();
        borderImg.sprite = MakeRoundedRectSprite(8);
        borderImg.color  = new Color(1f, 0.85f, 0.22f, 0.25f);
        borderImg.type   = Image.Type.Sliced;

        // "SKILL" 제목
        var titleGo = Mk("Title", transform);
        var titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin        = new Vector2(0f, 1f);
        titleRt.anchorMax        = new Vector2(1f, 1f);
        titleRt.pivot            = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -PAD);
        titleRt.sizeDelta        = new Vector2(0f, 14f);
        var titleTx = titleGo.AddComponent<Text>();
        titleTx.font      = BuiltinFont();
        titleTx.fontSize  = 10;
        titleTx.fontStyle = FontStyle.Bold;
        titleTx.alignment = TextAnchor.MiddleCenter;
        titleTx.color     = new Color(1f, 0.85f, 0.22f, 0.90f);
        titleTx.text      = "SKILL";

        // 각 슬롯
        for (int i = 0; i < order.Count; i++)
        {
            float yFromTop = -(PAD + 14f + GAP + i * (slotH + GAP) + slotH * 0.5f);
            slots.Add(BuildSlot(order[i], i + 1, yFromTop));
        }
    }

    // ── 슬롯 생성 ───────────────────────────────────────────────────────

    SlotUI BuildSlot(AllyType type, int keyNum, float yFromTop)
    {
        float slotH = ICON + KEY_H + 4f;

        var root = Mk($"Slot{keyNum}", transform);
        var rt   = root.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, yFromTop);
        rt.sizeDelta        = new Vector2(ICON, slotH);

        // ── 아이콘 영역 (상단 ICON × ICON) ──────────────────────────────
        var iconAreaGo = Mk("IconArea", root.transform);
        var iconAreaRt = iconAreaGo.AddComponent<RectTransform>();
        iconAreaRt.anchorMin        = new Vector2(0f, 1f);
        iconAreaRt.anchorMax        = new Vector2(1f, 1f);
        iconAreaRt.pivot            = new Vector2(0.5f, 1f);
        iconAreaRt.anchoredPosition = Vector2.zero;
        iconAreaRt.sizeDelta        = new Vector2(0f, ICON);

        // 원형 배경
        var bgGo  = Mk("Bg", iconAreaGo.transform);
        Stretch(bgGo);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = CircleSprite(32);
        bgImg.color  = new Color(0.08f, 0.10f, 0.18f, 1f);

        // 스킬 아이콘 (원 안쪽 여백 10%)
        var iconGo = Mk("Icon", iconAreaGo.transform);
        var iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.10f, 0.10f);
        iconRt.anchorMax = new Vector2(0.90f, 0.90f);
        iconRt.offsetMin = Vector2.zero;
        iconRt.offsetMax = Vector2.zero;
        var iconImg     = iconGo.AddComponent<Image>();
        iconImg.sprite  = LoadSkillIcon(type);
        iconImg.preserveAspect = true;

        // ── 쿨다운 Radial360 오버레이 ────────────────────────────────────
        // fillClockwise=false + Origin=Top → 색이 12시부터 시계방향으로 복원됨
        var radGo  = Mk("Radial", iconAreaGo.transform);
        Stretch(radGo);
        var radImg = radGo.AddComponent<Image>();
        radImg.sprite        = CircleSprite(128);
        radImg.color         = new Color(0f, 0f, 0f, 0.78f);
        radImg.type          = Image.Type.Filled;
        radImg.fillMethod    = Image.FillMethod.Radial360;
        radImg.fillOrigin    = (int)Image.Origin360.Top;
        radImg.fillClockwise = false;   // 12시→시계방향으로 색 복원
        radImg.fillAmount    = 0f;
        radGo.SetActive(false);

        // 쿨다운 남은 초 텍스트
        var cdGo = Mk("CdTxt", iconAreaGo.transform);
        Stretch(cdGo);
        var cdTx = cdGo.AddComponent<Text>();
        cdTx.font      = BuiltinFont();
        cdTx.fontSize  = 22;
        cdTx.fontStyle = FontStyle.Bold;
        cdTx.color     = Color.white;
        cdTx.alignment = TextAnchor.MiddleCenter;
        cdGo.SetActive(false);

        // ── 잠금 오버레이 ─────────────────────────────────────────────────
        var lockGo  = Mk("Lock", iconAreaGo.transform);
        Stretch(lockGo);
        var lockImg = lockGo.AddComponent<Image>();
        lockImg.sprite = CircleSprite(32);
        lockImg.color  = new Color(0f, 0f, 0f, 0.65f);
        // "LOCK" 텍스트
        var lockTxtGo = Mk("LockTxt", lockGo.transform);
        Stretch(lockTxtGo);
        var lockTx = lockTxtGo.AddComponent<Text>();
        lockTx.font      = BuiltinFont();
        lockTx.fontSize  = 12;
        lockTx.fontStyle = FontStyle.Bold;
        lockTx.color     = new Color(1f, 1f, 1f, 0.60f);
        lockTx.alignment = TextAnchor.MiddleCenter;
        lockTx.text      = "LOCK";
        lockGo.SetActive(false);

        // ── 쿨다운 완료 반짝임 ─────────────────────────────────────────────
        var glowGo = Mk("Glow", iconAreaGo.transform);
        var glowRt = glowGo.AddComponent<RectTransform>();
        glowRt.anchorMin = new Vector2(-0.18f, -0.18f);
        glowRt.anchorMax = new Vector2(1.18f,  1.18f);
        glowRt.offsetMin = Vector2.zero;
        glowRt.offsetMax = Vector2.zero;
        var glowImg = glowGo.AddComponent<Image>();
        glowImg.sprite = CircleSprite(96);
        glowImg.color  = Color.clear;
        glowGo.SetActive(false);

        // ── 키 힌트 "[1]" (하단) ───────────────────────────────────────────
        var keyGo = Mk("Key", root.transform);
        var keyRt = keyGo.AddComponent<RectTransform>();
        keyRt.anchorMin        = new Vector2(0f, 0f);
        keyRt.anchorMax        = new Vector2(1f, 0f);
        keyRt.pivot            = new Vector2(0.5f, 0f);
        keyRt.anchoredPosition = Vector2.zero;
        keyRt.sizeDelta        = new Vector2(0f, KEY_H + 4f);
        var keyTx = keyGo.AddComponent<Text>();
        keyTx.font      = BuiltinFont();
        keyTx.fontSize  = 11;
        keyTx.fontStyle = FontStyle.Bold;
        keyTx.color     = new Color(1f, 1f, 1f, 0.55f);
        keyTx.alignment = TextAnchor.MiddleCenter;
        keyTx.text      = $"[{keyNum}]";

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

    // ── 매 프레임 업데이트 ──────────────────────────────────────────────

    void Update()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var ui   = slots[i];
            var type = ui.allyType;

            bool unlocked  = SkillSystem.IsUnlocked(type);
            float remaining = SkillSystem.GetCooldownRemaining(type);
            float total     = SkillSystem.GetCooldownTotal(type);
            bool onCd       = remaining > 0.05f;

            // ── 잠금 오버레이 ───────────────────────────────────────────
            ui.lockOverlay.gameObject.SetActive(!unlocked);

            // ── 쿨다운 Radial 오버레이 ──────────────────────────────────
            bool showRadial = unlocked && onCd;
            ui.radial.gameObject.SetActive(showRadial);
            if (showRadial && total > 0f)
                ui.radial.fillAmount = remaining / total;

            // ── 쿨다운 숫자 ────────────────────────────────────────────
            bool showCd = unlocked && onCd;
            ui.cdTxt.gameObject.SetActive(showCd);
            if (showCd)
                ui.cdTxt.text = Mathf.CeilToInt(remaining).ToString();

            // ── 쿨다운 완료 감지 → 반짝임 ─────────────────────────────
            if (ui.wasOnCd && !onCd && unlocked)
                StartCoroutine(ReadyFlash(ui.glowImg));

            ui.wasOnCd = onCd;
        }
    }

    // ── 반짝임 코루틴 ──────────────────────────────────────────────────

    IEnumerator ReadyFlash(Image glowImg)
    {
        if (glowImg == null) yield break;
        var rt = glowImg.GetComponent<RectTransform>();
        glowImg.gameObject.SetActive(true);

        for (int p = 0; p < 3; p++)
        {
            float t = 0f;
            while (t < 0.22f)
            {
                t += Time.unscaledDeltaTime;
                float progress = t / 0.22f;
                float alpha    = Mathf.Sin(progress * Mathf.PI) * 0.90f;
                float scale    = 1f + Mathf.Sin(progress * Mathf.PI) * 0.20f;
                glowImg.color     = new Color(1f, 0.92f, 0.25f, alpha);
                rt.localScale     = Vector3.one * scale;
                yield return null;
            }
        }

        glowImg.color     = Color.clear;
        rt.localScale     = Vector3.one;
        glowImg.gameObject.SetActive(false);
    }

    // ── 아이콘 로딩 ────────────────────────────────────────────────────

    static Sprite LoadSkillIcon(AllyType type)
    {
        Sprite spr = SkillSystem.GetIconSprite(type);
        if (spr != null) return spr;
        // 폴백: 절차적 초상화 스프라이트
        return AllyVisualGenerator.CreatePortraitSprite(type);
    }

    // ── UI 유틸리티 ────────────────────────────────────────────────────

    static GameObject Mk(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Font BuiltinFont() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    /// <summary>원형 스프라이트 (반투명 가장자리 포함)</summary>
    static Sprite CircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        float r = size * 0.5f - 0.5f;
        float c = size * 0.5f - 0.5f;
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float dx = x - c, dy = y - c;
            float alpha = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.8f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// <summary>9-슬라이스용 둥근 사각형 스프라이트</summary>
    static Sprite MakeRoundedRectSprite(int cornerRadius = 6)
    {
        int sz = cornerRadius * 2 + 4;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[sz * sz];
        float cx = (sz - 1) * 0.5f, cy = (sz - 1) * 0.5f;
        float inner = cornerRadius;
        for (int x = 0; x < sz; x++)
        for (int y = 0; y < sz; y++)
        {
            float ax = Mathf.Max(Mathf.Abs(x - cx) - (cx - cornerRadius), 0f);
            float ay = Mathf.Max(Mathf.Abs(y - cy) - (cy - cornerRadius), 0f);
            float dist = Mathf.Sqrt(ax * ax + ay * ay);
            float alpha = Mathf.Clamp01(cornerRadius - dist + 0.5f);
            pixels[y * sz + x] = new Color(1f, 1f, 1f, alpha);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        int b = cornerRadius;
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz,
                             0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }
}
