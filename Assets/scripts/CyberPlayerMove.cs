using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Standalone CyberPunkCity runner controller. It intentionally does not inherit
/// from PlayerMove and does not use Desert's physics-driven movement code.
/// </summary>
public class CyberPlayerMove : MonoBehaviour, IRunnerController
{
    [Header("Cyber Track")]
    [SerializeField] private float trackCenterXOffset = -18f;
    [SerializeField] private float laneDistance = 3.5f;
    [SerializeField, Range(0, 2)] private int laneIndex = 1;

    [Header("Deterministic Movement")]
    [SerializeField, Min(0.1f)] private float forwardSpeed = 15f;
    [SerializeField, Min(0.1f)] private float laneChangeDuration = 0.32f;
    [SerializeField, Min(0.1f)] private float jumpSpeed = 6.5f;
    [SerializeField, Min(0.1f)] private float gravity = 14f;
    [SerializeField] private float groundOffset = 0f;
    [SerializeField, Min(10f)] private float minimumSwipeDistance = 50f;

    [Header("Score Speed Progression")]
    [SerializeField, Min(1f)] private float speedMultiplierAt300m = 4f;
    [SerializeField, Min(1f)] private float speedMultiplierAt800m = 2f;
    [SerializeField, Min(1f)] private float speedMultiplierAt1100m = 1.5f;
    [SerializeField] private float currentForwardSpeed;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform characterModel;
    [SerializeField, Min(0.1f)] private float slideDuration = 1.35f;
    [SerializeField, Min(0.01f)] private float runTransitionDuration = 0.12f;
    [SerializeField] private float deathGroundContactOffset = 0f;
    [SerializeField, Min(0.1f)] private float deathAnimationDuration = 2.6f;
    [SerializeField, Min(0.1f)] private float forwardDeathAnimationDuration = 1.7f;
    [SerializeField, Range(0.5f, 1f)] private float minimumRunPlaybackSpeed = 0.65f;
    [SerializeField, Range(1f, 1.5f)] private float maximumRunPlaybackSpeed = 1.3f;

    [Header("Collision & Audio")]
    [SerializeField] private CapsuleCollider playerCollider;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private AudioSource impactAudio;

    private Rigidbody body;
    private BottleHealthSystem healthSystem;
    private float originalForwardSpeed;
    private float speedMultiplier = 1f;
    private float targetLaneX;
    private float laneStartX;
    private float laneTimer;
    private bool laneChanging;
    private bool grounded = true;
    private bool sliding;
    private bool dead;
    private float verticalSpeed;
    private float lastGroundY;
    private float originalColliderHeight;
    private Vector3 originalColliderCenter;
    private bool hasGroundedParameter;
    private bool hasStableGroundedModelHeight;
    private float stableGroundedModelLocalY;
    private bool finalDeathPoseLocked;
    private float lockedDeathModelWorldY;
    private Button leftButton;
    private Button rightButton;
    private Button jumpButton;
    private Button slideButton;
    private Vector2 touchStartPosition;
    private bool trackingTouch;

    public Transform RunnerTransform => transform;
    public float TrackCenterXOffset => trackCenterXOffset;
    public float LaneDistance => laneDistance;
    public float StartingZ { get; private set; }
    public bool IsDead => dead;
    public float CurrentForwardSpeed => currentForwardSpeed;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        playerCollider = playerCollider != null ? playerCollider : GetComponent<CapsuleCollider>();

        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();

        // Kinematic MovePosition makes Cyber lane motion deterministic and prevents
        // the segmented road meshes from adding sideways collision impulses.
        body.useGravity = false;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.constraints = RigidbodyConstraints.FreezeRotation;

        if (playerCollider != null)
        {
            playerCollider.isTrigger = true;
            originalColliderHeight = playerCollider.height;
            originalColliderCenter = playerCollider.center;
        }
    }

    private void Start()
    {
        dead = false;
        sliding = false;
        originalForwardSpeed = forwardSpeed;
        StartingZ = transform.position.z;
        healthSystem = FindAnyObjectByType<BottleHealthSystem>();

        ResolveAnimator();
        CacheAnimatorParameters();

        if (HasAnimator())
        {
            // Foot IK and stabilization keep the running pose planted instead of
            // making the legs look weak or as though they are skating over the road.
            animator.stabilizeFeet = true;
            animator.speed = 1f;
        }

        if (impactAudio != null)
        {
            impactAudio.playOnAwake = false;
            impactAudio.Stop();
        }

        targetLaneX = CalculateLaneX(laneIndex);
        Vector3 position = body.position;
        position.x = targetLaneX;
        if (TryGetRoadY(position, out float roadY))
        {
            lastGroundY = roadY;
            position.y = roadY + groundOffset;
        }
        body.position = position;

        SetAnimatorGrounded(true);
        BindMovementButtons();
    }

    private void Update()
    {
        if (dead || Time.timeScale == 0f)
            return;

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            LeftMove();
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            RightMove();
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow))
            Jump();
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            Slide();

        HandleTouchInput();
    }

    private void FixedUpdate()
    {
        if (dead || Time.timeScale == 0f)
            return;

        float deltaTime = Time.fixedDeltaTime;
        Vector3 current = body.position;
        Vector3 next = current;

        if (laneChanging)
        {
            laneTimer += deltaTime;
            float progress = Mathf.Clamp01(laneTimer / laneChangeDuration);
            float smoothProgress = progress * progress * (3f - 2f * progress);
            next.x = Mathf.Lerp(laneStartX, targetLaneX, smoothProgress);

            if (progress >= 1f)
            {
                next.x = targetLaneX;
                laneChanging = false;
            }
        }
        else
        {
            next.x = targetLaneX;
        }

        Vector3 roadProbePosition = new Vector3(next.x, current.y, current.z);
        if (TryGetRoadY(roadProbePosition, out float roadY))
            lastGroundY = roadY;

        float groundedY = lastGroundY + groundOffset;
        if (!grounded)
        {
            verticalSpeed -= gravity * deltaTime;
            next.y += verticalSpeed * deltaTime;
            if (verticalSpeed <= 0f && next.y <= groundedY)
            {
                next.y = groundedY;
                verticalSpeed = 0f;
                grounded = true;
                SetAnimatorGrounded(true);
                CrossFadeToRun();
            }
        }
        else
        {
            next.y = groundedY;
        }

        currentForwardSpeed = GetCurrentForwardSpeed();
        next.z += currentForwardSpeed * deltaTime;

        if (OverlapsObstacleAt(next))
        {
            TriggerObstacleDeath();
            return;
        }

        body.MovePosition(next);
    }

    private void LateUpdate()
    {
        UpdateRunningAnimationSpeed();

        if (characterModel == null)
            return;

        if (dead)
        {
            if (finalDeathPoseLocked)
                RestoreLockedDeathModelHeight();
            else
                AlignVisibleModelToRoad(deathGroundContactOffset);
        }
        else if (grounded)
        {
            StabilizeGroundedModelHeight();
        }
    }

    private void StabilizeGroundedModelHeight()
    {
        // Renderer bounds change as each foot advances. Re-aligning from those bounds
        // every frame creates a feedback loop that lifts and lowers the whole model.
        // Establish contact from the first evaluated running pose, then keep the model
        // root at that height while grounded. The leg animation can still move normally.
        if (!hasStableGroundedModelHeight)
        {
            AlignVisibleModelToColliderBottom();
            stableGroundedModelLocalY = characterModel.localPosition.y;
            hasStableGroundedModelHeight = true;
        }

        Vector3 localPosition = characterModel.localPosition;
        localPosition.y = stableGroundedModelLocalY;
        characterModel.localPosition = localPosition;
    }

    private void UpdateRunningAnimationSpeed()
    {
        if (!HasAnimator())
            return;

        if (dead)
            return;

        if (sliding || !grounded)
        {
            animator.speed = 1f;
            return;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (!state.IsName("Running"))
        {
            animator.speed = 1f;
            return;
        }

        float movementSpeed = currentForwardSpeed > 0.01f
            ? currentForwardSpeed
            : originalForwardSpeed;
        float movementRatio = originalForwardSpeed > 0.01f
            ? movementSpeed / originalForwardSpeed
            : 1f;

        animator.speed = Mathf.Clamp(
            movementRatio,
            minimumRunPlaybackSpeed,
            maximumRunPlaybackSpeed);
    }

    public void LeftMove()
    {
        if (dead || laneIndex <= 0)
            return;

        laneIndex--;
        BeginLaneChange();
    }

    public void RightMove()
    {
        if (dead || laneIndex >= 2)
            return;

        laneIndex++;
        BeginLaneChange();
    }

    public void Jump()
    {
        if (dead || !grounded || sliding)
            return;

        grounded = false;
        verticalSpeed = jumpSpeed;
        SetAnimatorGrounded(false);
        if (HasAnimator())
        {
            animator.speed = 1f;
            animator.ResetTrigger("jump");
            animator.SetTrigger("jump");
        }
    }

    public void Slide()
    {
        if (!dead && grounded && !sliding)
            StartCoroutine(SlideRoutine());
    }

    public void SetScoreSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(1f, multiplier);
    }

    public void TriggerObstacleDeath()
    {
        if (!dead)
            StartCoroutine(DeathRoutine(false));
    }

    public void KnockoutByEnemy()
    {
        if (!dead)
            StartCoroutine(DeathRoutine(true));
    }

    public void RestoreAnimatorAfterPause()
    {
        if (dead || !HasAnimator())
            return;

        // A humanoid Animator frozen at timeScale zero can retain a partially
        // evaluated IK/transition pose. Rebind the skeleton and explicitly return
        // it to the locomotion state represented by the Cyber movement controller.
        animator.enabled = true;
        animator.speed = 1f;
        animator.applyRootMotion = false;
        animator.Rebind();

        PlayerGunController gunController = GetComponent<PlayerGunController>();
        bool hasGun = gunController != null && gunController.HasGun;
        bool isAiming = gunController != null && gunController.IsAiming;
        SetAnimatorBoolIfPresent("hasGun", hasGun);
        SetAnimatorBoolIfPresent("isAiming", isAiming);
        SetAnimatorGrounded(grounded);

        string resumeState = !grounded
            ? "jump"
            : sliding
                ? "slide"
                : isAiming
                    ? "Shooting"
                    : "Running";

        animator.Play(resumeState, 0, 0f);
        animator.Update(0f);

        // Rebind resets the animated hierarchy, so establish the model's grounded
        // height again from this freshly evaluated, valid pose.
        hasStableGroundedModelHeight = false;
        if (grounded)
            StabilizeGroundedModelHeight();
    }

    private void BeginLaneChange()
    {
        laneStartX = body.position.x;
        targetLaneX = CalculateLaneX(laneIndex);
        laneTimer = 0f;
        laneChanging = true;
    }

    private float CalculateLaneX(int lane)
    {
        return trackCenterXOffset + ((lane - 1) * laneDistance);
    }

    private float GetCurrentForwardSpeed()
    {
        // Calculate the milestone locally as a fallback. This keeps Cyber speed
        // progression working even if the HUD score component initializes later.
        float distanceTravelled = Mathf.Max(0f, body.position.z - StartingZ);
        float milestoneMultiplier = GetMilestoneMultiplier(distanceTravelled);
        float effectiveMultiplier = Mathf.Max(speedMultiplier, milestoneMultiplier);
        float scoreAdjustedSpeed = originalForwardSpeed * effectiveMultiplier;
        if (healthSystem == null || healthSystem.health > 0.3f)
            return scoreAdjustedSpeed;

        float lowHealthAmount = Mathf.Clamp01((0.3f - healthSystem.health) / 0.3f);
        return Mathf.Lerp(scoreAdjustedSpeed, 2f, lowHealthAmount);
    }

    private float GetMilestoneMultiplier(float distanceTravelled)
    {
        // The last branch is deliberately the maximum. Distance beyond 1100 m
        // never increases the multiplier any further.
        if (distanceTravelled >= 1100f)
            return speedMultiplierAt1100m;
        if (distanceTravelled >= 800f)
            return speedMultiplierAt800m;
        if (distanceTravelled >= 300f)
            return speedMultiplierAt300m;
        return 1f;
    }

    private IEnumerator SlideRoutine()
    {
        sliding = true;
        if (HasAnimator())
        {
            animator.speed = 1f;
            animator.SetBool("slide", true);
        }

        if (playerCollider != null)
        {
            playerCollider.height = originalColliderHeight * 0.5f;
            playerCollider.center = new Vector3(
                originalColliderCenter.x,
                originalColliderCenter.y - originalColliderHeight * 0.25f,
                originalColliderCenter.z);
        }

        yield return new WaitForSeconds(slideDuration);

        if (playerCollider != null)
        {
            playerCollider.height = originalColliderHeight;
            playerCollider.center = originalColliderCenter;
        }

        if (HasAnimator())
        {
            animator.SetBool("slide", false);
            CrossFadeToRun();
        }
        sliding = false;
    }

    private IEnumerator DeathRoutine(bool forwardDeath)
    {
        dead = true;
        finalDeathPoseLocked = false;
        laneChanging = false;
        verticalSpeed = 0f;

        if (impactAudio != null)
            impactAudio.Play();

        if (HasAnimator())
        {
            animator.speed = 1f;
            animator.applyRootMotion = false;
            if (forwardDeath)
                animator.CrossFadeInFixedTime("forwardDeath", 0.08f, 0);
            else
                // Direct state playback works even when the collision happens in
                // the jump/slide states, which have no Death trigger transition.
                animator.CrossFadeInFixedTime("Death", 0.08f, 0);
        }

        float duration = forwardDeath ? forwardDeathAnimationDuration : deathAnimationDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Vector3 position = body.position;
            if (TryGetRoadY(position, out float roadY))
            {
                lastGroundY = roadY;
                position.y = roadY + groundOffset;
                body.position = position;
            }
            yield return null;
        }

        LockFinalDeathPose(forwardDeath ? "forwardDeath" : "Death");
        // Allow renderer bounds to refresh from the final sampled pose, then seat
        // the lowest visible point exactly on the road one final time.
        yield return new WaitForEndOfFrame();
        RestoreLockedDeathModelHeight();

        if (GameManager.Instance != null)
            GameManager.Instance.TriggerGameOverSequence();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!dead && HasTagInParents(other.transform, "Obstacle"))
            TriggerObstacleDeath();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!dead && HasTagInParents(collision.transform, "Obstacle"))
            TriggerObstacleDeath();
    }

    private bool TryGetRoadY(Vector3 position, out float roadY)
    {
        int mask = groundLayer == 0 ? ~0 : (int)groundLayer;
        Vector3 origin = new Vector3(position.x, position.y + 12f, position.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 30f, mask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            if (IsRoadSurface(hit.collider))
            {
                roadY = hit.point.y;
                return true;
            }
        }

        roadY = lastGroundY;
        return false;
    }

    private bool IsRoadSurface(Collider collider)
    {
        if (collider == null || collider.isTrigger)
            return false;
        if (collider.transform == transform || collider.transform.IsChildOf(transform))
            return false;
        if (RunnerControllerLocator.GetFrom(collider) != null)
            return false;
        if (collider.GetComponentInParent<WaterBottleItem>() != null ||
            collider.GetComponentInParent<CoinScript>() != null ||
            collider.GetComponentInParent<GunPickup>() != null ||
            HasTagInParents(collider.transform, "Obstacle") ||
            HasTagInParents(collider.transform, "Coin"))
            return false;

        if (collider.CompareTag("Ground"))
            return true;

        string objectName = collider.name.ToLowerInvariant();
        if (objectName.Contains("building") || objectName.Contains("sign") ||
            objectName.Contains("wall") || objectName.Contains("roof") ||
            objectName.Contains("light") || objectName.Contains("wire"))
            return false;

        return objectName.Contains("road") || objectName.Contains("ground") ||
               objectName.Contains("tile") || objectName.Contains("plane") ||
               objectName.Contains("pavement") || objectName.Contains("terrain") ||
               objectName.Contains("shoulder") || objectName.Contains("scut");
    }

    private bool OverlapsObstacleAt(Vector3 rootPosition)
    {
        if (playerCollider == null || !playerCollider.enabled)
            return false;

        Vector3 scale = transform.lossyScale;
        float radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float radius = Mathf.Max(0.05f, playerCollider.radius * radiusScale * 0.9f);
        float height = Mathf.Max(radius * 2f, playerCollider.height * Mathf.Abs(scale.y));
        Vector3 scaledCenter = Vector3.Scale(playerCollider.center, scale);
        Vector3 center = rootPosition + transform.rotation * scaledCenter;
        float halfSegment = Mathf.Max(0f, (height * 0.5f) - radius);
        Vector3 top = center + transform.up * halfSegment;
        Vector3 bottom = center - transform.up * halfSegment;

        Collider[] overlaps = Physics.OverlapCapsule(
            top,
            bottom,
            radius,
            ~0,
            QueryTriggerInteraction.Collide);

        foreach (Collider overlap in overlaps)
        {
            if (overlap == null || overlap.transform == transform || overlap.transform.IsChildOf(transform))
                continue;

            if (HasTagInParents(overlap.transform, "Obstacle"))
                return true;
        }

        return false;
    }

    private void AlignVisibleModelToRoad(float contactOffset)
    {
        if (!TryGetRoadY(body.position, out float roadY))
            return;

        AlignVisibleModelToHeight(roadY + contactOffset);
    }

    private void AlignVisibleModelToColliderBottom()
    {
        if (playerCollider == null || !playerCollider.enabled)
            return;

        AlignVisibleModelToHeight(playerCollider.bounds.min.y);
    }

    private void AlignVisibleModelToHeight(float targetY)
    {
        if (characterModel == null)
            return;

        bool hasBounds = false;
        Bounds bounds = new Bounds();
        foreach (Renderer renderer in characterModel.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled)
                continue;
            string rendererName = renderer.name.ToLowerInvariant();
            if (rendererName.Contains("gun") || rendererName.Contains("weapon"))
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
            characterModel.position += Vector3.up * (targetY - bounds.min.y);
    }

    private void AlignVisibleModelToHeightExact(float targetY)
    {
        if (characterModel == null)
            return;

        bool hasVisiblePoint = false;
        float minimumWorldY = float.PositiveInfinity;
        Mesh bakedMesh = new Mesh { name = "CyberDeathPoseGroundingMesh" };

        foreach (Renderer renderer in characterModel.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled)
                continue;

            string rendererName = renderer.name.ToLowerInvariant();
            if (rendererName.Contains("gun") || rendererName.Contains("weapon"))
                continue;

            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (skinned == null)
            {
                minimumWorldY = Mathf.Min(minimumWorldY, renderer.bounds.min.y);
                hasVisiblePoint = true;
                continue;
            }

            bakedMesh.Clear();
            skinned.BakeMesh(bakedMesh);
            Vector3[] vertices = bakedMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                minimumWorldY = Mathf.Min(
                    minimumWorldY,
                    skinned.transform.TransformPoint(vertices[i]).y);
                hasVisiblePoint = true;
            }
        }

        Destroy(bakedMesh);

        if (hasVisiblePoint)
            characterModel.position += Vector3.up * (targetY - minimumWorldY);
    }

    private void LockFinalDeathPose(string stateName)
    {
        if (!HasAnimator())
            return;

        animator.Play(stateName, 0, 0.999f);
        animator.Update(0f);
        animator.speed = 0f;

        if (TryGetRoadY(body.position, out float roadY))
            AlignVisibleModelToHeightExact(roadY + deathGroundContactOffset);

        if (characterModel != null)
        {
            lockedDeathModelWorldY = characterModel.position.y;
            finalDeathPoseLocked = true;
        }
    }

    private void RestoreLockedDeathModelHeight()
    {
        if (characterModel == null || !finalDeathPoseLocked)
            return;

        Vector3 position = characterModel.position;
        position.y = lockedDeathModelWorldY;
        characterModel.position = position;
    }

    private void ResolveAnimator()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            foreach (Animator candidate in GetComponentsInChildren<Animator>(true))
            {
                if (candidate.runtimeAnimatorController != null)
                {
                    animator = candidate;
                    break;
                }
            }
        }

        if (characterModel == null && animator != null)
            characterModel = animator.transform;
    }

    private void CacheAnimatorParameters()
    {
        hasGroundedParameter = false;
        if (!HasAnimator())
            return;

        int groundedHash = Animator.StringToHash("grounded");
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == groundedHash && parameter.type == AnimatorControllerParameterType.Bool)
            {
                hasGroundedParameter = true;
                break;
            }
        }
    }

    private void SetAnimatorGrounded(bool value)
    {
        if (hasGroundedParameter && animator != null)
            animator.SetBool("grounded", value);
    }

    private void SetAnimatorBoolIfPresent(string parameterName, bool value)
    {
        if (!HasAnimator())
            return;

        int parameterHash = Animator.StringToHash(parameterName);
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == parameterHash && parameter.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(parameterHash, value);
                return;
            }
        }
    }

    private void CrossFadeToRun()
    {
        if (HasAnimator())
            animator.CrossFadeInFixedTime("Running", runTransitionDuration, 0);
    }

    private bool HasAnimator()
    {
        return animator != null && animator.runtimeAnimatorController != null;
    }

    private void BindMovementButtons()
    {
        leftButton = FindButton("leftMove");
        rightButton = FindButton("RightMove");
        jumpButton = FindButton("jump");
        slideButton = FindButton("slide");

        if (leftButton != null) leftButton.onClick.AddListener(LeftMove);
        if (rightButton != null) rightButton.onClick.AddListener(RightMove);
        if (jumpButton != null) jumpButton.onClick.AddListener(Jump);
        if (slideButton != null) slideButton.onClick.AddListener(Slide);
    }

    private static Button FindButton(string objectName)
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            if (button.name == objectName)
                return button;
        }
        return null;
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount == 0)
        {
            trackingTouch = false;
            return;
        }

        Touch touch = Input.GetTouch(0);
        if (touch.phase == TouchPhase.Began)
        {
            touchStartPosition = touch.position;
            trackingTouch = true;
            return;
        }

        if (!trackingTouch || (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled))
            return;

        trackingTouch = false;
        Vector2 swipe = touch.position - touchStartPosition;
        if (swipe.magnitude < minimumSwipeDistance)
            return;

        if (Mathf.Abs(swipe.x) > Mathf.Abs(swipe.y))
        {
            if (swipe.x < 0f) LeftMove();
            else RightMove();
        }
        else
        {
            if (swipe.y > 0f) Jump();
            else Slide();
        }
    }

    private static bool HasTagInParents(Transform current, string tagName)
    {
        while (current != null)
        {
            if (current.CompareTag(tagName))
                return true;
            current = current.parent;
        }
        return false;
    }

    private void OnDestroy()
    {
        if (leftButton != null) leftButton.onClick.RemoveListener(LeftMove);
        if (rightButton != null) rightButton.onClick.RemoveListener(RightMove);
        if (jumpButton != null) jumpButton.onClick.RemoveListener(Jump);
        if (slideButton != null) slideButton.onClick.RemoveListener(Slide);
    }
}
