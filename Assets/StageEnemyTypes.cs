using UnityEngine;
using System.Collections;

// ══════════════════════════════════════════════════════════════════
//  Stage 1 — 초원의 전투  (자연/풀 속성)
//  색상: 밝은 초록, 흙갈색, 연두 / 이펙트: 잎사귀·덩굴·가시
// ══════════════════════════════════════════════════════════════════

/// <summary>초원 저격수 — 빠른 잎사귀 화살이 날아가 꽂힘</summary>
public class GrassSniper : EnemyBase
{
    static readonly Color Robe   = new Color(0.15f, 0.42f, 0.10f);
    static readonly Color Dark   = new Color(0.28f, 0.22f, 0.08f);
    static readonly Color Eye    = new Color(0.30f, 0.90f, 0.20f);

    protected override void Awake()
    {
        enemyName = "숲 사수"; attackRange = 6f; attackDamage = 80f; attackCooldown = 3.0f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite =
                EnemyVisualGenerator.TryLoadSprite("s1_sn") ??
                EnemyVisualGenerator.CreateSniperSprite(Robe, Dark, Eye);
    }
    protected override Color RangeColor()      => new Color(0.2f, 0.8f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.4f, 0.9f, 0.1f);

    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(0.3f, 0.9f, 0.15f);
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.color = orig;
        }
        if (target != null)
            StartCoroutine(ProjectileEffect(target,
                new Color(0.25f, 0.75f, 0.10f), 17f,
                new Color(0.40f, 0.95f, 0.20f)));
    }
}

/// <summary>초원 창병 — 덩굴 채찍이 지그재그로 내리침</summary>
public class GrassSpearman : EnemyBase
{
    static readonly Color Armor  = new Color(0.12f, 0.42f, 0.14f);
    static readonly Color Accent = new Color(0.62f, 0.50f, 0.18f);
    static readonly Color Weapon = new Color(0.45f, 0.32f, 0.12f);

    protected override void Awake()
    {
        enemyName = "덩굴 창병"; attackRange = 3.5f; attackDamage = 60f; attackCooldown = 1.5f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite =
                EnemyVisualGenerator.TryLoadSprite("s1_sp") ??
                EnemyVisualGenerator.CreateSpearmanSprite(Armor, Accent, Weapon);
    }
    protected override Color RangeColor()      => new Color(0.3f, 0.75f, 0.15f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.35f, 0.85f, 0.15f);

    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(0.35f, 0.85f, 0.15f);
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.color = orig;
        }
        if (target != null)
            StartCoroutine(ZigzagEffect(target,
                new Color(0.35f, 0.80f, 0.10f),
                new Color(0.50f, 0.95f, 0.25f), 2, 0.20f));
    }
}

/// <summary>초원 근접병 — 가시 돌풍이 부채꼴로 분사</summary>
public class GrassBrawler : EnemyBase
{
    static readonly Color Armor  = new Color(0.12f, 0.60f, 0.12f);
    static readonly Color Accent = new Color(0.68f, 0.92f, 0.12f);

    protected override void Awake()
    {
        enemyName = "가시 전사"; attackRange = 1.5f; attackDamage = 40f; attackCooldown = 0.6f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite =
                EnemyVisualGenerator.TryLoadSprite("s1_br") ??
                EnemyVisualGenerator.CreateBrawlerSprite(Armor, Accent);
    }
    protected override Color RangeColor()      => new Color(0.4f, 0.9f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.5f, 0.95f, 0.1f);

    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(0.40f, 0.95f, 0.15f);
            yield return new WaitForSeconds(0.04f);
            spriteRenderer.color = orig;
        }
        if (target != null)
            StartCoroutine(ParticleSprayEffect(target,
                new Color(0.08f, 0.60f, 0.08f),
                new Color(0.60f, 0.95f, 0.15f), 12, 0.36f, 32f));
    }
}

// ══════════════════════════════════════════════════════════════════
//  Stage 2 — 사막의 요새  (모래/독 속성)
//  색상: 모래황, 뼈흰색, 갈색 / 이펙트: 모래폭풍·뼈창·독침
// ══════════════════════════════════════════════════════════════════

/// <summary>전갈 사수 — 독침 투사체가 빠르게 날아가 박힘</summary>
public class DesertSniper : EnemyBase
{
    static readonly Color Robe   = new Color(0.52f, 0.38f, 0.18f);
    static readonly Color Dark   = new Color(0.32f, 0.22f, 0.08f);
    static readonly Color Eye    = new Color(0.92f, 0.55f, 0.05f);

    protected override void Awake()
    {
        enemyName = "전갈 사수"; attackRange = 6f; attackDamage = 80f; attackCooldown = 3.0f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite =
                EnemyVisualGenerator.TryLoadSprite("s2_sn") ??
                EnemyVisualGenerator.CreateSniperSprite(Robe, Dark, Eye);
    }
    protected override Color RangeColor()      => new Color(0.9f, 0.65f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.85f, 0.70f, 0.10f);

    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(0.95f, 0.70f, 0.10f);
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.color = orig;
        }
        if (target != null)
            StartCoroutine(ProjectileEffect(target,
                new Color(0.85f, 0.80f, 0.20f), 15f,
                new Color(0.75f, 0.90f, 0.10f)));
    }
}

/// <summary>모래 창병 — 뼈창이 황사처럼 지그재그로 날아감</summary>
public class DesertSpearman : EnemyBase
{
    static readonly Color Armor  = new Color(0.82f, 0.72f, 0.48f);
    static readonly Color Accent = new Color(0.88f, 0.78f, 0.38f);
    static readonly Color Weapon = new Color(0.85f, 0.82f, 0.72f);

    protected override void Awake()
    {
        enemyName = "모래 창병"; attackRange = 3.5f; attackDamage = 60f; attackCooldown = 1.5f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite =
                EnemyVisualGenerator.TryLoadSprite("s2_sp") ??
                EnemyVisualGenerator.CreateSpearmanSprite(Armor, Accent, Weapon);
    }
    protected override Color RangeColor()      => new Color(0.9f, 0.75f, 0.2f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.9f, 0.80f, 0.30f);

    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(0.95f, 0.82f, 0.30f);
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.color = orig;
        }
        if (target != null)
            StartCoroutine(ZigzagEffect(target,
                new Color(0.92f, 0.80f, 0.35f),
                new Color(0.90f, 0.72f, 0.10f), 2, 0.22f));
    }
}

/// <summary>사막 전사 — 모래폭풍이 넓은 부채꼴로 퍼짐</summary>
public class DesertBrawler : EnemyBase
{
    static readonly Color Armor  = new Color(0.78f, 0.55f, 0.22f);
    static readonly Color Accent = new Color(0.52f, 0.30f, 0.10f);

    protected override void Awake()
    {
        enemyName = "사막 전사"; attackRange = 1.5f; attackDamage = 40f; attackCooldown = 0.6f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite =
                EnemyVisualGenerator.TryLoadSprite("s2_br") ??
                EnemyVisualGenerator.CreateBrawlerSprite(Armor, Accent);
    }
    protected override Color RangeColor()      => new Color(0.9f, 0.70f, 0.2f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.9f, 0.65f, 0.15f);

    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(0.95f, 0.70f, 0.20f);
            yield return new WaitForSeconds(0.04f);
            spriteRenderer.color = orig;
        }
        if (target != null)
            StartCoroutine(ParticleSprayEffect(target,
                new Color(0.82f, 0.65f, 0.22f),
                new Color(0.95f, 0.88f, 0.55f), 12, 0.36f, 38f));
    }
}

// ══════════════════════════════════════════════════════════════════
//  Stage 3 — 화산의 심판  (화염/용암 속성)
//  EnemyBrawler(기존) 그대로 사용 + 창병/저격수 신규
//  색상: 진홍, 주황, 용암검정 / 이펙트: 용암폭발·마그마빔
// ══════════════════════════════════════════════════════════════════

/// <summary>마그마 저격수 — 진홍 마그마 빔이 순간 관통</summary>
public class VolcanoSniper : EnemyBase
{
    static readonly Color Robe   = new Color(0.55f, 0.05f, 0.05f);
    static readonly Color Dark   = new Color(0.20f, 0.02f, 0.02f);
    static readonly Color Eye    = new Color(1.00f, 0.10f, 0.00f);

    protected override void Awake()
    {
        enemyName = "마그마 저격수"; attackRange = 6f; attackDamage = 80f; attackCooldown = 3.0f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite =
                EnemyVisualGenerator.TryLoadSprite("s3_sn") ??
                EnemyVisualGenerator.CreateSniperSprite(Robe, Dark, Eye);
    }
    protected override Color RangeColor()      => new Color(0.9f, 0.1f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(1.0f, 0.3f, 0.0f);

    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(0.9f, 0.05f, 0.05f);
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.color = orig;
        }
        if (target != null)
            StartCoroutine(ThinBeamEffect(target,
                new Color(1.00f, 0.10f, 0.00f),
                new Color(1.00f, 0.40f, 0.05f)));
    }
}

/// <summary>화산 창병 — 용암 전기가 지그재그로 불길을 남김</summary>
public class VolcanoSpearman : EnemyBase
{
    static readonly Color Armor  = new Color(0.22f, 0.14f, 0.12f);
    static readonly Color Accent = new Color(0.90f, 0.32f, 0.04f);
    static readonly Color Weapon = new Color(0.65f, 0.30f, 0.10f);

    protected override void Awake()
    {
        enemyName = "화산 창병"; attackRange = 3.5f; attackDamage = 60f; attackCooldown = 1.5f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite =
                EnemyVisualGenerator.TryLoadSprite("s3_sp") ??
                EnemyVisualGenerator.CreateSpearmanSprite(Armor, Accent, Weapon);
    }
    protected override Color RangeColor()      => new Color(0.9f, 0.3f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(1.0f, 0.4f, 0.05f);

    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(1.0f, 0.35f, 0.05f);
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.color = orig;
        }
        if (target != null)
            StartCoroutine(ZigzagEffect(target,
                new Color(1.00f, 0.35f, 0.05f),
                new Color(1.00f, 0.60f, 0.10f), 2, 0.22f));
    }
}

// ══════════════════════════════════════════════════════════════════
//  Stage 4 — 어둠의 미궁  (공허/저주 속성)
//  색상: 흑보라, 심연검정 / 이펙트: 그림자 촉수·공허창·영혼빔
// ══════════════════════════════════════════════════════════════════

/// <summary>영혼 저격수 — 어둠의 영혼 빔이 무소리로 꿰뚫음</summary>
public class ShadowSniper : EnemyBase
{
    static readonly Color Robe   = new Color(0.22f, 0.05f, 0.38f);
    static readonly Color Dark   = new Color(0.07f, 0.02f, 0.14f);
    static readonly Color Eye    = new Color(0.75f, 0.00f, 1.00f);

    protected override void Awake()
    {
        enemyName = "영혼 저격수"; attackRange = 6f; attackDamage = 80f; attackCooldown = 3.0f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite =
                EnemyVisualGenerator.TryLoadSprite("s4_sn") ??
                EnemyVisualGenerator.CreateSniperSprite(Robe, Dark, Eye);
    }
    protected override Color RangeColor()      => new Color(0.55f, 0.1f, 0.8f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.65f, 0.1f, 0.9f);

    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(0.55f, 0.05f, 0.85f);
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.color = orig;
        }
        if (target != null)
            StartCoroutine(ThinBeamEffect(target,
                new Color(0.60f, 0.00f, 0.90f),
                new Color(0.35f, 0.00f, 0.60f)));
    }
}

/// <summary>공허 창병 — 보이지 않는 공허의 창이 4회 점멸</summary>
public class ShadowSpearman : EnemyBase
{
    static readonly Color Armor  = new Color(0.10f, 0.04f, 0.18f);
    static readonly Color Accent = new Color(0.50f, 0.08f, 0.75f);
    static readonly Color Weapon = new Color(0.30f, 0.05f, 0.55f);

    protected override void Awake()
    {
        enemyName = "공허 창병"; attackRange = 3.5f; attackDamage = 60f; attackCooldown = 1.5f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite =
                EnemyVisualGenerator.TryLoadSprite("s4_sp") ??
                EnemyVisualGenerator.CreateSpearmanSprite(Armor, Accent, Weapon);
    }
    protected override Color RangeColor()      => new Color(0.5f, 0.1f, 0.7f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.55f, 0.08f, 0.8f);

    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(0.55f, 0.08f, 0.85f);
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.color = orig;
        }
        if (target != null)
            StartCoroutine(ZigzagEffect(target,
                new Color(0.55f, 0.05f, 0.85f),
                new Color(0.30f, 0.00f, 0.55f), 4, 0.25f));
    }
}

/// <summary>그림자 전사 — 어둠의 촉수가 소용돌이치며 분사</summary>
public class ShadowBrawler : EnemyBase
{
    static readonly Color Armor  = new Color(0.08f, 0.05f, 0.12f);
    static readonly Color Accent = new Color(0.42f, 0.10f, 0.62f);

    protected override void Awake()
    {
        enemyName = "그림자 전사"; attackRange = 1.5f; attackDamage = 40f; attackCooldown = 0.6f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite =
                EnemyVisualGenerator.TryLoadSprite("s4_br") ??
                EnemyVisualGenerator.CreateBrawlerSprite(Armor, Accent);
    }
    protected override Color RangeColor()      => new Color(0.5f, 0.1f, 0.7f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.5f, 0.08f, 0.75f);

    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(0.50f, 0.08f, 0.80f);
            yield return new WaitForSeconds(0.04f);
            spriteRenderer.color = orig;
        }
        if (target != null)
            StartCoroutine(ParticleSprayEffect(target,
                new Color(0.25f, 0.02f, 0.45f),
                new Color(0.55f, 0.08f, 0.80f), 14, 0.42f, 28f));
    }
}

// ══════════════════════════════════════════════════════════════════
//  Stage 5 — 최후의 요새  (강철/황금 속성)
//  색상: 은강철, 황금, 심청 / 이펙트: 에너지 충격파·뇌폭풍·황금빔
// ══════════════════════════════════════════════════════════════════

/// <summary>황금 저격수 — 황금빔 + 강대한 충격 폭발</summary>
public class FortressSniper : EnemyBase
{
    static readonly Color Robe   = new Color(0.68f, 0.52f, 0.10f);
    static readonly Color Dark   = new Color(0.22f, 0.15f, 0.05f);
    static readonly Color Eye    = new Color(1.00f, 0.88f, 0.00f);

    protected override void Awake()
    {
        enemyName = "황금 저격수"; attackRange = 6f; attackDamage = 80f; attackCooldown = 3.0f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite =
                EnemyVisualGenerator.TryLoadSprite("s5_sn") ??
                EnemyVisualGenerator.CreateSniperSprite(Robe, Dark, Eye);
    }
    protected override Color RangeColor()      => new Color(0.9f, 0.75f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(1.0f, 0.88f, 0.0f);

    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(1.0f, 0.88f, 0.05f);
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.color = orig;
        }
        if (target != null)
            StartCoroutine(GoldenBeamEffect(target));
    }

    // 황금빔 전용: 평범 빔보다 두껍고 폭발이 큼
    IEnumerator GoldenBeamEffect(AllyBase target)
    {
        if (target == null) yield break;
        Vector3 src = transform.position, dst = target.transform.position;

        var lr = CreateTempLineRenderer(2, new Color(1f, 0.88f, 0f), 0.06f, 28);
        lr.endColor = new Color(1f, 0.95f, 0.5f, 0.4f);
        lr.SetPosition(0, src);
        lr.SetPosition(1, dst);

        float elapsed = 0f;
        while (elapsed < 0.22f)
        {
            elapsed += Time.deltaTime;
            float a = 1f - (elapsed / 0.22f) * (elapsed / 0.22f);
            lr.startColor = new Color(1f, 0.88f, 0f,  a);
            lr.endColor   = new Color(1f, 0.95f, 0.5f, a * 0.4f);
            yield return null;
        }
        Destroy(lr.gameObject);
        SpawnImpactFlash(dst, new Color(1f, 0.90f, 0.10f, 1f), 0.30f);
    }
}

/// <summary>요새 창병 — 청전기 번개가 4회 강타</summary>
public class FortressSpearman : EnemyBase
{
    static readonly Color Armor  = new Color(0.40f, 0.44f, 0.55f);
    static readonly Color Accent = new Color(0.15f, 0.30f, 0.80f);
    static readonly Color Weapon = new Color(0.70f, 0.75f, 0.85f);

    protected override void Awake()
    {
        enemyName = "요새 창병"; attackRange = 3.5f; attackDamage = 60f; attackCooldown = 1.5f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite =
                EnemyVisualGenerator.TryLoadSprite("s5_sp") ??
                EnemyVisualGenerator.CreateSpearmanSprite(Armor, Accent, Weapon);
    }
    protected override Color RangeColor()      => new Color(0.2f, 0.4f, 0.9f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.3f, 0.55f, 1.0f);

    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(0.4f, 0.6f, 1.0f);
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.color = orig;
        }
        if (target != null)
            StartCoroutine(ZigzagEffect(target,
                new Color(0.30f, 0.60f, 1.00f),
                new Color(0.70f, 0.88f, 1.00f), 4, 0.22f));
    }
}

/// <summary>철벽 전사 — 황금빛 에너지 충격파가 넓게 분사</summary>
public class FortressBrawler : EnemyBase
{
    static readonly Color Armor  = new Color(0.52f, 0.54f, 0.60f);
    static readonly Color Accent = new Color(0.88f, 0.72f, 0.10f);

    protected override void Awake()
    {
        enemyName = "철벽 전사"; attackRange = 1.5f; attackDamage = 40f; attackCooldown = 0.6f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite =
                EnemyVisualGenerator.TryLoadSprite("s5_br") ??
                EnemyVisualGenerator.CreateBrawlerSprite(Armor, Accent);
    }
    protected override Color RangeColor()      => new Color(0.7f, 0.6f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.9f, 0.75f, 0.1f);

    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(0.9f, 0.80f, 0.15f);
            yield return new WaitForSeconds(0.04f);
            spriteRenderer.color = orig;
        }
        if (target != null)
            StartCoroutine(ParticleSprayEffect(target,
                new Color(0.55f, 0.55f, 0.62f),
                new Color(1.00f, 0.88f, 0.15f), 16, 0.40f, 40f));
    }
}
