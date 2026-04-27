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
    }

    System.Collections.Generic.List<BtnData> btns = new();
    Canvas uiCanvas;
    GameObject gemPanel;
    readonly System.Collections.Generic.List<GemEntryUi> gemEntryUis = new();
    GameObject settingsPanel;
    Text settingsVolumeLbl;
    Text settingsFsStatLbl;

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
            bool over = RectTransformUtility.RectangleContainsScreenPoint(b.rt, mp, null);
            b.fill.color = over ? b.hover : b.normal;
            if (over && mouse.leftButton.wasPressedThisFrame)
                b.action?.Invoke();
        }
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
        // 하늘 그라디언트 (상단 어두운 파랑 → 하단 밝은 파랑)
        CreateWorldQuad("Sky", new Vector3(0, 1f, 5f), new Vector3(22f, 7f), COL_SKY_TOP, -20);
        CreateWorldQuad("SkyBot", new Vector3(0, -1.5f, 5f), new Vector3(22f, 4f), COL_SKY_BOT, -19);

        // 땅
        CreateWorldQuad("Ground", new Vector3(0, -3.5f, 4f), new Vector3(22f, 3f), COL_GROUND, -18);

        // 흙길 (가로)
        CreateWorldQuad("Path", new Vector3(0, -2.3f, 3f), new Vector3(22f, 1.0f), COL_PATH, -17);

        // 달 (원형 흰색)
        CreateWorldCircle("Moon", new Vector3(-6f, 3.2f, 3f), 0.55f, new Color(1f, 0.97f, 0.85f), -16);

        // 달 헤일로
        CreateWorldCircle("MoonHalo", new Vector3(-6f, 3.2f, 3.1f), 0.72f, new Color(1f, 0.97f, 0.85f, 0.15f), -17);

        // 별들
        var rng = new System.Random(42);
        for (int i = 0; i < 60; i++)
        {
            float x = (float)(rng.NextDouble() * 20 - 10);
            float y = (float)(rng.NextDouble() * 4 + 0.5f);
            float s = (float)(rng.NextDouble() * 0.06f + 0.02f);
            float a = (float)(rng.NextDouble() * 0.6f + 0.3f);
            CreateWorldCircle($"Star{i}", new Vector3(x, y, 3.5f), s, new Color(1f, 1f, 1f, a), -15);
        }

        // 탑 3개 (왼쪽, 중앙 오른쪽, 오른쪽)
        DrawTower(-7.5f, -1.8f, new Color(0.20f, 0.25f, 0.35f), new Color(0.30f, 0.15f, 0.05f));
        DrawTower(-3.5f, -1.4f, new Color(0.18f, 0.23f, 0.32f), new Color(0.28f, 0.13f, 0.04f));
        DrawTower(6.5f,  -1.6f, new Color(0.22f, 0.27f, 0.37f), new Color(0.32f, 0.17f, 0.06f));

        // 나무들
        DrawTree(-9f,  -2.2f);
        DrawTree(-5.5f,-2.4f);
        DrawTree(4f,   -2.3f);
        DrawTree(8.5f, -2.1f);

        // 횃불 빛 효과
        CreateWorldCircle("Torch1Glow", new Vector3(-7.5f, -0.8f, 2f), 0.5f, new Color(1f, 0.5f, 0.1f, 0.25f), -14);
        CreateWorldCircle("Torch2Glow", new Vector3(-3.5f, -0.4f, 2f), 0.5f, new Color(1f, 0.5f, 0.1f, 0.25f), -14);
        CreateWorldCircle("Torch3Glow", new Vector3(6.5f,  -0.6f, 2f), 0.5f, new Color(1f, 0.5f, 0.1f, 0.25f), -14);
        CreateWorldCircle("Torch1",     new Vector3(-7.5f, -0.8f, 1.9f), 0.08f, new Color(1f, 0.7f, 0.2f), -13);
        CreateWorldCircle("Torch2",     new Vector3(-3.5f, -0.4f, 1.9f), 0.08f, new Color(1f, 0.7f, 0.2f), -13);
        CreateWorldCircle("Torch3",     new Vector3(6.5f,  -0.6f, 1.9f), 0.08f, new Color(1f, 0.7f, 0.2f), -13);

        // 안개 느낌 (하단 반투명)
        CreateWorldQuad("Fog", new Vector3(0, -2.8f, 2f), new Vector3(22f, 1.5f), new Color(0.4f, 0.55f, 0.45f, 0.18f), -12);
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
    void CreateWorldQuad(string name, Vector3 pos, Vector3 scale, Color color, int order)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeSquareSprite();
        sr.color  = color;
        sr.sortingOrder = order;
        go.transform.position   = pos;
        go.transform.localScale = scale;
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

        // 상단 반투명 어두운 오버레이 (제목 읽기 쉽도록)
        CreateImg(cgo.transform, new Color(0f, 0f, 0f, 0.35f), new Vector2(0, 160), new Vector2(1280, 280));

        // 제목 장식선
        CreateImg(cgo.transform, new Color(1f, 0.85f, 0.2f, 0.8f), new Vector2(0, 90), new Vector2(520, 3));

        // 제목 그림자 + 본문
        CreateTxt(cgo.transform, "랜 덤 오 펜 스", COL_TITLE_SH, new Vector2(5, 45),  new Vector2(720, 100), 76);
        CreateTxt(cgo.transform, "랜 덤 오 펜 스", COL_TITLE,    new Vector2(0, 50),  new Vector2(720, 100), 76);
        CreateTxt(cgo.transform, "RANDOM OFFENSE", new Color(1f, 0.95f, 0.6f), new Vector2(0, -20), new Vector2(600, 55), 34);

        // 하단 장식선
        CreateImg(cgo.transform, new Color(1f, 0.85f, 0.2f, 0.8f), new Vector2(0, -55), new Vector2(520, 3));

        // 버튼 패널 배경
        CreateImg(cgo.transform, new Color(0f, 0f, 0f, 0.45f), new Vector2(0, -215), new Vector2(320, 330));

        // 버튼 4개
        RegBtn(cgo.transform, "start", "▶  시  작", new Vector2(0, -115), new Vector2(260, 54), COL_BTN_START,  COL_BTN_S_HOV,  OnStart);
        RegBtn(cgo.transform, "gem",   "◆  보  석", new Vector2(0, -180), new Vector2(260, 54), COL_BTN_GEM,    COL_BTN_GEM_H,  OnGemMenu);
        RegBtn(cgo.transform, "set",   "⚙  설  정", new Vector2(0, -245), new Vector2(260, 54), COL_BTN_SET,    COL_BTN_SET_H,  OnSettings);
        RegBtn(cgo.transform, "exit",  "✕  종  료", new Vector2(0, -310), new Vector2(260, 54), COL_BTN_EXIT,   COL_BTN_EXIT_H, OnExit);

        // 버전
        CreateTxt(cgo.transform, "v0.1 Alpha", new Color(1f, 1f, 1f, 0.3f), new Vector2(560, -330), new Vector2(160, 28), 15);

        BuildGemPanel(cgo.transform);
        BuildSettingsPanel(cgo.transform);
    }

    void RegBtn(Transform p, string id, string label, Vector2 pos, Vector2 size, Color n, Color h, System.Action cb)
    {
        var go = new GameObject("Btn_" + id); go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);
        var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = size + new Vector2(4, 4);

        var inner = new GameObject("Fill"); inner.transform.SetParent(go.transform, false);
        var fi = inner.AddComponent<Image>(); fi.color = n;
        var ir = inner.GetComponent<RectTransform>(); ir.anchoredPosition = Vector2.zero; ir.sizeDelta = size;

        var tg = new GameObject("Lbl"); tg.transform.SetParent(inner.transform, false);
        var tx = tg.AddComponent<Text>(); tx.text = label; tx.color = Color.white; tx.fontSize = 26;
        tx.alignment = TextAnchor.MiddleCenter; tx.fontStyle = FontStyle.Bold;
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var tr = tg.GetComponent<RectTransform>(); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = size;

        btns.Add(new BtnData { rt = ir, fill = fi, normal = n, hover = h, action = cb });
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

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(row.transform, false);
            var icon = iconGo.AddComponent<Image>();
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchoredPosition = new Vector2(-280, y);
            iconRt.sizeDelta = new Vector2(62, 62);

            var title = CreateTxtReturn(row.transform, "", Color.white, new Vector2(-140, y + 18), new Vector2(300, 28), 23);
            title.alignment = TextAnchor.MiddleLeft;
            var desc = CreateTxtReturn(row.transform, "", new Color(0.84f, 0.88f, 0.92f), new Vector2(-110, y - 15), new Vector2(380, 40), 17);
            desc.alignment = TextAnchor.MiddleLeft;
            var status = CreateTxtReturn(row.transform, "", COL_TITLE, new Vector2(170, y), new Vector2(150, 28), 20);

            gemEntryUis.Add(new GemEntryUi
            {
                icon = icon,
                title = title,
                desc = desc,
                status = status
            });

            int capturedStage = defs[i].stageIndex;
            RegBtn(row.transform, $"gem_toggle_{capturedStage}", "",
                new Vector2(280, y), new Vector2(130, 48),
                defs[i].color * 0.85f, defs[i].color,
                () => ToggleGem(capturedStage));
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

            ui.icon.sprite = MakeCircleSprite(32);
            ui.icon.color = unlocked ? def.color : new Color(0.35f, 0.38f, 0.45f, 0.95f);
            ui.title.text = $"STAGE {def.stageIndex}  {def.gemName}";
            ui.desc.text = unlocked ? def.effectSummary : "아직 해금되지 않았습니다";
            ui.status.text = !unlocked ? "잠김" : (active ? "활성" : "비활성");
            ui.status.color = !unlocked
                ? new Color(0.65f, 0.68f, 0.74f)
                : (active ? new Color(0.42f, 1f, 0.52f) : new Color(1f, 0.85f, 0.35f));
        }
    }

    void CreateImg(Transform p, Color c, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Img"); go.transform.SetParent(p, false);
        go.AddComponent<Image>().color = c;
        var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    void CreateTxt(Transform p, string s, Color c, Vector2 pos, Vector2 size, int fs)
    {
        var go = new GameObject("Txt"); go.transform.SetParent(p, false);
        var tx = go.AddComponent<Text>(); tx.text = s; tx.color = c; tx.fontSize = fs;
        tx.alignment = TextAnchor.MiddleCenter; tx.fontStyle = FontStyle.Bold;
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    Text CreateTxtReturn(Transform p, string s, Color c, Vector2 pos, Vector2 size, int fs)
    {
        var go = new GameObject("Txt"); go.transform.SetParent(p, false);
        var tx = go.AddComponent<Text>(); tx.text = s; tx.color = c; tx.fontSize = fs;
        tx.alignment = TextAnchor.MiddleCenter; tx.fontStyle = FontStyle.Bold;
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = size;
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

        CreateTxt(settingsPanel.transform, "설  정", COL_TITLE, new Vector2(0, 148), new Vector2(400, 50), 36);
        CreateImg(settingsPanel.transform, new Color(1f, 1f, 1f, 0.15f), new Vector2(0, 116), new Vector2(500, 2));

        // 볼륨 행
        CreateTxt(settingsPanel.transform, "마스터 볼륨", new Color(0.85f, 0.88f, 0.94f), new Vector2(-95, 62), new Vector2(190, 36), 22);
        settingsVolumeLbl = CreateTxtReturn(settingsPanel.transform, VolumePct(), COL_TITLE, new Vector2(130, 62), new Vector2(120, 36), 22);
        RegBtn(settingsPanel.transform, "vol_dn", "−", new Vector2(-42, 15), new Vector2(52, 44), COL_BTN_SET, COL_BTN_SET_H, () => AdjustVolume(-0.1f));
        RegBtn(settingsPanel.transform, "vol_up", "+", new Vector2(42,  15), new Vector2(52, 44), COL_BTN_SET, COL_BTN_SET_H, () => AdjustVolume(+0.1f));

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

    void AdjustVolume(float delta)
    {
        SettingsManager.Volume = SettingsManager.Volume + delta;
        if (settingsVolumeLbl != null) settingsVolumeLbl.text = VolumePct();
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
}
