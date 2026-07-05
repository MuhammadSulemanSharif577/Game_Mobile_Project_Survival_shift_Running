using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform player;
 
    private Vector3 offset;
    private float pitch;
    private PlayerMove playerMove;
    private bool hasRecordedDeathHeight = false;
    private float lockedY;

    [Header("Intro Settings")]
    [SerializeField] private float introDuration = 3.0f;
    private float introTimer = 0f;
    private bool isIntroActive = true;
    private Transform enemy;
    private Vector3 startOffset;
    private Quaternion startRot;
 
    void Awake()
    {
        if (player != null)
        {
            playerMove = player.GetComponent<PlayerMove>();
            CalculateOffset();
        }
    }
 
    void Start()
    {
        GameObject playerObj = GameObject.Find("player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerMove = playerObj.GetComponent<PlayerMove>();
        }

        CalculateOffset();

        GameObject enemyObj = GameObject.Find("Enemy");
        if (enemyObj != null && player != null)
        {
            enemy = enemyObj.transform;
            isIntroActive = true;
            
            // Calculate startOffset relative to the player's forward vector.
            // The enemy runs 6 units behind the player.
            // The camera is placed 4 units in front of the enemy (so Z = player.position.z - 2.0f) and 1.5 units high.
            startOffset = -2.0f * player.forward + Vector3.up * 1.5f;
            
            // Calculate startRot looking towards the enemy's chest (1.0 units high)
            Vector3 enemyChestLocal = -6.0f * player.forward + Vector3.up * 1.0f;
            startRot = Quaternion.LookRotation(enemyChestLocal - startOffset);
        }
        else
        {
            isIntroActive = false;
        }
        introTimer = 0f;
    }
 
    private void CalculateOffset()
    {
        offset = transform.position - player.position;
        pitch = transform.eulerAngles.x; // Store initial pitch (looking down angle)
 
        // If the camera is placed roughly behind the player, align it perfectly on the X-axis
        if (Mathf.Abs(offset.x) < 1.0f)
        {
            offset.x = 0f;
        }
    }
 
    private float deathTransitionTimer = 0f;
    [SerializeField] private float deathTransitionDuration = 1.0f; // 1.0s smooth pan transition
    private Vector3 lockedCameraStartPos;
    private Quaternion lockedCameraStartRot;
 
    void LateUpdate()
    {
        if (player == null)
            return;
 
        if (playerMove != null && playerMove.IsDead)
        {
            if (!hasRecordedDeathHeight)
            {
                lockedCameraStartPos = transform.position;
                lockedCameraStartRot = transform.rotation;
                hasRecordedDeathHeight = true;
                deathTransitionTimer = 0f;
            }
 
            // Smoothly advance transition timer using unscaled time
            deathTransitionTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(deathTransitionTimer / deathTransitionDuration);
            // Smoothstep curve for premium, organic cinematic feel
            t = t * t * (3f - 2f * t);
 
            // 1. Target cinematic side-view position (4.0m to the left, 1.2m above player height)
            Vector3 targetOffset = new Vector3(-4.0f, 1.2f, 0f);
            Vector3 targetSidePosition = player.position + targetOffset;
 
            // 2. Direct smooth Lerp from the impact frame position to the target side-view position
            transform.position = Vector3.Lerp(lockedCameraStartPos, targetSidePosition, t);
 
            // 3. Direct smooth Slerp from the impact frame rotation to face the player's chest/hips
            Vector3 lookTarget = player.position + Vector3.up * 0.2f;
            Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
            transform.rotation = Quaternion.Slerp(lockedCameraStartRot, targetRotation, t);
        }
        else
        {
            hasRecordedDeathHeight = false;
            deathTransitionTimer = 0f;
 
            if (isIntroActive && enemy != null)
            {
                introTimer += Time.deltaTime;
                float t = Mathf.Clamp01(introTimer / introDuration);
                float smoothT = t * t * (3f - 2f * t);

                // Lerp relative offsets and rotation
                Vector3 currentOffset = Vector3.Lerp(startOffset, offset, smoothT);

                Quaternion gameplayRot = Quaternion.Euler(pitch, player.eulerAngles.y, 0f);
                Quaternion currentRot = Quaternion.Slerp(startRot, gameplayRot, smoothT);

                transform.position = player.position + currentOffset;
                transform.rotation = currentRot;

                if (t >= 1.0f)
                {
                    isIntroActive = false;
                }
            }
            else
            {
                // Standard gameplay camera tracking
                transform.position = player.position + offset;
                transform.rotation = Quaternion.Euler(pitch, player.eulerAngles.y, 0f);
            }
        }
    }
}