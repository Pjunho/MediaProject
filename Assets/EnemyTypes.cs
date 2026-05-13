using UnityEngine;
using System.Collections;

/// <summary>
/// 저격수 - 사거리 길고 공격 느림 / 저격 빔 + 충격 이펙트
/// </summary>
public class EnemySniper : EnemyBase
{
    protected override void Awake()
    {
        enemyName      = "저격수";
        attackRange    = 6f;
        attackDamage   = 80f;
        attackCooldown = 3.0f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite = EnemyVisualGenerator.CreateSniperSprite();
    }

    protected override Color RangeColor()      => new Color(0.9f, 0.1f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(1f,   0.9f, 0.1f);

    // 저격 빔: 극세 진홍색 레이저가 순간 발사되고 빠르게 소멸, 피격 지점에 충격 폭발
    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        GameAudio.PlaySniperShot();
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(0.9f, 0.05f, 0.05f);
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.color = orig;
        }
        if (target == null) yield break;

        Vector3 src = transform.position;
        Vector3 dst = target.transform.position;

        var lr = CreateTempLineRenderer(2, new Color(1f, 0.05f, 0.05f), 0.035f, 28);
        lr.SetPosition(0, src);
        lr.SetPosition(1, dst);

        // 빔이 밝게 등장했다가 빠르게 소멸
        float dur = 0.20f, elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float a = 1f - t * t;
            lr.startColor = new Color(1f, 0.05f, 0.05f, a);
            lr.endColor   = new Color(1f, 0.55f, 0.55f, a * 0.55f);
            yield return null;
        }
        Destroy(lr.gameObject);

        SpawnImpactFlash(dst, new Color(1f, 0.1f, 0.1f, 1f), 0.22f);
    }
}

/// <summary>
/// 창병 - 중간 사거리 균형형 / 번개 창: 초록 지그재그 번개가 3회 점멸 후 충격
/// </summary>
public class EnemySpearman : EnemyBase
{
    protected override void Awake()
    {
        enemyName      = "창병";
        attackRange    = 3.5f;
        attackDamage   = 60f;
        attackCooldown = 1.5f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite = EnemyVisualGenerator.CreateSpearmanSprite();
    }

    protected override Color RangeColor()      => new Color(0.2f, 0.8f, 0.2f, 0.7f);
    protected override Color AttackLineColor() => new Color(0.3f, 1f,   0.3f);

    // 번개 창: 지그재그 초록 번개가 3회 점멸, 피격 지점에 초록 충격
    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        GameAudio.PlayBluntSwing();
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(0.3f, 1f, 0.35f);
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.color = orig;
        }
        if (target == null) yield break;

        Vector3 src = transform.position;
        Vector3 dst = target.transform.position;

        var lr = CreateTempLineRenderer(10, new Color(0.3f, 1f, 0.2f), 0.07f, 28);
        lr.endColor = new Color(0.7f, 1f, 0.4f, 0.25f);

        // 3회 지그재그 점멸 (매번 경로를 재계산해 살아있는 번개처럼 보임)
        for (int flash = 0; flash < 3; flash++)
        {
            Vector3[] pts = BuildLightningPath(src, dst, 9, 0.30f);
            for (int i = 0; i < pts.Length; i++) lr.SetPosition(i, pts[i]);
            lr.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.045f);
            lr.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.03f);
        }
        Destroy(lr.gameObject);

        SpawnImpactFlash(dst, new Color(0.4f, 1f, 0.3f, 1f), 0.18f);
    }
}

/// <summary>
/// 근접병 - 짧은 사거리 빠른 공격 / 화염 분사: 주황-빨강 불꽃 파티클이 부채꼴로 분사
/// </summary>
public class EnemyBrawler : EnemyBase
{
    protected override void Awake()
    {
        enemyName      = "근접병";
        attackRange    = 1.5f;
        attackDamage   = 40f;
        attackCooldown = 0.6f;
        base.Awake();
        if (spriteRenderer != null)
            spriteRenderer.sprite = EnemyVisualGenerator.CreateBrawlerSprite();
    }

    protected override Color RangeColor()      => new Color(1f,   0.5f, 0.1f, 0.7f);
    protected override Color AttackLineColor() => new Color(1f,   0.4f, 0.1f);

    // 화염 분사: 불꽃 파티클이 ±30° 부채꼴로 퍼지며 소멸
    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        GameAudio.PlayBluntSwing();
        if (spriteRenderer != null)
        {
            Color orig = spriteRenderer.color;
            spriteRenderer.color = new Color(1f, 0.35f, 0.05f);
            yield return new WaitForSeconds(0.04f);
            spriteRenderer.color = orig;
        }
        if (target == null) yield break;

        Vector3 src  = transform.position + Vector3.up * 0.15f;
        Vector3 dst  = target.transform.position;
        Vector3 dir  = (dst - src).normalized;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        float dist   = Vector3.Distance(src, dst);

        const int count = 13;
        var gos    = new GameObject[count];
        var speeds = new float[count];
        var sizes  = new float[count];
        var pdirs  = new Vector3[count];
        var cols   = new Color[count];

        Sprite circleSpr = MakeCircleSprite(7);

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("FlameP");
            go.transform.position = src + dir * (dist * Random.Range(0f, 0.18f));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circleSpr;
            // 불꽃 색상: 빨강 → 주황 → 노랑 그라데이션
            float t = Random.value;
            Color c = Color.Lerp(new Color(1f, 0.15f, 0f, 1f), new Color(1f, 0.85f, 0.1f, 1f), t);
            sr.color = c;
            cols[i]  = c;
            sr.sortingOrder = 30;

            float ang = Random.Range(-30f, 30f) * Mathf.Deg2Rad;
            pdirs[i]  = (dir * Mathf.Cos(ang) + perp * Mathf.Sin(ang)).normalized;
            speeds[i] = Random.Range(4f, 9f);
            sizes[i]  = Random.Range(0.10f, 0.26f);
            go.transform.localScale = Vector3.one * sizes[i];
            gos[i] = go;
        }

        float duration = 0.38f, elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            for (int i = 0; i < count; i++)
            {
                if (gos[i] == null) continue;
                gos[i].transform.position += pdirs[i] * speeds[i] * Time.deltaTime;
                var sr = gos[i].GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Color c = cols[i];
                    c.a = 1f - t * t;
                    sr.color = c;
                }
                gos[i].transform.localScale = Vector3.one * (sizes[i] * Mathf.Lerp(1.2f, 0.2f, t));
            }
            yield return null;
        }

        for (int i = 0; i < count; i++)
            if (gos[i] != null) Destroy(gos[i]);
    }
}
