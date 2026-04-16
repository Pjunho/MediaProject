using UnityEngine;

// ── 아군 타입 열거형 ────────────────────────────────────────────────
public enum AllyType { Warrior, Archer, Mage, Cleric }

// ── HP바 컴포넌트 ───────────────────────────────────────────────────
public class HpBar : MonoBehaviour
{
    public AllyBase       ally;
    public Transform      fillTf;
    public SpriteRenderer fillSr;

    void Update()
    {
        if (ally == null || fillTf == null) return;
        float ratio = ally.HpRatio;
        fillTf.localScale = new Vector3(ratio, 1f, 1f);
        fillSr.color = Color.Lerp(Color.red, Color.green, ratio);
    }
}
