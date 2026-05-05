using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class EnemyInspector : MonoBehaviour
{
    public static EnemyInspector Instance { get; private set; }

    private Canvas panelCanvas;
    private GameObject panelRoot;
    private Text titleText;
    private Text bodyText;
    private EnemyBase selectedEnemy;
    readonly List<RaycastResult> uiHits = new();

    static readonly Color COL_PANEL = new Color(0.05f, 0.06f, 0.10f, 0.92f);
    static readonly Color COL_BORDER = new Color(1f, 0.82f, 0.20f, 0.32f);
    static readonly Color COL_TITLE = new Color(1f, 0.88f, 0.35f, 1f);
    static readonly Color COL_TEXT = new Color(0.92f, 0.94f, 0.98f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<EnemyInspector>() != null) return;

        var go = new GameObject("EnemyInspector");
        go.AddComponent<EnemyInspector>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HideSelection();
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        bool blocked = GameManager.Instance != null && GameManager.Instance.ShouldBlockGameplayInput();
        if (blocked)
        {
            HideSelection();
            return;
        }

        Vector2 screenPos = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (IsPointerOverBlockingUi(screenPos)) return;

            var hitEnemy = FindEnemyAtScreenPosition(screenPos);
            if (hitEnemy != null) SelectEnemy(hitEnemy);
            else HideSelection();
        }

        if (selectedEnemy == null || !selectedEnemy.gameObject.activeInHierarchy)
        {
            HideSelection();
            return;
        }

        RefreshPanel();
    }

    public bool IsMouseOverEnemy(Vector2 screenPos)
    {
        if (IsPointerOverBlockingUi(screenPos)) return false;
        return FindEnemyAtScreenPosition(screenPos) != null;
    }

    void SelectEnemy(EnemyBase enemy)
    {
        if (enemy == null) return;
        if (selectedEnemy == enemy)
        {
            HideSelection();
            return;
        }

        if (selectedEnemy != null && selectedEnemy != enemy)
            RestoreDefaultRangeVisibility(selectedEnemy);

        selectedEnemy = enemy;
        selectedEnemy.ShowRange();
        RefreshPanel();
        panelRoot.SetActive(true);
    }

    void HideSelection()
    {
        if (selectedEnemy != null)
            RestoreDefaultRangeVisibility(selectedEnemy);

        selectedEnemy = null;
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    void RestoreDefaultRangeVisibility(EnemyBase enemy)
    {
        if (enemy == null) return;
        enemy.HideRange();
    }

    EnemyBase FindEnemyAtScreenPosition(Vector2 screenPos)
    {
        if (Camera.main == null) return null;

        Vector3 world = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
        world.z = 0f;

        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        EnemyBase bestEnemy = null;
        float bestDist = float.MaxValue;

        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            Vector3 pos = enemy.transform.position;
            pos.z = 0f;
            float dist = Vector2.Distance(world, pos);
            if (dist > Mathf.Max(enemy.GetClickRadius(), 0.9f)) continue;
            if (dist < bestDist)
            {
                bestDist = dist;
                bestEnemy = enemy;
            }
        }

        return bestEnemy;
    }

    bool IsPointerOverBlockingUi(Vector2 screenPos)
    {
        var allyPanel = FindFirstObjectByType<AllyOrderPanel>();
        if (allyPanel != null && allyPanel.IsMouseOverPanel(screenPos))
            return true;

        if (EventSystem.current == null)
            return false;

        var pointer = new PointerEventData(EventSystem.current)
        {
            position = screenPos
        };

        uiHits.Clear();
        EventSystem.current.RaycastAll(pointer, uiHits);
        foreach (var hit in uiHits)
        {
            if (hit.gameObject == null)
                continue;

            if (IsBlockingUiElement(hit.gameObject))
                return true;
        }

        return false;
    }

    bool IsBlockingUiElement(GameObject go)
    {
        if (go == null || !go.activeInHierarchy)
            return false;

        if (go.GetComponentInParent<Selectable>() != null)
            return true;

        for (Transform t = go.transform; t != null; t = t.parent)
        {
            string n = t.name;
            if (n.Contains("Panel") || n.Contains("Popup") || n.Contains("Overlay") ||
                n.Contains("Window") || n.Contains("Dialog") || n.Contains("Modal") ||
                n.Contains("Button") || n.Contains("Btn") || n.Contains("Toggle") ||
                n.Contains("Card") || n.Contains("Slot") || n.Contains("Detail") ||
                n.Contains("Settings") || n.Contains("Pause") || n.Contains("Result"))
                return true;

            if (t.GetComponent<Canvas>() != null)
                break;
        }

        return false;
    }

    void RefreshPanel()
    {
        if (selectedEnemy == null) return;

        titleText.text = selectedEnemy.enemyName;
        bodyText.text = selectedEnemy.GetInspectionText();
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("EnemyInspectorCanvas");
        canvasGo.transform.SetParent(transform, false);
        panelCanvas = canvasGo.AddComponent<Canvas>();
        panelCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        panelCanvas.sortingOrder = 65;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        panelRoot = CreateRect("EnemyInfoPanel", canvasGo.transform);
        var panelRt = panelRoot.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 1f);
        panelRt.anchorMax = new Vector2(1f, 1f);
        panelRt.pivot = new Vector2(1f, 1f);
        panelRt.anchoredPosition = new Vector2(-18f, -72f);
        panelRt.sizeDelta = new Vector2(220f, 150f);
        panelRoot.AddComponent<Image>().color = COL_PANEL;

        var border = CreateRect("Border", panelRoot.transform);
        var borderRt = border.GetComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(-2f, -2f);
        borderRt.offsetMax = new Vector2(2f, 2f);
        border.AddComponent<Image>().color = COL_BORDER;
        border.transform.SetAsFirstSibling();

        titleText = CreateText(
            "Title",
            panelRoot.transform,
            string.Empty,
            COL_TITLE,
            22,
            FontStyle.Bold,
            TextAnchor.UpperLeft,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(14f, -42f),
            new Vector2(-14f, -10f));

        bodyText = CreateText(
            "Body",
            panelRoot.transform,
            string.Empty,
            COL_TEXT,
            17,
            FontStyle.Normal,
            TextAnchor.UpperLeft,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(14f, 14f),
            new Vector2(-14f, -44f));

        panelRoot.SetActive(false);
    }

    GameObject CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    Text CreateText(
        string name,
        Transform parent,
        string content,
        Color color,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor anchor,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        var go = CreateRect(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var text = go.AddComponent<Text>();
        text.text = content;
        text.color = color;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = anchor;
        text.font = UiPixelFont.Get();
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.alignByGeometry = true;
        text.raycastTarget = false;
        return text;
    }
}
