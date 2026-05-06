using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 타이틀 화면 - 디펜스 게임 배경 + 시작/설정/종료 버튼
/// 시작 버튼 → StageSelect 씬으로 이동
/// </summary>
public class MainMenu : MonoBehaviour
{
    // ── 색상 ───────────────────────────────────────────────────────
    static readonly Color COL_SKY_TOP    = new Color(0.05f, 0.08f, 0.18f);
    static readonly Color COL_SKY_BOT    = new Color(0.10f, 0.16f, 0.28f);
    static readonly Color COL_GROUND     = new Color(0.12f, 0.22f, 0.10f);
    static readonly Color COL_PATH       = new Color(0.45f, 0.32f, 0.14f);
    static readonly Color COL_TITLE      = new Color(1.00f, 0.85f, 0.20f);
    static readonly Color COL_TITLE_SH   = new Color(0.40f, 0.28f, 0.00f);
    static readonly Color COL_BTN_START  = new Color(0.15f, 0.55f, 0.25f);
    static readonly Color COL_BTN_S_HOV  = new Color(0.22f, 0.72f, 0.35f);
    static readonly Color COL_BTN_GEM    = new Color(0.18f, 0.42f, 0.62f);
    static readonly Color COL_BTN_GEM_H  = new Color(0.30f, 0.58f, 0.84f);
    static readonly Color COL_BTN_SET    = new Color(0.25f, 0.35f, 0.55f);
    static readonly Color COL_BTN_SET_H  = new Color(0.35f, 0.48f, 0.72f);
    static readonly Color COL_BTN_EXIT   = new Color(0.55f, 0.12f, 0.12f);
    static readonly Color COL_BTN_EXIT_H = new Color(0.75f, 0.18f, 0.18f);
    static readonly Color COL_PANEL      = new Color(0.05f, 0.07f, 0.13f, 0.97f);

    struct BtnData
    {
        public RectTransform rt;
        public Image fill;
        public Color normal, hover;
        public System.Action action;
    }

    struct GemEntryUi
    {
        public Image icon;
        public Text title;
        public Text desc;
        public Text status;
        public int buttonIndex;
    }

    System.Collections.Generic.List<BtnData> btns = new();
    Canvas uiCanvas;
    GameObject gemPanel;
    readonly System.Collections.Generic.List<GemEntryUi> gemEntryUis = new();
    GameObject settingsPanel;
    Text settingsVolumeLbl;
    Text settingsFsStatLbl;
    RectTransform volumeSliderRt;
    RectTransform volumeSliderFillRt;
    RectTransform volumeSliderHandleRt;
    bool volumeSliderDragging;
    readonly System.Collections.Generic.Dictionary<int, Sprite> gemSpriteCache = new();
    readonly System.Collections.Generic.Dictionary<int, Sprite> lockedGemSpriteCache = new();

    void Start()
    {
        if (StageManager.Instance == null)
        {
            var smGo = new GameObject("StageManager");
            smGo.AddComponent<StageManager>();
        }

        SetupCamera();
        DrawBackground();
        BuildUI();
        SettingsManager.Apply();
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 mp = mouse.position.ReadValue();
        foreach (var b in btns)
        {
            if (b.rt == null || !b.rt.gameObject.activeInHierarchy) continue;
            b.fill.color = b.normal;
        }

        if (HandleVolumeSlider(mp, mouse))
            return;

        for (int i = btns.Count - 1; i >= 0; i--)
        {
            var b = btns[i];
            if (b.rt == null || !b.rt.gameObject.activeInHierarchy) continue;
            if (!IsButtonInActiveInputLayer(b.rt)) continue;
            if (!RectTransformUtility.RectangleContainsScreenPoint(b.rt, mp, null)) continue;

            b.fill.color = b.hover;
            if (mouse.leftButton.wasPressedThisFrame)
                b.action?.Invoke();

            return;
        }
    }

    bool IsButtonInActiveInputLayer(RectTransform rt)
    {
        if (settingsPanel != null && settingsPanel.activeInHierarchy)
            return rt.transform.IsChildOf(settingsPanel.transform);

        if (gemPanel != null && gemPanel.activeInHierarchy)
            return rt.transform.IsChildOf(gemPanel.transform);

        return true;
    }

    bool HandleVolumeSlider(Vector2 mousePos, Mouse mouse)
    {
        if (settingsPanel == null || !settingsPanel.activeInHierarchy || volumeSliderRt == null)
            return false;

        bool overTrack = RectTransformUtility.RectangleContainsScreenPoint(volumeSliderRt, mousePos, null);
        bool overHandle = volumeSliderHandleRt != null &&
            RectTransformUtility.RectangleContainsScreenPoint(volumeSliderHandleRt, mousePos, null);
        bool over = overTrack || overHandle;
        if (over && mouse.leftButton.wasPressedThisFrame)
            volumeSliderDragging = true;

        if (mouse.leftButton.wasReleasedThisFrame)
            volumeSliderDragging = false;

        if (!volumeSliderDragging)
            return over;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(volumeSliderRt, mousePos, null, out var localPos))
        {
            float width = volumeSliderRt.rect.width;
            float pct = Mathf.InverseLerp(-width * 0.5f, width * 0.5f, localPos.x);
            SetVolumePercent(Mathf.RoundToInt(Mathf.Clamp01(pct) * 100f));
        }

        return true;
    }

    // ── 카메라 ─────────────────────────────────────────────────────
    void SetupCamera()
    {
        var cam = FindFirstObjectByType<Camera>();
        if (cam == null) { var g = new GameObject("Main Camera"); g.tag = "MainCamera"; cam = g.AddComponent<Camera>(); }
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = COL_SKY_TOP;
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0, 0, -10f);
    }

    // ── 디펜스 배경 ────────────────────────────────────────────────
    void DrawBackground()
    {
        var texture = Resources.Load<Texture2D>("UI/MainMenuBackground");
        if (texture == null)
        {
            CreateWorldQuad("SkyFallback", Vector3.zero, new Vector3(22f, 12f), COL_SKY_TOP, -30);
            return;
        }

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        var sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        var go = new GameObject("MainMenuBackground");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = -30;
        go.transform.position = new Vector3(0f, 0f, 5f);

        var cam = Camera.main ?? FindFirstObjectByType<Camera>();
        float worldHeight = cam != null ? cam.orthographicSize * 2f : 10f;
        float worldWidth = cam != null ? worldHeight * cam.aspect : 17.78f;
        Vector2 spriteSize = sprite.bounds.size;
        float scale = Mathf.Max(worldWidth / spriteSize.x, worldHeight / spriteSize.y);
        go.transform.localScale = Vector3.one * scale;
    }

    void DrawPathSegment(float x1, float y1, float x2, float y2, int idx)
    {
        Vector2 mid = new Vector2((x1 + x2) * 0.5f, (y1 + y2) * 0.5f);
        float len = Vector2.Distance(new Vector2(x1, y1), new Vector2(x2, y2));
        float angle = Mathf.Atan2(y2 - y1, x2 - x1) * Mathf.Rad2Deg;
        CreateWorldQuad($"PathShadow{idx}", new Vector3(mid.x, mid.y - 0.07f, 2.2f), new Vector3(len, 0.58f), new Color(0.18f, 0.10f, 0.04f), -18).transform.rotation = Quaternion.Euler(0, 0, angle);
        CreateWorldQuad($"Path{idx}", new Vector3(mid.x, mid.y, 2.1f), new Vector3(len, 0.42f), new Color(0.70f, 0.52f, 0.28f), -17).transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    void DrawRock(float x, float y)
    {
        CreateWorldCircle($"Rock{x}_{y}", new Vector3(x, y, 2.3f), 0.18f, new Color(0.26f, 0.24f, 0.30f), -14);
    }

    void DrawCrystal(float x, float y, int i)
    {
        CreateWorldCircle($"CrystalGlow{i}", new Vector3(x, y, 2.2f), 0.35f, new Color(0.45f, 0.25f, 1f, 0.22f), -15);
        CreateWorldQuad($"Crystal{i}", new Vector3(x, y, 2f), new Vector3(0.16f, 0.55f), new Color(0.55f, 0.30f, 1f), -13);
        CreateWorldQuad($"CrystalHi{i}", new Vector3(x + 0.07f, y + 0.04f, 1.9f), new Vector3(0.05f, 0.42f), new Color(0.86f, 0.72f, 1f), -12);
    }

    void DrawRuins(float x, float y)
    {
        for (int i = 0; i < 3; i++)
            CreateWorldQuad($"RuinCol{i}", new Vector3(x + i * 0.52f, y + 0.55f, 2f), new Vector3(0.25f, 1.15f - i * 0.25f), new Color(0.45f, 0.45f, 0.42f), -13);
        CreateWorldQuad("RuinBase", new Vector3(x + 0.5f, y - 0.1f, 2f), new Vector3(1.45f, 0.25f), new Color(0.34f, 0.34f, 0.32f), -12);
    }

    void DrawPortal(float x, float y)
    {
        CreateWorldCircle("PortalGlow", new Vector3(x, y, 2.5f), 0.8f, new Color(0.55f, 0.18f, 1f, 0.25f), -15);
        CreateWorldCircle("Portal", new Vector3(x, y, 2f), 0.42f, new Color(0.22f, 0.03f, 0.42f), -13);
        CreateWorldCircle("PortalCore", new Vector3(x, y, 1.9f), 0.24f, new Color(0.80f, 0.25f, 1f), -12);
    }

    void DrawVolcano(float x, float y, float s)
    {
        CreateWorldQuad($"VolcanoBase{x}", new Vector3(x, y - 0.45f, 2.2f), new Vector3(1.3f * s, 1.2f * s), new Color(0.18f, 0.10f, 0.09f), -14);
        CreateWorldQuad($"VolcanoLava{x}", new Vector3(x, y - 0.15f, 2f), new Vector3(0.18f * s, 1.25f * s), new Color(1f, 0.20f, 0.02f), -12);
        CreateWorldCircle($"Smoke{x}", new Vector3(x + 0.1f, y + 0.65f * s, 2.2f), 0.34f * s, new Color(0.15f, 0.13f, 0.16f, 0.75f), -13);
    }

    void DrawGoal(float x, float y)
    {
        CreateWorldQuad("GoalBridge", new Vector3(x, y - 0.25f, 2f), new Vector3(2.0f, 0.35f), new Color(0.42f, 0.25f, 0.10f), -12);
        CreateWorldQuad("GoalSign", new Vector3(x, y + 0.35f, 1.9f), new Vector3(1.25f, 0.58f), new Color(0.16f, 0.08f, 0.02f), -11);
    }

    void DrawTower(float x, float baseY, Color wallColor, Color roofColor)
    {
        // 탑 몸체
        CreateWorldQuad($"TW_{x}", new Vector3(x, baseY + 0.9f, 2f), new Vector3(1.0f, 1.8f), wallColor, -16);
        // 탑 지붕 (삼각형 느낌으로 좁은 직사각형)
        CreateWorldQuad($"TR_{x}", new Vector3(x, baseY + 1.9f, 2f), new Vector3(1.2f, 0.25f), wallColor * 0.8f, -15);
        CreateWorldQuad($"TP_{x}", new Vector3(x, baseY + 2.15f, 2f), new Vector3(0.8f, 0.4f), roofColor, -14);
            // 창문
        CreateWorldQuad($"TW1_{x}", new Vector3(x, baseY + 1.2f, 1.9f), new Vector3(0.22f, 0.28f), new Color(1f, 0.85f, 0.3f, 0.7f), -13);
        CreateWorldQuad($"TW2_{x}", new Vector3(x, baseY + 0.6f, 1.9f), new Vector3(0.22f, 0.22f), new Color(0.6f, 0.75f, 1f, 0.4f), -13);
    }

    void DrawTree(float x, float y)
    {
        var trunkColor = new Color(0.30f, 0.18f, 0.06f);
        var leafColor  = new Color(0.10f, 0.35f, 0.10f);
        var leafColor2 = new Color(0.14f, 0.45f, 0.14f);
        CreateWorldQuad($"Trunk_{x}", new Vector3(x, y + 0.25f, 2.5f), new Vector3(0.18f, 0.5f), trunkColor, -15);
        CreateWorldCircle($"Leaf1_{x}", new Vector3(x, y + 0.85f, 2.4f), 0.42f, leafColor, -14);
        CreateWorldCircle($"Leaf2_{x}", new Vector3(x + 0.2f, y + 1.05f, 2.4f), 0.32f, leafColor2, -13);
        CreateWorldCircle($"Leaf3_{x}", new Vector3(x - 0.2f, y + 1.1f,  2.4f), 0.28f, leafColor2, -13);
    }

    // ── World 오브젝트 생성 헬퍼 ──────────────────────────────────
    GameObject CreateWorldQuad(string name, Vector3 pos, Vector3 scale, Color color, int order)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeSquareSprite();
        sr.color  = color;
        sr.sortingOrder = order;
        go.transform.position   = pos;
        go.transform.localScale = scale;
        return go;
    }

    void CreateWorldCircle(string name, Vector3 pos, float radius, Color color, int order)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite(32);
        sr.color  = color;
        sr.sortingOrder = order;
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * radius * 2f;
    }

    // ── UI 빌드 ────────────────────────────────────────────────────
    void BuildUI()
    {
        var cgo = new GameObject("UICanvas");
        uiCanvas = cgo.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 100;

        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1280, 720);
        sc.matchWidthOrHeight  = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        // 버튼 패널 배경
        CreateImg(cgo.transform, new Color(0f, 0f, 0f, 0.34f), new Vector2(0, -220), new Vector2(350, 320));

        // 버튼 4개
        RegBtn(cgo.transform, "start", "▶ 시작", new Vector2(0, -122), new Vector2(286, 58), COL_BTN_START,  COL_BTN_S_HOV,  OnStart);
        RegBtn(cgo.transform, "gem",   "◆ 보석", new Vector2(0, -188), new Vector2(286, 58), COL_BTN_GEM,    COL_BTN_GEM_H,  OnGemMenu);
        RegBtn(cgo.transform, "set",   "⚙ 설정", new Vector2(0, -254), new Vector2(286, 58), COL_BTN_SET,    COL_BTN_SET_H,  OnSettings);
        RegBtn(cgo.transform, "exit",  "✕ 종료", new Vector2(0, -320), new Vector2(286, 58), COL_BTN_EXIT,   COL_BTN_EXIT_H, OnExit);

        // 버전
        CreateTxt(cgo.transform, "v0.1 Alpha", new Color(1f, 1f, 1f, 0.3f), new Vector2(560, -330), new Vector2(160, 28), 15);

        BuildGemPanel(cgo.transform);
        BuildSettingsPanel(cgo.transform);
    }

    // ── RectTransform 공통 헬퍼 ──────────────────────────────────────
    static void SR(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
    }

    int RegBtn(Transform p, string id, string label, Vector2 pos, Vector2 size, Color n, Color h, System.Action cb)
    {
        var go = new GameObject("Btn_" + id); go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);
        SR(go.GetComponent<RectTransform>(), pos, size + new Vector2(4, 4));

        var inner = new GameObject("Fill"); inner.transform.SetParent(go.transform, false);
        var fi = inner.AddComponent<Image>(); fi.color = n;
        SR(inner.GetComponent<RectTransform>(), Vector2.zero, size);

        var tg = new GameObject("Lbl"); tg.transform.SetParent(inner.transform, false);
        var tx = tg.AddComponent<Text>(); tx.text = label; tx.color = Color.white; tx.fontSize = 26;
        tx.alignment          = TextAnchor.MiddleCenter; tx.fontStyle = FontStyle.Normal;
        tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        tx.verticalOverflow   = VerticalWrapMode.Overflow;
        tx.alignByGeometry    = true;
        tx.raycastTarget      = false;
        tx.font               = UiPixelFont.Get();
        SR(tg.GetComponent<RectTransform>(), Vector2.zero, size);

        int index = btns.Count;
        btns.Add(new BtnData { rt = inner.GetComponent<RectTransform>(), fill = fi, normal = n, hover = h, action = cb });
        return index;
    }

    void BuildGemPanel(Transform parent)
    {
        gemPanel = new GameObject("GemPanel");
        gemPanel.transform.SetParent(parent, false);

        CreateImg(gemPanel.transform, new Color(0f, 0f, 0f, 0.78f), Vector2.zero, new Vector2(1920, 1080));
        CreateImg(gemPanel.transform, COL_PANEL, Vector2.zero, new Vector2(860, 560));
        CreateImg(gemPanel.transform, new Color(1f, 1f, 1f, 0.12f), Vector2.zero, new Vector2(864, 564));

        CreateTxt(gemPanel.transform, "보석 메뉴", COL_TITLE, new Vector2(0, 225), new Vector2(680, 50), 36);
        CreateTxt(gemPanel.transform,
            "스테이지를 클리어하면 보석이 해금됩니다. 해금된 보석은 여기서 활성화 상태를 확인할 수 있습니다.",
            new Color(0.82f, 0.88f, 0.96f), new Vector2(0, 182), new Vector2(820, 56), 18);

        var defs = GemInventory.GetDefinitions();
        for (int i = 0; i < defs.Length; i++)
        {
            float y = 88f - i * 124f;
            var row = new GameObject("GemRow_" + i);
            row.transform.SetParent(gemPanel.transform, false);

            CreateImg(row.transform, new Color(0f, 0f, 0f, 0.36f), new Vector2(0, y), new Vector2(720, 102));
            CreateImg(row.transform, new Color(1f, 1f, 1f, 0.08f), new Vector2(0, y), new Vector2(724, 106));

            int capturedStage = defs[i].stageIndex;
            int buttonIndex = RegBtn(row.transform, $"gem_toggle_{capturedStage}", "",
                new Vector2(280, y), new Vector2(86, 86),
                new Color(0.08f, 0.10f, 0.14f), new Color(0.15f, 0.18f, 0.24f),
                () => ToggleGem(capturedStage));

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(row.transform, false);
            var icon = iconGo.AddComponent<Image>();
            icon.raycastTarget = false;
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchoredPosition = new Vector2(280, y);
            iconRt.sizeDelta = new Vector2(62, 62);

            var title = CreateTxtReturn(row.transform, "", Color.white, new Vector2(-140, y + 18), new Vector2(300, 28), 23);
            title.alignment = TextAnchor.MiddleLeft;
            var desc = CreateTxtReturn(row.transform, "", new Color(0.84f, 0.88f, 0.92f), new Vector2(-110, y - 15), new Vector2(380, 40), 17);
            desc.alignment = TextAnchor.MiddleLeft;
            var status = CreateTxtReturn(row.transform, "", COL_TITLE, new Vector2(155, y), new Vector2(120, 28), 20);

            gemEntryUis.Add(new GemEntryUi
            {
                icon = icon,
                title = title,
                desc = desc,
                status = status,
                buttonIndex = buttonIndex
            });
        }

        RegBtn(gemPanel.transform, "gem_close", "← 닫기",
            new Vector2(0, -225), new Vector2(220, 52),
            COL_BTN_EXIT, COL_BTN_EXIT_H, CloseGemMenu);

        gemPanel.SetActive(false);
    }

    void RefreshGemPanel()
    {
        var defs = GemInventory.GetDefinitions();
        for (int i = 0; i < defs.Length && i < gemEntryUis.Count; i++)
        {
            var def = defs[i];
            var ui = gemEntryUis[i];
            bool unlocked = GemInventory.IsUnlocked(def.stageIndex);
            bool active = GemInventory.IsActive(def.stageIndex);

            ui.icon.sprite = MakeGemSprite(def.stageIndex, unlocked);
            ui.icon.color = !unlocked
                ? new Color(0.55f, 0.57f, 0.62f, 1f)
                : (active ? Color.white : new Color(0.42f, 0.44f, 0.48f, 1f));
            SetButtonColors(ui.buttonIndex, BuildGemButtonColor(def.color, unlocked, active, false),
                BuildGemButtonColor(def.color, unlocked, active, true));
            ui.title.text = $"STAGE {def.stageIndex}  {def.gemName}";
            ui.desc.text = unlocked ? def.effectSummary : "아직 해금되지 않았습니다";
            ui.status.text = !unlocked ? "잠김" : (active ? "활성" : "비활성");
            ui.status.color = !unlocked
                ? new Color(0.65f, 0.68f, 0.74f)
                : (active ? new Color(0.42f, 1f, 0.52f) : new Color(1f, 0.85f, 0.35f));
        }
    }

    Color BuildGemButtonColor(Color gemColor, bool unlocked, bool active, bool hover)
    {
        if (!unlocked)
            return hover ? new Color(0.18f, 0.19f, 0.23f) : new Color(0.10f, 0.11f, 0.14f);

        if (!active)
            return hover ? new Color(0.20f, 0.21f, 0.25f) : new Color(0.12f, 0.13f, 0.16f);

        Color baseColor = Color.Lerp(new Color(0.11f, 0.13f, 0.17f), gemColor, hover ? 0.52f : 0.36f);
        baseColor.a = 1f;
        return baseColor;
    }

    void SetButtonColors(int index, Color normal, Color hover)
    {
        if (index < 0 || index >= btns.Count)
            return;

        var data = btns[index];
        data.normal = normal;
        data.hover = hover;
        data.fill.color = normal;
        btns[index] = data;
    }

    void CreateImg(Transform p, Color c, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Img"); go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = c;
        SR(go.GetComponent<RectTransform>(), pos, size);
    }

    void CreateTxt(Transform p, string s, Color c, Vector2 pos, Vector2 size, int fs)
    {
        var go = new GameObject("Txt"); go.transform.SetParent(p, false);
        var tx = go.AddComponent<Text>(); tx.text = s; tx.color = c; tx.fontSize = fs;
        tx.alignment          = TextAnchor.MiddleCenter; tx.fontStyle = FontStyle.Normal;
        tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        tx.verticalOverflow   = VerticalWrapMode.Overflow;
        tx.alignByGeometry    = true;
        tx.raycastTarget      = false;
        tx.font               = UiPixelFont.Get();
        SR(go.GetComponent<RectTransform>(), pos, size);
    }

    Text CreateTxtReturn(Transform p, string s, Color c, Vector2 pos, Vector2 size, int fs)
    {
        var go = new GameObject("Txt"); go.transform.SetParent(p, false);
        var tx = go.AddComponent<Text>(); tx.text = s; tx.color = c; tx.fontSize = fs;
        tx.alignment          = TextAnchor.MiddleCenter; tx.fontStyle = FontStyle.Normal;
        tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        tx.verticalOverflow   = VerticalWrapMode.Overflow;
        tx.alignByGeometry    = true;
        tx.raycastTarget      = false;
        tx.font               = UiPixelFont.Get();
        SR(go.GetComponent<RectTransform>(), pos, size);
        return tx;
    }

    // ── 이벤트 ────────────────────────────────────────────────────
    void OnStart()    => SceneManager.LoadScene("StageSelect", LoadSceneMode.Single);
    void OnGemMenu()
    {
        RefreshGemPanel();
        gemPanel.SetActive(true);
    }
    void CloseGemMenu() => gemPanel.SetActive(false);
    void ToggleGem(int stageIndex)
    {
        if (!GemInventory.IsUnlocked(stageIndex))
            return;

        GemInventory.SetActive(stageIndex, !GemInventory.IsActive(stageIndex));
        RefreshGemPanel();
    }
    void OnSettings()
    {
        if (settingsVolumeLbl != null) settingsVolumeLbl.text = VolumePct();
        if (settingsFsStatLbl != null) settingsFsStatLbl.text = FsText();
        RefreshVolumeSlider();
        settingsPanel.SetActive(true);
    }

    void BuildSettingsPanel(Transform parent)
    {
        settingsPanel = new GameObject("SettingsPanel");
        settingsPanel.transform.SetParent(parent, false);

        // 어두운 오버레이
        CreateImg(settingsPanel.transform, new Color(0f, 0f, 0f, 0.78f), Vector2.zero, new Vector2(1920, 1080));
        // 패널 배경
        CreateImg(settingsPanel.transform, COL_PANEL, Vector2.zero, new Vector2(560, 380));
        CreateImg(settingsPanel.transform, new Color(1f, 1f, 1f, 0.12f), Vector2.zero, new Vector2(564, 384));

        CreateTxt(settingsPanel.transform, "설정", COL_TITLE, new Vector2(0, 148), new Vector2(400, 50), 36);
        CreateImg(settingsPanel.transform, new Color(1f, 1f, 1f, 0.15f), new Vector2(0, 116), new Vector2(500, 2));

        // 볼륨 행
        CreateTxt(settingsPanel.transform, "마스터 볼륨", new Color(0.85f, 0.88f, 0.94f), new Vector2(-95, 62), new Vector2(190, 36), 22);
        settingsVolumeLbl = CreateTxtReturn(settingsPanel.transform, VolumePct(), COL_TITLE, new Vector2(130, 62), new Vector2(120, 36), 22);
        CreateVolumeSlider(settingsPanel.transform, new Vector2(0, 15), new Vector2(320, 22));

        // 전체화면 행
        CreateTxt(settingsPanel.transform, "전체화면", new Color(0.85f, 0.88f, 0.94f), new Vector2(-95, -52), new Vector2(190, 36), 22);
        settingsFsStatLbl = CreateTxtReturn(settingsPanel.transform, FsText(), COL_TITLE, new Vector2(130, -52), new Vector2(120, 36), 22);
        RegBtn(settingsPanel.transform, "fs_toggle", "전환", new Vector2(0, -98), new Vector2(120, 44), COL_BTN_SET, COL_BTN_SET_H, ToggleFs);

        // 구분선 + 닫기
        CreateImg(settingsPanel.transform, new Color(1f, 1f, 1f, 0.15f), new Vector2(0, -136), new Vector2(500, 2));
        RegBtn(settingsPanel.transform, "set_close", "← 닫기",
            new Vector2(0, -163), new Vector2(220, 52),
            COL_BTN_EXIT, COL_BTN_EXIT_H, CloseSettings);

        settingsPanel.SetActive(false);
    }

    void CreateVolumeSlider(Transform parent, Vector2 pos, Vector2 size)
    {
        var trackGo = new GameObject("VolumeSlider");
        trackGo.transform.SetParent(parent, false);
        var trackImg = trackGo.AddComponent<Image>();
        trackImg.color = new Color(0.02f, 0.03f, 0.05f, 0.95f);
        volumeSliderRt = trackGo.GetComponent<RectTransform>();
        volumeSliderRt.anchoredPosition = pos;
        volumeSliderRt.sizeDelta = size;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(trackGo.transform, false);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = COL_TITLE;
        volumeSliderFillRt = fillGo.GetComponent<RectTransform>();
        volumeSliderFillRt.anchorMin = new Vector2(0f, 0f);
        volumeSliderFillRt.anchorMax = new Vector2(0f, 1f);
        volumeSliderFillRt.pivot = new Vector2(0f, 0.5f);
        volumeSliderFillRt.anchoredPosition = Vector2.zero;

        var handleGo = new GameObject("Handle");
        handleGo.transform.SetParent(trackGo.transform, false);
        var handleImg = handleGo.AddComponent<Image>();
        handleImg.color = Color.white;
        handleImg.raycastTarget = false;
        volumeSliderHandleRt = handleGo.GetComponent<RectTransform>();
        volumeSliderHandleRt.anchorMin = new Vector2(0f, 0.5f);
        volumeSliderHandleRt.anchorMax = new Vector2(0f, 0.5f);
        volumeSliderHandleRt.pivot = new Vector2(0.5f, 0.5f);
        volumeSliderHandleRt.sizeDelta = new Vector2(14, 34);

        CreateTxt(parent, "0", new Color(0.60f, 0.64f, 0.72f), pos + new Vector2(-178, -26), new Vector2(40, 20), 13);
        CreateTxt(parent, "100", new Color(0.60f, 0.64f, 0.72f), pos + new Vector2(178, -26), new Vector2(48, 20), 13);
        RefreshVolumeSlider();
    }

    void SetVolumePercent(int percent)
    {
        SettingsManager.Volume = Mathf.Clamp(percent, 0, 100) / 100f;
        if (settingsVolumeLbl != null) settingsVolumeLbl.text = VolumePct();
        RefreshVolumeSlider();
    }

    void RefreshVolumeSlider()
    {
        if (volumeSliderRt == null || volumeSliderFillRt == null || volumeSliderHandleRt == null)
            return;

        float pct = Mathf.Clamp01(SettingsManager.Volume);
        float width = volumeSliderRt.rect.width;
        volumeSliderFillRt.sizeDelta = new Vector2(width * pct, 0f);
        volumeSliderHandleRt.anchoredPosition = new Vector2(width * pct, 0f);
    }

    void ToggleFs()
    {
        SettingsManager.IsFullscreen = !SettingsManager.IsFullscreen;
        if (settingsFsStatLbl != null) settingsFsStatLbl.text = FsText();
    }

    void CloseSettings() => settingsPanel.SetActive(false);
    string VolumePct()  => $"{Mathf.RoundToInt(SettingsManager.Volume * 100)}%";
    string FsText()     => SettingsManager.IsFullscreen ? "ON" : "OFF";
    void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── 스프라이트 헬퍼 ───────────────────────────────────────────
    Sprite MakeSquareSprite()
    {
        var t = new Texture2D(1, 1); t.SetPixel(0, 0, Color.white); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
    }

    Sprite MakeCircleSprite(int res)
    {
        var t = new Texture2D(res, res, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;
        int c = res / 2;
        for (int x = 0; x < res; x++)
        for (int y = 0; y < res; y++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            float a = Mathf.Clamp01(1f - (d / c));
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, res, res), Vector2.one * 0.5f, res);
    }

    Sprite MakeGemSprite(int stageIndex, bool unlocked)
    {
        var cache = unlocked ? gemSpriteCache : lockedGemSpriteCache;
        int key = stageIndex;
        if (cache.TryGetValue(key, out var cached) && cached != null)
            return cached;

        var resourceSprite = LoadIconSprite(GetGemIconResourceName(stageIndex));
        if (resourceSprite != null)
        {
            cache[key] = resourceSprite;
            return resourceSprite;
        }

        const int res = 96;
        var t = new Texture2D(res, res, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;

        Color baseColor;
        Color darkColor;
        Color lightColor;
        Color accentColor;

        switch (stageIndex)
        {
            case 2:
                baseColor = new Color(0.95f, 0.65f, 0.16f);
                darkColor = new Color(0.42f, 0.23f, 0.06f);
                lightColor = new Color(1.00f, 0.92f, 0.40f);
                accentColor = new Color(1.00f, 0.77f, 0.25f);
                break;
            case 3:
                baseColor = new Color(0.90f, 0.12f, 0.10f);
                darkColor = new Color(0.25f, 0.03f, 0.04f);
                lightColor = new Color(1.00f, 0.50f, 0.20f);
                accentColor = new Color(1.00f, 0.88f, 0.22f);
                break;
            default:
                baseColor = new Color(0.12f, 0.78f, 0.33f);
                darkColor = new Color(0.03f, 0.25f, 0.13f);
                lightColor = new Color(0.56f, 1.00f, 0.62f);
                accentColor = new Color(0.18f, 0.94f, 0.54f);
                break;
        }

        if (!unlocked)
        {
            baseColor = Color.Lerp(baseColor, new Color(0.38f, 0.40f, 0.45f), 0.82f);
            darkColor = new Color(0.14f, 0.15f, 0.18f);
            lightColor = new Color(0.62f, 0.65f, 0.72f);
            accentColor = new Color(0.50f, 0.52f, 0.58f);
        }

        var gem = new[]
        {
            new Vector2(48, 8),
            new Vector2(78, 28),
            new Vector2(68, 70),
            new Vector2(48, 88),
            new Vector2(28, 70),
            new Vector2(18, 28)
        };

        var topFacet = new[] { gem[0], gem[1], new Vector2(57, 36), new Vector2(39, 36), gem[5] };
        var leftFacet = new[] { gem[5], new Vector2(39, 36), new Vector2(48, 88), gem[4] };
        var rightFacet = new[] { gem[1], gem[2], new Vector2(48, 88), new Vector2(57, 36) };
        var coreFacet = new[] { new Vector2(39, 36), new Vector2(57, 36), new Vector2(48, 88) };

        for (int x = 0; x < res; x++)
        for (int y = 0; y < res; y++)
        {
            float dx = x - 48f;
            float dy = y - 48f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float glow = Mathf.Clamp01(1f - dist / 48f);
            Color px = new Color(baseColor.r, baseColor.g, baseColor.b, 0.13f * glow * glow);

            if (PointInPolygon(new Vector2(x, y), gem))
            {
                float shade = Mathf.InverseLerp(90f, 6f, y);
                Color c = Color.Lerp(darkColor, baseColor, shade);

                if (PointInPolygon(new Vector2(x, y), topFacet))
                    c = Color.Lerp(c, lightColor, 0.46f);
                else if (PointInPolygon(new Vector2(x, y), leftFacet))
                    c = Color.Lerp(c, darkColor, 0.24f);
                else if (PointInPolygon(new Vector2(x, y), rightFacet))
                    c = Color.Lerp(c, accentColor, 0.20f);
                else if (PointInPolygon(new Vector2(x, y), coreFacet))
                    c = Color.Lerp(c, lightColor, 0.16f);

                if (stageIndex == 2)
                    c = Color.Lerp(c, new Color(1f, 0.48f, 0.06f), Mathf.Clamp01((x - 50f) / 50f) * 0.18f);
                else if (stageIndex == 3)
                    c = Color.Lerp(c, new Color(0.18f, 0.02f, 0.02f), Mathf.Clamp01((y - 44f) / 44f) * 0.20f);

                c.a = 1f;
                px = c;
            }

            t.SetPixel(x, y, px);
        }

        DrawGemLine(t, gem[0], gem[1], lightColor, 0.95f);
        DrawGemLine(t, gem[1], gem[2], darkColor, 0.90f);
        DrawGemLine(t, gem[2], gem[3], darkColor, 0.90f);
        DrawGemLine(t, gem[3], gem[4], darkColor, 0.90f);
        DrawGemLine(t, gem[4], gem[5], darkColor, 0.90f);
        DrawGemLine(t, gem[5], gem[0], lightColor, 0.80f);
        DrawGemLine(t, gem[5], new Vector2(39, 36), lightColor, 0.52f);
        DrawGemLine(t, gem[1], new Vector2(57, 36), lightColor, 0.52f);
        DrawGemLine(t, new Vector2(39, 36), gem[3], lightColor, 0.42f);
        DrawGemLine(t, new Vector2(57, 36), gem[3], darkColor, 0.36f);

        DrawGemSpark(t, 32, 25, 4, Color.white, unlocked ? 0.95f : 0.45f);
        DrawGemSpark(t, 61, 31, 3, Color.white, unlocked ? 0.75f : 0.35f);

        if (stageIndex == 3 && unlocked)
        {
            DrawGemLine(t, new Vector2(48, 38), new Vector2(42, 56), accentColor, 0.92f);
            DrawGemLine(t, new Vector2(42, 56), new Vector2(50, 67), accentColor, 0.92f);
            DrawGemLine(t, new Vector2(55, 44), new Vector2(62, 58), accentColor, 0.75f);
        }
        else if (stageIndex == 1 && unlocked)
        {
            DrawGemLine(t, new Vector2(35, 61), new Vector2(43, 51), accentColor, 0.65f);
            DrawGemLine(t, new Vector2(43, 51), new Vector2(52, 61), accentColor, 0.65f);
        }
        else if (stageIndex == 2 && unlocked)
        {
            DrawGemLine(t, new Vector2(30, 47), new Vector2(66, 43), accentColor, 0.48f);
            DrawGemLine(t, new Vector2(32, 55), new Vector2(63, 52), accentColor, 0.34f);
        }

        t.Apply();
        var sprite = Sprite.Create(t, new Rect(0, 0, res, res), Vector2.one * 0.5f, res);
        cache[key] = sprite;
        return sprite;
    }

    string GetGemIconResourceName(int stageIndex) => stageIndex switch
    {
        2 => "dark_crystal",
        3 => "volcano_crystal",
        _ => "green_crystal"
    };

    Sprite LoadIconSprite(string iconName)
    {
        var sprite = Resources.Load<Sprite>($"Icon/{iconName}");
        if (sprite != null)
            return sprite;

        var sprites = Resources.LoadAll<Sprite>($"Icon/{iconName}");
        return sprites != null && sprites.Length > 0 ? sprites[0] : null;
    }

    bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                inside = !inside;
        }
        return inside;
    }

    void DrawGemLine(Texture2D t, Vector2 a, Vector2 b, Color color, float alpha)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(a, b));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, steps == 0 ? 0f : i / (float)steps);
            BlendPixel(t, Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), color, alpha);
            BlendPixel(t, Mathf.RoundToInt(p.x) + 1, Mathf.RoundToInt(p.y), color, alpha * 0.45f);
        }
    }

    void DrawGemSpark(Texture2D t, int cx, int cy, int r, Color color, float alpha)
    {
        for (int i = -r; i <= r; i++)
        {
            float a = alpha * (1f - Mathf.Abs(i) / (float)(r + 1));
            BlendPixel(t, cx + i, cy, color, a);
            BlendPixel(t, cx, cy + i, color, a);
        }
    }

    void BlendPixel(Texture2D t, int x, int y, Color color, float alpha)
    {
        if (x < 0 || y < 0 || x >= t.width || y >= t.height)
            return;

        Color current = t.GetPixel(x, y);
        color.a = Mathf.Clamp01(alpha);
        t.SetPixel(x, y, Color.Lerp(current, color, color.a));
    }
}
