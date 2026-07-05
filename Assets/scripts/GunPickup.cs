using UnityEngine;

public class GunPickup : MonoBehaviour
{
    [SerializeField] float rotationSpeed = 100f;
    private Transform playerTransform;

    void Start()
    {
        PlayerMove player = FindFirstObjectByType<PlayerMove>();
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    void Update()
    {
        // Spin the gun in the air
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0, Space.World);

        // Distance-based failsafe collection check (in case physics trigger is skipped at high speed)
        if (playerTransform == null)
        {
            PlayerMove player = FindFirstObjectByType<PlayerMove>();
            if (player != null) playerTransform = player.transform;
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
        if (other.CompareTag("Player") || other.CompareTag("player") || other.GetComponent<PlayerMove>() != null)
        {
            Collect();
        }
    }

    void Collect()
    {
        if (playerTransform != null)
        {
            var gunController = playerTransform.GetComponent<PlayerGunController>();
            if (gunController != null)
            {
                gunController.EquipGun();
            }
        }
        Destroy(gameObject);
    }
}
