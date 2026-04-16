using UnityEngine;

/// <summary>
/// 성직자 - 근접 지원형 아군, 높은 내구력
/// </summary>
public class Cleric : AllyBase
{
    protected override void Awake()
    {
        allyName  = "성직자";
        maxHp     = 200f;
        moveSpeed = 2.8f;
        base.Awake();

        walkBobAmplitude   = 0.030f;
        walkSquashAmount   = 0.040f;
        walkTiltAngle      = 4f;
        walkCycleFrequency = 6.0f;
        idleBreathAmount   = 0.016f;
        idleBreathScale    = 0.022f;
        idleArmSwing       = 4f;
        idleHeadSway       = 2.5f;
        armSwingAngle      = 9f;
        legSwingAngle      = 12f;
        headNodAngle       = 2f;
    }
}
