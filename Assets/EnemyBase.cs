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

    private LineRenderer rangeIndicator;
    private SpriteRenderer rangeFillIndicator;
    private LineRenderer attackLine;

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

    protected virtual void Attack(AllyBase target)
    {
        if (target == null || target.isDead) return;
        target.TakeDamage(attackDamage);
        StartCoroutine(ShowAttackEffect(target));
    }

    IEnumerator ShowAttackEffect(AllyBase target)
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
        if (rangeIndicator == null) rangeIndicator = CreateRangeIndicator();
        if (rangeFillIndicator == null) rangeFillIndicator = CreateRangeFillIndicator();
        rangeFillIndicator.gameObject.SetActive(true);
        rangeIndicator.gameObject.SetActive(true);
    }

    public void HideRange()
    {
        if (rangeFillIndicator != null)
            rangeFillIndicator.gameObject.SetActive(false);
        if (rangeIndicator != null)
            rangeIndicator.gameObject.SetActive(false);
    }

    SpriteRenderer CreateRangeFillIndicator()
    {
        var go = new GameObject("RangeFillIndicator");
        go.transform.SetParent(transform, false);
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
        go.transform.SetParent(transform, false);
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
        string state = isPlaced ? "전투 중" : "배치 대기";
        return
            $"상태: {state}\n" +
            $"사거리: {attackRange:0.0}\n" +
            $"공격력: {attackDamage:0.0}\n" +
            $"공격 속도: {attackCooldown:0.0}초";
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
