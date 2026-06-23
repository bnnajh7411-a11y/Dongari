using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(Rigidbody2D))]
public class BuildingSideViewPlayerAnimator : MonoBehaviour
{
    private const string BuildingSceneName = "Building";
    private const string PlayerObjectName = "Player";
    private const int BaseLayerIndex = 0;
    private const float GroundNormalThreshold = 0.5f;

    private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");
    private static readonly int WalkStartStateHash = Animator.StringToHash("Base Layer.WalkStart");
    private static readonly int WalkStateHash = Animator.StringToHash("Base Layer.Walk");
    private static readonly int RunStartStateHash = Animator.StringToHash("Base Layer.RunStart");
    private static readonly int RunStateHash = Animator.StringToHash("Base Layer.Run");
    private static readonly int JumpStateHash = Animator.StringToHash("Base Layer.Jump");

    [SerializeField] private bool restrictToBuildingScene = true;
    [SerializeField] private bool playStartAnimations = true;
    [SerializeField, Min(0f)] private float horizontalInputThreshold = 0.01f;
    [SerializeField, Min(0f)] private float airborneVelocityThreshold = 0.1f;

    private readonly HashSet<Collider2D> groundColliders = new HashSet<Collider2D>();

    private Animator animator;
    private Rigidbody2D playerRigidbody;
    private PlayerStamina playerStamina;
    private float defaultAnimatorSpeed = 1f;
    private int currentStateHash;
    private int queuedLoopStateHash;
    private bool isPlayingStartAnimation;
    private MotionMode currentMotionMode = MotionMode.None;

    private enum MotionMode
    {
        None,
        Idle,
        Walk,
        Run,
        Jump
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterSceneLoaded()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryInstallOnBuildingPlayer(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstallOnBuildingPlayer(scene);
    }

    private static void TryInstallOnBuildingPlayer(Scene scene)
    {
        if (!scene.IsValid() || scene.name != BuildingSceneName)
        {
            return;
        }

        GameObject playerObject = GameObject.Find(PlayerObjectName);
        if (playerObject == null
            || playerObject.GetComponent<BuildingSideViewPlayerAnimator>() != null
            || playerObject.GetComponent<Animator>() == null)
        {
            return;
        }

        playerObject.AddComponent<BuildingSideViewPlayerAnimator>();
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerRigidbody = GetComponent<Rigidbody2D>();
        playerStamina = GetComponent<PlayerStamina>();

        if (animator != null)
        {
            defaultAnimatorSpeed = animator.speed;
        }

        if (restrictToBuildingScene && SceneManager.GetActiveScene().name != BuildingSceneName)
        {
            enabled = false;
            return;
        }

        PlayState(IdleStateHash, true);
        currentMotionMode = MotionMode.Idle;
    }

    private void LateUpdate()
    {
        if (animator == null || playerRigidbody == null)
        {
            return;
        }

        if (GamePauseState.IsPaused)
        {
            animator.speed = 0f;
            return;
        }

        if (!Mathf.Approximately(animator.speed, defaultAnimatorSpeed))
        {
            animator.speed = defaultAnimatorSpeed;
        }

        UpdateAnimationState();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        UpdateGroundContact(collision, true);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        UpdateGroundContact(collision, true);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision != null && collision.collider != null)
        {
            groundColliders.Remove(collision.collider);
        }
    }

    private void UpdateAnimationState()
    {
        if (ShouldPlayJump())
        {
            SetMotionMode(MotionMode.Jump);
            return;
        }

        float horizontalInput = PlayerInputBindings.GetHorizontalInput();
        bool wantsHorizontalMove = Mathf.Abs(horizontalInput) > horizontalInputThreshold;

        if (!wantsHorizontalMove)
        {
            SetMotionMode(MotionMode.Idle);
            return;
        }

        if (playerStamina == null)
        {
            playerStamina = GetComponent<PlayerStamina>();
        }

        bool wantsRun = PlayerInputBindings.IsRunPressed()
            && (playerStamina == null || playerStamina.CanSprint);

        SetMotionMode(wantsRun ? MotionMode.Run : MotionMode.Walk);
    }

    private bool ShouldPlayJump()
    {
        if (PlayerInputBindings.WasJumpPressedThisFrame())
        {
            return true;
        }

        if (currentMotionMode == MotionMode.Jump && groundColliders.Count == 0)
        {
            return true;
        }

        return groundColliders.Count == 0
            && Mathf.Abs(playerRigidbody.linearVelocity.y) > airborneVelocityThreshold;
    }

    private void SetMotionMode(MotionMode motionMode)
    {
        if (motionMode == currentMotionMode)
        {
            FinishStartAnimationIfReady();
            return;
        }

        currentMotionMode = motionMode;
        isPlayingStartAnimation = false;
        queuedLoopStateHash = 0;

        switch (motionMode)
        {
            case MotionMode.Walk:
                PlayStartOrLoop(WalkStartStateHash, WalkStateHash);
                break;
            case MotionMode.Run:
                PlayStartOrLoop(RunStartStateHash, RunStateHash);
                break;
            case MotionMode.Jump:
                PlayState(JumpStateHash, true);
                break;
            default:
                PlayState(IdleStateHash, false);
                break;
        }
    }

    private void PlayStartOrLoop(int startStateHash, int loopStateHash)
    {
        if (playStartAnimations && animator.HasState(BaseLayerIndex, startStateHash))
        {
            queuedLoopStateHash = loopStateHash;
            isPlayingStartAnimation = true;
            PlayState(startStateHash, true);
            return;
        }

        PlayState(loopStateHash, false);
    }

    private void FinishStartAnimationIfReady()
    {
        if (!isPlayingStartAnimation || queuedLoopStateHash == 0)
        {
            return;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(BaseLayerIndex);
        if (stateInfo.fullPathHash != currentStateHash || stateInfo.normalizedTime < 1f)
        {
            return;
        }

        isPlayingStartAnimation = false;
        PlayState(queuedLoopStateHash, false);
        queuedLoopStateHash = 0;
    }

    private void PlayState(int stateHash, bool restart)
    {
        if (stateHash == 0 || !animator.HasState(BaseLayerIndex, stateHash))
        {
            return;
        }

        if (!restart && currentStateHash == stateHash)
        {
            return;
        }

        animator.Play(stateHash, BaseLayerIndex, 0f);
        animator.Update(0f);
        currentStateHash = stateHash;
    }

    private void UpdateGroundContact(Collision2D collision, bool isContacting)
    {
        if (collision == null || collision.collider == null)
        {
            return;
        }

        bool hasGroundContact = false;
        if (isContacting)
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y > GroundNormalThreshold)
                {
                    hasGroundContact = true;
                    break;
                }
            }
        }

        if (hasGroundContact)
        {
            groundColliders.Add(collision.collider);
            return;
        }

        groundColliders.Remove(collision.collider);
    }
}
