using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)]
public class PlayerMove : MonoBehaviour, IRunnerController
{
    [Header("Track Alignment")]
    [SerializeField] protected float trackCenterXOffset = -15.0f;
    [SerializeField] protected float laneDistance = 0.2f;

    [Header("Movement")]
    [SerializeField] protected int laneIndex = 1;
    [SerializeField] protected float sideSpeed = 10f;
    [Range(1f, 10f)][SerializeField] protected float laneChangeSmoothness = 5f;
    public float forwardSpeed = 5f;

    [Header("Score Speed Progression")]
    [SerializeField, Min(1f)] private float scoreSpeedMultiplier = 1f;
    [SerializeField, Min(1f)] private float speedMultiplierAt300m = 1.1f;
    [SerializeField, Min(1f)] private float speedMultiplierAt800m = 1.2f;
    [SerializeField, Min(1f)] private float speedMultiplierAt1100m = 1.3f;

    public float TrackCenterXOffset => trackCenterXOffset;
    public float LaneDistance => laneDistance;
    public bool IsDead => isDead;
    public float StartingZ { get; protected set; }
    public Transform RunnerTransform => transform;

    [Header("Animation")]
    [SerializeField] protected Animator animator;

    [Header("Jump & Slide")]
    [SerializeField] protected float jumpForce = 6.5f;
    [SerializeField, Min(0.1f)] private float jumpGravity = 14f;
    [SerializeField, Min(0f)] private float minimumJumpForce = 5.5f;
    [SerializeField, Min(0f)] private float maximumJumpForce = 7f;
    [SerializeField] protected bool isGrounded = true;
    [SerializeField] protected bool isSliding = false;
    [SerializeField] protected CapsuleCollider playerCollider;
    [SerializeField] protected MeshCollider flatBottomCollider;
    [SerializeField] protected float slideDuration = 1.35f;
    [SerializeField, Min(0.01f)] private float runTransitionDuration = 0.12f;

    [Header("Ground Check")]
    [SerializeField] protected LayerMask groundLayer;
    [SerializeField] protected float groundCheckDistance = 0.35f;
    [SerializeField] protected float groundSnapOffset = 0f;
    [SerializeField] protected float groundRaycastHeight = 5f;
    [SerializeField] protected float groundRaycastDistance = 14f;
    [SerializeField] protected Transform characterModel;

    protected Vector3 originalCharacterLocalPos;
    [SerializeField] protected float deathMeshYOffset = -0.5f;
    [SerializeField, Min(0.1f)] private float deathAnimationDuration = 2.6f;
    [SerializeField, Min(0.1f)] private float forwardDeathAnimationDuration = 1.7f;
    [SerializeField] private float deathGroundContactOffset = 0f;
    [SerializeField, Min(0.01f)] private float deathGroundSettleDuration = 0.25f;
    [SerializeField, Range(0.5f, 1f)] private float minimumRunPlaybackSpeed = 0.65f;
    [SerializeField, Range(1f, 1.5f)] private float maximumRunPlaybackSpeed = 1.3f;
    [Header("Obstacle & Game Over Settings")]
    [SerializeField] protected AudioSource impactAudio;
    protected bool isDead = false;

    protected float originalHeight;
    protected Vector3 originalCenter;
    protected Rigidbody rb;
    protected float targetXPosition;
    protected float feetOffsetFromRoot;
    protected float groundedRootY;
    protected float originalForwardSpeed;
    protected BottleHealthSystem healthSystem;
    private bool hasGroundedAnimatorParameter;
    private Mesh flatBottomColliderMesh;
    private bool hasStableGroundedVisualHeight;
    private float stableGroundedVisualLocalY;
    private bool finalDeathPoseLocked;
    private float lockedDeathMeshWorldY;

    /// <summary>Sets the multiplier chosen by the run score milestones.</summary>
    public void SetScoreSpeedMultiplier(float multiplier)
    {
        scoreSpeedMultiplier = Mathf.Clamp(multiplier, 1f, speedMultiplierAt1100m);
    }

    public void TriggerObstacleDeath()
    {
        if (!isDead)
            StartCoroutine(CollapseSequence());
    }

    public void RestoreAnimatorAfterPause()
    {
        if (isDead || !HasPlayableAnimatorController(animator))
            return;

        // Clear any stale transition/IK pose left by an Animator that was frozen at
        // timeScale zero, then restore the locomotion state represented by physics.
        animator.enabled = true;
        animator.applyRootMotion = false;
        animator.Rebind();

        PlayerGunController gunController = GetComponent<PlayerGunController>();
        bool hasGun = gunController != null && gunController.HasGun;
        bool isAiming = gunController != null && gunController.IsAiming;
        animator.SetBool("hasGun", hasGun);
        animator.SetBool("isAiming", isAiming);
        SetAnimatorGrounded(isGrounded);

        string resumeState = !isGrounded
            ? "jump"
            : isSliding
                ? "slide"
                : isAiming
                    ? "Shooting"
                    : "Running";

        animator.Play(resumeState, 0, 0f);
        animator.Update(0f);
    }

    protected virtual void Start()
    {
        // Keep a standard runner jump: high enough for the obstacles, without launching into the air.
        maximumJumpForce = Mathf.Max(minimumJumpForce, maximumJumpForce);
        jumpForce = Mathf.Clamp(jumpForce, minimumJumpForce, maximumJumpForce);
        speedMultiplierAt300m = Mathf.Max(1f, speedMultiplierAt300m);
        speedMultiplierAt800m = Mathf.Max(speedMultiplierAt300m, speedMultiplierAt800m);
        speedMultiplierAt1100m = Mathf.Max(speedMultiplierAt800m, speedMultiplierAt1100m);

        Application.runInBackground = true;
        isDead = false;
        isSliding = false;
        rb = GetComponent<Rigidbody>();
        originalForwardSpeed = forwardSpeed;
        healthSystem = FindAnyObjectByType<BottleHealthSystem>();

        // Impact audio is event-driven. Never let its assigned clip play when gameplay starts.
        if (impactAudio != null)
        {
            impactAudio.playOnAwake = false;
            impactAudio.Stop();
        }

        ResolveAnimatorReference();
        CacheAnimatorParameters();
        SetAnimatorGrounded(true);

        if (playerCollider == null)
            playerCollider = GetComponent<CapsuleCollider>();

        if (playerCollider != null)
            playerCollider.enabled = true;

        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (animator != null)
        {
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.stabilizeFeet = true;
            animator.speed = 1f;
        }

        // Auto-align the mesh to the physics root and fit the capsule to it.
        AlignMeshAndCollider();

        originalHeight = playerCollider.height;
        originalCenter = playerCollider.center;

        // Use a capsule-shaped convex collider with a flat circular base. The original
        // CapsuleCollider remains as the authored size reference for ground checks and sliding.
        RebuildFlatBottomCollider(originalHeight, originalCenter);
        playerCollider.enabled = false;

        feetOffsetFromRoot = CalculateFeetOffsetFromRoot();
        SnapToGround(true);

        targetXPosition = CalculateLaneX(laneIndex);

        Vector3 startPos = rb.position;
        startPos.x = targetXPosition;
        rb.position = startPos;
        groundedRootY = startPos.y;

        rb.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationY |
            RigidbodyConstraints.FreezeRotationZ;


        if (characterModel != null)
        {
            originalCharacterLocalPos = characterModel.localPosition;
        }

        StartingZ = transform.position.z;
    }

    protected virtual void Update()
    {
        if (isDead || Time.timeScale == 0f) return;

        if (Input.GetKeyDown(KeyCode.A))
        {
            LeftMove();
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            RightMove();
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Jump();
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            Slide();
        }
    }

    protected virtual void FixedUpdate()
    {
        // If the player is dead, skip all updates including ground checks and snapping
        if (isDead) return;

        if (DetectObstacleOverlap())
        {
            TriggerObstacleDeath();
            return;
        }

        isGrounded = CheckGrounded();
        SetAnimatorGrounded(isGrounded);

        // Match Cyber's airborne timing. Unity's default gravity left Desert in
        // the air after the non-looping jump clip had already finished.
        if (!isGrounded && !isSliding)
        {
            float extraDownwardAcceleration = Mathf.Max(
                0f,
                jumpGravity - Mathf.Abs(Physics.gravity.y));
            rb.AddForce(Vector3.down * extraDownwardAcceleration, ForceMode.Acceleration);
        }

        // Calculate the milestone here as well as accepting scoreController updates.
        // This keeps Desert progression functional even if the HUD/controller resolves late.
        float distanceTravelled = Mathf.Max(0f, rb.position.z - StartingZ);
        float effectiveSpeedMultiplier = Mathf.Max(
            scoreSpeedMultiplier,
            GetDistanceSpeedMultiplier(distanceTravelled));
        float scoreAdjustedSpeed = originalForwardSpeed * effectiveSpeedMultiplier;

        // Reduce speed dynamically when health is <= 30%, while retaining score progression.
        if (healthSystem != null && healthSystem.health <= 0.3f)
        {
            forwardSpeed = Mathf.Lerp(scoreAdjustedSpeed, 2.0f, (0.3f - healthSystem.health) / 0.3f);
        }
        else if (healthSystem != null && healthSystem.health > 0.3f)
        {
            forwardSpeed = scoreAdjustedSpeed;
        }
        else
        {
            forwardSpeed = scoreAdjustedSpeed;
        }

        float currentX = rb.position.x;
        float xDifference = targetXPosition - currentX;

        float targetSideVelocity = Mathf.Clamp(xDifference * laneChangeSmoothness, -sideSpeed, sideSpeed);

        if (Mathf.Abs(xDifference) < 0.02f)
        {
            targetSideVelocity = 0f;
            Vector3 snapped = rb.position;
            snapped.x = targetXPosition;
            rb.position = snapped;
        }

        rb.linearVelocity = new Vector3(
            targetSideVelocity,
            isSliding ? 0f : rb.linearVelocity.y,
            forwardSpeed
        );

        float minX = CalculateLaneX(0);
        float maxX = CalculateLaneX(2);
        float clampedX = Mathf.Clamp(rb.position.x, minX, maxX);

        if (!Mathf.Approximately(clampedX, rb.position.x))
        {
            Vector3 bounded = rb.position;
            bounded.x = clampedX;
            rb.position = bounded;
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, rb.linearVelocity.z);
        }

        // Snap to ground at the end of FixedUpdate, utilizing the newly updated velocities!
        if (isGrounded && !isSliding)
        {
            SnapToGround(false);
        }
    }

    void AlignMeshAndCollider()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform) continue;
            if (child.parent != transform) continue;
            child.localPosition = Vector3.zero;
        }

        bool first = true;
        Bounds combined = new Bounds();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.transform == transform) continue;
            if (!r.enabled) continue;
            if (r.bounds.extents.sqrMagnitude < 0.0001f) continue;
            if (r.name.ToLower().Contains("gun") || r.name.ToLower().Contains("weapon")) continue;

            if (first) { combined = r.bounds; first = false; }
            else combined.Encapsulate(r.bounds);
        }

        if (first)
        {
            Debug.LogWarning("[PlayerMove] AlignMeshAndCollider: no valid child renderers found. Collider unchanged.");
            return;
        }

        Vector3 localCenter = transform.InverseTransformPoint(combined.center);
        float localHeight = combined.size.y;
        float localMinY = localCenter.y - localHeight * 0.5f;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform) continue;
            if (child.parent != transform) continue;
            child.localPosition = new Vector3(0f, -localMinY, 0f);
        }

        playerCollider.height = Mathf.Max(localHeight, 0.2f);
        playerCollider.center = new Vector3(0f, playerCollider.height * 0.5f, 0f);
        playerCollider.radius = Mathf.Clamp(playerCollider.height * 0.18f, 0.1f, 0.49f * playerCollider.height);
    }

    private void RebuildFlatBottomCollider(float height, Vector3 center)
    {
        if (flatBottomCollider == null)
        {
            flatBottomCollider = GetComponent<MeshCollider>();
            if (flatBottomCollider == null)
                flatBottomCollider = gameObject.AddComponent<MeshCollider>();
        }

        float radius = Mathf.Min(playerCollider.radius, height * 0.5f);
        float bottomY = center.y - height * 0.5f;
        float topY = center.y + height * 0.5f;
        float hemisphereCenterY = topY - radius;
        const int segments = 16;
        const int hemisphereRings = 4;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        int bottomCenter = vertices.Count;
        vertices.Add(new Vector3(center.x, bottomY, center.z));

        // Keep a genuinely flat base while beveling its outer edge slightly. A sharp
        // 90-degree rim can catch on the many small road-mesh seams in CyberPunkCity.
        float baseRadius = radius * 0.9f;
        float bevelHeight = Mathf.Min(radius * 0.12f, 0.06f);

        int bottomRing = vertices.Count;
        AddColliderRing(vertices, center, bottomY, baseRadius, segments);

        int bevelRing = vertices.Count;
        AddColliderRing(vertices, center, bottomY + bevelHeight, radius, segments);

        int previousRing = vertices.Count;
        AddColliderRing(vertices, center, hemisphereCenterY, radius, segments);

        // Flat bottom disc.
        for (int segment = 0; segment < segments; segment++)
        {
            int next = (segment + 1) % segments;
            triangles.Add(bottomCenter);
            triangles.Add(bottomRing + next);
            triangles.Add(bottomRing + segment);
        }

        AddColliderRingBridge(triangles, bottomRing, bevelRing, segments);
        AddColliderRingBridge(triangles, bevelRing, previousRing, segments);

        // Rounded capsule top.
        for (int ring = 1; ring < hemisphereRings; ring++)
        {
            float angle = (Mathf.PI * 0.5f) * ring / hemisphereRings;
            float ringRadius = Mathf.Cos(angle) * radius;
            float ringY = hemisphereCenterY + Mathf.Sin(angle) * radius;
            int currentRing = vertices.Count;
            AddColliderRing(vertices, center, ringY, ringRadius, segments);
            AddColliderRingBridge(triangles, previousRing, currentRing, segments);
            previousRing = currentRing;
        }

        int topPoint = vertices.Count;
        vertices.Add(new Vector3(center.x, topY, center.z));
        for (int segment = 0; segment < segments; segment++)
        {
            int next = (segment + 1) % segments;
            triangles.Add(previousRing + segment);
            triangles.Add(previousRing + next);
            triangles.Add(topPoint);
        }

        if (flatBottomColliderMesh != null)
            Destroy(flatBottomColliderMesh);

        flatBottomColliderMesh = new Mesh { name = "PlayerFlatBottomCapsule" };
        flatBottomColliderMesh.SetVertices(vertices);
        flatBottomColliderMesh.SetTriangles(triangles, 0);
        flatBottomColliderMesh.RecalculateBounds();

        flatBottomCollider.enabled = false;
        flatBottomCollider.sharedMesh = null;
        flatBottomCollider.convex = true;
        flatBottomCollider.sharedMesh = flatBottomColliderMesh;
        flatBottomCollider.isTrigger = false;
        flatBottomCollider.sharedMaterial = playerCollider.sharedMaterial;
        flatBottomCollider.enabled = true;
    }

    private static void AddColliderRing(List<Vector3> vertices, Vector3 center, float y, float radius, int segments)
    {
        for (int segment = 0; segment < segments; segment++)
        {
            float angle = Mathf.PI * 2f * segment / segments;
            vertices.Add(new Vector3(
                center.x + Mathf.Cos(angle) * radius,
                y,
                center.z + Mathf.Sin(angle) * radius));
        }
    }

    private static void AddColliderRingBridge(List<int> triangles, int lowerRing, int upperRing, int segments)
    {
        for (int segment = 0; segment < segments; segment++)
        {
            int next = (segment + 1) % segments;
            triangles.Add(lowerRing + segment);
            triangles.Add(upperRing + next);
            triangles.Add(upperRing + segment);
            triangles.Add(lowerRing + segment);
            triangles.Add(lowerRing + next);
            triangles.Add(upperRing + next);
        }
    }

    void ResolveAnimatorReference()
    {
        Animator localAnimator = GetComponent<Animator>();
        if (localAnimator != null && localAnimator.runtimeAnimatorController != null)
        {
            animator = localAnimator;
            return;
        }

        if (animator == null || !HasPlayableAnimatorController(animator))
        {
            foreach (Animator candidate in GetComponentsInChildren<Animator>(true))
            {
                if (HasPlayableAnimatorController(candidate))
                {
                    animator = candidate;
                    return;
                }
            }
        }
    }

    private void CacheAnimatorParameters()
    {
        hasGroundedAnimatorParameter = false;
        if (!HasPlayableAnimatorController(animator))
            return;

        int groundedHash = Animator.StringToHash("grounded");
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == groundedHash && parameter.type == AnimatorControllerParameterType.Bool)
            {
                hasGroundedAnimatorParameter = true;
                break;
            }
        }
    }

    private void SetAnimatorGrounded(bool grounded)
    {
        if (hasGroundedAnimatorParameter && animator != null)
            animator.SetBool("grounded", grounded);
    }

    static bool HasPlayableAnimatorController(Animator candidate)
    {
        return candidate != null && candidate.runtimeAnimatorController != null;
    }

    protected virtual void LateUpdate()
    {
        // Keep correcting the rendered pose through the delay between the death
        // animation ending and the Game Over panel freezing time.
        if (isDead)
        {
            if (finalDeathPoseLocked)
                RestoreLockedDeathMeshHeight();
            else
                AlignVisibleMeshToGround(deathGroundContactOffset);
            return;
        }

        UpdateRunningAnimationSpeed();

        if (animator != null && animator.transform != transform)
        {
            Vector3 localPos = animator.transform.localPosition;
            animator.transform.localPosition = new Vector3(0f, localPos.y, 0f);
        }

        // Keep the visible root at one stable grounded height. Recalculating it from
        // animated renderer bounds every frame makes the whole player bob each step.
        if (isGrounded)
            StabilizeGroundedVisualHeight();
    }

    private void StabilizeGroundedVisualHeight()
    {
        Transform meshRoot = GetVisibleMeshRoot();
        if (meshRoot == null)
            return;

        if (!hasStableGroundedVisualHeight)
        {
            AlignVisibleMeshToColliderBottom();
            stableGroundedVisualLocalY = meshRoot.localPosition.y;
            hasStableGroundedVisualHeight = true;
        }

        Vector3 localPosition = meshRoot.localPosition;
        localPosition.y = stableGroundedVisualLocalY;
        meshRoot.localPosition = localPosition;
    }

    private void UpdateRunningAnimationSpeed()
    {
        if (!HasPlayableAnimatorController(animator))
            return;

        if (isSliding || !isGrounded)
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

        float movementRatio = originalForwardSpeed > 0.01f
            ? forwardSpeed / originalForwardSpeed
            : 1f;

        animator.speed = Mathf.Clamp(
            movementRatio,
            minimumRunPlaybackSpeed,
            maximumRunPlaybackSpeed);
    }

    bool CheckGrounded()
    {
        // If the player is moving upward (e.g., jumping), they are not grounded.
        // This prevents SnapToGround from pulling the player back down during a jump.
        if (rb != null && rb.linearVelocity.y > 0.1f)
        {
            return false;
        }

        Vector3 bottom = rb.position + playerCollider.center
                         - Vector3.up * (playerCollider.height * 0.5f - playerCollider.radius);

        int layerMask = groundLayer == 0 ? ~0 : (int)groundLayer;

        RaycastHit[] hits = Physics.SphereCastAll(
            bottom,
            playerCollider.radius * 0.9f,
            Vector3.down,
            groundCheckDistance,
            layerMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hitInfo in hits)
        {
            if (hitInfo.collider.transform == transform || hitInfo.collider.transform.IsChildOf(transform))
                continue;

            if (IsGroundCollider(hitInfo.collider))
                return true;
        }

        return false;
    }

    bool IsGroundCollider(Collider collider)
    {
        if (collider == null || collider.isTrigger)
            return false;

        if (collider.transform == transform || collider.transform.IsChildOf(transform))
            return false;

        if (collider.GetComponentInParent<WaterBottleItem>() != null ||
            collider.GetComponentInParent<CoinScript>() != null)
        {
            return false;
        }

        Transform current = collider.transform;
        while (current != null)
        {
            if (current.CompareTag("Obstacle") || current.CompareTag("Coin"))
                return false;
            current = current.parent;
        }

        if (collider.CompareTag("Ground"))
            return true;

        if (collider.transform.parent != null && collider.transform.parent.CompareTag("Ground"))
            return true;

        string colliderName = collider.name;
        return colliderName.Contains("Tile") ||
               colliderName.Contains("Plane") ||
               colliderName.Contains("Road") ||
               colliderName.Contains("Terrain") ||
               colliderName.Contains("Ground");
    }

    float CalculateFeetOffsetFromRoot()
    {
        float capsuleBottomLocal = playerCollider.center.y - playerCollider.height * 0.5f;
        return -capsuleBottomLocal;
    }

    bool TryGetGroundY(out float groundY)
    {
        // Use a fixed high Y coordinate of 25.0f to ensure the raycast origin starts way above the road
        Vector3 origin = new Vector3(rb.position.x, 25f, rb.position.z);
        int layerMask = groundLayer == 0 ? ~0 : (int)groundLayer;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            40f, // Use a larger distance of 40.0f to ensure it reaches below the road
            layerMask,
            QueryTriggerInteraction.Ignore
        );

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hitInfo in hits)
        {
            if (IsGroundCollider(hitInfo.collider))
            {
                groundY = hitInfo.point.y;
                return true;
            }
        }

        groundY = 0f;
        return false;
    }

    protected virtual void SnapToGround(bool force)
    {
        if (!TryGetGroundY(out float groundY))
            return;

        float targetRootY = groundY + feetOffsetFromRoot + groundSnapOffset;

        if (!force && Mathf.Abs(rb.position.y - targetRootY) < 0.005f)
            return;

        Vector3 pos = rb.position;
        pos.y = targetRootY;

        if (force)
        {
            rb.position = pos;
        }
        else
        {
            // Correct height only. Horizontal motion is already integrated from
            // linearVelocity; advancing X/Z here as well makes some road contacts
            // apply movement twice and produces visible lane/forward stutter.
            rb.position = pos;
        }

        groundedRootY = targetRootY;

        if (!rb.isKinematic)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, isDead ? rb.linearVelocity.y : 0f, rb.linearVelocity.z);
    }

    float CalculateLaneX(int lane)
    {
        return trackCenterXOffset + ((lane - 1) * laneDistance);
    }

    private float GetDistanceSpeedMultiplier(float distanceTravelled)
    {
        if (distanceTravelled >= 1100f) return speedMultiplierAt1100m;
        if (distanceTravelled >= 800f) return speedMultiplierAt800m;
        if (distanceTravelled >= 300f) return speedMultiplierAt300m;
        return 1f;
    }

    public void Jump()
    {
        if (isDead) return;

        if (isGrounded && !isSliding)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
            SetAnimatorGrounded(false);

            if (HasPlayableAnimatorController(animator))
            {
                animator.speed = 1f;
                animator.ResetTrigger("jump");
                animator.SetTrigger("jump");
                // Enter the animation immediately. This avoids the armed Running state
                // consuming or delaying the trigger while the Rigidbody is already airborne.
                animator.CrossFadeInFixedTime("jump", 0.05f, 0);
            }
        }
    }

    public void Slide()
    {
        if (isDead) return;

        if (isGrounded && !isSliding)
        {
            StartCoroutine(PhysicsSafeSlideRoutine());
        }
    }

    IEnumerator PhysicsSafeSlideRoutine()
    {
        isSliding = true;

        if (HasPlayableAnimatorController(animator))
        {
            animator.speed = 1f;
            animator.SetBool("slide", true);
        }

        SnapToGround(true);

        float targetHeight = originalHeight / 2f;
        float transitionTime = 0.15f;
        Vector3 targetCenter = new Vector3(originalCenter.x, targetHeight * 0.5f, originalCenter.z);

        RebuildFlatBottomCollider(targetHeight, targetCenter);

        float timer = 0f;
        while (timer < transitionTime)
        {
            timer += Time.fixedDeltaTime;
            float t = timer / transitionTime;
            playerCollider.height = Mathf.Lerp(originalHeight, targetHeight, t);
            playerCollider.center = new Vector3(originalCenter.x, playerCollider.height * 0.5f, originalCenter.z);

            yield return new WaitForFixedUpdate();
        }

        playerCollider.height = targetHeight;
        playerCollider.center = targetCenter;

        yield return new WaitForSeconds(Mathf.Max(0.1f, slideDuration - (transitionTime * 2)));

        timer = 0f;
        while (timer < transitionTime)
        {
            timer += Time.fixedDeltaTime;
            float t = timer / transitionTime;
            playerCollider.height = Mathf.Lerp(targetHeight, originalHeight, t);
            playerCollider.center = new Vector3(0f, playerCollider.height * 0.5f, 0f);

            yield return new WaitForFixedUpdate();
        }

        playerCollider.height = originalHeight;
        playerCollider.center = originalCenter;
        RebuildFlatBottomCollider(originalHeight, originalCenter);

        SnapToGround(true);

        if (HasPlayableAnimatorController(animator))
        {
            animator.SetBool("slide", false);
            animator.CrossFadeInFixedTime("Running", runTransitionDuration, 0);
        }

        isSliding = false;
    }

    void OnCollisionStay(Collision collision)
    {
        if (isDead) return;

        if (IsGroundCollider(collision.collider))
        {
            isGrounded = true;
            SetAnimatorGrounded(true);
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (isDead) return;

        if (IsGroundCollider(collision.collider))
        {
            if (!CheckGrounded())
                isGrounded = false;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isDead) return;

        if (HasTagInParents(collision.transform, "Obstacle"))
        {
            SnapToGround(true);
            TriggerObstacleDeath();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isDead) return;

        if (HasTagInParents(other.transform, "Obstacle"))
        {
            SnapToGround(true);
            TriggerObstacleDeath();
            return;
        }

        CoinScript coin = other.GetComponent<CoinScript>();
        if (coin == null)
        {
            coin = other.GetComponentInChildren<CoinScript>();
        }

        if (coin != null)
        {
            coin.CollectCoin();
            return;
        }

        if (other.CompareTag("Coin"))
        {
            coin = other.GetComponentInParent<CoinScript>();
            if (coin != null)
            {
                coin.CollectCoin();
            }
        }
    }

    IEnumerator CollapseSequence()
    {
        if (isDead)
            yield break;

        isDead = true;
        finalDeathPoseLocked = false;

        if (impactAudio != null)
            impactAudio.Play();

        // Separate camera
        if (Camera.main != null && Camera.main.transform.IsChildOf(transform))
        {
            Camera.main.transform.SetParent(null);
        }

        // Stop movement and lock physics immediately
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = true;

        if (playerCollider != null)
            playerCollider.enabled = false;

        if (flatBottomCollider != null)
            flatBottomCollider.enabled = false;

        // Ignore obstacle collisions
        gameObject.layer = 2;

        rb.constraints =
            RigidbodyConstraints.FreezePositionX |
            RigidbodyConstraints.FreezePositionZ |
            RigidbodyConstraints.FreezeRotation;

        // Play death animation
        if (animator != null && HasPlayableAnimatorController(animator))
        {
            animator.speed = 1f;
            animator.ResetTrigger("Death");
            animator.applyRootMotion = false;
            // Force the state directly so impacts while jumping or sliding cannot
            // be blocked by missing Animator transitions from those states.
            animator.CrossFadeInFixedTime("Death", 0.08f, 0);
        }

        SnapToGround(true);

        // Re-seat the rendered pose after every Animator update while the collapse plays.
        yield return StartCoroutine(GroundVisibleMeshDuringAnimation(deathAnimationDuration));
        LockFinalDeathPose("Death");
        yield return StartCoroutine(SettleVisibleMeshOnGround());

        // Final lockdown
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerGameOverSequence();
        }
    }

    public void KnockoutByEnemy()
    {
        if (isDead) return;
        StartCoroutine(EnemyKnockoutSequence());
    }

    IEnumerator EnemyKnockoutSequence()
    {
        isDead = true;
        finalDeathPoseLocked = false;

        if (impactAudio != null)
            impactAudio.Play();

        // Stop all movement immediately and lock physics
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        // Keep the original collider offset while snapping. Shrinking it moves the root down and
        // makes the visual mesh sink into the road, so simply disable it after death instead.
        if (playerCollider != null)
        {
            playerCollider.enabled = false;
        }

        if (flatBottomCollider != null)
        {
            flatBottomCollider.enabled = false;
        }

        // Ignore further obstacle collisions
        gameObject.layer = 2; // Ignore Raycast

        // Use the normal feet offset so the player root stays correctly above the road.
        SnapToGround(true);

        // Keep character fixed on current lane
        rb.constraints = RigidbodyConstraints.FreezePositionX |
                         RigidbodyConstraints.FreezePositionZ |
                         RigidbodyConstraints.FreezeRotation;

        // Trigger forward death animation
        if (animator != null && HasPlayableAnimatorController(animator))
        {
            animator.speed = 1f;
            animator.applyRootMotion = false;
            animator.CrossFadeInFixedTime("forwardDeath", 0.08f, 0);
        }

        // Keep the forward-death pose grounded throughout the clip, not only after it ends.
        yield return StartCoroutine(GroundVisibleMeshDuringAnimation(forwardDeathAnimationDuration));
        LockFinalDeathPose("forwardDeath");
        yield return StartCoroutine(SettleVisibleMeshOnGround());

        // Freeze everything after animation finishes
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerGameOverSequence();
        }
    }

    public void LeftMove()
    {
        if (isDead) return;

        if (laneIndex > 0)
        {
            laneIndex--;
            targetXPosition = CalculateLaneX(laneIndex);
        }
    }

    public void RightMove()
    {
        if (isDead) return;

        if (laneIndex < 2)
        {
            laneIndex++;
            targetXPosition = CalculateLaneX(laneIndex);
        }
    }

    private bool DetectObstacleOverlap()
    {
        Collider activeCollider = flatBottomCollider != null && flatBottomCollider.enabled
            ? flatBottomCollider
            : playerCollider;

        if (activeCollider == null || !activeCollider.enabled)
            return false;

        Bounds bounds = activeCollider.bounds;
        Vector3 halfExtents = Vector3.Max(bounds.extents * 0.9f, Vector3.one * 0.05f);
        Collider[] overlaps = Physics.OverlapBox(
            bounds.center,
            halfExtents,
            transform.rotation,
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

    private void AlignVisibleMeshToGround(float contactOffset)
    {
        if (!TryGetGroundY(out float groundY))
            return;

        AlignVisibleMeshToHeight(groundY + contactOffset);
    }

    private void AlignVisibleMeshToColliderBottom()
    {
        Collider contactCollider = flatBottomCollider != null && flatBottomCollider.enabled
            ? flatBottomCollider
            : playerCollider;

        if (contactCollider == null || !contactCollider.enabled)
            return;

        AlignVisibleMeshToHeight(contactCollider.bounds.min.y);
    }

    private void AlignVisibleMeshToHeight(float targetY)
    {

        Transform meshRoot = GetVisibleMeshRoot();
        if (meshRoot == null)
            return;

        bool hasBounds = false;
        Bounds meshBounds = new Bounds();
        foreach (Renderer renderer in meshRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled)
                continue;

            string rendererName = renderer.name.ToLowerInvariant();
            if (rendererName.Contains("gun") || rendererName.Contains("weapon"))
                continue;

            if (!hasBounds)
            {
                meshBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                meshBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            return;

        float correction = targetY - meshBounds.min.y;
        meshRoot.position += Vector3.up * correction;
    }

    private void AlignVisibleMeshToHeightExact(float targetY)
    {
        Transform meshRoot = GetVisibleMeshRoot();
        if (meshRoot == null)
            return;

        bool hasVisiblePoint = false;
        float minimumWorldY = float.PositiveInfinity;
        Mesh bakedMesh = new Mesh { name = "DeathPoseGroundingMesh" };

        foreach (Renderer renderer in meshRoot.GetComponentsInChildren<Renderer>(true))
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
            meshRoot.position += Vector3.up * (targetY - minimumWorldY);
    }

    private Transform GetVisibleMeshRoot()
    {
        return characterModel != null ? characterModel : animator != null ? animator.transform : null;
    }

    private void LockFinalDeathPose(string stateName)
    {
        if (!HasPlayableAnimatorController(animator))
            return;

        // Evaluate just before the normalized-time wrap point and freeze that
        // complete pose so the controller cannot transition back to a raised pose.
        animator.Play(stateName, 0, 0.999f);
        animator.Update(0f);
        animator.speed = 0f;

        if (TryGetGroundY(out float groundY))
            AlignVisibleMeshToHeightExact(groundY + deathGroundContactOffset);

        Transform meshRoot = GetVisibleMeshRoot();
        if (meshRoot != null)
        {
            lockedDeathMeshWorldY = meshRoot.position.y;
            finalDeathPoseLocked = true;
        }
    }

    private void RestoreLockedDeathMeshHeight()
    {
        Transform meshRoot = GetVisibleMeshRoot();
        if (meshRoot == null)
            return;

        Vector3 position = meshRoot.position;
        position.y = lockedDeathMeshWorldY;
        meshRoot.position = position;
    }

    private IEnumerator SettleVisibleMeshOnGround()
    {
        float elapsed = 0f;
        while (elapsed < deathGroundSettleDuration)
        {
            // Sample after each animation update so the visual pose, not an earlier pose, is grounded.
            yield return new WaitForEndOfFrame();
            if (finalDeathPoseLocked)
                RestoreLockedDeathMeshHeight();
            else
                AlignVisibleMeshToGround(deathGroundContactOffset);
            elapsed += Time.deltaTime;
        }

        if (finalDeathPoseLocked)
            RestoreLockedDeathMeshHeight();
        else
            AlignVisibleMeshToGround(deathGroundContactOffset);
    }

    private IEnumerator GroundVisibleMeshDuringAnimation(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return new WaitForEndOfFrame();
            AlignVisibleMeshToGround(deathGroundContactOffset);
            elapsed += Time.deltaTime;
        }
    }
}
