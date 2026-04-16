using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 검객 슬롯으로 사용하는 근접 공격형 아군
/// </summary>
public class Warrior : AllyBase
{
    protected override void Awake()
    {
        allyName  = "검객";
        maxHp     = 180f;
        moveSpeed = 3.2f;
        base.Awake();

        walkBobAmplitude   = 0.05f;
        walkSquashAmount   = 0.07f;
        walkTiltAngle      = 7.5f;
        walkCycleFrequency = 8.8f;
        idleBreathAmount   = 0.016f;
        idleBreathScale    = 0.028f;
        idleArmSwing       = 5f;
        idleHeadSway       = 3f;
        armSwingAngle      = 12f;
        legSwingAngle      = 22f;
        headNodAngle       = 3f;
    }
}
