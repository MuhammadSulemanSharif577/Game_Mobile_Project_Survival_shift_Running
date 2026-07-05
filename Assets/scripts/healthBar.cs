using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class BottleHealthSystem : MonoBehaviour
{
    public Image waterImage;
    public AudioSource alertAudio;
    public Transform bottleTransform; // Assign the parent Bottle object here

    public float health = 1.0f;

    [Tooltip("How much health is lost per second. 0.02 = 50 seconds total duration.")]
    public float drainSpeed = 0.02f;

    [Header("Refill Settings")]
    [Tooltip("How much health is restored when a water bottle is collected (e.g., 0.25 = 25%).")]
    [SerializeField] private float refillAmount = 0.25f;
    [SerializeField] private AudioSource refillAudio; // Optional: Sound effect for refilling

    [SerializeField] private GameObject percentageBox;
    private TMP_Text percentageText;
    private bool isAlerting = false;
    private bool standardGameOverTriggered = false; // Prevents triggering game over multiple times

    void Start()
    {
        percentageText = percentageBox.GetComponent<TMP_Text>();
        standardGameOverTriggered = false;
    }

    void Update()
    {
        // Stop draining if a game over has already sequence-started
        if (standardGameOverTriggered) return;

        health -= drainSpeed * Time.deltaTime;
        health = Mathf.Max(health, 0);

        waterImage.fillAmount = health;
        int displayPercentage = Mathf.RoundToInt(health * 100);
        percentageText.text = $"{displayPercentage}%";

        if (displayPercentage <= 50 && displayPercentage > 0)
        {
            waterImage.color = Color.red;
            if (!isAlerting)
            {
                StartCoroutine(PlayAlertAnimation());
            }
        }
        else
        {
            waterImage.color = Color.blue;
        }

        if (health <= 0)
        {
            GameOver();
        }
    }

    /// <summary>
    /// Restores the bottle health by the designated refill amount, capping at 100%.
    /// Call this from your Water Bottle collection script.
    /// </summary>
    public void RefillWater()
    {
        // Don't refill if already dead
        if (health <= 0 || standardGameOverTriggered) return;

        health += refillAmount;
        health = Mathf.Min(health, 1.0f); // Keep it capped at 1.0 (100%)

        if (refillAudio != null)
        {
            refillAudio.Play();
        }

        Debug.Log($"[BottleHealthSystem] Bottle refilled by {refillAmount * 100}%. Current health: {health * 100}%");
    }

    IEnumerator PlayAlertAnimation()
    {
        isAlerting = true;

        if (alertAudio != null) alertAudio.Play();

        // Animate the entire bottle
        Vector3 originalScale = bottleTransform.localScale;

        // Pop up
        bottleTransform.localScale = originalScale * 1.15f;
        yield return new WaitForSeconds(0.1f);

        // Return to normal
        bottleTransform.localScale = originalScale;

        yield return new WaitForSeconds(0.4f);

        isAlerting = false;
    }

    void GameOver()
    {
        if (standardGameOverTriggered) return;
        standardGameOverTriggered = true;

        Debug.Log("[BottleHealthSystem] Dehydration Game Over triggered!");

        // Tell the player to safely run their crash sequence method
        PlayerMove player = FindFirstObjectByType<PlayerMove>();
        if (player != null)
        {
            // Safely fires off the clean animation sequence handler via string identifier name
            player.StartCoroutine("CollapseSequence");
        }
        else
        {
            // Fallback safety panel load if no player avatar is found in world scene
            if (GameManager.Instance != null)
            {
                GameManager.Instance.TriggerGameOverSequence();
            }
        }
    }
}