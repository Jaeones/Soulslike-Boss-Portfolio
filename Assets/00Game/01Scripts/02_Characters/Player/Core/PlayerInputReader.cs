using UnityEngine;

public readonly struct PlayerInputSnapshot
{
    public PlayerInputSnapshot(
        Vector2 move,
        bool parryPressed,
        bool attackShortReleased,
        bool attackLongPressTriggered,
        bool rollPressed,
        bool sprintHeld,
        bool toggleBattleStancePressed,
        bool useFlaskPressed,
        bool interactPressed)
    {
        Move = move;
        ParryPressed = parryPressed;
        AttackShortReleased = attackShortReleased;
        AttackLongPressTriggered = attackLongPressTriggered;
        RollPressed = rollPressed;
        SprintHeld = sprintHeld;
        ToggleBattleStancePressed = toggleBattleStancePressed;
        UseFlaskPressed = useFlaskPressed;
        InteractPressed = interactPressed;
    }

    public Vector2 Move { get; }
    public bool ParryPressed { get; }
    public bool AttackShortReleased { get; }
    public bool AttackLongPressTriggered { get; }
    public bool RollPressed { get; }
    public bool SprintHeld { get; }
    public bool ToggleBattleStancePressed { get; }
    public bool UseFlaskPressed { get; }
    public bool InteractPressed { get; }

    public bool HasMoveInput => Move.sqrMagnitude > 0.0001f;
}

public sealed class PlayerInputReader
{
    private const string DefaultHorizontalAxisName = "Horizontal";
    private const string DefaultVerticalAxisName = "Vertical";
    private const KeyCode DefaultRollKey = KeyCode.Space;
    private const KeyCode DefaultSprintKey = KeyCode.LeftShift;
    private const int AttackMouseButton = 0;
    private const int ParryMouseButton = 1;

    private readonly KeyCode _battleStanceToggleKey;
    private readonly KeyCode _flaskUseKey;
    private readonly KeyCode _interactionKey;
    private readonly string _horizontalAxisName;
    private readonly string _verticalAxisName;
    private readonly float _attackHoldThreshold;
    private float _attackPressStartTime = -1f;
    private bool _attackLongPressTriggeredThisHold;

    public PlayerInputReader(
        KeyCode battleStanceToggleKey,
        KeyCode flaskUseKey,
        KeyCode interactionKey,
        float attackHoldThreshold,
        string horizontalAxisName = DefaultHorizontalAxisName,
        string verticalAxisName = DefaultVerticalAxisName)
    {
        _battleStanceToggleKey = battleStanceToggleKey;
        _flaskUseKey = flaskUseKey;
        _interactionKey = interactionKey;
        _attackHoldThreshold = Mathf.Max(0.05f, attackHoldThreshold);
        _horizontalAxisName = horizontalAxisName;
        _verticalAxisName = verticalAxisName;
    }

    public PlayerInputSnapshot Capture()
    {
        Vector2 move = new Vector2(
            Input.GetAxisRaw(_horizontalAxisName),
            Input.GetAxisRaw(_verticalAxisName));
        bool attackPressed = Input.GetMouseButtonDown(AttackMouseButton);
        bool attackHeld = Input.GetMouseButton(AttackMouseButton);
        bool attackReleased = Input.GetMouseButtonUp(AttackMouseButton);
        bool attackShortReleased = false;
        bool attackLongPressTriggered = false;
        float attackHoldDuration = 0f;

        if (attackPressed)
        {
            _attackPressStartTime = Time.unscaledTime;
            _attackLongPressTriggeredThisHold = false;
        }

        if (attackHeld && _attackPressStartTime >= 0f)
        {
            attackHoldDuration = Mathf.Max(0f, Time.unscaledTime - _attackPressStartTime);
            if (!_attackLongPressTriggeredThisHold && attackHoldDuration >= _attackHoldThreshold)
            {
                attackLongPressTriggered = true;
                _attackLongPressTriggeredThisHold = true;
            }
        }

        if (attackReleased)
        {
            attackHoldDuration = _attackPressStartTime >= 0f
                ? Mathf.Max(0f, Time.unscaledTime - _attackPressStartTime)
                : 0f;

            if (!_attackLongPressTriggeredThisHold && attackHoldDuration >= _attackHoldThreshold)
            {
                attackLongPressTriggered = true;
            }
            else
            {
                attackShortReleased = !_attackLongPressTriggeredThisHold;
            }

            _attackPressStartTime = -1f;
            _attackLongPressTriggeredThisHold = false;
        }

        return new PlayerInputSnapshot(
            move,
            Input.GetMouseButtonDown(ParryMouseButton),
            attackShortReleased,
            attackLongPressTriggered,
            Input.GetKeyDown(DefaultRollKey),
            Input.GetKey(DefaultSprintKey),
            Input.GetKeyDown(_battleStanceToggleKey),
            Input.GetKeyDown(_flaskUseKey),
            Input.GetKeyDown(_interactionKey));
    }
}
