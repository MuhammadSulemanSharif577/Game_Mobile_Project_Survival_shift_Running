using UnityEngine;
using System.Collections;

public class CoinScript : MonoBehaviour
{
    [SerializeField] bool collectedCoin;
    private AudioSource coinSound;

    [Header("Rotation Settings")]
    [SerializeField] float rotationSpeed = 90f; // Degrees per second

    [Header("Collection Animation Settings")]
    [SerializeField] float floatUpSpeed = 6f;
    [SerializeField] float shrinkSpeed = 2.5f;

    private Vector3 targetScale = Vector3.zero;
    private Vector3 initialScale;
    private Transform playerTransform;

    public void CollectCoin()
    {
        if (collectedCoin) return; // Prevent double collection

        collectedCoin = true;

        scoreController.AddCoin();

        // Disable the Animator so it releases control of transform properties,
        // allowing this script to execute the float-up and shrink animation.
        Animator anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.enabled = false;

        if (coinSound != null) coinSound.Play();

        Debug.Log($"[CoinScript] CollectCoin() called on {gameObject.name} at pos: {transform.position}");

        StartCoroutine(DeleteCoin());
    }

    void Start()
    {
        initialScale = transform.localScale;

        GameObject coinAudioObject = GameObject.Find("coin");

        if (coinAudioObject != null)
        {
            coinSound = coinAudioObject.GetComponent<AudioSource>();

            if (coinSound == null)
            {
                Debug.LogWarning(
                    "[CoinScript] The 'coin' object exists but has no AudioSource."
                );
            }
        }
        else
        {
            Debug.LogWarning(
                "[CoinScript] Scene audio object named 'coin' was not found."
            );
        }

        IRunnerController player = RunnerControllerLocator.Find();

        if (player != null)
        {
            playerTransform = player.RunnerTransform;
        }
    }

    void Update()
    {
        if (!collectedCoin)
        {
            // ROTATION: Always spin the coin via script.
            // The Animator is disabled at spawn time (it has no animation states),
            // so we always handle rotation here.
            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0, Space.World);

            // FAIL-SAFE: Distance-based collection backup.
            // This catches cases where the physics trigger doesn't fire.
            if (playerTransform == null)
            {
                IRunnerController player = RunnerControllerLocator.Find();
                if (player != null) playerTransform = player.RunnerTransform;
            }

            if (playerTransform != null)
            {
                // Use XZ distance (horizontal only) so the Y height difference
                // between coin (waist height) and player root doesn't inflate the distance.
                Vector3 coinPos = transform.position;
                Vector3 playerPos = playerTransform.position;
                float dx = coinPos.x - playerPos.x;
                float dz = coinPos.z - playerPos.z;
                float sqrDist = dx * dx + dz * dz;

                // 1.0f squared is 1.0f. Using squared distance avoids expensive Mathf.Sqrt.
                if (sqrDist < 1.0f)
                {
                    Debug.Log($"[CoinScript] {gameObject.name} distance failsafe triggered! sqrDist: {sqrDist:F3}");
                    CollectCoin();
                }
            }
        }
        else
        {
            // COLLECTION ANIMATION: Swoop towards the top-right of the viewport (UI Coin Score Panel)
            if (Camera.main != null)
            {
                // Viewport coordinates: x=0.8, y=0.85 matches the top-right HUD area, 2.0m depth
                Vector3 targetWorldPos = Camera.main.ViewportToWorldPoint(new Vector3(0.8f, 0.85f, 2.0f));
                float distance = Vector3.Distance(transform.position, targetWorldPos);
                float speed = Mathf.Max(floatUpSpeed, distance * 8f);
                transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, speed * Time.deltaTime);
            }
            else
            {
                transform.Translate(Vector3.up * floatUpSpeed * Time.deltaTime, Space.World);
            }

            // Shrink over time
            transform.localScale = Vector3.MoveTowards(transform.localScale, targetScale, Time.deltaTime * shrinkSpeed);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[CoinScript] {gameObject.name} OnTriggerEnter with: {other.gameObject.name} | Tag: {other.tag}");

        // The player is tagged "Player" (built-in, capital P) in the scene.
        // Also check by component as a fail-safe.
        if (other.CompareTag("Player") || other.CompareTag("player") || RunnerControllerLocator.GetFrom(other) != null)
        {
            Debug.Log($"[CoinScript] Trigger match! Collecting.");
            CollectCoin();
        }
    }

    IEnumerator DeleteCoin()
    {
        yield return new WaitForSeconds(0.7f);
        this.gameObject.SetActive(false);
    }
}
