using UnityEngine;
using System.Collections;

/// <summary>
/// Stage 3 (화산의 심판) 전용 환경 위험 효과.
/// 웨이브 진행 중 살아 있는 모든 아군에게 매 초 최대 HP의 3%를 피해로 입힙니다.
///
/// GameManager.InitStageHazards()에서 생성합니다.
/// </summary>
public class VolcanoHazard : MonoBehaviour
{
    [Tooltip("초당 최대 체력 대비 감소 비율 (0.03 = 3%)")]
    public float drainRatePerSec = 0.03f;

    [Tooltip("피해 적용 간격 (초)")]
    public float tickInterval = 1.0f;

    [Tooltip("최소 체력 (이 이하로는 떨어지지 않음)")]
    public float minHpFloor = 1f;

    private bool isActive = false;
    private Coroutine drainCoroutine;

    // ── 웨이브 진행 상태 연동 ────────────────────────────────────────

    /// <summary>웨이브 시작 시 GameManager에서 호출</summary>
    public void StartDrain()
    {
        if (isActive) return;
        isActive = true;
        drainCoroutine = StartCoroutine(DrainLoop());
    }

    /// <summary>웨이브 종료(클리어/실패) 시 GameManager에서 호출</summary>
    public void StopDrain()
    {
        isActive = false;
        if (drainCoroutine != null)
        {
            StopCoroutine(drainCoroutine);
            drainCoroutine = null;
        }
    }

    // ── 내부 루프 ────────────────────────────────────────────────────

    IEnumerator DrainLoop()
    {
        while (isActive)
        {
            yield return new WaitForSeconds(tickInterval);
            if (!isActive) yield break;

            ApplyDrainToAllAllies();
        }
    }

    void ApplyDrainToAllAllies()
    {
        // 현재 씬의 모든 AllyBase 탐색
        var allies = FindObjectsByType<AllyBase>(FindObjectsSortMode.None);
        foreach (var ally in allies)
        {
            if (ally == null || ally.isDead) continue;
            if (!ally.gameObject.activeInHierarchy) continue;

            // 최대 체력의 drainRatePerSec × tickInterval 만큼 감소
            float damage = ally.maxHp * drainRatePerSec * tickInterval;

            // 최소 체력 보장 (즉사 방지)
            float newHp = Mathf.Max(minHpFloor, ally.currentHp - damage);
            float actualDamage = ally.currentHp - newHp;

            if (actualDamage > 0f)
                ally.TakeDamage(actualDamage);
        }
    }

    void OnDestroy()
    {
        StopDrain();
    }
}
