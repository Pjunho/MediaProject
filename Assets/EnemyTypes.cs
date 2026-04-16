using UnityEngine;

/// <summary>
/// 저격수 - 사거리 길고 공격 느림
/// </summary>
public class EnemySniper : EnemyBase
{
    protected override void Awake()
    {
        enemyName      = "저격수";
        attackRange    = 6f;
        attackDamage   = 80f;   // 25 → 80
        attackCooldown = 3.0f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite = EnemyVisualGenerator.CreateSniperSprite();
    }

    protected override Color RangeColor()      => new Color(0.9f, 0.1f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(1f,   0.9f, 0.1f);
}

/// <summary>
/// 창병 - 중간 사거리 균형형
/// </summary>
public class EnemySpearman : EnemyBase
{
    protected override void Awake()
    {
        enemyName      = "창병";
        attackRange    = 3.5f;
        attackDamage   = 60f;   // 15 → 60
        attackCooldown = 1.5f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite = EnemyVisualGenerator.CreateSpearmanSprite();
    }

    protected override Color RangeColor()      => new Color(0.2f, 0.8f, 0.2f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.3f, 1f,   0.3f);
}

/// <summary>
/// 근접병 - 짧은 사거리 빠른 공격
/// </summary>
public class EnemyBrawler : EnemyBase
{
    protected override void Awake()
    {
        enemyName      = "근접병";
        attackRange    = 1.5f;
        attackDamage   = 40f;   // 8 → 40
        attackCooldown = 0.6f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite = EnemyVisualGenerator.CreateBrawlerSprite();
    }

    protected override Color RangeColor()      => new Color(1f,   0.5f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(1f,   0.4f, 0.1f);
}
