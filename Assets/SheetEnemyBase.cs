using UnityEngine;
using System.Collections;

/// <summary>
/// 스프라이트 시트 기반 적의 공통 기반 클래스.
/// 4방향(위/왼/아래/오른) 대기·공격 프레임을 캐싱하고,
/// 타겟 방향으로 자동으로 몸을 돌린다.
///
/// LPC 표준 row 배치:
///   Spellcast  0-3  (7 frames)
///   Thrust     4-7  (8 frames)
///   Walk       8-11 (9 frames)
///   Slash     12-15 (6 frames)
///   Shoot     16-19 (13 frames)
/// 방향 순서: 위(0) → 왼(1) → 아래(2) → 오른(3)
/// </summary>
public abstract class SheetEnemyBase : EnemyBase
{
    // ── 서브클래스가 채워야 할 설정 ──────────────────────────────────────
    protected abstract string SheetName      { get; }   // "s1_archer"
    protected abstract int[]  IdleRows       { get; }   // { 8,9,10,11 }
    protected abstract int[]  AtkRows        { get; }   // { 16,17,18,19 }
    protected abstract int    AtkFrameCount  { get; }   // 8
    protected abstract int    ReleaseFrame   { get; }   // 공격 투사체/이펙트 타이밍
    protected abstract float  AnimFps        { get; }   // 12f
    protected virtual  float  Ppu            => 44f;    // 픽셀 퍼 유닛

    // ── 캐시 ─────────────────────────────────────────────────────────
    protected readonly Sprite[]   IdleByDir = new Sprite[4];
    protected readonly Sprite[][] AtkByDir  = new Sprite[4][];

    // ── 방향 인덱스: 0=위, 1=왼, 2=아래, 3=오른 ──────────────────────
    protected int DirIndex(Vector2 diff)
    {
        if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y))
            return diff.x < 0f ? 1 : 3;
        return diff.y > 0f ? 0 : 2;
    }

    // ── 시트 프레임 일괄 로드 ─────────────────────────────────────────
    protected void LoadSheetFrames(Sprite fallback)
    {
        for (int d = 0; d < 4; d++)
        {
            IdleByDir[d] = EnemyVisualGenerator.TryLoadSheetFrame(
                               SheetName, 1, IdleRows[d], ppu: Ppu) ?? fallback;
            AtkByDir[d] = new Sprite[AtkFrameCount];
            for (int f = 0; f < AtkFrameCount; f++)
                AtkByDir[d][f] = EnemyVisualGenerator.TryLoadSheetFrame(
                                     SheetName, f, AtkRows[d], ppu: Ppu) ?? IdleByDir[d];
        }
        SetupSpriteAnimation(IdleByDir[2], AtkByDir[2]); // 기본: 아래 방향
    }

    // ── 타겟 방향으로 공격 프레임 세트 교체 ───────────────────────────
    protected void SetAttackDir(AllyBase target)
    {
        if (target == null) return;
        int d = DirIndex(target.transform.position - transform.position);
        idleSprite         = IdleByDir[d];
        attackFrameSprites = AtkByDir[d];
    }

    // ── Update: 대기 중 타겟 방향으로 스프라이트 갱신 ─────────────────
    protected override void Update()
    {
        base.Update();
        if (!isPlayingAttackAnim && currentTarget != null && spriteRenderer != null)
        {
            int d = DirIndex(currentTarget.transform.position - transform.position);
            if (spriteRenderer.sprite != IdleByDir[d])
            {
                spriteRenderer.sprite = IdleByDir[d];
                idleSprite            = IdleByDir[d];
            }
        }
    }

    // ── 공격 이펙트: 방향 전환 → 애니메이션 → ReleaseFrame에서 이펙트 ─
    protected override IEnumerator ShowAttackEffect(AllyBase target)
    {
        SetAttackDir(target);
        StartCoroutine(PlayAttackAnim(AnimFps));
        yield return new WaitForSeconds(ReleaseFrame / AnimFps);
        if (target != null)
            yield return StartCoroutine(OnReleaseEffect(target));
    }

    /// <summary>ReleaseFrame 시점에 실행되는 공격 이펙트. 서브클래스에서 override.</summary>
    protected virtual IEnumerator OnReleaseEffect(AllyBase target) { yield break; }
}
