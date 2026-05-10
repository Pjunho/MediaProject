using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AllyBase : MonoBehaviour
{
    [Header("Stats")]
    public float  maxHp     = 100f;
    public float  moveSpeed = 3f;
    public string allyName  = "Ally";

    [Header("State")]
    public float currentHp;
    public bool  isDead = false;

    protected List<Vector3> waypoints = new List<Vector3>();
    protected int   currentWaypointIndex = 0;
    protected float waypointReachDist    = 0.1f;

    protected Transform visualRoot;
    protected SpriteRenderer spriteRenderer;
    protected SpriteRenderer[] visualRenderers = System.Array.Empty<SpriteRenderer>();

    protected Transform torsoPart;
    protected Transform headPart;
    protected Transform frontArmPart;
    protected Transform backArmPart;
    protected Transform frontLegPart;
    protected Transform backLegPart;
    protected AllyDirectionalSprite directionalSprite;
    protected AllyDirectionalAnimator directionalAnimator;

    protected float walkBobAmplitude    = 0.045f;
    protected float walkSquashAmount    = 0.06f;
    protected float walkTiltAngle       = 6f;
    protected float walkCycleFrequency  = 8f;
    protected float idleBreathAmount    = 0.018f;  // 아이들 Y 부유량
    protected float idleBreathScale     = 0.025f;  // 아이들 흉부 스케일 변화량
    protected float idleArmSwing        = 6f;      // 아이들 팔 흔들림 (도)
    protected float idleHeadSway        = 4f;      // 아이들 머리 흔들림 (도)
    protected float motionSharpness     = 12f;
    protected float armSwingAngle       = 16f;
    protected float legSwingAngle       = 20f;
    protected float headNodAngle        = 4f;

    public System.Action<AllyBase> OnReachedGoal;
    public System.Action<AllyBase> OnDied;
    private bool hasCompletedPath = false;

    private bool  hasSkillShield = false;
    private GameObject shieldVisual;
    private SpriteRenderer shieldSr;
    private float hitStunTimer  = 0f;
    private bool skillInvulnerable;
    private float smokeEvadeChance;
    private GameObject yellowAuraVisual;
    private GameObject smokeVisual;
    private GameObject mageBarrierVisual;
    private SpriteRenderer mageBarrierSr;
    private Coroutine warriorWillRoutine;
    private Coroutine mageBarrierRoutine;
    private Coroutine clericHealRoutine;
    private Coroutine rogueSmokeRoutine;
    private Coroutine paladinOathRoutine;
    private static readonly List<AllyBase> activeMageBarriers = new List<AllyBase>();
    private static AllyBase activePaladinProtector;
    private static Sprite[] paralysisArrowFrames;
    private const float MageBarrierRadius = 1.45f;

    // ── HP 바 ──────────────────────────────────────────────────────────
    private GameObject     hpBarRoot;
    private SpriteRenderer hpBarFill;
    const float HP_BAR_OFFSET_Y = 0.65f;
    const float HP_BAR_WIDTH    = 0.50f;
    const float HP_BAR_HEIGHT   = 0.065f;
    private float walkCycle     = 0f;
    private float facingSign   = 1f;
    private Vector3 lastFramePosition;
    private AllyVisualGenerator.CharDirection currentFacing = AllyVisualGenerator.CharDirection.Side;

    private Vector3 visualBaseLocalPosition;
    private Vector3 visualBaseLocalScale = Vector3.one;
    private Quaternion visualBaseLocalRotation = Quaternion.identity;

    private Vector3 torsoBaseLocalPosition;
    private Quaternion torsoBaseLocalRotation = Quaternion.identity;
    private Vector3 headBaseLocalPosition;
    private Quaternion headBaseLocalRotation = Quaternion.identity;
    private Vector3 frontArmBaseLocalPosition;
    private Quaternion frontArmBaseLocalRotation = Quaternion.identity;
    private Vector3 backArmBaseLocalPosition;
    private Quaternion backArmBaseLocalRotation = Quaternion.identity;
    private Vector3 frontLegBaseLocalPosition;
    private Quaternion frontLegBaseLocalRotation = Quaternion.identity;
    private Vector3 backLegBaseLocalPosition;
    private Quaternion backLegBaseLocalRotation = Quaternion.identity;

    protected virtual void Awake()
    {
        currentHp = maxHp;

        visualRoot = transform.Find("Visual");
        if (visualRoot == null)
            visualRoot = transform;

        visualRenderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (visualRenderers.Length == 0)
            visualRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        spriteRenderer = visualRenderers.Length > 0 ? visualRenderers[0] : null;

        torsoPart = visualRoot.Find(AllyVisualGenerator.PartTorso);
        headPart = visualRoot.Find(AllyVisualGenerator.PartHead);
        frontArmPart = visualRoot.Find(AllyVisualGenerator.PartFrontArm);
        backArmPart = visualRoot.Find(AllyVisualGenerator.PartBackArm);
        frontLegPart = visualRoot.Find(AllyVisualGenerator.PartFrontLeg);
        backLegPart = visualRoot.Find(AllyVisualGenerator.PartBackLeg);
        directionalSprite = visualRoot.GetComponent<AllyDirectionalSprite>();
        if (directionalSprite == null)
            directionalSprite = visualRoot.GetComponentInChildren<AllyDirectionalSprite>(true);
        directionalAnimator = visualRoot.GetComponent<AllyDirectionalAnimator>();
        if (directionalAnimator == null)
            directionalAnimator = visualRoot.GetComponentInChildren<AllyDirectionalAnimator>(true);

        CacheTransformBase(visualRoot, out visualBaseLocalPosition, out visualBaseLocalRotation, out visualBaseLocalScale);
        CacheTransformBase(torsoPart, out torsoBaseLocalPosition, out torsoBaseLocalRotation);
        CacheTransformBase(headPart, out headBaseLocalPosition, out headBaseLocalRotation);
        CacheTransformBase(frontArmPart, out frontArmBaseLocalPosition, out frontArmBaseLocalRotation);
        CacheTransformBase(backArmPart, out backArmBaseLocalPosition, out backArmBaseLocalRotation);
        CacheTransformBase(frontLegPart, out frontLegBaseLocalPosition, out frontLegBaseLocalRotation);
        CacheTransformBase(backLegPart, out backLegBaseLocalPosition, out backLegBaseLocalRotation);

        lastFramePosition = transform.position;
        CreateHpBar();
    }

    protected virtual void Update()
    {
        if (isDead || hasCompletedPath) return;

        hitStunTimer -= Time.deltaTime;
        if (hitStunTimer <= 0f)
            MoveAlongPath();

        if (shieldVisual != null)
        {
            shieldVisual.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 40f);
            float pulse = 0.88f + Mathf.Sin(Time.time * 3f) * 0.07f;
            shieldVisual.transform.localScale = Vector3.one * pulse;
            if (shieldSr != null)
                shieldSr.color = new Color(0.3f, 0.6f, 1f, 0.38f + Mathf.Sin(Time.time * 2.5f) * 0.12f);
        }
        if (mageBarrierVisual != null)
        {
            float pulse = 0.96f + Mathf.Sin(Time.time * 5f) * 0.04f;
            mageBarrierVisual.transform.localScale = Vector3.one * (MageBarrierRadius * 2f * pulse);
            if (mageBarrierSr != null)
                mageBarrierSr.color = new Color(0.25f, 0.58f, 1f, 0.24f + Mathf.Sin(Time.time * 4f) * 0.07f);
        }
        if (yellowAuraVisual != null)
        {
            float pulse = 0.92f + Mathf.Sin(Time.time * 6f) * 0.08f;
            yellowAuraVisual.transform.localScale = Vector3.one * pulse;
        }
        if (smokeVisual != null)
            smokeVisual.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 35f);

        UpdateVisualMotion();
        lastFramePosition = transform.position;
    }

    public void SetWaypoints(List<Vector3> points)
    {
        waypoints = new List<Vector3>(points);
        currentWaypointIndex = 0;
        hasCompletedPath = false;
        if (waypoints.Count > 0)
            transform.position = waypoints[0];

        lastFramePosition = transform.position;
    }

    protected void MoveAlongPath()
    {
        if (waypoints == null || currentWaypointIndex >= waypoints.Count) return;

        Vector3 target = waypoints[currentWaypointIndex];
        Vector3 toTarget = target - transform.position;
        Vector3 direction = toTarget.sqrMagnitude > 0.000001f ? toTarget.normalized : Vector3.zero;
        transform.position = Vector3.MoveTowards(
            transform.position,
            target,
            moveSpeed * Time.deltaTime);

        if (direction.x < -0.01f) facingSign = -1f;
        else if (direction.x > 0.01f) facingSign = 1f;

        UpdateFacingDirection(direction);

        float reachDist = currentWaypointIndex >= waypoints.Count - 1
            ? Mathf.Max(waypointReachDist, 0.22f)
            : waypointReachDist;

        if (Vector3.Distance(transform.position, target) <= reachDist ||
            transform.position == target)
        {
            transform.position = target;
            currentWaypointIndex++;
            if (currentWaypointIndex >= waypoints.Count)
                ReachedGoal();
        }
    }

    public void EnableSkillShield()
    {
        hasSkillShield = true;
        CreateShieldVisual();
    }

    void CreateShieldVisual()
    {
        if (shieldVisual != null) return;
        shieldVisual = new GameObject("ShieldVisual");
        shieldVisual.transform.SetParent(transform, false);
        shieldVisual.transform.localPosition = Vector3.zero;

        shieldSr = shieldVisual.AddComponent<SpriteRenderer>();
        shieldSr.sprite = CreateShieldSprite();
        shieldSr.color = new Color(0.3f, 0.6f, 1f, 0.5f);
        shieldSr.sortingOrder = 25;
    }

    void ClearShieldVisual()
    {
        if (shieldVisual == null) return;
        Destroy(shieldVisual);
        shieldVisual = null;
        shieldSr = null;
    }

    Sprite CreateShieldSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float cx = size / 2f, cy = size / 2f;
        float outerR = size / 2f - 1f;
        float innerR = outerR - 6f;

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float dx = x - cx, dy = y - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist >= innerR && dist <= outerR)
            {
                float t = (dist - innerR) / (outerR - innerR);
                float alpha = 1f - Mathf.Abs(t - 0.5f) * 2f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            else if (dist < innerR)
            {
                float fill = Mathf.Clamp01(1f - dist / innerR) * 0.18f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, fill));
            }
            else
                tex.SetPixel(x, y, Color.clear);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    public void EnableSkillRegen() => StartCoroutine(SkillRegenCoroutine());

    IEnumerator SkillRegenCoroutine()
    {
        while (!isDead && !hasCompletedPath)
        {
            if (currentHp < maxHp)
            {
                currentHp = Mathf.Min(currentHp + maxHp * 0.03f, maxHp);
                HealEffect.Spawn(transform.position);
                UpdateHpBar();
            }
            yield return new WaitForSeconds(1f);
        }
    }

    public virtual void TakeDamage(float damage)
    {
        TakeDamageInternal(damage, true, true);
    }

    void TakeDamageInternal(float damage, bool allowRedirect, bool applyStun)
    {
        if (isDead) return;
        if (damage <= 0f) return;
        if (skillInvulnerable || IsProtectedByMageBarrier())
        {
            SpawnGuardFlash(transform.position, new Color(0.35f, 0.65f, 1f, 0.85f));
            return;
        }
        if (smokeEvadeChance > 0f && Random.value < smokeEvadeChance)
        {
            SpawnGuardFlash(transform.position, new Color(0.70f, 0.28f, 1f, 0.85f));
            return;
        }
        if (allowRedirect && activePaladinProtector != null &&
            activePaladinProtector != this && !activePaladinProtector.isDead)
        {
            float redirected = damage * 0.80f;
            float remaining  = damage - redirected;
            activePaladinProtector.TakeDamageInternal(redirected, false, true);
            TakeDamageInternal(remaining, false, false);
            return;
        }
        if (hasSkillShield)
        {
            hasSkillShield = false;
            StartCoroutine(ShieldBreakEffect());
            return;
        }
        currentHp -= damage;
        if (applyStun) OnDamaged();
        else UpdateHpBar();
        if (currentHp <= 0f)
            Die();
    }

    public AllyType GetAllyType()
    {
        if (this is Warrior) return AllyType.Warrior;
        if (this is Archer)  return AllyType.Archer;
        if (this is Mage)    return AllyType.Mage;
        if (this is Cleric)  return AllyType.Cleric;
        if (this is Rogue)   return AllyType.Rogue;
        if (this is Paladin) return AllyType.Paladin;
        return AllyType.Warrior;
    }

    public void ActivateWarriorWill()
    {
        if (warriorWillRoutine != null) return;
        warriorWillRoutine = StartCoroutine(WarriorWillRoutine());
    }

    IEnumerator WarriorWillRoutine()
    {
        skillInvulnerable = true;
        CreateShieldVisual();
        float elapsed = 0f;
        while (elapsed < 2f && !isDead)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        skillInvulnerable = false;
        ClearShieldVisual();
        warriorWillRoutine = null;
    }

    public void FireParalysisArrow(EnemyBase target)
    {
        if (target == null || isDead) return;
        StartCoroutine(ParalysisArrowRoutine(target));
    }

    IEnumerator ParalysisArrowRoutine(EnemyBase target)
    {
        Vector3 src = transform.position + Vector3.up * 0.15f;
        Vector3 dst = target.transform.position;

        Sprite[] arrowFrames = ProjectileSpriteLibrary.GetArrowFrames();

        var go = new GameObject("ParalysisArrow");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = arrowFrames.Length > 0 ? arrowFrames[0] : ProjectileSpriteLibrary.GetArrowSprite();
        sr.color = new Color(0.50f, 0.92f, 1f);   // 마비 화살: 청록 색조
        sr.sortingOrder = 70;
        go.transform.localScale = Vector3.one * 0.48f;

        float elapsed = 0f;
        float duration = Mathf.Max(0.10f, Vector3.Distance(src, dst) / 14f);
        while (elapsed < duration && target != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            dst = target.transform.position;
            Vector3 dir = (dst - src).sqrMagnitude > 0.0001f ? (dst - src).normalized : Vector3.right;
            go.transform.position = Vector3.Lerp(src, dst, t);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            if (arrowFrames.Length > 1)
                sr.sprite = arrowFrames[Mathf.Min((int)(t * arrowFrames.Length), arrowFrames.Length - 1)];
            yield return null;
        }
        Destroy(go);
        if (target != null)
            target.ApplyParalysis(3f);
    }

    public void ActivateMageBarrier()
    {
        if (mageBarrierRoutine != null) return;
        mageBarrierRoutine = StartCoroutine(MageBarrierRoutine());
    }

    IEnumerator MageBarrierRoutine()
    {
        if (!activeMageBarriers.Contains(this))
            activeMageBarriers.Add(this);
        CreateMageBarrierVisual();
        float elapsed = 0f;
        while (elapsed < 1.5f && !isDead)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        activeMageBarriers.Remove(this);
        ClearMageBarrierVisual();
        mageBarrierRoutine = null;
    }

    public void ActivateClericHeal()
    {
        if (clericHealRoutine != null) return;
        clericHealRoutine = StartCoroutine(GroupHealRoutine(5f, 0.03f, false));
    }

    public void ActivateRogueSmoke()
    {
        if (rogueSmokeRoutine != null) return;
        rogueSmokeRoutine = StartCoroutine(GroupSmokeRoutine());
    }

    public void ActivatePaladinOath()
    {
        if (paladinOathRoutine != null) return;
        paladinOathRoutine = StartCoroutine(PaladinOathRoutine());
    }

    IEnumerator GroupHealRoutine(float duration, float healRatioPerSecond, bool selfOnly)
    {
        List<AllyBase> targets = selfOnly ? new List<AllyBase> { this } : GetLivingAllies();
        foreach (var ally in targets)
            ally.SetYellowAura(true);

        float elapsed = 0f;
        float tick = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            tick += Time.deltaTime;
            if (tick >= 1f)
            {
                tick -= 1f;
                foreach (var ally in targets)
                    if (ally != null && !ally.isDead)
                        ally.HealByRatio(healRatioPerSecond);
            }
            yield return null;
        }

        foreach (var ally in targets)
            if (ally != null)
                ally.SetYellowAura(false);
        clericHealRoutine = null;
    }

    IEnumerator GroupSmokeRoutine()
    {
        List<AllyBase> targets = GetLivingAllies();
        foreach (var ally in targets)
            if (ally != null && !ally.isDead)
                ally.SetSmokeEvade(0.50f, true);

        float elapsed = 0f;
        while (elapsed < 5f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        foreach (var ally in targets)
            if (ally != null)
                ally.SetSmokeEvade(0f, false);
        rogueSmokeRoutine = null;
    }

    IEnumerator PaladinOathRoutine()
    {
        activePaladinProtector = this;
        SetYellowAura(true);

        float elapsed = 0f;
        float tick = 0f;
        while (elapsed < 4f && !isDead)
        {
            elapsed += Time.deltaTime;
            tick += Time.deltaTime;
            if (tick >= 1f)
            {
                tick -= 1f;
                HealByRatio(0.05f);
            }
            yield return null;
        }

        if (activePaladinProtector == this)
            activePaladinProtector = null;
        SetYellowAura(false);
        paladinOathRoutine = null;
    }

    bool IsProtectedByMageBarrier()
    {
        for (int i = activeMageBarriers.Count - 1; i >= 0; i--)
        {
            var barrier = activeMageBarriers[i];
            if (barrier == null || barrier.isDead)
            {
                activeMageBarriers.RemoveAt(i);
                continue;
            }
            if (Vector3.Distance(transform.position, barrier.transform.position) <= MageBarrierRadius)
                return true;
        }
        return false;
    }

    static List<AllyBase> GetLivingAllies()
    {
        var result = new List<AllyBase>();
        AllyBase[] allies = FindObjectsByType<AllyBase>(FindObjectsSortMode.None);
        foreach (var ally in allies)
            if (ally != null && !ally.isDead)
                result.Add(ally);
        return result;
    }

    void HealByRatio(float ratio)
    {
        if (isDead || currentHp >= maxHp) return;
        currentHp = Mathf.Min(currentHp + maxHp * ratio, maxHp);
        HealEffect.Spawn(transform.position);
        UpdateHpBar();
    }

    void SetSmokeEvade(float chance, bool show)
    {
        smokeEvadeChance = chance;
        if (show) CreateSmokeVisual();
        else ClearSmokeVisual();
    }

    void SetYellowAura(bool show)
    {
        if (show)
        {
            if (yellowAuraVisual != null) return;
            yellowAuraVisual = CreateAuraVisual("YellowHealAura", new Color(1f, 0.86f, 0.15f, 0.34f), 0.95f, 22);
        }
        else if (yellowAuraVisual != null)
        {
            Destroy(yellowAuraVisual);
            yellowAuraVisual = null;
        }
    }

    void CreateSmokeVisual()
    {
        if (smokeVisual != null) return;
        smokeVisual = new GameObject("SmokeAura");
        smokeVisual.transform.SetParent(transform, false);
        smokeVisual.transform.localPosition = new Vector3(0f, -0.18f, 0f);

        for (int i = 0; i < 6; i++)
        {
            var p = new GameObject("SmokePuff");
            p.transform.SetParent(smokeVisual.transform, false);
            float angle = i / 6f * Mathf.PI * 2f;
            p.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.18f, Mathf.Sin(angle) * 0.05f, 0f);
            p.transform.localScale = Vector3.one * Random.Range(0.12f, 0.22f);
            var sr = p.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite(10);
            sr.color = new Color(0.50f, 0.18f, 0.75f, 0.38f);
            sr.sortingOrder = 19;
        }
    }

    void ClearSmokeVisual()
    {
        if (smokeVisual == null) return;
        Destroy(smokeVisual);
        smokeVisual = null;
    }

    void CreateMageBarrierVisual()
    {
        if (mageBarrierVisual != null) return;
        mageBarrierVisual = CreateAuraVisual("MageBarrier", new Color(0.25f, 0.58f, 1f, 0.30f), MageBarrierRadius * 2f, 24);
        mageBarrierSr = mageBarrierVisual.GetComponent<SpriteRenderer>();
    }

    void ClearMageBarrierVisual()
    {
        if (mageBarrierVisual == null) return;
        Destroy(mageBarrierVisual);
        mageBarrierVisual = null;
        mageBarrierSr = null;
    }

    GameObject CreateAuraVisual(string name, Color color, float scale, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite(32);
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return go;
    }

    static void SpawnGuardFlash(Vector3 pos, Color color)
    {
        var go = new GameObject("GuardFlash");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite(10);
        sr.color = color;
        sr.sortingOrder = 36;
        Object.Destroy(go, 0.18f);
    }

    IEnumerator ShieldBreakEffect()
    {
        if (shieldVisual != null)
        {
            Destroy(shieldVisual);
            shieldVisual = null;
            shieldSr = null;
        }
        for (int i = 0; i < 3; i++)
        {
            SetAllVisualColors(new Color(0.3f, 0.6f, 1f, 1f));
            yield return new WaitForSeconds(0.06f);
            SetAllVisualColors(Color.white);
            yield return new WaitForSeconds(0.05f);
        }
    }

    protected virtual void OnDamaged()
    {
        hitStunTimer = 0.15f;
        UpdateHpBar();
        StartCoroutine(HitEffect());
    }

    IEnumerator HitEffect()
    {
        if (visualRenderers == null || visualRenderers.Length == 0) yield break;

        for (int i = 0; i < 2; i++)
        {
            SetAllVisualColors(new Color(1f, 0.15f, 0.15f, 1f));
            yield return new WaitForSeconds(0.07f);
            SetAllVisualColors(Color.white);
            yield return new WaitForSeconds(0.05f);
        }
    }

    protected virtual void Die()
    {
        if (isDead || hasCompletedPath) return;
        isDead = true;
        if (shieldVisual != null)
        {
            Destroy(shieldVisual);
            shieldVisual = null;
            shieldSr = null;
        }
        ClearAllSkillVisuals();
        OnDied?.Invoke(this);
        StartCoroutine(DeathEffect());
    }

    IEnumerator DeathEffect()
    {
        Vector3 deathPos = transform.position;
        float t = 0f;
        float duration = 0.25f;
        Vector3 originalScale = visualRoot.localScale;

        while (t < duration)
        {
            t += Time.deltaTime;
            float ratio = 1f - (t / duration);
            visualRoot.localScale = originalScale * ratio;
            SetAllVisualColors(new Color(0.6f, 0.6f, 0.6f, ratio));
            yield return null;
        }

        foreach (var sr in visualRenderers)
        {
            if (sr != null)
                sr.enabled = false;
        }

        SmokeEffect.Spawn(deathPos);
        yield return new WaitForSeconds(0.1f);
        Destroy(gameObject);
    }

    protected virtual void ReachedGoal()
    {
        if (hasCompletedPath) return;
        hasCompletedPath = true;
        isDead = true;
        if (shieldVisual != null)
        {
            Destroy(shieldVisual);
            shieldVisual = null;
            shieldSr = null;
        }
        ClearAllSkillVisuals();

        foreach (var sr in visualRenderers)
        {
            if (sr != null)
                sr.enabled = false;
        }

        var childRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in childRenderers)
        {
            if (renderer != null)
                renderer.enabled = false;
        }

        OnReachedGoal?.Invoke(this);
        Destroy(gameObject);
    }

    void UpdateVisualMotion()
    {
        if (visualRoot == null) return;

        Vector3 delta = transform.position - lastFramePosition;
        bool isMoving = delta.sqrMagnitude > 0.000001f && hitStunTimer <= 0f;
        bool usesAnimatorSprite = directionalAnimator != null;
        bool usesDirectionalSprite = directionalSprite != null;
        bool usesAnimatedSprite = usesAnimatorSprite || usesDirectionalSprite;
        bool isSideView = !usesAnimatedSprite && currentFacing == AllyVisualGenerator.CharDirection.Side;

        directionalSprite?.SetMotion(new Vector2(delta.x, delta.y), isMoving);
        directionalAnimator?.SetMotion(new Vector2(delta.x, delta.y), isMoving);

        if (isMoving)
        {
            float cycleFrequency = usesAnimatedSprite ? walkCycleFrequency * 0.68f : walkCycleFrequency;
            walkCycle += Time.deltaTime * cycleFrequency * Mathf.Clamp(moveSpeed / 3f, 0.7f, 1.45f);
        }
        else
            walkCycle += Time.deltaTime * 1.2f;

        float bob         = Mathf.Sin(walkCycle);
        float bounce      = Mathf.Abs(bob);
        float squashPhase = Mathf.Abs(Mathf.Cos(walkCycle));

        // 아이들 숨쉬기: walkCycle * 0.55 → ~0.17 Hz (자연스러운 숨결 속도)
        float breathPhase = Mathf.Sin(walkCycle * 0.55f);

        float animatedMoveBobMultiplier = usesAnimatorSprite ? 0.22f : usesDirectionalSprite ? 0.34f : 1f;
        float animatedIdleBobMultiplier = usesAnimatorSprite ? 0.45f : usesDirectionalSprite ? 0.28f : 1f;

        float rootY = isMoving
            ? bounce * walkBobAmplitude * animatedMoveBobMultiplier
            : breathPhase * idleBreathAmount * animatedIdleBobMultiplier;
        float rootTilt = isSideView
            ? (isMoving ? -facingSign * walkTiltAngle * bob : 0f)
            : 0f;

        float xFacing = isSideView ? facingSign : 1f;

        Vector3 targetRootScale = visualBaseLocalScale;
        if (isMoving)
        {
            float squashFactor = usesAnimatorSprite ? walkSquashAmount * 0.18f : usesDirectionalSprite ? walkSquashAmount * 0.28f : walkSquashAmount;
            targetRootScale.x = Mathf.Abs(targetRootScale.x) * xFacing * (1f - squashPhase * squashFactor * 0.35f);
            targetRootScale.y *= 1f + squashPhase * squashFactor;
        }
        else
        {
            // 흡기: Y 팽창 + X 수축 (가슴 부풀어오르는 느낌)
            float breathScale = usesAnimatorSprite ? idleBreathScale * 0.4f : usesDirectionalSprite ? idleBreathScale * 0.18f : idleBreathScale;
            targetRootScale.x = Mathf.Abs(targetRootScale.x) * xFacing * (1f - breathPhase * breathScale * 0.5f);
            targetRootScale.y *= 1f + breathPhase * breathScale;
        }

        visualRoot.localPosition = Vector3.Lerp(
            visualRoot.localPosition,
            visualBaseLocalPosition + Vector3.up * rootY,
            Time.deltaTime * motionSharpness);

        visualRoot.localScale = Vector3.Lerp(
            visualRoot.localScale,
            targetRootScale,
            Time.deltaTime * motionSharpness);

        Quaternion targetRootRot = visualBaseLocalRotation * Quaternion.Euler(0f, 0f, rootTilt);
        visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, targetRootRot, Time.deltaTime * motionSharpness);

        // Animator 기반 단일 스프라이트 또는 Front/Back 뷰에서는 파츠 애니메이션을 건너뜁니다.
        if (usesAnimatedSprite || !isSideView)
            return;

        float legSwing = isMoving ? legSwingAngle  * bob  : legSwingAngle * 0.06f * Mathf.Sin(walkCycle * 0.8f);
        float armSwing = isMoving ? armSwingAngle  * -bob : idleArmSwing  * Mathf.Sin(walkCycle * 0.48f);
        float headTilt = isMoving ? headNodAngle   * Mathf.Sin(walkCycle * 0.5f)
                                  : idleHeadSway   * Mathf.Sin(walkCycle * 0.37f);

        ApplyPartPose(frontLegPart,  frontLegBaseLocalPosition,  frontLegBaseLocalRotation,   legSwing,            0.002f * bob);
        ApplyPartPose(backLegPart,   backLegBaseLocalPosition,   backLegBaseLocalRotation,   -legSwing,           -0.002f * bob);
        ApplyPartPose(frontArmPart,  frontArmBaseLocalPosition,  frontArmBaseLocalRotation,   armSwing,            0.003f * bob);
        ApplyPartPose(backArmPart,   backArmBaseLocalPosition,   backArmBaseLocalRotation,   -armSwing,           -0.002f * bob);
        ApplyPartPose(torsoPart,     torsoBaseLocalPosition,     torsoBaseLocalRotation,      -rootTilt * 0.25f,   0f);
        ApplyPartPose(headPart,      headBaseLocalPosition,      headBaseLocalRotation,        headTilt,            0.002f * Mathf.Sin(walkCycle * 0.5f));
    }

    void ApplyPartPose(Transform part, Vector3 basePos, Quaternion baseRot, float zAngle, float yOffset)
    {
        if (part == null) return;

        Vector3 targetPos = basePos + new Vector3(0f, yOffset, 0f);
        Quaternion targetRot = baseRot * Quaternion.Euler(0f, 0f, zAngle);

        part.localPosition = Vector3.Lerp(part.localPosition, targetPos, Time.deltaTime * motionSharpness);
        part.localRotation = Quaternion.Slerp(part.localRotation, targetRot, Time.deltaTime * motionSharpness);
    }

    // ── 방향 감지 및 스프라이트 교체 ────────────────────────────────
    void UpdateFacingDirection(Vector3 moveDir)
    {
        if (directionalAnimator != null || directionalSprite != null)
            return;

        float absX = Mathf.Abs(moveDir.x);
        float absY = Mathf.Abs(moveDir.y);

        AllyVisualGenerator.CharDirection newFacing;
        if (absX >= absY)
            newFacing = AllyVisualGenerator.CharDirection.Side;
        else if (moveDir.y > 0f)
            newFacing = AllyVisualGenerator.CharDirection.Back;   // 위 = 뒷모습
        else
            newFacing = AllyVisualGenerator.CharDirection.Front;  // 아래 = 앞모습

        if (newFacing == currentFacing) return;
        currentFacing = newFacing;

        if (visualRoot != null)
            AllyVisualGenerator.ApplyDirectionSprites(GetAllyTypeEnum(), visualRoot, currentFacing);
    }

    AllyType GetAllyTypeEnum()
    {
        return GetAllyType();
    }

    void SetAllVisualColors(Color color)
    {
        if (visualRenderers == null) return;
        foreach (var sr in visualRenderers)
        {
            if (sr != null)
                sr.color = color;
        }
    }

    void CacheTransformBase(Transform target, out Vector3 pos, out Quaternion rot)
    {
        if (target == null)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            return;
        }

        pos = target.localPosition;
        rot = target.localRotation;
    }

    void CacheTransformBase(Transform target, out Vector3 pos, out Quaternion rot, out Vector3 scale)
    {
        if (target == null)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            scale = Vector3.one;
            return;
        }

        pos = target.localPosition;
        rot = target.localRotation;
        scale = target.localScale;
    }

    public float HpRatio => Mathf.Clamp01(currentHp / maxHp);

    // ── HP 바 생성 및 갱신 ─────────────────────────────────────────────
    void CreateHpBar()
    {
        hpBarRoot = new GameObject("HpBar");
        hpBarRoot.transform.SetParent(transform, false);
        hpBarRoot.transform.localPosition = Vector3.zero;

        var bgGo = new GameObject("Bg");
        bgGo.transform.SetParent(hpBarRoot.transform, false);
        bgGo.transform.localPosition = new Vector3(0f, HP_BAR_OFFSET_Y, -0.1f);
        bgGo.transform.localScale    = new Vector3(HP_BAR_WIDTH, HP_BAR_HEIGHT, 1f);
        var bgSr = bgGo.AddComponent<SpriteRenderer>();
        bgSr.sprite = MakePixelSprite();
        bgSr.color  = new Color(0.08f, 0.08f, 0.08f, 0.80f);
        bgSr.sortingOrder = 30;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(hpBarRoot.transform, false);
        hpBarFill = fillGo.AddComponent<SpriteRenderer>();
        hpBarFill.sprite = MakePixelSprite();
        hpBarFill.sortingOrder = 31;

        hpBarRoot.SetActive(false);
    }

    static Sprite MakePixelSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    static Sprite CreateCircleSprite(int radius)
    {
        int size = radius * 2 + 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var clear = new Color(0, 0, 0, 0);
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
            tex.SetPixel(x, y, clear);

        float cx = size * 0.5f;
        float cy = size * 0.5f;
        float max = radius;
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float dx = x - cx;
            float dy = y - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist <= max)
            {
                float edge = Mathf.Clamp01(1f - dist / max);
                float alpha = Mathf.Lerp(0.16f, 0.95f, edge);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static Sprite[] GetParalysisArrowFrames()
    {
        if (paralysisArrowFrames != null)
            return paralysisArrowFrames;

        var sprites = Resources.LoadAll<Sprite>("Effect/clean_paralyzing_arrow_effect");
        if (sprites == null || sprites.Length == 0)
            sprites = Resources.LoadAll<Sprite>("Effect/paralyzing_arrow_effect");

        if (sprites == null || sprites.Length == 0)
        {
            paralysisArrowFrames = System.Array.Empty<Sprite>();
            return paralysisArrowFrames;
        }

        System.Array.Sort(sprites, (a, b) => ParseSpriteSuffix(a.name).CompareTo(ParseSpriteSuffix(b.name)));
        paralysisArrowFrames = sprites;
        return paralysisArrowFrames;
    }

    static int ParseSpriteSuffix(string name)
    {
        int i = name.LastIndexOf('_');
        return i >= 0 && int.TryParse(name.Substring(i + 1), out int n) ? n : 0;
    }

    void ClearAllSkillVisuals()
    {
        activeMageBarriers.Remove(this);
        if (activePaladinProtector == this)
            activePaladinProtector = null;
        skillInvulnerable = false;
        smokeEvadeChance = 0f;
        ClearMageBarrierVisual();
        ClearSmokeVisual();
        SetYellowAura(false);
    }

    void UpdateHpBar()
    {
        if (hpBarRoot == null || hpBarFill == null) return;
        float ratio = HpRatio;
        bool  show  = ratio < 0.999f && !isDead;
        hpBarRoot.SetActive(show);
        if (!show) return;

        Color barColor;
        if (ratio > 0.5f)
            barColor = Color.Lerp(new Color(0.95f, 0.82f, 0.10f), new Color(0.18f, 0.88f, 0.22f), (ratio - 0.5f) * 2f);
        else
            barColor = Color.Lerp(new Color(1f, 0.12f, 0.12f), new Color(0.95f, 0.82f, 0.10f), ratio * 2f);
        hpBarFill.color = barColor;

        float fillW = HP_BAR_WIDTH * ratio;
        hpBarFill.transform.localPosition = new Vector3(-HP_BAR_WIDTH * 0.5f + fillW * 0.5f, HP_BAR_OFFSET_Y, -0.15f);
        hpBarFill.transform.localScale    = new Vector3(fillW, HP_BAR_HEIGHT * 0.80f, 1f);
    }
}
