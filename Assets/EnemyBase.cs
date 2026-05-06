using UnityEngine;
using System.Collections;

public class EnemyBase : MonoBehaviour
{
    [Header("Stats")]
    public string enemyName    = "Enemy";
    public float  attackRange  = 3f;
    public float  attackDamage = 10f;
    public float  attackCooldown = 1.5f;

    [Header("State")]
    public bool isPlaced = false;

    protected float      attackTimer   = 0f;
    protected AllyBase   currentTarget = null;
    protected SpriteRenderer spriteRenderer;

    // ── 프레임 애니메이션 ──────────────────────────────────────────────
    protected Sprite   idleSprite;
    protected Sprite[] attackFrameSprites;
    protected bool     isPlayingAttackAnim;

    private GameObject rangeIndicatorRoot;
    private LineRenderer rangeIndicator;
    private SpriteRenderer rangeFillIndicator;
    private LineRenderer attackLine;
    private float paralysisTimer;
    private GameObject paralysisVisual;

    // ── 숨쉬기 애니메이션 ──────────────────────────────────────────────
    private float     breathCycle;
    private Vector3   breathBasePosition;
    private Vector3   breathBaseScale;

    protected virtual void Awake()
    {
        spriteRenderer     = GetComponent<SpriteRenderer>();
        breathBasePosition = transform.position;
        breathBaseScale    = transform.localScale;
        CreateAttackLine();
    }

    protected virtual void Update()
    {
        UpdateBreathAnimation();
        UpdateRangeIndicatorPosition();

        if (!isPlaced) return;
        if (paralysisTimer > 0f)
        {
            paralysisTimer -= Time.deltaTime;
            UpdateParalysisVisual();
            if (paralysisTimer <= 0f)
                ClearParalysisVisual();
            return;
        }
        attackTimer -= Time.deltaTime;

        if (currentTarget == null || currentTarget.isDead)
            currentTarget = FindNearestAlly();

        if (currentTarget != null && attackTimer <= 0f)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (dist <= attackRange) { Attack(currentTarget); attackTimer = attackCooldown; }
            else currentTarget = null;
        }
    }

    protected virtual void OnDestroy()
    {
        if (rangeIndicatorRoot != null)
            Destroy(rangeIndicatorRoot);
        ClearParalysisVisual();
    }

    protected virtual void Attack(AllyBase target)
    {
        if (target == null || target.isDead) return;
        target.TakeDamage(attackDamage);
        StartCoroutine(ShowAttackEffect(target));
    }

    protected virtual IEnumerator ShowAttackEffect(AllyBase target)
    {
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = Color.yellow;
            yield return new WaitForSeconds(0.06f);
            spriteRenderer.color = orig;
        }
        if (attackLine != null && target != null)
        {
            attackLine.gameObject.SetActive(true);
            attackLine.SetPosition(0, transform.position);
            attackLine.SetPosition(1, target.transform.position);
            yield return new WaitForSeconds(0.12f);
            attackLine.gameObject.SetActive(false);
        }
    }

    // ── 공격 이펙트 헬퍼 (서브클래스 공용) ─────────────────────────────
    protected void SpawnImpactFlash(Vector3 pos, Color color, float duration)
        => StartCoroutine(ImpactFlashRoutine(pos, color, duration));

    IEnumerator ImpactFlashRoutine(Vector3 pos, Color color, float duration)
    {
        var go = new GameObject("ImpactFlash");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite(8);
        sr.color = color;
        sr.sortingOrder = 32;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 0.55f, t);
            Color c = color; c.a = 1f - t;
            sr.color = c;
            yield return null;
        }
        Destroy(go);
    }

    protected static Sprite MakeCircleSprite(int radius)
    {
        int size = radius * 2 + 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var clear = new Color(0, 0, 0, 0);
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
            tex.SetPixel(x, y, clear);
        int cx = size / 2, cy = size / 2;
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
            if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                tex.SetPixel(x, y, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    protected static LineRenderer CreateTempLineRenderer(int pointCount, Color color, float width, int sortOrder)
    {
        var go = new GameObject("TempLR");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace   = true;
        lr.positionCount   = pointCount;
        lr.widthMultiplier = width;
        lr.sortingOrder    = sortOrder;
        lr.startColor      = color;
        lr.endColor        = new Color(color.r, color.g, color.b, 0.2f);
        lr.material        = new Material(Shader.Find("Sprites/Default"));
        return lr;
    }

    protected static Vector3[] BuildLightningPath(Vector3 from, Vector3 to, int segments, float maxOffset)
    {
        var pts = new Vector3[segments + 1];
        pts[0]        = from;
        pts[segments] = to;
        Vector3 d    = (to - from).normalized;
        Vector3 perp = new Vector3(-d.y, d.x, 0f);
        for (int i = 1; i < segments; i++)
        {
            float t = (float)i / segments;
            pts[i] = Vector3.Lerp(from, to, t) + perp * Random.Range(-maxOffset, maxOffset);
        }
        return pts;
    }

    // ── 프레임 애니메이션 API ──────────────────────────────────────────
    /// <summary>idle 스프라이트와 공격 프레임 배열을 등록하고 idle 표시</summary>
    protected void SetupSpriteAnimation(Sprite idle, Sprite[] attackFrames)
    {
        idleSprite         = idle;
        attackFrameSprites = attackFrames;
        if (spriteRenderer != null && idle != null)
            spriteRenderer.sprite = idle;
    }

    /// <summary>공격 프레임을 fps 속도로 재생 후 idle로 복귀</summary>
    protected IEnumerator PlayAttackAnim(float fps = 10f)
    {
        if (attackFrameSprites == null || attackFrameSprites.Length == 0
            || spriteRenderer == null || isPlayingAttackAnim) yield break;

        isPlayingAttackAnim = true;
        float delay = 1f / fps;
        foreach (var frame in attackFrameSprites)
        {
            if (frame != null) spriteRenderer.sprite = frame;
            yield return new WaitForSeconds(delay);
        }
        if (idleSprite != null) spriteRenderer.sprite = idleSprite;
        isPlayingAttackAnim = false;
    }

    // ── 재사용 가능한 파라메트릭 이펙트 코루틴 ────────────────────────────

    /// <summary>부채꼴 파티클 분사. Brawler 계열 전용.</summary>
    protected IEnumerator ParticleSprayEffect(AllyBase target, Color colorA, Color colorB,
        int count = 12, float duration = 0.38f, float spreadDeg = 30f)
    {
        if (target == null) yield break;
        Vector3 src  = transform.position + Vector3.up * 0.12f;
        Vector3 dst  = target.transform.position;
        Vector3 dir  = (dst - src).normalized;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        float dist   = Vector3.Distance(src, dst);

        Sprite spr = MakeCircleSprite(7);
        var gos    = new GameObject[count];
        var speeds = new float[count];
        var sizes  = new float[count];
        var pdirs  = new Vector3[count];
        var cols   = new Color[count];

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("SprayP");
            go.transform.position = src + dir * (dist * Random.Range(0f, 0.18f));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            Color c = Color.Lerp(colorA, colorB, Random.value); c.a = 1f;
            sr.color = c; cols[i] = c;
            sr.sortingOrder = 30;
            float ang = Random.Range(-spreadDeg, spreadDeg) * Mathf.Deg2Rad;
            pdirs[i]  = (dir * Mathf.Cos(ang) + perp * Mathf.Sin(ang)).normalized;
            speeds[i] = Random.Range(4f, 9f);
            sizes[i]  = Random.Range(0.10f, 0.26f);
            go.transform.localScale = Vector3.one * sizes[i];
            gos[i] = go;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            for (int i = 0; i < count; i++)
            {
                if (gos[i] == null) continue;
                gos[i].transform.position += pdirs[i] * speeds[i] * Time.deltaTime;
                var sr = gos[i].GetComponent<SpriteRenderer>();
                if (sr != null) { Color c = cols[i]; c.a = 1f - t * t; sr.color = c; }
                gos[i].transform.localScale = Vector3.one * (sizes[i] * Mathf.Lerp(1.2f, 0.2f, t));
            }
            yield return null;
        }
        for (int i = 0; i < count; i++)
            if (gos[i] != null) Destroy(gos[i]);
    }

    /// <summary>지그재그 번개. Spearman 계열 전용.</summary>
    protected IEnumerator ZigzagEffect(AllyBase target, Color boltColor, Color impactColor,
        int flashCount = 3, float offsetMax = 0.30f)
    {
        if (target == null) yield break;
        Vector3 src = transform.position;
        Vector3 dst = target.transform.position;

        var lr = CreateTempLineRenderer(10, boltColor, 0.07f, 28);
        lr.endColor = new Color(boltColor.r * 0.8f, boltColor.g * 0.8f, boltColor.b * 0.8f, 0.25f);

        for (int flash = 0; flash < flashCount; flash++)
        {
            Vector3[] pts = BuildLightningPath(src, dst, 9, offsetMax);
            for (int i = 0; i < pts.Length; i++) lr.SetPosition(i, pts[i]);
            lr.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.045f);
            lr.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.030f);
        }
        Destroy(lr.gameObject);
        SpawnImpactFlash(dst, impactColor, 0.18f);
    }

    /// <summary>투사체 이동. Sniper 계열(자연/사막) 전용.</summary>
    protected IEnumerator ProjectileEffect(AllyBase target, Color color,
        float speed = 16f, Color impactColor = default)
    {
        if (target == null) yield break;
        Vector3 src = transform.position;
        Vector3 dst = target.transform.position;
        float dist  = Vector3.Distance(src, dst);

        var go = new GameObject("Proj");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite(6);
        sr.color = color;
        sr.sortingOrder = 30;
        go.transform.position = src;
        go.transform.localScale = Vector3.one * 0.16f;

        float elapsed = 0f, maxTime = dist / speed;
        while (elapsed < maxTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Min(elapsed / maxTime, 1f);
            go.transform.position = Vector3.Lerp(src, dst, t);
            if (t >= 1f) break;
            yield return null;
        }
        Destroy(go);
        SpawnImpactFlash(dst, impactColor.a > 0f ? impactColor : color, 0.20f);
    }

    /// <summary>얇은 빔 페이드. Sniper 계열(화산/어둠/요새) 전용.</summary>
    protected IEnumerator ThinBeamEffect(AllyBase target, Color beamColor,
        Color impactColor, float duration = 0.20f)
    {
        if (target == null) yield break;
        Vector3 src = transform.position;
        Vector3 dst = target.transform.position;

        var lr = CreateTempLineRenderer(2, beamColor, 0.035f, 28);
        lr.SetPosition(0, src);
        lr.SetPosition(1, dst);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float a = 1f - t * t;
            lr.startColor = new Color(beamColor.r, beamColor.g, beamColor.b, a);
            lr.endColor   = new Color(beamColor.r * 0.6f, beamColor.g * 0.6f, beamColor.b * 0.6f, a * 0.5f);
            yield return null;
        }
        Destroy(lr.gameObject);
        SpawnImpactFlash(dst, impactColor, 0.22f);
    }

    void CreateAttackLine()
    {
        var go = new GameObject("AttackLine");
        go.transform.SetParent(transform, false);
        attackLine = go.AddComponent<LineRenderer>();
        attackLine.useWorldSpace   = true;
        attackLine.positionCount   = 2;
        attackLine.widthMultiplier = 0.06f;
        attackLine.sortingOrder    = 25;
        Color c = AttackLineColor();
        attackLine.startColor = c;
        attackLine.endColor   = new Color(c.r, c.g, c.b, 0f);
        attackLine.material   = new Material(Shader.Find("Sprites/Default"));
        attackLine.gameObject.SetActive(false);
    }

    protected virtual Color AttackLineColor() => new Color(1f, 0.8f, 0.1f);

    AllyBase FindNearestAlly()
    {
        AllyBase[] allies = FindObjectsByType<AllyBase>(FindObjectsSortMode.None);
        AllyBase nearest  = null;
        float minDist = attackRange;
        foreach (var ally in allies)
        {
            if (ally == null || ally.isDead) continue;
            float d = Vector3.Distance(transform.position, ally.transform.position);
            if (d < minDist) { minDist = d; nearest = ally; }
        }
        return nearest;
    }

    // ── 배치 메서드 ────────────────────────────────────────────────

    /// <summary>활성 배치 (즉시 공격 시작)</summary>
    public virtual void Place(Vector3 worldPos)
    {
        transform.position = worldPos;
        breathBasePosition = worldPos;
        isPlaced = true;
        HideRange();
    }

    /// <summary>비활성 배치 (경로 그리기 단계 - 공격 안 함)</summary>
    public virtual void PlaceInactive(Vector3 worldPos)
    {
        transform.position = worldPos;
        breathBasePosition = worldPos;
        isPlaced = false;
        HideRange();
    }

    /// <summary>게임 시작 시 활성화</summary>
    public virtual void Activate()
    {
        isPlaced = true;
        HideRange(); // 게임 시작하면 사거리 원 숨김
    }

    // ── 사거리 표시 ────────────────────────────────────────────────
    public void ShowRange()
    {
        if (rangeIndicatorRoot == null) rangeIndicatorRoot = CreateRangeIndicatorRoot();
        if (rangeIndicator == null) rangeIndicator = CreateRangeIndicator();
        if (rangeFillIndicator == null) rangeFillIndicator = CreateRangeFillIndicator();
        UpdateRangeIndicatorPosition();
        rangeIndicatorRoot.SetActive(true);
        rangeFillIndicator.gameObject.SetActive(true);
        rangeIndicator.gameObject.SetActive(true);
    }

    public void HideRange()
    {
        if (rangeIndicatorRoot != null)
            rangeIndicatorRoot.SetActive(false);
        if (rangeFillIndicator != null)
            rangeFillIndicator.gameObject.SetActive(false);
        if (rangeIndicator != null)
            rangeIndicator.gameObject.SetActive(false);
    }

    GameObject CreateRangeIndicatorRoot()
    {
        var go = new GameObject($"{enemyName}_RangeIndicatorRoot");
        go.transform.localScale = Vector3.one;
        return go;
    }

    void UpdateRangeIndicatorPosition()
    {
        if (rangeIndicatorRoot == null || !rangeIndicatorRoot.activeSelf) return;
        rangeIndicatorRoot.transform.position = breathBasePosition;
    }

    SpriteRenderer CreateRangeFillIndicator()
    {
        var go = new GameObject("RangeFillIndicator");
        go.transform.SetParent(rangeIndicatorRoot.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one * attackRange * 2f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite(32);
        sr.color = new Color(1f, 0.86f, 0f, 0.22f);
        sr.sortingOrder = 9;
        return sr;
    }

    LineRenderer CreateRangeIndicator()
    {
        var go = new GameObject("RangeIndicator");
        go.transform.SetParent(rangeIndicatorRoot.transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace   = false;
        lr.loop            = true;
        lr.widthMultiplier = 0.07f;
        lr.sortingOrder    = 20;
        int seg = 40;
        lr.positionCount = seg;
        for (int i = 0; i < seg; i++)
        {
            float a = (float)i / seg * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * attackRange, Mathf.Sin(a) * attackRange, 0f));
        }
        Color rc = new Color(1f, 0.86f, 0f, 0.95f);
        lr.startColor = rc; lr.endColor = rc;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.material.color = rc;
        return lr;
    }

    protected virtual Color RangeColor() => new Color(1f, 0.86f, 0f, 0.95f);

    public float GetClickRadius()
    {
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            var ext = spriteRenderer.bounds.extents;
            return Mathf.Max(ext.x, ext.y) + 0.15f;
        }
        return 0.55f;
    }

    public string GetInspectionText()
    {
        return
            $"사거리: {attackRange:0.0}\n" +
            $"공격력: {attackDamage:0.0}\n" +
            $"공격 속도: {attackCooldown:0.0}초";
    }

    public void ApplyParalysis(float duration)
    {
        paralysisTimer = Mathf.Max(paralysisTimer, duration);
        attackTimer = Mathf.Max(attackTimer, attackCooldown);
        CreateParalysisVisual();
    }

    void CreateParalysisVisual()
    {
        if (paralysisVisual != null) return;
        paralysisVisual = new GameObject("ParalysisVisual");
        paralysisVisual.transform.SetParent(transform, false);
        paralysisVisual.transform.localPosition = new Vector3(0f, 0.65f, 0f);

        var sr = paralysisVisual.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite(14);
        sr.color = new Color(0.55f, 1f, 0.35f, 0.72f);
        sr.sortingOrder = 36;
        paralysisVisual.transform.localScale = Vector3.one * 0.42f;
    }

    void UpdateParalysisVisual()
    {
        if (paralysisVisual == null) return;
        paralysisVisual.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 180f);
        float pulse = 0.38f + Mathf.Sin(Time.time * 10f) * 0.06f;
        paralysisVisual.transform.localScale = Vector3.one * pulse;
    }

    void ClearParalysisVisual()
    {
        if (paralysisVisual == null) return;
        Destroy(paralysisVisual);
        paralysisVisual = null;
    }

    // ── 숨쉬기 애니메이션 ──────────────────────────────────────────────
    void UpdateBreathAnimation()
    {
        breathCycle += Time.deltaTime;

        // 흡기: Y 늘어남 + X 줄어듦 (자연스러운 숨결)
        float inhale = Mathf.Sin(breathCycle * 1.4f);   // ~0.22 Hz
        // 상하 부유 (흡기보다 살짝 느린 주기)
        float floatY = Mathf.Sin(breathCycle * 0.9f) * 0.024f;

        transform.localScale = new Vector3(
            breathBaseScale.x * (1f - inhale * 0.018f),
            breathBaseScale.y * (1f + inhale * 0.030f),
            breathBaseScale.z);

        transform.position = breathBasePosition + Vector3.up * floatY;
    }
}
