using UnityEngine;

public enum AllyFacingDirection
{
    Down,
    Up,
    Left,
    Right
}

public class AllyDirectionalSprite : MonoBehaviour
{
    const float AnimationFps = 4.2f;
    const float MotionThreshold = 0.00008f;
    const float DefaultMovingPlaybackScale = 0.68f;
    const float StopHoldDuration = 0.12f;

    AllyType allyType;
    SpriteRenderer spriteRenderer;
    AllyFacingDirection currentDirection = AllyFacingDirection.Down;
    bool isMoving;
    float animationTimer;
    float playbackScale = 1f;
    float movingPlaybackScale = DefaultMovingPlaybackScale;
    float stopHoldTimer;

    public void Initialize(AllyType type, SpriteRenderer renderer, float animationSpeedMultiplier = 1f)
    {
        allyType = type;
        spriteRenderer = renderer;
        currentDirection = AllyFacingDirection.Down;
        animationTimer = 0f;
        isMoving = false;
        playbackScale = 1f;
        movingPlaybackScale = DefaultMovingPlaybackScale * Mathf.Max(0.1f, animationSpeedMultiplier);
        stopHoldTimer = 0f;
        RefreshSprite();
    }

    public void SetMotion(Vector2 worldDelta, bool moving)
    {
        if (spriteRenderer == null)
            return;

        bool hasMotion = moving && worldDelta.sqrMagnitude > MotionThreshold;
        if (hasMotion)
            stopHoldTimer = StopHoldDuration;
        else
            stopHoldTimer = Mathf.Max(0f, stopHoldTimer - Time.deltaTime);

        bool nextMoving = hasMotion || stopHoldTimer > 0f;
        var nextDirection = hasMotion ? ResolveDirection(worldDelta, currentDirection) : currentDirection;
        if (nextDirection != currentDirection)
            animationTimer = 0f;

        currentDirection = nextDirection;
        isMoving = nextMoving;
        // 픽셀 아군은 타입별 이동속도 차이가 커도 프레임 전환은 비슷하게 보이도록 고정에 가깝게 유지합니다.
        playbackScale = nextMoving ? movingPlaybackScale : 1f;
        RefreshSprite();
    }

    void Update()
    {
        if (spriteRenderer == null)
            return;

        if (isMoving)
            animationTimer += Time.deltaTime * playbackScale;

        RefreshSprite();
    }

    void RefreshSprite()
    {
        Sprite[] frames = AllyVisualGenerator.GetWalkFrames(allyType, currentDirection);
        if (frames == null || frames.Length == 0)
            return;

        int index = isMoving
            ? GetLoopedFrameIndex(frames.Length, animationTimer * AnimationFps)
            : GetIdleFrameIndex(frames.Length);

        spriteRenderer.sprite = frames[index];
    }

    static int GetIdleFrameIndex(int frameCount)
    {
        if (frameCount <= 1)
            return 0;

        return frameCount / 2;
    }

    static int GetLoopedFrameIndex(int frameCount, float timeValue)
    {
        if (frameCount <= 1)
            return 0;

        if (frameCount == 2)
            return Mathf.FloorToInt(timeValue) % 2;

        int loopLength = frameCount * 2 - 2;
        int rawIndex = Mathf.FloorToInt(timeValue) % loopLength;
        return rawIndex < frameCount ? rawIndex : loopLength - rawIndex;
    }

    static AllyFacingDirection ResolveDirection(Vector2 delta, AllyFacingDirection currentDirection)
    {
        if (delta.sqrMagnitude <= 0.000001f)
            return currentDirection;

        float absX = Mathf.Abs(delta.x);
        float absY = Mathf.Abs(delta.y);
        bool currentHorizontal = currentDirection == AllyFacingDirection.Left || currentDirection == AllyFacingDirection.Right;
        bool currentVertical = currentDirection == AllyFacingDirection.Up || currentDirection == AllyFacingDirection.Down;

        // 이미 가로 방향으로 보고 있다면, 세로가 충분히 우세해질 때까지 방향을 유지합니다.
        if (currentHorizontal && absX >= absY * 0.8f)
            return delta.x >= 0f ? AllyFacingDirection.Right : AllyFacingDirection.Left;

        // 이미 세로 방향으로 보고 있다면, 가로가 충분히 우세해질 때까지 방향을 유지합니다.
        if (currentVertical && absY >= absX * 0.8f)
            return delta.y >= 0f ? AllyFacingDirection.Up : AllyFacingDirection.Down;

        // 축 전환은 한쪽이 확실히 더 클 때만 일어납니다.
        if (absX > absY * 1.25f)
            return delta.x >= 0f ? AllyFacingDirection.Right : AllyFacingDirection.Left;

        if (absY > absX * 1.25f)
            return delta.y >= 0f ? AllyFacingDirection.Up : AllyFacingDirection.Down;

        return currentDirection;
    }
}
