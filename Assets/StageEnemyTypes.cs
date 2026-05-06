using UnityEngine;
using System.Collections;

// ══════════════════════════════════════════════════════════════════
//  Stage 1 — 초원의 전투
// ══════════════════════════════════════════════════════════════════

/// <summary>숲 사수 — 활로 방향을 맞춰 화살을 날림 (s1_archer)</summary>
public class GrassSniper : SheetEnemyBase
{
    static readonly Color Robe = new Color(0.15f, 0.42f, 0.10f);
    static readonly Color Dark = new Color(0.28f, 0.22f, 0.08f);
    static readonly Color Eye  = new Color(0.30f, 0.90f, 0.20f);

    protected override string SheetName     => "s1_archer";
    protected override int[]  IdleRows      => new[] { 8, 9, 10, 11 };
    protected override int[]  AtkRows       => new[] { 16, 17, 18, 19 };
    protected override int    AtkFrameCount => 8;
    protected override int    ReleaseFrame  => 6;
    protected override float  AnimFps       => 12f;

    protected override void Awake()
    {
        enemyName = "숲 사수"; attackRange = 6f; attackDamage = 80f; attackCooldown = 3.0f;
        base.Awake();
        LoadSheetFrames(EnemyVisualGenerator.CreateSniperSprite(Robe, Dark, Eye));
    }
    protected override Color RangeColor()      => new Color(0.2f, 0.8f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.4f, 0.9f, 0.1f);
    protected override IEnumerator OnReleaseEffect(AllyBase target)
    {
        yield return StartCoroutine(ProjectileEffect(target,
            new Color(0.25f, 0.75f, 0.10f), 17f,
            new Color(0.40f, 0.95f, 0.20f), "clean_green_arrow_effect"));
    }
}

/// <summary>숲 새총병 — 새총으로 탄환을 날림 (s1_slingshot_enemy)</summary>
public class GrassSpearman : SheetEnemyBase
{
    static readonly Color Armor  = new Color(0.12f, 0.42f, 0.14f);
    static readonly Color Accent = new Color(0.62f, 0.50f, 0.18f);
    static readonly Color Weapon = new Color(0.45f, 0.32f, 0.12f);

    protected override string SheetName     => "s1_slingshot_enemy";
    protected override int[]  IdleRows      => new[] { 8, 9, 10, 11 };
    protected override int[]  AtkRows       => new[] { 16, 17, 18, 19 };
    protected override int    AtkFrameCount => 8;
    protected override int    ReleaseFrame  => 5;
    protected override float  AnimFps       => 14f;

    protected override void Awake()
    {
        enemyName = "숲 새총병"; attackRange = 3.5f; attackDamage = 60f; attackCooldown = 1.5f;
        base.Awake();
        LoadSheetFrames(EnemyVisualGenerator.CreateSpearmanSprite(Armor, Accent, Weapon));
    }
    protected override Color RangeColor()      => new Color(0.3f, 0.75f, 0.15f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.35f, 0.85f, 0.15f);
    protected override IEnumerator OnReleaseEffect(AllyBase target)
    {
        yield return StartCoroutine(OrbProjectileEffect(target,
            new Color(0.55f, 0.78f, 0.20f), 18f,
            new Color(0.60f, 0.95f, 0.25f), 0.34f, "clean_green_arrow_effect"));
    }
}

/// <summary>도끼 좀비 — 도끼를 휘둘러 근접 난타 (s1_axe_zombie)</summary>
public class GrassBrawler : SheetEnemyBase
{
    static readonly Color Armor  = new Color(0.12f, 0.60f, 0.12f);
    static readonly Color Accent = new Color(0.68f, 0.92f, 0.12f);

    protected override string SheetName     => "s1_axe_zombie";
    protected override int[]  IdleRows      => new[] { 8, 9, 10, 11 };
    protected override int[]  AtkRows       => new[] { 12, 13, 14, 15 }; // slash
    protected override int    AtkFrameCount => 6;
    protected override int    ReleaseFrame  => 3;
    protected override float  AnimFps       => 12f;

    protected override void Awake()
    {
        enemyName = "도끼 좀비"; attackRange = 1.5f; attackDamage = 40f; attackCooldown = 0.6f;
        base.Awake();
        LoadSheetFrames(EnemyVisualGenerator.CreateBrawlerSprite(Armor, Accent));
    }
    protected override Color RangeColor()      => new Color(0.4f, 0.9f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.5f, 0.95f, 0.1f);
    protected override IEnumerator OnReleaseEffect(AllyBase target)
    {
        yield return StartCoroutine(ParticleSprayEffect(target,
            new Color(0.08f, 0.60f, 0.08f),
            new Color(0.60f, 0.95f, 0.15f), 12, 0.36f, 32f, "clean_axe_effect"));
    }
}

// ══════════════════════════════════════════════════════════════════
//  Stage 2 — 사막의 요새
// ══════════════════════════════════════════════════════════════════

/// <summary>사막 궁수 — 독화살을 멀리 쏨 (s2_archer)</summary>
public class DesertSniper : SheetEnemyBase
{
    static readonly Color Robe = new Color(0.52f, 0.38f, 0.18f);
    static readonly Color Dark = new Color(0.32f, 0.22f, 0.08f);
    static readonly Color Eye  = new Color(0.92f, 0.55f, 0.05f);

    protected override string SheetName     => "s2_archer";
    protected override int[]  IdleRows      => new[] { 8, 9, 10, 11 };
    protected override int[]  AtkRows       => new[] { 16, 17, 18, 19 };
    protected override int    AtkFrameCount => 8;
    protected override int    ReleaseFrame  => 6;
    protected override float  AnimFps       => 12f;

    protected override void Awake()
    {
        enemyName = "사막 궁수"; attackRange = 6f; attackDamage = 80f; attackCooldown = 3.0f;
        base.Awake();
        LoadSheetFrames(EnemyVisualGenerator.CreateSniperSprite(Robe, Dark, Eye));
    }
    protected override Color RangeColor()      => new Color(0.9f, 0.65f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.85f, 0.70f, 0.10f);
    protected override IEnumerator OnReleaseEffect(AllyBase target)
    {
        yield return StartCoroutine(ProjectileEffect(target,
            new Color(0.85f, 0.80f, 0.20f), 15f,
            new Color(0.75f, 0.90f, 0.10f), "clean_green_arrow_effect"));
    }
}

/// <summary>지팡이 술사 — 마법 지팡이로 지그재그 에너지를 날림 (s2_rod_enemy)</summary>
public class DesertSpearman : SheetEnemyBase
{
    static readonly Color Armor  = new Color(0.82f, 0.72f, 0.48f);
    static readonly Color Accent = new Color(0.88f, 0.78f, 0.38f);
    static readonly Color Weapon = new Color(0.85f, 0.82f, 0.72f);

    protected override string SheetName     => "s2_rod_enemy";
    protected override int[]  IdleRows      => new[] { 8, 9, 10, 11 };
    protected override int[]  AtkRows       => new[] { 0, 1, 2, 3 };   // spellcast
    protected override int    AtkFrameCount => 7;
    protected override int    ReleaseFrame  => 4;
    protected override float  AnimFps       => 10f;

    protected override void Awake()
    {
        enemyName = "사막 술사"; attackRange = 3.5f; attackDamage = 60f; attackCooldown = 1.5f;
        base.Awake();
        LoadSheetFrames(EnemyVisualGenerator.CreateSpearmanSprite(Armor, Accent, Weapon));
    }
    protected override Color RangeColor()      => new Color(0.9f, 0.75f, 0.2f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.9f, 0.80f, 0.30f);
    protected override IEnumerator OnReleaseEffect(AllyBase target)
    {
        yield return StartCoroutine(ZigzagEffect(target,
            new Color(0.92f, 0.80f, 0.35f),
            new Color(0.90f, 0.72f, 0.10f), 2, 0.22f));
    }
}

/// <summary>곡괭이 전사 — 곡괭이로 근접 강타 (s2_pickaxe_enemy)</summary>
public class DesertBrawler : SheetEnemyBase
{
    static readonly Color Armor  = new Color(0.78f, 0.55f, 0.22f);
    static readonly Color Accent = new Color(0.52f, 0.30f, 0.10f);

    protected override string SheetName     => "s2_pickaxe_enemy";
    protected override int[]  IdleRows      => new[] { 8, 9, 10, 11 };
    protected override int[]  AtkRows       => new[] { 12, 13, 14, 15 }; // slash
    protected override int    AtkFrameCount => 6;
    protected override int    ReleaseFrame  => 3;
    protected override float  AnimFps       => 12f;

    protected override void Awake()
    {
        enemyName = "곡괭이 전사"; attackRange = 1.5f; attackDamage = 40f; attackCooldown = 0.6f;
        base.Awake();
        LoadSheetFrames(EnemyVisualGenerator.CreateBrawlerSprite(Armor, Accent));
    }
    protected override Color RangeColor()      => new Color(0.9f, 0.70f, 0.2f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.9f, 0.65f, 0.15f);
    protected override IEnumerator OnReleaseEffect(AllyBase target)
    {
        yield return StartCoroutine(ParticleSprayEffect(target,
            new Color(0.82f, 0.65f, 0.22f),
            new Color(0.95f, 0.88f, 0.55f), 12, 0.36f, 38f, "clean_pickaxe_effect"));
    }
}

// ══════════════════════════════════════════════════════════════════
//  Stage 3 — 화산의 심판
// ══════════════════════════════════════════════════════════════════

/// <summary>화산 궁수 — 불화살로 관통 (s3_archer)</summary>
public class VolcanoSniper : SheetEnemyBase
{
    static readonly Color Robe = new Color(0.55f, 0.05f, 0.05f);
    static readonly Color Dark = new Color(0.20f, 0.02f, 0.02f);
    static readonly Color Eye  = new Color(1.00f, 0.10f, 0.00f);

    protected override string SheetName     => "s3_archer";
    protected override int[]  IdleRows      => new[] { 8, 9, 10, 11 };
    protected override int[]  AtkRows       => new[] { 16, 17, 18, 19 };
    protected override int    AtkFrameCount => 8;
    protected override int    ReleaseFrame  => 6;
    protected override float  AnimFps       => 12f;

    protected override void Awake()
    {
        enemyName = "화산 궁수"; attackRange = 6f; attackDamage = 80f; attackCooldown = 3.0f;
        base.Awake();
        LoadSheetFrames(EnemyVisualGenerator.CreateSniperSprite(Robe, Dark, Eye));
    }
    protected override Color RangeColor()      => new Color(0.9f, 0.1f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(1.0f, 0.3f, 0.0f);
    protected override IEnumerator OnReleaseEffect(AllyBase target)
    {
        yield return StartCoroutine(ProjectileEffect(target,
            new Color(1.00f, 0.28f, 0.05f), 17f,
            new Color(1.00f, 0.40f, 0.05f), "clean_red_arrow_effect"));
    }
}

/// <summary>부메랑 투척수 — 부메랑이 지그재그로 날아감 (s3_booberang_enemy)</summary>
public class VolcanoSpearman : SheetEnemyBase
{
    static readonly Color Armor  = new Color(0.22f, 0.14f, 0.12f);
    static readonly Color Accent = new Color(0.90f, 0.32f, 0.04f);
    static readonly Color Weapon = new Color(0.65f, 0.30f, 0.10f);

    protected override string SheetName     => "s3_booberang_enemy";
    protected override int[]  IdleRows      => new[] { 8, 9, 10, 11 };
    protected override int[]  AtkRows       => new[] { 16, 17, 18, 19 }; // throw
    protected override int    AtkFrameCount => 8;
    protected override int    ReleaseFrame  => 5;
    protected override float  AnimFps       => 12f;

    protected override void Awake()
    {
        enemyName = "부메랑 투척수"; attackRange = 3.5f; attackDamage = 60f; attackCooldown = 1.5f;
        base.Awake();
        LoadSheetFrames(EnemyVisualGenerator.CreateSpearmanSprite(Armor, Accent, Weapon));
    }
    protected override Color RangeColor()      => new Color(0.9f, 0.3f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(1.0f, 0.4f, 0.05f);
    protected override IEnumerator OnReleaseEffect(AllyBase target)
    {
        yield return StartCoroutine(BoomerangEffect(target,
            new Color(1.00f, 0.35f, 0.05f),
            new Color(1.00f, 0.60f, 0.10f), 13f, 0.55f));
    }
}

/// <summary>화산 도끼전사 — 불도끼로 강타 (s3_axe_enemy)</summary>
public class VolcanoBrawler : SheetEnemyBase
{
    static readonly Color Armor  = new Color(0.25f, 0.10f, 0.08f);
    static readonly Color Accent = new Color(0.90f, 0.35f, 0.05f);

    protected override string SheetName     => "s3_axe_enemy";
    protected override int[]  IdleRows      => new[] { 8, 9, 10, 11 };
    protected override int[]  AtkRows       => new[] { 12, 13, 14, 15 }; // slash
    protected override int    AtkFrameCount => 6;
    protected override int    ReleaseFrame  => 3;
    protected override float  AnimFps       => 12f;

    protected override void Awake()
    {
        enemyName = "화산 도끼전사"; attackRange = 1.5f; attackDamage = 40f; attackCooldown = 0.6f;
        base.Awake();
        LoadSheetFrames(EnemyVisualGenerator.CreateBrawlerSprite(Armor, Accent));
    }
    protected override Color RangeColor()      => new Color(0.9f, 0.3f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(1.0f, 0.4f, 0.05f);
    protected override IEnumerator OnReleaseEffect(AllyBase target)
    {
        yield return StartCoroutine(ParticleSprayEffect(target,
            new Color(0.90f, 0.25f, 0.02f),
            new Color(1.00f, 0.60f, 0.10f), 14, 0.40f, 35f, "clean_axe_effect"));
    }
}

// ══════════════════════════════════════════════════════════════════
//  Stage 4 — 어둠의 미궁  (절차적 스프라이트 — 시트 없음)
// ══════════════════════════════════════════════════════════════════

public class ShadowSniper : EnemyBase
{
    static readonly Color Robe = new Color(0.22f, 0.05f, 0.38f);
    static readonly Color Dark = new Color(0.07f, 0.02f, 0.14f);
    static readonly Color Eye  = new Color(0.75f, 0.00f, 1.00f);
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
        if (spriteRenderer != null) { Color o = spriteRenderer.color; spriteRenderer.color = new Color(0.55f,0.05f,0.85f); yield return new WaitForSeconds(0.05f); spriteRenderer.color = o; }
        if (target != null) StartCoroutine(ThinBeamEffect(target, new Color(0.60f,0.00f,0.90f), new Color(0.35f,0.00f,0.60f)));
    }
}

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
        if (spriteRenderer != null) { Color o = spriteRenderer.color; spriteRenderer.color = new Color(0.55f,0.08f,0.85f); yield return new WaitForSeconds(0.05f); spriteRenderer.color = o; }
        if (target != null) StartCoroutine(ZigzagEffect(target, new Color(0.55f,0.05f,0.85f), new Color(0.30f,0.00f,0.55f), 4, 0.25f));
    }
}

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
        if (spriteRenderer != null) { Color o = spriteRenderer.color; spriteRenderer.color = new Color(0.50f,0.08f,0.80f); yield return new WaitForSeconds(0.04f); spriteRenderer.color = o; }
        if (target != null) StartCoroutine(ParticleSprayEffect(target, new Color(0.25f,0.02f,0.45f), new Color(0.55f,0.08f,0.80f), 14, 0.42f, 28f));
    }
}

// ══════════════════════════════════════════════════════════════════
//  Stage 5 — 최후의 요새  (절차적 스프라이트 — 시트 없음)
// ══════════════════════════════════════════════════════════════════

public class FortressSniper : EnemyBase
{
    static readonly Color Robe = new Color(0.68f, 0.52f, 0.10f);
    static readonly Color Dark = new Color(0.22f, 0.15f, 0.05f);
    static readonly Color Eye  = new Color(1.00f, 0.88f, 0.00f);
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
        if (spriteRenderer != null) { Color o = spriteRenderer.color; spriteRenderer.color = new Color(1.0f,0.88f,0.05f); yield return new WaitForSeconds(0.05f); spriteRenderer.color = o; }
        if (target != null) StartCoroutine(GoldenBeamEffect(target));
    }
    IEnumerator GoldenBeamEffect(AllyBase target)
    {
        if (target == null) yield break;
        var lr = CreateTempLineRenderer(2, new Color(1f,0.88f,0f), 0.06f, 28);
        lr.endColor = new Color(1f,0.95f,0.5f,0.4f);
        lr.SetPosition(0, transform.position); lr.SetPosition(1, target.transform.position);
        float e = 0f;
        while (e < 0.22f) { e += Time.deltaTime; float a = 1f-(e/0.22f)*(e/0.22f); lr.startColor=new Color(1f,0.88f,0f,a); lr.endColor=new Color(1f,0.95f,0.5f,a*0.4f); yield return null; }
        Destroy(lr.gameObject);
        SpawnImpactFlash(target.transform.position, new Color(1f,0.90f,0.10f,1f), 0.30f);
    }
}

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
        if (spriteRenderer != null) { Color o = spriteRenderer.color; spriteRenderer.color = new Color(0.4f,0.6f,1.0f); yield return new WaitForSeconds(0.05f); spriteRenderer.color = o; }
        if (target != null) StartCoroutine(ZigzagEffect(target, new Color(0.30f,0.60f,1.00f), new Color(0.70f,0.88f,1.00f), 4, 0.22f));
    }
}

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
        if (spriteRenderer != null) { Color o = spriteRenderer.color; spriteRenderer.color = new Color(0.9f,0.80f,0.15f); yield return new WaitForSeconds(0.04f); spriteRenderer.color = o; }
        if (target != null) StartCoroutine(ParticleSprayEffect(target, new Color(0.55f,0.55f,0.62f), new Color(1.00f,0.88f,0.15f), 16, 0.40f, 40f));
    }
}
