using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(PlayerMovement), typeof(Animator))]
public class DeerClipAnimator : MonoBehaviour
{
    private const float DirectionInputThreshold = 0.01f;
    private const float MovementVelocityThreshold = 0.01f;

    [SerializeField] private ViewMode viewMode = ViewMode.SideView;
    [SerializeField] private bool playStartClips = true;
    [SerializeField] private SideViewClips sideView = new SideViewClips();
    [SerializeField] private TopViewClips topView = new TopViewClips();

    private PlayerMovement movement;
    private Animator animator;
    private PlayableGraph graph;
    private AnimationPlayableOutput output;
    private AnimationClipPlayable currentPlayable;
    private AnimationClip currentClip;
    private AnimationClip queuedLoopClip;
    private MotionState currentState = MotionState.None;
    private Direction currentDirection = Direction.Down;
    private float clipTime;
    private bool hasPlayable;
    private bool isPlayingStartClip;

    private enum ViewMode
    {
        SideView,
        TopView
    }

    private enum MotionState
    {
        None,
        Idle,
        Walk,
        Run,
        Jump
    }

    private enum Direction
    {
        Down,
        Side,
        SideDown,
        Up,
        UpSide
    }

    [System.Serializable]
    private sealed class SideViewClips
    {
        public AnimationClip idle = null;
        public AnimationClip walkStart = null;
        public AnimationClip walk = null;
        public AnimationClip runStart = null;
        public AnimationClip run = null;
        public AnimationClip jump = null;
    }

    [System.Serializable]
    private sealed class DirectionClips
    {
        public AnimationClip idle = null;
        public AnimationClip walkStart = null;
        public AnimationClip walk = null;
        public AnimationClip runStart = null;
        public AnimationClip run = null;
    }

    [System.Serializable]
    private sealed class TopViewClips
    {
        public DirectionClips down = new DirectionClips();
        public DirectionClips side = new DirectionClips();
        public DirectionClips sideDown = new DirectionClips();
        public DirectionClips up = new DirectionClips();
        public DirectionClips upSide = new DirectionClips();
    }

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = gameObject.AddComponent<Animator>();
        }

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.runtimeAnimatorController = null;
        CreateGraph();
        PlayState(MotionState.Idle, restart: true);
    }

    private void OnEnable()
    {
        if (graph.IsValid())
        {
            graph.Play();
        }
    }

    private void OnDisable()
    {
        if (graph.IsValid())
        {
            graph.Stop();
        }
    }

    private void OnDestroy()
    {
        if (graph.IsValid())
        {
            graph.Destroy();
        }
    }

    private void LateUpdate()
    {
        if (movement == null)
        {
            return;
        }

        SetPlaybackPaused(GamePauseState.IsPaused);
        if (GamePauseState.IsPaused)
        {
            return;
        }

        UpdateStateFromMovement();
        AdvanceCurrentClip(Time.deltaTime);
    }

    private void CreateGraph()
    {
        if (graph.IsValid())
        {
            return;
        }

        graph = PlayableGraph.Create($"{nameof(DeerClipAnimator)}_{name}");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        output = AnimationPlayableOutput.Create(graph, "Animation", animator);
        graph.Play();
    }

    private void UpdateStateFromMovement()
    {
        Vector2 animationMovement = GetAnimationMovement();
        Direction nextDirection = currentDirection;
        if (viewMode == ViewMode.TopView && HasMovement(animationMovement))
        {
            nextDirection = GetDirection(animationMovement.x, animationMovement.y);
        }

        MotionState nextState = GetNextState(animationMovement);
        bool shouldRestart = viewMode == ViewMode.TopView
            && nextState != MotionState.Jump
            && nextDirection != currentDirection;

        currentDirection = nextDirection;
        PlayState(nextState, shouldRestart);
    }

    private MotionState GetNextState(Vector2 animationMovement)
    {
        if (viewMode == ViewMode.SideView
            && (movement.JumpPressedThisFrame || PlayerInputBindings.WasJumpPressedThisFrame() || movement.IsAirborne))
        {
            return MotionState.Jump;
        }

        if (!HasMovement(animationMovement))
        {
            return MotionState.Idle;
        }

        return IsRunning(animationMovement) ? MotionState.Run : MotionState.Walk;
    }

    private bool IsRunning(Vector2 animationMovement)
    {
        if (movement.IsRunning)
        {
            return true;
        }

        return !movement.HasMovementInput
            && HasMovement(animationMovement)
            && PlayerInputBindings.IsRunPressed();
    }

    private void PlayState(MotionState nextState, bool restart)
    {
        if (nextState == currentState && !restart)
        {
            FinishStartClipIfReady();
            return;
        }

        currentState = nextState;
        isPlayingStartClip = false;
        queuedLoopClip = null;

        switch (nextState)
        {
            case MotionState.Walk:
                PlayStartOrLoop(GetWalkStartClip(), GetWalkClip());
                break;
            case MotionState.Run:
                PlayStartOrLoop(GetRunStartClip(), GetRunClip());
                break;
            case MotionState.Jump:
                PlayClip(sideView.jump, restart: true);
                break;
            default:
                PlayClip(GetIdleClip(), restart: restart);
                break;
        }
    }

    private void PlayStartOrLoop(AnimationClip startClip, AnimationClip loopClip)
    {
        if (playStartClips && startClip != null)
        {
            queuedLoopClip = loopClip;
            isPlayingStartClip = true;
            PlayClip(startClip, restart: true);
            return;
        }

        PlayClip(loopClip, restart: true);
    }

    private void AdvanceCurrentClip(float deltaTime)
    {
        if (currentClip == null)
        {
            return;
        }

        clipTime += deltaTime;
        FinishStartClipIfReady();
    }

    private void FinishStartClipIfReady()
    {
        if (!isPlayingStartClip || currentClip == null || queuedLoopClip == null)
        {
            return;
        }

        if (clipTime < currentClip.length)
        {
            return;
        }

        isPlayingStartClip = false;
        PlayClip(queuedLoopClip, restart: true);
        queuedLoopClip = null;
    }

    private void PlayClip(AnimationClip clip, bool restart)
    {
        if (clip == null)
        {
            return;
        }

        if (clip == currentClip && !restart)
        {
            return;
        }

        CreateGraph();
        if (hasPlayable && currentPlayable.IsValid())
        {
            graph.DestroyPlayable(currentPlayable);
        }

        currentClip = clip;
        clipTime = 0f;
        currentPlayable = AnimationClipPlayable.Create(graph, currentClip);
        currentPlayable.SetApplyFootIK(false);
        currentPlayable.SetTime(0d);
        currentPlayable.SetSpeed(GamePauseState.IsPaused ? 0d : 1d);
        output.SetSourcePlayable(currentPlayable);
        hasPlayable = true;

        if (!graph.IsPlaying())
        {
            graph.Play();
        }
    }

    private void SetPlaybackPaused(bool isPaused)
    {
        if (hasPlayable && currentPlayable.IsValid())
        {
            currentPlayable.SetSpeed(isPaused ? 0d : 1d);
        }
    }

    private AnimationClip GetIdleClip()
    {
        return viewMode == ViewMode.TopView
            ? GetDirectionClips(currentDirection).idle
            : sideView.idle;
    }

    private AnimationClip GetWalkStartClip()
    {
        return viewMode == ViewMode.TopView
            ? GetDirectionClips(currentDirection).walkStart
            : sideView.walkStart;
    }

    private AnimationClip GetWalkClip()
    {
        return viewMode == ViewMode.TopView
            ? GetDirectionClips(currentDirection).walk
            : sideView.walk;
    }

    private AnimationClip GetRunStartClip()
    {
        return viewMode == ViewMode.TopView
            ? GetDirectionClips(currentDirection).runStart
            : sideView.runStart;
    }

    private AnimationClip GetRunClip()
    {
        return viewMode == ViewMode.TopView
            ? GetDirectionClips(currentDirection).run
            : sideView.run;
    }

    private DirectionClips GetDirectionClips(Direction direction)
    {
        switch (direction)
        {
            case Direction.Side:
                return topView.side;
            case Direction.SideDown:
                return topView.sideDown;
            case Direction.Up:
                return topView.up;
            case Direction.UpSide:
                return topView.upSide;
            default:
                return topView.down;
        }
    }

    private Vector2 GetAnimationMovement()
    {
        Vector2 movementInput = movement.MovementInput;
        if (HasMovement(movementInput))
        {
            return movementInput;
        }

        Vector2 directInput = new Vector2(
            PlayerInputBindings.GetHorizontalInput(),
            PlayerInputBindings.GetVerticalInput());
        if (HasMovement(directInput))
        {
            return directInput;
        }

        Vector2 velocity = movement.CurrentVelocity;
        if (Mathf.Abs(velocity.x) <= MovementVelocityThreshold)
        {
            velocity.x = 0f;
        }

        if (Mathf.Abs(velocity.y) <= MovementVelocityThreshold)
        {
            velocity.y = 0f;
        }

        return velocity;
    }

    private static bool HasMovement(Vector2 movementValue)
    {
        return Mathf.Abs(movementValue.x) > DirectionInputThreshold
            || Mathf.Abs(movementValue.y) > DirectionInputThreshold;
    }

    private static Direction GetDirection(float horizontalInput, float verticalInput)
    {
        bool hasHorizontalInput = Mathf.Abs(horizontalInput) > DirectionInputThreshold;
        bool hasUpInput = verticalInput > DirectionInputThreshold;
        bool hasDownInput = verticalInput < -DirectionInputThreshold;

        if (hasUpInput)
        {
            return hasHorizontalInput ? Direction.UpSide : Direction.Up;
        }

        if (hasDownInput)
        {
            return hasHorizontalInput ? Direction.SideDown : Direction.Down;
        }

        return hasHorizontalInput ? Direction.Side : Direction.Down;
    }
}
