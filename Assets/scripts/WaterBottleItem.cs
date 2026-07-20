using UnityEngine;
using System.Collections;

public class WaterBottleItem : MonoBehaviour
{
    [Header("Collection Settings")]
    [SerializeField] protected bool collected = false;
    [SerializeField] protected AudioSource collectSound;

    [Header("Rotation Settings")]
    [SerializeField] protected float rotationSpeed = 90f;

    [Header("Light-Up Flash Effect")]
    [Tooltip("Assign a glowing prefab, a Unity Light, or a bright particle system here.")]
    [SerializeField] protected GameObject lightEffectPrefab;
    [SerializeField] protected float effectDuration = 0.5f;
    [SerializeField] protected float maxEffectScale = 2.0f;

    protected Transform playerTransform;
    protected Vector3 initialScale;

    [Header("Model Scaling")]
    [SerializeField] protected float targetScaleFactor = 0.18f; // Scale factor to resize the large bottle model

    protected virtual void Start()
    {
        // Scale down the large bottle model to a realistic size
        transform.localScale = new Vector3(targetScaleFactor, targetScaleFactor, targetScaleFactor);
        initialScale = transform.localScale;

        // Ensure there is a trigger collider for native Unity trigger detection
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
        }
        col.isTrigger = true;

        // Cache player for a distance fail-safe (similar to your Coin script)
        IRunnerController player = RunnerControllerLocator.Find();
        if (player != null) playerTransform = player.RunnerTransform;
    }

    protected virtual void Update()
    {
        if (!collected)
        {
            // Idle rotation while waiting to be picked up
            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0, Space.World);

            // Fail-safe distance pickup (using XZ horizontal plane)
            if (playerTransform != null)
            {
                Vector3 bottlePos = transform.position;
                Vector3 playerPos = playerTransform.position;
                float dx = bottlePos.x - playerPos.x;
                float dz = bottlePos.z - playerPos.z;
                float sqrDist = dx * dx + dz * dz;

                // 1.0f squared is 1.0f. Using squared distance avoids expensive Mathf.Sqrt.
                if (sqrDist < 1.0f)
                {
                    CollectBottle();
                }
            }
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (collected) return;

        // Matches both Unity's standard and lowercase tag formats, or checks component
        if (other.CompareTag("Player") || other.CompareTag("player") || RunnerControllerLocator.GetFrom(other) != null)
        {
            CollectBottle();
        }
    }

    protected virtual void CollectBottle()
    {
        collected = true;

        // Find the UI/Health manager and trigger the refill logic
        BottleHealthSystem healthSystem = FindFirstObjectByType<BottleHealthSystem>();
        if (healthSystem != null)
        {
            healthSystem.RefillWater();
        }
        else
        {
            Debug.LogWarning("[WaterBottleItem] Could not find BottleHealthSystem in the scene!");
        }

        GameObject DesertBottleSoundObject = GameObject.Find("BottleCollectSound");
        if(DesertBottleSoundObject != null)
        {
            collectSound = DesertBottleSoundObject.GetComponent<AudioSource>();
        }

        if (collectSound != null)
        {
            collectSound.Play();
        }

        // Start the visual flash sequence
        StartCoroutine(FlashAndDestroySequence());
    }

    protected virtual IEnumerator FlashAndDestroySequence()
    {
        // 1. Hide the bottle's main 3D model immediately so it looks "picked up"
        MeshRenderer mesh = GetComponent<MeshRenderer>();
        if (mesh == null) mesh = GetComponentInChildren<MeshRenderer>();
        if (mesh != null) mesh.enabled = false;

        // Also disable collider to stop double triggers
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 2. Spawn and animate the Light-Up effect
        if (lightEffectPrefab != null)
        {
            // Instantiate the burst effect at the bottle's current position
            GameObject effectInstance = Instantiate(lightEffectPrefab, transform.position, Quaternion.identity);
            effectInstance.transform.localScale = Vector3.zero;

            float elapsedTime = 0f;
            while (elapsedTime < effectDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / effectDuration;

                // Expand the light/aura outward quickly
                float currentScale = Mathf.Lerp(0f, maxEffectScale, progress);
                effectInstance.transform.localScale = new Vector3(currentScale, currentScale, currentScale);

                // If the effect object contains a Unity Light component, make it flash brighter then dim down
                Light lightComp = effectInstance.GetComponent<Light>();
                if (lightComp == null) lightComp = effectInstance.GetComponentInChildren<Light>();
                if (lightComp != null)
                {
                    // Intensity peaks halfway through, then fades
                    lightComp.intensity = Mathf.Sin(progress * Mathf.PI) * 5f;
                }

                yield return null;
            }

            // Clean up the instantiated effect object
            Destroy(effectInstance);
        }

        // 3. Deactivate the primary pickup object
        gameObject.SetActive(false);
    }
}
