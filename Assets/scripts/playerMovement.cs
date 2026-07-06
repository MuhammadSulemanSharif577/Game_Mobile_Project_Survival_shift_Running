using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)]
public class PlayerMove : MonoBehaviour
{
    [Header("Track Alignment")]
    [SerializeField] float trackCenterXOffset = -15.0f;
    [SerializeField] float laneDistance = 0.2f;

    [Header("Movement")]
    [SerializeField] int laneIndex = 1;
    [SerializeField] float sideSpeed = 10f;
    [Range(1f, 10f)][SerializeField] float laneChangeSmoothness = 5f;
    public float forwardSpeed = 5f;

    public float TrackCenterXOffset => trackCenterXOffset;
    public float LaneDistance => laneDistance;
    public bool IsDead => isDead;

    [Header("Animation")]
    [SerializeField] Animator animator;

    [Header("Jump & Slide")]
    [SerializeField] float jumpForce = 5f;
    [SerializeField] bool isGrounded = true;
    [SerializeField] bool isSliding = false;
    [SerializeField] CapsuleCollider playerCollider;
    [SerializeField] float slideDuration = 1.6f;

    [Header("Ground Check")]
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float groundCheckDistance = 0.35f;
    [SerializeField] float groundSnapOffset = 0.02f;
    [SerializeField] float groundRaycastHeight = 5f;
    [SerializeField] float groundRaycastDistance = 14f;
    [SerializeField] Transform characterModel;

    private Vector3 originalCharacterLocalPos;
    [SerializeField] float deathMeshYOffset = -0.5f;
    [Header("Obstacle & Game Over Settings")]
    [SerializeField] private AudioSource impactAudio;
    private bool isDead = false;

    private float originalHeight;
    private Vector3 originalCenter;
    private Rigidbody rb;
    private float targetXPosition;
    private float feetOffsetFromRoot;
    private float groundedRootY;
    private float originalForwardSpeed;
    private BottleHealthSystem healthSystem;

    void Start()
    {
        Application.runInBackground = true;
        isDead = false;
        isSliding = false;
        rb = GetComponent<Rigidbody>();
        originalForwardSpeed = forwardSpeed;
        healthSystem = FindAnyObjectByType<BottleHealthSystem>();

        ResolveAnimatorReference();

        if (playerCollider == null)
            playerCollider = GetComponent<CapsuleCollider>();

        if (playerCollider != null)
            playerCollider.enabled = true;

        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (animator != null)
            animator.updateMode = AnimatorUpdateMode.Normal;

        // Auto-align the mesh to the physics root and fit the capsule to it.
        AlignMeshAndCollider();

        originalHeight = playerCollider.height;
        originalCenter = playerCollider.center;

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

    }

    void FixedUpdate()
    {
        // If the player is dead, skip all updates including ground checks and snapping
        if (isDead) return;

        isGrounded = CheckGrounded();

        if (isGrounded && !isSliding)
        {
            SnapToGround(false);
        }

        // Reduce speed dynamically when health is <= 30%
        if (healthSystem != null && healthSystem.health <= 0.3f)
        {
            forwardSpeed = Mathf.Lerp(originalForwardSpeed, 2.0f, (0.3f - healthSystem.health) / 0.3f);
        }
        else if (healthSystem != null && healthSystem.health > 0.3f)
        {
            forwardSpeed = originalForwardSpeed;
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

    static bool HasPlayableAnimatorController(Animator candidate)
    {
        return candidate != null && candidate.runtimeAnimatorController != null;
    }

    void LateUpdate()
    {
        // Suppress update overrides entirely when dead so our dedicated death drop works properly
        if (isDead) return;

        if (animator != null && animator.transform != transform)
        {
            Vector3 localPos = animator.transform.localPosition;
            animator.transform.localPosition = new Vector3(0f, localPos.y, 0f);
        }
    }

    bool CheckGrounded()
    {
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

    void SnapToGround(bool force)
    {
        if (!TryGetGroundY(out float groundY))
            return;

        float targetRootY = groundY + feetOffsetFromRoot + groundSnapOffset;

        if (!force && Mathf.Abs(rb.position.y - targetRootY) < 0.005f)
            return;

        Vector3 pos = rb.position;
        pos.y = targetRootY;

        if (force)
            rb.position = pos;
        else
            rb.MovePosition(pos);

        groundedRootY = targetRootY;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, isDead ? rb.linearVelocity.y : 0f, rb.linearVelocity.z);
    }

    float CalculateLaneX(int lane)
    {
        return trackCenterXOffset + ((lane - 1) * laneDistance);
    }

    public void Jump()
    {
        if (isDead) return;

        if (isGrounded && !isSliding)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;

            if (HasPlayableAnimatorController(animator))
            {
                animator.SetTrigger("jump");
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
            animator.SetBool("slide", true);
        }

        SnapToGround(true);

        float targetHeight = originalHeight / 2f;
        float transitionTime = 0.15f;

        float timer = 0f;
        while (timer < transitionTime)
        {
            timer += Time.fixedDeltaTime;
            float t = timer / transitionTime;
            playerCollider.height = Mathf.Lerp(originalHeight, targetHeight, t);
            playerCollider.center = new Vector3(0f, playerCollider.height * 0.5f, 0f);

            yield return new WaitForFixedUpdate();
        }

        playerCollider.height = targetHeight;
        playerCollider.center = new Vector3(0f, targetHeight * 0.5f, 0f);

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

        SnapToGround(true);

        if (HasPlayableAnimatorController(animator))
        {
            animator.SetBool("slide", false);
        }

        isSliding = false;
    }

    void OnCollisionStay(Collision collision)
    {
        if (isDead) return;

        if (IsGroundCollider(collision.collider))
        {
            isGrounded = true;
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

        if (collision.gameObject.CompareTag("Obstacle"))
        {
            SnapToGround(true);
            StartCoroutine(CollapseSequence());
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isDead) return;

        if (other.CompareTag("Obstacle"))
        {
            SnapToGround(true);
            StartCoroutine(CollapseSequence());
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

        if (impactAudio != null)
            impactAudio.Play();

        // Separate camera
        if (Camera.main != null && Camera.main.transform.IsChildOf(transform))
        {
            Camera.main.transform.SetParent(null);
        }

        // Stop movement and lock physics immediately
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        // Ignore obstacle collisions
        gameObject.layer = 2;

        rb.constraints =
            RigidbodyConstraints.FreezePositionX |
            RigidbodyConstraints.FreezePositionZ |
            RigidbodyConstraints.FreezeRotation;

        // Play death animation
        if (animator != null && HasPlayableAnimatorController(animator))
        {
            animator.ResetTrigger("Death");
            animator.applyRootMotion = false;
            animator.SetTrigger("Death");
        }

        yield return null;

        // Force capsule onto ground
        SnapToGround(true);

        // Extra safety snap
        if (TryGetGroundY(out float absoluteGroundY))
        {
            Vector3 pos = rb.position;
            pos.y = absoluteGroundY + feetOffsetFromRoot + groundSnapOffset;
            rb.position = pos;
        }

        // Move the visible character model down
        if (characterModel != null)
        {
            Vector3 startPos = characterModel.localPosition;

            Vector3 targetPos = startPos;
            targetPos.y = -0.8f;
            targetPos.z = -0.8f;

            float duration = 0.25f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                characterModel.localPosition =
                    Vector3.Lerp(startPos, targetPos, elapsed / duration);

                yield return null;
            }

            characterModel.localPosition = targetPos;
        }

        yield return new WaitForSeconds(1.0f);

        // Final lockdown
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
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

        if (impactAudio != null)
            impactAudio.Play();

        // Stop all movement immediately and lock physics
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        // Reduce capsule size so player lies flat on the ground
        if (playerCollider != null)
        {
            playerCollider.height = 0.2f;
            playerCollider.center = new Vector3(0f, 0.1f, 0f);
        }

        // Ignore further obstacle collisions
        gameObject.layer = 2; // Ignore Raycast

        // Recalculate feet offset and snap to ground
        feetOffsetFromRoot = CalculateFeetOffsetFromRoot();
        SnapToGround(true);

        // Keep character fixed on current lane
        rb.constraints = RigidbodyConstraints.FreezePositionX |
                         RigidbodyConstraints.FreezePositionZ |
                         RigidbodyConstraints.FreezeRotation;

        // Trigger forward death animation
        if (animator != null && HasPlayableAnimatorController(animator))
        {
            animator.applyRootMotion = true;
            animator.SetTrigger("forwardDeath");
        }

        // Move the visible character model down to touch the ground
        if (characterModel != null)
        {
            Vector3 startPos = characterModel.localPosition;
            Vector3 targetPos = startPos;
            targetPos.y = -0.8f;
            targetPos.z = -0.8f;
            float duration = 0.25f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                characterModel.localPosition = Vector3.Lerp(startPos, targetPos, elapsed / duration);
                yield return null;
            }
            characterModel.localPosition = targetPos;
        }

        // Wait for animation to play
        yield return new WaitForSeconds(2.0f);

        // Freeze everything after animation finishes
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
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
}