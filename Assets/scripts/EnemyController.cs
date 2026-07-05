using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private Transform player;
    private Animator animator;
    private BottleHealthSystem healthSystem;
    private PlayerMove playerMove;
    private CapsuleCollider enemyCollider;
    private float originalHeight;
    private Vector3 originalCenter;

    [Header("Follow Settings")]
    [SerializeField] float followDistance = 6.0f;
    [SerializeField] float punchRadius = 1.3f;
    [SerializeField] float catchUpSpeed = 1.5f; // rate at which followDistance decreases when health <= 30%

    private float currentFollowDistance;
    private bool hasPunched = false;

    void Start()
    {
        GameObject playerObj = GameObject.Find("player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerMove = playerObj.GetComponent<PlayerMove>();
        }
        animator = GetComponent<Animator>();
        healthSystem = FindAnyObjectByType<BottleHealthSystem>();
        
        enemyCollider = GetComponent<CapsuleCollider>();
        if (enemyCollider != null)
        {
            originalHeight = enemyCollider.height;
            originalCenter = enemyCollider.center;
        }

        currentFollowDistance = followDistance;
        hasPunched = false;
    }

    void Update()
    {
        if (player == null || playerMove == null) return;

        if (playerMove.IsDead)
        {
            // If the player died but the enemy did not punch them (e.g. hit an obstacle), stop running
            if (!hasPunched && animator != null)
            {
                animator.speed = 0f;
            }
            return;
        }

        // Determine if player health is <= 30%
        if (healthSystem != null && healthSystem.health <= 0.3f)
        {
            // Slowly decrease followDistance to punchRadius
            currentFollowDistance = Mathf.MoveTowards(currentFollowDistance, punchRadius, catchUpSpeed * Time.deltaTime);
        }

        // Align position behind the player (matching player lane X and following in Z)
        Vector3 targetPosition = new Vector3(
            player.position.x,
            player.position.y,
            player.position.z - currentFollowDistance
        );

        transform.position = targetPosition;
        transform.rotation = player.rotation;

        // Check if we are in punch radius and haven't punched yet
        if (!hasPunched && currentFollowDistance <= punchRadius + 0.05f)
        {
            hasPunched = true;
            TriggerPunchSequence();
        }
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
}
