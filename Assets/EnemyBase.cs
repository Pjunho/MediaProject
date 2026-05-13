using UnityEngine;
using System.Collections;

public class EnemyBase : MonoBehaviour
{
    static readonly System.Collections.Generic.Dictionary<string, Sprite[]> effectFrameCache = new();

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
    private SpriteRenderer paralysisVisualRenderer;
    private Sprite[] paralysisEffectFrames;
    private float _paralysisNormScale = 1f;
    private GameObject paralysisTintVisual;
    private SpriteRenderer paralysisTintRenderer;

    // ── 숨쉬기 애니메이션 ──────────────────────────────────────────────
    private float     breathCycle;
    private Vector3   breathBasePosition;
    protected Vector3 breathBaseScale;

    // Stage 1 SheetEnemyBase 기준 크기 (64px / 44ppu)
    protected const float EnemyStage1Height = 64f / 44f;

    protected bool IsParalyzed => paralysisTimer > 0f;

    protected virtual void Awake()
    {
        spriteRenderer     = GetComponent<SpriteRenderer>();
        breathBasePosition = transform.position;
        breathBaseScale    = transform.localScale;
        CreateAttackLine();
    }

    protected virtual void Update()
    {
        UpdateRangeIndicatorPosition();

        if (IsParalyzed)
        {
            paralysisTimer -= Time.deltaTime;
            UpdateParalysisVisual();
            if (paralysisTimer <= 0f)
                ClearParalysisVisual();
            return;
        }

        UpdateBreathAnimation();

        if (!isPlaced) return;
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
        StartCoroutine(AttackRoutine(target));
    }

    IEnumerator AttackRoutine(AllyBase target)
    {
        yield return StartCoroutine(ShowAttackEffect(target));
        if (IsParalyzed) yield break;
        if (target != null && !target.isDead)
        {
            target.TakeDamage(attackDamage);
            SpawnHitConfirm(target.transform.position);
        }
    }

    protected virtual IEnumerator ShowAttackEffect(AllyBase target)
    {
        if (IsParalyzed) yield break;
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = Color.yellow;
            yield return new WaitForSeconds(0.06f);
            spriteRenderer.color = orig;
        }
        if (IsParalyzed) yield break;
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

    protected void SpawnEffectSheet(string resourceName, Vector3 pos, int frameCount,
        float ppu, float fps, float scale, int sortingOrder, float rotationDeg = 0f)
        => StartCoroutine(EffectSheetRoutine(resourceName, pos, frameCount, ppu, fps, scale, sortingOrder, rotationDeg));

    // 캐릭터 크기(1.18 Unity units)보다 약간 작은 목표 크기
    const float EFFECT_TARGET_SIZE = 0.9f;

    IEnumerator EffectSheetRoutine(string resourceName, Vector3 pos, int frameCount,
        float ppu, float fps, float scale, int sortingOrder, float rotationDeg)
    {
        Sprite[] frames = LoadEffectFrames(resourceName, frameCount, ppu);
        if (frames == null || frames.Length == 0) yield break;

        // 프레임 중 최대 자연 크기를 기준으로 scale 정규화
        float maxNaturalDim = 0f;
        foreach (var f in frames)
            if (f != null) maxNaturalDim = Mathf.Max(maxNaturalDim, f.bounds.size.x, f.bounds.size.y);
        float displayScale = maxNaturalDim > 0.01f ? scale * EFFECT_TARGET_SIZE / maxNaturalDim : scale;

        var go = new GameObject(resourceName);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * displayScale;
        go.transform.rotation = Quaternion.Euler(0f, 0f, rotationDeg);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;

        float delay = 1f / Mathf.Max(1f, fps);
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null) sr.sprite = frames[i];
            yield return new WaitForSeconds(delay);
        }
        Destroy(go);
    }

    static Sprite[] LoadEffectFrames(string resourceName, int frameCount = 0, float ppu = 0f)
    {
        if (string.IsNullOrEmpty(resourceName)) return null;
        if (effectFrameCache.TryGetValue(resourceName, out var cached))
            return cached;

        var sprites = Resources.LoadAll<Sprite>($"Effect/{resourceName}");
        if (sprites == null || sprites.Length == 0) { effectFrameCache[resourceName] = null; return null; }

        System.Array.Sort(sprites, (a, b) => ParseSpriteSuffix(a.name).CompareTo(ParseSpriteSuffix(b.name)));
        effectFrameCache[resourceName] = sprites;
        return sprites;
    }

    static int ParseSpriteSuffix(string name)
    {
        int i = name.LastIndexOf('_');
        return i >= 0 && int.TryParse(name.Substring(i + 1), out int n) ? n : 0;
    }

    // 슬라이스 스프라이트의 최대 자연 크기를 기준으로 표시 스케일을 정규화
    static float NormalizeEffectScale(Sprite[] frames, float baseScale, float targetSize = EFFECT_TARGET_SIZE)
    {
        if (frames == null || frames.Length == 0) return baseScale;
        float maxDim = 0f;
        foreach (var f in frames)
            if (f != null) maxDim = Mathf.Max(maxDim, f.bounds.size.x, f.bounds.size.y);
        return maxDim > 0.01f ? baseScale * targetSize / maxDim : baseScale;
    }

    void SpawnHitConfirm(Vector3 pos)
    {
        Sprite[] frames = LoadEffectFrames("attacked_team", 6, 256f);
        if (frames != null && frames.Length > 0)
        {
            StartCoroutine(EffectSheetRoutine("attacked_team", pos + Vector3.up * 0.18f, 6, 256f, 18f, 0.88f, 46, 0f));
            return;
        }
        SpawnImpactFlash(pos, new Color(1f, 0.35f, 0.15f, 0.95f), 0.18f);
    }

    IEnumerator ImpactFlashRoutine(Vector3 pos, Color color, float duration)
    {
        var go = new GameObject("ImpactFlash");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite(18);
        sr.color = color;
        sr.sortingOrder = 38;

        var ringGo = new GameObject("ImpactRing");
        ringGo.transform.position = pos;
        var ring = ringGo.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = 28;
        ring.widthMultiplier = 0.055f;
        ring.sortingOrder = 39;
        ring.material = new Material(Shader.Find("Sprites/Default"));
        Color ringColor = new Color(color.r, color.g, color.b, 0.95f);
        ring.startColor = ringColor;
        ring.endColor = ringColor;

        const int sparkCount = 8;
        GameObject[] sparks = new GameObject[sparkCount];
        Vector3[] sparkDirs = new Vector3[sparkCount];
        for (int i = 0; i < sparkCount; i++)
        {
            float a = (float)i / sparkCount * Mathf.PI * 2f + Random.Range(-0.18f, 0.18f);
            sparkDirs[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            sparks[i] = CreateSpark(pos, color, Random.Range(0.14f, 0.24f), 40);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.30f, 1.18f, t);
            Color c = color; c.a = 1f - t;
            sr.color = c;

            float radius = Mathf.Lerp(0.20f, 0.78f, t);
            for (int i = 0; i < ring.positionCount; i++)
            {
                float a = (float)i / ring.positionCount * Mathf.PI * 2f;
                ring.SetPosition(i, pos + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
            }
            ringColor.a = 0.95f * (1f - t);
            ring.startColor = ringColor;
            ring.endColor = ringColor;

            for (int i = 0; i < sparks.Length; i++)
            {
                if (sparks[i] == null) continue;
                sparks[i].transform.position += sparkDirs[i] * (3.1f * Time.deltaTime);
                sparks[i].transform.localScale = Vector3.one * Mathf.Lerp(0.24f, 0.05f, t);
                var sparkSr = sparks[i].GetComponent<SpriteRenderer>();
                if (sparkSr != null)
                {
                    Color sc = color;
                    sc.a = 1f - t;
                    sparkSr.color = sc;
                }
            }
            yield return null;
        }
        for (int i = 0; i < sparks.Length; i++)
            if (sparks[i] != null) Destroy(sparks[i]);
        Destroy(ringGo);
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

    static GameObject CreateSpark(Vector3 pos, Color color, float scale, int sortOrder)
    {
        var go = new GameObject("ImpactSpark");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite(5);
        sr.color = color;
        sr.sortingOrder = sortOrder;
        go.transform.localScale = Vector3.one * scale;
        return go;
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
            if (IsParalyzed) break;
            if (frame != null) spriteRenderer.sprite = frame;
            yield return new WaitForSeconds(delay);
        }
        if (idleSprite != null) spriteRenderer.sprite = idleSprite;
        isPlayingAttackAnim = false;
    }

    // ── 재사용 가능한 파라메트릭 이펙트 코루틴 ────────────────────────────

    /// <summary>부채꼴 파티클 분사. Brawler 계열 전용.</summary>
    protected IEnumerator ParticleSprayEffect(AllyBase target, Color colorA, Color colorB,
        int count = 12, float duration = 0.38f, float spreadDeg = 30f, string effectSheet = null)
    {
        if (target == null) yield break;
        Vector3 src  = transform.position + Vector3.up * 0.12f;
        Vector3 dst  = target.transform.position;
        Vector3 dir  = (dst - src).normalized;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        float dist   = Vector3.Distance(src, dst);

        if (!string.IsNullOrEmpty(effectSheet))
        {
            Sprite[] sheetFrames = LoadEffectFrames(effectSheet);
            if (sheetFrames != null && sheetFrames.Length > 0)
            {
                Vector3 center = Vector3.Lerp(src, dst, 0.72f);
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                SpawnEffectSheet(effectSheet, center, sheetFrames.Length, 0f, 24f, 1.12f, 41, angle);
                yield return new WaitForSeconds(sheetFrames.Length / 24f);
                SpawnImpactFlash(dst, colorB, 0.16f);
                yield break;
            }
        }

        Sprite spr = MakeCircleSprite(7);
        var slash = CreateTempLineRenderer(12, colorB, 0.16f, 37);
        slash.endColor = new Color(colorA.r, colorA.g, colorA.b, 0.18f);
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
            Vector3 slashCenter = Vector3.Lerp(src, dst, Mathf.SmoothStep(0.12f, 0.86f, t));
            float slashRadius = Mathf.Lerp(0.15f, 0.48f, t);
            float startAngle = -65f * Mathf.Deg2Rad;
            float endAngle = 65f * Mathf.Deg2Rad;
            for (int i = 0; i < slash.positionCount; i++)
            {
                float u = (float)i / (slash.positionCount - 1);
                float a = Mathf.Lerp(startAngle, endAngle, u);
                Vector3 arc = dir * Mathf.Cos(a) + perp * Mathf.Sin(a);
                slash.SetPosition(i, slashCenter + arc * slashRadius);
            }
            Color slashColor = colorB;
            slashColor.a = 0.85f * (1f - t);
            slash.startColor = slashColor;
            slash.endColor = new Color(colorA.r, colorA.g, colorA.b, 0.15f * (1f - t));

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
        Destroy(slash.gameObject);
        for (int i = 0; i < count; i++)
            if (gos[i] != null) Destroy(gos[i]);
        SpawnImpactFlash(dst, colorB, 0.20f);
    }

    /// <summary>지그재그 번개. Spearman 계열 전용.</summary>
    protected IEnumerator ZigzagEffect(AllyBase target, Color boltColor, Color impactColor,
        int flashCount = 3, float offsetMax = 0.30f)
    {
        if (target == null) yield break;
        Vector3 src = transform.position;
        Vector3 dst = target.transform.position;

        var glow = CreateTempLineRenderer(12, new Color(boltColor.r, boltColor.g, boltColor.b, 0.28f), 0.22f, 31);
        var lr = CreateTempLineRenderer(12, boltColor, 0.09f, 32);
        lr.endColor = new Color(boltColor.r * 0.8f, boltColor.g * 0.8f, boltColor.b * 0.8f, 0.35f);

        for (int flash = 0; flash < flashCount; flash++)
        {
            Vector3[] pts = BuildLightningPath(src, dst, 11, offsetMax);
            for (int i = 0; i < pts.Length; i++)
            {
                lr.SetPosition(i, pts[i]);
                glow.SetPosition(i, pts[i]);
            }
            glow.gameObject.SetActive(true);
            lr.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.06f);
            lr.gameObject.SetActive(false);
            glow.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.025f);
        }
        Destroy(glow.gameObject);
        Destroy(lr.gameObject);
        SpawnImpactFlash(dst, impactColor, 0.24f);
    }

    /// <summary>투사체 이동. Sniper 계열(자연/사막) 전용.</summary>
    protected IEnumerator ProjectileEffect(AllyBase target, Color color,
        float speed = 16f, Color impactColor = default, string impactEffect = null)
    {
        if (target == null) yield break;
        Vector3 src = transform.position + Vector3.up * 0.18f;
        Vector3 dst = target.transform.position;
        float dist  = Vector3.Distance(src, dst);

        Sprite[] arrowFrames = ProjectileSpriteLibrary.GetArrowFrames();

        var go = new GameObject("Proj");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = arrowFrames.Length > 0 ? arrowFrames[0] : ProjectileSpriteLibrary.GetArrowSprite();
        sr.color = color;
        sr.sortingOrder = 36;
        go.transform.position = src;
        go.transform.localScale = Vector3.one * 0.46f;

        var trail = CreateTempLineRenderer(2, new Color(color.r, color.g, color.b, 0.72f), 0.075f, 35);
        trail.endColor = new Color(color.r, color.g, color.b, 0f);

        float elapsed = 0f, maxTime = Mathf.Max(0.10f, dist / speed);
        while (elapsed < maxTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Min(elapsed / maxTime, 1f);
            if (target != null) dst = target.transform.position;
            Vector3 pos = Vector3.Lerp(src, dst, Mathf.SmoothStep(0f, 1f, t));
            Vector3 dir = (dst - src).sqrMagnitude > 0.0001f ? (dst - src).normalized : Vector3.right;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            if (arrowFrames.Length > 1)
                sr.sprite = arrowFrames[Mathf.Min((int)(t * arrowFrames.Length), arrowFrames.Length - 1)];
            trail.SetPosition(0, pos - dir * 0.62f);
            trail.SetPosition(1, pos);
            if (t >= 1f) break;
            yield return null;
        }
        Destroy(trail.gameObject);
        Destroy(go);
        Color hitColor = impactColor.a > 0f ? impactColor : color;
        string sheet = impactEffect ?? (hitColor.r > hitColor.g ? "clean_red_arrow_effect" : "clean_green_arrow_effect");
        if (LoadEffectFrames(sheet, 8, 443f) != null)
            SpawnEffectSheet(sheet, dst, 8, 443f, 22f, 1.35f, 40);
        else
            SpawnImpactFlash(dst, hitColor, 0.26f);
    }

    protected IEnumerator OrbProjectileEffect(AllyBase target, Color color,
        float speed = 15f, Color impactColor = default, float arcHeight = 0.28f, string impactEffect = null)
    {
        if (target == null) yield break;
        Vector3 src = transform.position + Vector3.up * 0.18f;
        Vector3 dst = target.transform.position;
        float dist = Vector3.Distance(src, dst);

        var go = new GameObject("OrbProj");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite(9);
        sr.color = color;
        sr.sortingOrder = 36;
        go.transform.localScale = Vector3.one * 0.22f;

        var trail = CreateTempLineRenderer(2, new Color(color.r, color.g, color.b, 0.52f), 0.09f, 35);
        trail.endColor = new Color(color.r, color.g, color.b, 0f);

        float elapsed = 0f, duration = Mathf.Max(0.12f, dist / speed);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (target != null) dst = target.transform.position;
            Vector3 pos = Vector3.Lerp(src, dst, Mathf.SmoothStep(0f, 1f, t));
            pos += Vector3.up * Mathf.Sin(t * Mathf.PI) * arcHeight;
            Vector3 dir = (dst - src).sqrMagnitude > 0.0001f ? (dst - src).normalized : Vector3.right;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * (0.20f + Mathf.Sin(t * Mathf.PI) * 0.08f);
            trail.SetPosition(0, pos - dir * 0.42f);
            trail.SetPosition(1, pos);
            yield return null;
        }
        Destroy(trail.gameObject);
        Destroy(go);
        Color hitColor = impactColor.a > 0f ? impactColor : color;
        string sheet = impactEffect ?? "clean_green_arrow_effect";
        if (LoadEffectFrames(sheet, 8, 443f) != null)
            SpawnEffectSheet(sheet, dst, 8, 443f, 22f, 1.20f, 40);
        else
            SpawnImpactFlash(dst, hitColor, 0.23f);
    }

    protected IEnumerator BoomerangEffect(AllyBase target, Color trailColor, Color impactColor,
        float speed = 13f, float curveHeight = 0.55f)
    {
        if (target == null) yield break;
        Vector3 src = transform.position + Vector3.up * 0.14f;
        Vector3 dst = target.transform.position;
        float dist = Vector3.Distance(src, dst);
        Vector3 dir  = (dst - src).sqrMagnitude > 0.0001f ? (dst - src).normalized : Vector3.right;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

        Sprite[] frames  = ProjectileSpriteLibrary.GetBoomerangFrames();
        bool     hasSheet = frames != null && frames.Length > 0;

        var go = new GameObject("BoomerangProj");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = hasSheet ? frames[0] : MakeBoomerangSprite();
        sr.color        = Color.white;   // 나무 원색 그대로
        sr.sortingOrder = 36;
        // ppu=443 → 자연 크기 1 world unit → 0.52f 배율로 표시
        go.transform.localScale = Vector3.one * (hasSheet ? 0.52f : 0.30f);

        // 불꽃 트레일
        var trail = CreateTempLineRenderer(8, new Color(trailColor.r, trailColor.g, trailColor.b, 0.70f), 0.11f, 35);
        trail.endColor = new Color(impactColor.r, impactColor.g, impactColor.b, 0f);
        Vector3[] trailPts = new Vector3[8];
        for (int i = 0; i < trailPts.Length; i++) { trailPts[i] = src; trail.SetPosition(i, src); }

        float elapsed = 0f, duration = Mathf.Max(0.18f, dist / speed);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (target != null) dst = target.transform.position;

            // 측면 곡선 + 약간의 수직 아치 → 입체적인 부메랑 궤도
            Vector3 basePos = Vector3.Lerp(src, dst, Mathf.SmoothStep(0f, 1f, t));
            float   arc     = Mathf.Sin(t * Mathf.PI);
            Vector3 pos     = basePos
                              + perp     * (arc * curveHeight)
                              + Vector3.up * (arc * 0.22f);

            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * 720f);  // 720°/s 회전

            if (hasSheet)
                sr.sprite = frames[(int)(elapsed * 18f) % frames.Length];

            for (int i = trailPts.Length - 1; i > 0; i--) trailPts[i] = trailPts[i - 1];
            trailPts[0] = pos;
            for (int i = 0; i < trailPts.Length; i++) trail.SetPosition(i, trailPts[i]);
            yield return null;
        }
        Destroy(trail.gameObject);
        Destroy(go);
        SpawnImpactFlash(dst, impactColor, 0.22f);
    }

    /// <summary>얇은 빔 페이드. Sniper 계열(화산/어둠/요새) 전용.</summary>
    protected IEnumerator ThinBeamEffect(AllyBase target, Color beamColor,
        Color impactColor, float duration = 0.20f)
    {
        if (target == null) yield break;
        Vector3 src = transform.position;
        Vector3 dst = target.transform.position;

        var glow = CreateTempLineRenderer(2, new Color(beamColor.r, beamColor.g, beamColor.b, 0.35f), 0.22f, 32);
        var lr = CreateTempLineRenderer(2, beamColor, 0.075f, 33);
        glow.SetPosition(0, src);
        glow.SetPosition(1, dst);
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
            glow.startColor = new Color(beamColor.r, beamColor.g, beamColor.b, a * 0.35f);
            glow.endColor = new Color(beamColor.r, beamColor.g, beamColor.b, 0f);
            yield return null;
        }
        Destroy(glow.gameObject);
        Destroy(lr.gameObject);
        SpawnImpactFlash(dst, impactColor, 0.28f);
    }

    static Sprite MakeBoomerangSprite()
    {
        int size = 24;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
            tex.SetPixel(x, y, Color.clear);

        Color edge = new Color(1f, 0.78f, 0.25f, 1f);
        Color core = new Color(1f, 0.35f, 0.08f, 1f);
        for (int i = 0; i < 10; i++)
        {
            SetSpritePx(tex, 5 + i, 6 + i, edge);
            SetSpritePx(tex, 6 + i, 6 + i, core);
            SetSpritePx(tex, 14 + i / 2, 15 - i, edge);
            SetSpritePx(tex, 15 + i / 2, 15 - i, core);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 18f);
    }

    static void SetSpritePx(Texture2D tex, int x, int y, Color color)
    {
        if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
            tex.SetPixel(x, y, color);
    }

    /// <summary>스프라이트 자연 높이 기준으로 transform.localScale을 조정하고 breathBaseScale도 갱신한다.</summary>
    protected void NormalizeScale(float targetWorldHeight)
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null) return;
        float naturalH = spriteRenderer.sprite.bounds.size.y;
        if (naturalH <= 0.001f) return;
        transform.localScale = Vector3.one * (targetWorldHeight / naturalH);
        breathBaseScale = transform.localScale;
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

    /// <summary>사거리 무관하게 가장 가까운 아군을 반환 (방향 전환용)</summary>
    protected AllyBase FindNearestAllyForFacing()
    {
        AllyBase[] allies = FindObjectsByType<AllyBase>(FindObjectsSortMode.None);
        AllyBase nearest  = null;
        float minDist = float.MaxValue;
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
        GameAudio.PlayParalysis();
        paralysisTimer = Mathf.Max(paralysisTimer, duration);
        attackTimer = Mathf.Max(attackTimer, attackCooldown);
        CreateParalysisVisual();
    }

    void CreateParalysisVisual()
    {
        if (paralysisVisual == null)
        {
            paralysisVisual = new GameObject("ParalysisVisual");
            paralysisVisual.transform.SetParent(transform, false);
            paralysisVisual.transform.localPosition = new Vector3(0f, 0.18f, 0f);

            paralysisVisualRenderer = paralysisVisual.AddComponent<SpriteRenderer>();
            paralysisEffectFrames = LoadEffectFrames("clean_paralyzing_arrow_effect", 8, 443f)
                ?? LoadEffectFrames("paralyzing_arrow_effect", 8, 443f);
            paralysisVisualRenderer.sprite = paralysisEffectFrames != null && paralysisEffectFrames.Length > 0
                ? paralysisEffectFrames[0]
                : MakeCircleSprite(14);
            paralysisVisualRenderer.color = new Color(1f, 0.95f, 0.32f, 0.55f);
            paralysisVisualRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 2 : 38;
            if (paralysisEffectFrames != null)
            {
                _paralysisNormScale = NormalizeEffectScale(paralysisEffectFrames, 1f, 1.1f);
                paralysisVisual.transform.localScale = Vector3.one * _paralysisNormScale;
            }
            else
            {
                _paralysisNormScale = 1f;
                paralysisVisual.transform.localScale = new Vector3(0.36f, 0.58f, 1f);
            }
        }

        if (paralysisTintVisual == null && spriteRenderer != null)
        {
            paralysisTintVisual = new GameObject("ParalysisTint");
            paralysisTintVisual.transform.SetParent(transform, false);
            paralysisTintVisual.transform.localPosition = Vector3.zero;
            paralysisTintRenderer = paralysisTintVisual.AddComponent<SpriteRenderer>();
            paralysisTintRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        }
    }

    void UpdateParalysisVisual()
    {
        if (paralysisVisual != null)
        {
            paralysisVisual.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 5.5f) * 4f);
            if (paralysisVisualRenderer != null)
            {
                if (paralysisEffectFrames != null && paralysisEffectFrames.Length > 0)
                    paralysisVisualRenderer.sprite = paralysisEffectFrames[(int)(Time.time * 14f) % paralysisEffectFrames.Length];
                paralysisVisualRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 2 : 38;
            }
            float pulse = paralysisEffectFrames != null
                ? (1.0f + Mathf.Sin(Time.time * 10f) * 0.05f) * _paralysisNormScale
                : 0.38f + Mathf.Sin(Time.time * 10f) * 0.06f;
            paralysisVisual.transform.localScale = Vector3.one * pulse;
        }

        if (paralysisTintRenderer != null && spriteRenderer != null)
        {
            paralysisTintRenderer.sprite = spriteRenderer.sprite;
            paralysisTintRenderer.flipX = spriteRenderer.flipX;
            paralysisTintRenderer.flipY = spriteRenderer.flipY;
            paralysisTintRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            float alpha = 0.42f + Mathf.Sin(Time.time * 7f) * 0.09f;
            paralysisTintRenderer.color = new Color(1f, 0.86f, 0.08f, alpha);
        }
    }

    void ClearParalysisVisual()
    {
        if (paralysisVisual != null)
        {
            Destroy(paralysisVisual);
            paralysisVisual = null;
            paralysisVisualRenderer = null;
            paralysisEffectFrames = null;
        }
        if (paralysisTintVisual != null)
        {
            Destroy(paralysisTintVisual);
            paralysisTintVisual = null;
            paralysisTintRenderer = null;
        }
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
