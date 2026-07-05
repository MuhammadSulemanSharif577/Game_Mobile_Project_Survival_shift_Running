using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class PlayerGunController : MonoBehaviour
{
    [Header("Ammo Settings")]
    [SerializeField] int maxAmmo = 10;
    private int currentAmmo = 0;
    private bool hasGun = false;

    [Header("References")]
    private GameObject gunInHand;
    private GameObject ammoPanel;
    private Transform bulletContainer;
    private GameObject bulletUIPrefab;
    private TextMeshProUGUI ammoText;
    private Button shootButton;
    private Animator animator;

    [Header("Audio Sources")]
    [SerializeField] AudioSource gunShotAudio;
    [SerializeField] AudioSource explosionAudio;

    public bool HasGun => hasGun;

    void Start()
    {
        animator = GetComponent<Animator>();

        // Find the attached gun in hand
        Transform hips = transform.Find("Ch31_nonPBR@Running/mixamorig9:Hips");
        if (hips != null)
        {
            Transform rHand = FindRecursive(hips, "mixamorig9:RightHand");
            if (rHand != null)
            {
                Transform gunTrans = rHand.Find("GunInHand");
                if (gunTrans != null)
                {
                    gunInHand = gunTrans.gameObject;
                }
            }
        }

        // Find UI elements recursively in Canvas (so we find them even if inactive at startup)
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "GameOverBulletsPanel")
                {
                    ammoPanel = t.gameObject;
                }
                else if (t.name == "shooting")
                {
                    shootButton = t.GetComponent<Button>();
                }
            }
        }

        if (ammoPanel != null)
        {
            Transform bulletsPanel = ammoPanel.transform.Find("BulletsPanel");
            if (bulletsPanel != null)
            {
                Transform container = bulletsPanel.Find("BulletContainer");
                if (container != null)
                {
                    bulletContainer = container;
                    if (container.childCount > 0)
                    {
                        bulletUIPrefab = container.GetChild(0).gameObject;
                    }
                }

                Transform countTrans = bulletsPanel.Find("AmmoCount");
                if (countTrans != null)
                {
                    ammoText = countTrans.GetComponent<TextMeshProUGUI>();
                }
            }

            // Hide the panel initially
            ammoPanel.SetActive(false);
        }

        if (shootButton != null)
        {
            shootButton.onClick.RemoveAllListeners();
            shootButton.onClick.AddListener(Shoot);
            shootButton.gameObject.SetActive(false);
        }

        // Add default AudioSources if not set
        if (gunShotAudio == null)
        {
            gunShotAudio = gameObject.AddComponent<AudioSource>();
            gunShotAudio.playOnAwake = false;
        }
        if (explosionAudio == null)
        {
            explosionAudio = gameObject.AddComponent<AudioSource>();
            explosionAudio.playOnAwake = false;
        }

        if (gunInHand != null)
        {
            gunInHand.SetActive(false);
        }
    }

    void Update()
    {
        if (!hasGun) return;

        // Support desktop firing with Space or Left Mouse Click (only if not clicking on UI buttons)
        bool clickedUI = UnityEngine.EventSystems.EventSystem.current != null && 
                          UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        if (Input.GetKeyDown(KeyCode.Space) || (Input.GetMouseButtonDown(0) && !clickedUI))
        {
            Shoot();
        }
    }

    public void EquipGun()
    {
        hasGun = true;
        currentAmmo = maxAmmo;

        if (gunInHand != null)
        {
            gunInHand.SetActive(true);
        }

        if (ammoPanel != null)
        {
            ammoPanel.SetActive(true);
        }

        if (shootButton != null)
        {
            shootButton.gameObject.SetActive(true);
        }

        if (animator != null)
        {
            animator.SetBool("hasGun", true);
        }

        UpdateAmmoUI();
    }

    public void Shoot()
    {
        if (!hasGun || currentAmmo <= 0) return;

        // Play gunshot sound
        if (gunShotAudio != null && gunShotAudio.clip != null)
        {
            gunShotAudio.Play();
        }
        else if (gunShotAudio != null)
        {
            // Fallback play click/beep so we can hear it works
            gunShotAudio.PlayOneShot(SystemInfo.supportsVibration ? null : null); 
        }

        // Cast raycast forward from player chest level (1.0m height)
        Ray ray = new Ray(transform.position + Vector3.up * 1.0f, transform.forward);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 35.0f))
        {
            GameObject hitObj = hit.collider.gameObject;
            // Detect obstacles
            if (hitObj.CompareTag("Obstacle") || 
                hitObj.name.ToLower().Contains("cactus") || 
                hitObj.name.ToLower().Contains("rock") || 
                hitObj.name.ToLower().Contains("bottle") || 
                hitObj.name.ToLower().Contains("bone") ||
                hitObj.name.ToLower().Contains("flat_bone") ||
                hitObj.name.ToLower().Contains("mixed_bone"))
            {
                // Play explosion sound
                if (explosionAudio != null && explosionAudio.clip != null)
                {
                    explosionAudio.Play();
                }

                // Spawn destruction physics debris
                SpawnDestructionEffect(hit.point, hitObj);

                // Destroy obstacle
                Destroy(hitObj);
            }
        }

        currentAmmo--;
        UpdateAmmoUI();

        if (currentAmmo <= 0)
        {
            Disarm();
        }
    }

    void Disarm()
    {
        hasGun = false;

        if (gunInHand != null)
        {
            gunInHand.SetActive(false);
        }

        if (ammoPanel != null)
        {
            ammoPanel.SetActive(false);
        }

        if (shootButton != null)
        {
            shootButton.gameObject.SetActive(false);
        }

        if (animator != null)
        {
            animator.SetBool("hasGun", false);
        }
    }

    void UpdateAmmoUI()
    {
        if (ammoText != null)
        {
            ammoText.text = $"{currentAmmo} / {maxAmmo}";
        }

        if (bulletContainer != null && bulletUIPrefab != null)
        {
            // Sync bullet count UI elements in vertical stack
            int count = bulletContainer.childCount;
            for (int i = 0; i < count; i++)
            {
                bulletContainer.GetChild(i).gameObject.SetActive(i < currentAmmo);
            }

            while (bulletContainer.childCount < currentAmmo)
            {
                GameObject bulletIcon = Instantiate(bulletUIPrefab, bulletContainer);
                bulletIcon.SetActive(true);
            }
        }
    }

    void SpawnDestructionEffect(Vector3 hitPoint, GameObject obstacle)
    {
        // Color-matched low-poly dust puff
        GameObject dust = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dust.transform.position = hitPoint;
        dust.transform.localScale = Vector3.one * 0.7f;
        var dustRb = dust.AddComponent<Rigidbody>();
        dustRb.linearVelocity = Vector3.up * 2.5f + Random.insideUnitSphere * 1.5f;
        dust.GetComponent<Collider>().enabled = false;
        
        Renderer obsRenderer = obstacle.GetComponentInChildren<Renderer>();
        if (obsRenderer != null && obsRenderer.sharedMaterial != null)
        {
            dust.GetComponent<Renderer>().material = obsRenderer.sharedMaterial;
        }
        Destroy(dust, 0.5f);

        // Spawn flying physics fragments
        for (int i = 0; i < 7; i++)
        {
            GameObject debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.transform.position = hitPoint + Random.insideUnitSphere * 0.25f;
            debris.transform.localScale = Vector3.one * Random.Range(0.12f, 0.32f);
            debris.transform.rotation = Random.rotation;
            
            var rb = debris.AddComponent<Rigidbody>();
            rb.linearVelocity = Vector3.up * Random.Range(3.5f, 6f) + Random.onUnitSphere * Random.Range(2f, 4.5f);
            rb.angularVelocity = Random.onUnitSphere * 400f;
            debris.GetComponent<Collider>().enabled = false;
            
            if (obsRenderer != null && obsRenderer.sharedMaterial != null)
            {
                debris.GetComponent<Renderer>().material = obsRenderer.sharedMaterial;
            }
            
            Destroy(debris, Random.Range(0.7f, 1.1f));
        }
    }

    Transform FindRecursive(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform found = FindRecursive(t.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
