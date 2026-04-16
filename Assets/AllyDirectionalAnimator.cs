using UnityEngine;

public class AllyDirectionalAnimator : MonoBehaviour
{
    const float MotionThreshold = 0.00008f;

    static readonly int DirectionHash = Animator.StringToHash("Direction");
    static readonly int MovingHash = Animator.StringToHash("Moving");

    Animator animator;
    SpriteRenderer spriteRenderer;
    AllyFacingDirection currentDirection = AllyFacingDirection.Down;

    public void Initialize(Animator targetAnimator)
    {
        animator = targetAnimator;
        spriteRenderer = animator != null ? animator.GetComponent<SpriteRenderer>() : null;
        currentDirection = AllyFacingDirection.Down;

        if (animator == null)
            return;

        animator.SetInteger(DirectionHash, (int)currentDirection);
        animator.SetBool(MovingHash, false);
    }

    public void SetMotion(Vector2 worldDelta, bool moving)
    {
        if (animator == null)
            return;

        bool nextMoving = moving && worldDelta.sqrMagnitude > MotionThreshold;
        if (nextMoving)
            currentDirection = ResolveDirection(worldDelta, currentDirection);

        if (spriteRenderer != null)
            spriteRenderer.flipX = currentDirection == AllyFacingDirection.Right;

        animator.SetInteger(DirectionHash, (int)currentDirection);
        animator.SetBool(MovingHash, nextMoving);
    }

    static AllyFacingDirection ResolveDirection(Vector2 delta, AllyFacingDirection previousDirection)
    {
        if (delta.sqrMagnitude <= 0.000001f)
            return previousDirection;

        float absX = Mathf.Abs(delta.x);
        float absY = Mathf.Abs(delta.y);
        bool currentHorizontal = previousDirection == AllyFacingDirection.Left || previousDirection == AllyFacingDirection.Right;
        bool currentVertical = previousDirection == AllyFacingDirection.Up || previousDirection == AllyFacingDirection.Down;

        if (currentHorizontal && absX >= absY * 0.8f)
            return delta.x >= 0f ? AllyFacingDirection.Right : AllyFacingDirection.Left;

        if (currentVertical && absY >= absX * 0.8f)
            return delta.y >= 0f ? AllyFacingDirection.Up : AllyFacingDirection.Down;

        if (absX > absY * 1.25f)
            return delta.x >= 0f ? AllyFacingDirection.Right : AllyFacingDirection.Left;

        if (absY > absX * 1.25f)
            return delta.y >= 0f ? AllyFacingDirection.Up : AllyFacingDirection.Down;

        return previousDirection;
    }
}
