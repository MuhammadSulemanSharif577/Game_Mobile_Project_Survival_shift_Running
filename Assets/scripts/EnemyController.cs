using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private Transform player;
    private Animator animator;
    private BottleHealthSystem healthSystem;
    private IRunnerController playerMove;
    private CapsuleCollider enemyCollider;
    private float originalHeight;
    private Vector3 originalCenter;
    [SerializeField] AudioSource MonsterRoar;
    [Header("Follow Settings")]
    [SerializeField] float followDistance = 6.0f;
    [SerializeField] float punchRadius = 1.3f;
    [SerializeField] float catchUpSpeed = 1.5f; // rate at which followDistance decreases when health <= 30%

    private float currentFollowDistance;
    private bool hasPunched = false;

    void Start()
    {
        if (MonsterRoar == null)
        {
            GameObject roarObject = GameObject.Find("MonsterRoar");
            if (roarObject != null)
                MonsterRoar = roarObject.GetComponent<AudioSource>();
        }

        GameObject playerObj = GameObject.Find("player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerMove = RunnerControllerLocator.GetFrom(playerObj.transform);
        }

        // The roar is event feedback for the low-health chase, not a scene-start sound.
        if (MonsterRoar != null)
        {
            MonsterRoar.playOnAwake = false;
            MonsterRoar.loop = false;
            MonsterRoar.Stop();
        }

        animator = GetComponent<Animator>();
        if (animator != null)
            animator.applyRootMotion = false;

        healthSystem = FindAnyObjectByType<BottleHealthSystem>();

        enemyCollider = GetComponent<CapsuleCollider>();
        if (enemyCollider != null)
        {
            originalHeight = enemyCollider.height;
            originalCenter = enemyCollider.center;
        }

        currentFollowDistance = followDistance;
        hasPunched = false;
        SnapBehindPlayer();
    }

    void Update()
    {
        if (player == null || playerMove == null) return;

        if (playerMove.IsDead)
        {
            // If the player died but the enemy did not punch them (e.g. hit an obstacle), stop running
            if (!hasPunched && animator != null)
                animator.speed = 0f;
            return;
        }

        // Determine if player health is <= 25%.
        if (healthSystem != null && healthSystem.health <= 0.25f)
        {
            if (currentFollowDistance < followDistance &&
                MonsterRoar != null &&
                !MonsterRoar.isPlaying)
            {
                MonsterRoar.Play();
            }

            currentFollowDistance = Mathf.MoveTowards(
                currentFollowDistance,
                punchRadius,
                catchUpSpeed * Time.deltaTime);
        }

        // Match the active lane and keep the enemy at an exact distance behind.
        SnapBehindPlayer();

        if (!hasPunched && currentFollowDistance <= punchRadius + 0.05f)
        {
            hasPunched = true;
            TriggerPunchSequence();
        }
    }

    private void SnapBehindPlayer()
    {
        if (player == null)
            return;

        transform.position = new Vector3(
            player.position.x,
            player.position.y,
            player.position.z - currentFollowDistance);
        transform.rotation = player.rotation;
    }

    void TriggerPunchSequence()
    {
        if (animator != null)
        {
            animator.SetTrigger("punch");
        }

        // Reduce enemy capsule collider so the model can touch the ground and punch properly
        if (enemyCollider != null)
        {
            enemyCollider.height = 0.2f;
            enemyCollider.center = new Vector3(0f, 0.1f, 0f);
        }

        // Wait a split second for the punch to connect, then knock out the player
        Invoke("KnockoutPlayer", 0.5f);
    }

    void KnockoutPlayer()
    {
        if (playerMove != null)
        {
            playerMove.KnockoutByEnemy();
        }
    }

    public void RestoreFollowDistanceAfterRefill()
    {
        // A collected bottle gives the player another chance. Cancel a punch that
        // has not connected yet and restore the enemy's original chase spacing.
        CancelInvoke(nameof(KnockoutPlayer));
        currentFollowDistance = followDistance;
        hasPunched = false;

        if (enemyCollider != null)
        {
            enemyCollider.height = originalHeight;
            enemyCollider.center = originalCenter;
        }

        if (animator != null)
        {
            animator.speed = 1f;
            animator.ResetTrigger("punch");
        }

        if (MonsterRoar != null && MonsterRoar.isPlaying)
            MonsterRoar.Stop();

        if (player != null)
            SnapBehindPlayer();
    }
}
