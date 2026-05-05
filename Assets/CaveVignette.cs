using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stage 2 (어둠의 동굴) 전용 비네트 효과.
/// 화면 정중앙을 기준으로 타원형 밝은 영역을 남기고
/// 가장자리로 갈수록 점점 어둡게 만드는 오버레이를 생성합니다.
///
/// GameManager.InitStageHazards()에서 생성합니다.
/// </summary>
public class CaveVignette : MonoBehaviour
{
    // ── 설정 ─────────────────────────────────────────────────────────
    [Tooltip("타원 X 반경 (0~1, 화면 반폭 기준)")]
    public float radiusX = 0.48f;   // 가로 반경 (화면 절반보다 약간 좁게)
    [Tooltip("타원 Y 반경 (0~1, 화면 반높이 기준)")]
    public float radiusY = 0.40f;   // 세로 반경
    [Tooltip("어두워지기 시작하는 가장자리 페이드 폭")]
    public float fadeWidth = 0.30f;
    [Tooltip("가장자리 최대 어두움 (0=완전투명, 1=완전검정)")]
    public float maxAlpha = 0.96f;
    [Tooltip("비네트 텍스처 해상도 (작을수록 성능↑, 128 권장)")]
    public int texResolution = 128;

    // ── 내부 ─────────────────────────────────────────────────────────
    private RawImage vignetteImage;
    private Texture2D vignetteTex;

    void Awake()
    {
        BuildOverlay();
    }

    void OnDestroy()
    {
        if (vignetteTex != null)
            Destroy(vignetteTex);
    }

    // ── 오버레이 구성 ────────────────────────────────────────────────

    void BuildOverlay()
    {
        // ── Canvas (Screen Space - Overlay, 최상위) ──────────────────
        var canvasGo = new GameObject("CaveVignetteCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;  // 모든 UI 위에 표시

        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // ── RawImage (전체 화면 채우기) ──────────────────────────────
        var imgGo = new GameObject("VignetteImage");
        imgGo.transform.SetParent(canvasGo.transform, false);

        vignetteImage = imgGo.AddComponent<RawImage>();
        vignetteImage.raycastTarget = false;  // 입력 차단 안 함

        var rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // ── 비네트 텍스처 생성 ───────────────────────────────────────
        vignetteTex = GenerateVignetteTexture(texResolution, texResolution);
        vignetteImage.texture = vignetteTex;
    }

    /// <summary>
    /// 타원형 그라디언트 비네트 텍스처를 절차적으로 생성합니다.
    /// 중심 = 투명, 외곽 = 검정
    /// </summary>
    Texture2D GenerateVignetteTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            // 정규화 좌표 (-1 ~ +1)
            float nx = (float)x / (w - 1) * 2f - 1f;
            float ny = (float)y / (h - 1) * 2f - 1f;

            // 타원 거리 (가로/세로 반경으로 나눔)
            float ex = nx / radiusX;
            float ey = ny / radiusY;
            float d  = Mathf.Sqrt(ex * ex + ey * ey);  // 0=중심, 1=타원 경계

            // 경계 바깥쪽 fade 계산
            // d < 1 : 타원 내부 (밝음)
            // d > 1 : 타원 외부 (어두움)
            float fadeStart = 1f - fadeWidth;
            float t = Mathf.Clamp01((d - fadeStart) / fadeWidth);
            t = t * t * (3f - 2f * t);  // smoothstep

            float alpha = t * maxAlpha;
            tex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
        }

        tex.Apply();
        return tex;
    }

    // ── 런타임 조정 API ──────────────────────────────────────────────

    /// <summary>비네트 가시성 켜기/끄기</summary>
    public void SetVisible(bool visible)
    {
        if (vignetteImage != null)
            vignetteImage.enabled = visible;
    }

    /// <summary>비네트 강도 조정 (0=없음, 1=최대)</summary>
    public void SetIntensity(float intensity)
    {
        intensity = Mathf.Clamp01(intensity);
        if (vignetteImage != null)
            vignetteImage.color = new Color(1f, 1f, 1f, intensity);
    }
}
