using UnityEngine;

public class GunPickup : MonoBehaviour
{
    [SerializeField] float rotationSpeed = 100f;
    private Transform playerTransform;

    void Start()
    {
        IRunnerController player = RunnerControllerLocator.Find();
        if (player != null)
        {
            playerTransform = player.RunnerTransform;
        }
    }

    void Update()
    {
        // Spin the gun in the air
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0, Space.World);

        // Distance-based failsafe collection check (in case physics trigger is skipped at high speed)
        if (playerTransform == null)
        {
            IRunnerController player = RunnerControllerLocator.Find();
            if (player != null) playerTransform = player.RunnerTransform;
        }

        if (playerTransform != null)
        {
            Vector3 pickupPos = transform.position;
            Vector3 playerPos = playerTransform.position;
            float dx = pickupPos.x - playerPos.x;
            float dz = pickupPos.z - playerPos.z;
            float sqrDist = dx * dx + dz * dz;

            if (sqrDist < 1.2f)
            {
                Collect();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[GunPickup] OnTriggerEnter with: {other.name} | Tag: {other.tag}");
        IRunnerController player = RunnerControllerLocator.GetFrom(other);
        if (player != null)
        {
            Debug.Log($"[GunPickup] Found runner controller on: {player.RunnerTransform.name}");
        }
        else
        {
            Debug.Log($"[GunPickup] No runner controller found in parent hierarchy of {other.name}");
        }

        if (player != null || other.CompareTag("Player") || other.CompareTag("player") || other.transform.root.CompareTag("Player") || other.transform.root.CompareTag("player"))
        {
            if (player != null)
            {
                playerTransform = player.RunnerTransform;
            }
            else
            {
                playerTransform = other.transform.root;
            }
            Debug.Log($"[GunPickup] Resolved playerTransform to: {playerTransform.name}");
            Collect();
        }
    }

    void Collect()
    {
        Transform targetPlayer = playerTransform;
        if (targetPlayer == null)
        {
            IRunnerController player = RunnerControllerLocator.Find();
            if (player != null) targetPlayer = player.RunnerTransform;
        }

        if (targetPlayer != null)
        {
            var gunController = targetPlayer.GetComponent<PlayerGunController>();
            if (gunController == null)
            {
                gunController = targetPlayer.GetComponentInChildren<PlayerGunController>();
            }

            if (gunController != null)
            {
                Debug.Log($"[GunPickup] Found PlayerGunController! Equipping gun.");
                gunController.EquipGun();
            }
            else
            {
                Debug.LogWarning($"[GunPickup] PlayerGunController NOT found on {targetPlayer.name}!");
            }
        }
        else
        {
            Debug.LogWarning($"[GunPickup] Target player transform is null!");
        }
        Destroy(gameObject);
    }
}
