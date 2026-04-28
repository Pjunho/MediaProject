using UnityEngine;

/// <summary>
/// 중장 방어형 아군. HP가 가장 높지만 이동 속도가 느리다.
/// </summary>
public class Paladin : AllyBase
{
    protected override void Awake()
    {
        allyName  = "성기사";
        maxHp     = 280f;
        moveSpeed = 1.8f;
        base.Awake();

        walkBobAmplitude   = 0.04f;
        walkSquashAmount   = 0.08f;
        walkTiltAngle      = 4f;
        walkCycleFrequency = 6.5f;
        idleBreathAmount   = 0.020f;
        idleBreathScale    = 0.030f;
        idleArmSwing       = 4f;
        idleHeadSway       = 2f;
        armSwingAngle      = 10f;
        legSwingAngle      = 16f;
        headNodAngle       = 2f;
    }
}
