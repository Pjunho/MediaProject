using UnityEngine;

/// <summary>
/// 마법사 - 원거리 광역 지원형 아군
/// </summary>
public class Mage : AllyBase
{
    protected override void Awake()
    {
        allyName  = "마법사";
        maxHp     = 110f;
        moveSpeed = 4.2f;
        base.Awake();

        walkBobAmplitude   = 0.036f;
        walkSquashAmount   = 0.042f;
        walkTiltAngle      = 8f;
        walkCycleFrequency = 9.5f;
        idleBreathAmount   = 0.022f;
        idleBreathScale    = 0.024f;
        idleArmSwing       = 9f;
        idleHeadSway       = 5f;
        armSwingAngle      = 22f;
        legSwingAngle      = 16f;
        headNodAngle       = 5f;
    }
}
