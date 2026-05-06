using UnityEngine;

/// <summary>
/// 민첩 기습형 아군. 속도가 가장 빠르지만 HP가 낮다.
/// </summary>
public class Rogue : AllyBase
{
    protected override void Awake()
    {
        allyName  = "도적";
        maxHp     = 90f;
        moveSpeed = 2.88f;
        base.Awake();

        walkBobAmplitude   = 0.06f;
        walkSquashAmount   = 0.05f;
        walkTiltAngle      = 9f;
        walkCycleFrequency = 10.5f;
        idleBreathAmount   = 0.014f;
        idleBreathScale    = 0.022f;
        idleArmSwing       = 7f;
        idleHeadSway       = 5f;
        armSwingAngle      = 18f;
        legSwingAngle      = 26f;
        headNodAngle       = 5f;
    }
}
