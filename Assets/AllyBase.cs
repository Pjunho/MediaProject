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

    private float hitStunTimer = 0f;
    private float walkCycle    = 0f;
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
    }

    protected virtual void Update()
    {
        if (isDead || hasCompletedPath) return;

        hitStunTimer -= Time.deltaTime;
        if (hitStunTimer <= 0f)
            MoveAlongPath();

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

    public virtual void TakeDamage(float damage)
    {
        if (isDead) return;
        currentHp -= damage;
        OnDamaged();
        if (currentHp <= 0f)
            Die();
    }

    protected virtual void OnDamaged()
    {
        hitStunTimer = 0.15f;
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
        if (this is Warrior) return AllyType.Warrior;
        if (this is Archer)  return AllyType.Archer;
        if (this is Mage)    return AllyType.Mage;
        if (this is Cleric)  return AllyType.Cleric;
        return AllyType.Warrior;
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
}
