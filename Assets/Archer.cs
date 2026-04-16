using UnityEngine;

/// <summary>
/// 궁수 슬롯으로 사용하는 중거리 지원형 아군
/// </summary>
public class Archer : AllyBase
{
    protected override void Awake()
    {
        allyName  = "궁수";
        maxHp     = 140f;
        moveSpeed = 3.8f;
        base.Awake();

        walkBobAmplitude   = 0.04f;
        walkSquashAmount   = 0.05f;
        walkTiltAngle      = 9f;
        walkCycleFrequency = 10.2f;
        idleBreathAmount   = 0.020f;
        idleBreathScale    = 0.022f;
        idleArmSwing       = 8f;
        idleHeadSway       = 5f;
        armSwingAngle      = 20f;
        legSwingAngle      = 18f;
        headNodAngle       = 4.5f;
    }
}
