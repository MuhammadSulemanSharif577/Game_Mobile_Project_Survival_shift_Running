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
    private bool isAiming = false;

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
    public bool IsAiming => isAiming;

    public void HideWeaponUI()
    {
        SetAiming(false);

        if (ammoPanel != null)
            ammoPanel.SetActive(false);

        if (shootButton != null)
            shootButton.gameObject.SetActive(false);
    }

    void Awake()
    {
        if (gunShotAudio != null)
        {
            gunShotAudio.playOnAwake = false;
            gunShotAudio.Stop();
        }
        if (explosionAudio != null)
        {
            explosionAudio.playOnAwake = false;
            explosionAudio.Stop();
        }
    }

    void Start()
    {
        animator = GetComponent<Animator>();

        // Find the attached gun in hand
        Transform hips = FindRecursive(transform, "mixamorig9:Hips");
        if (hips != null)
        {
            Transform rHand = FindRecursive(hips, "mixamorig9:RightHand");
            if (rHand != null)
            {
                Transform gunTrans = rHand.Find("GunInHand");
                if (gunTrans != null)
                {
                    gunInHand = gunTrans.gameObject;
                    gunInHand.SetActive(false); // Hide the gun model initially
                }
            }
        }

        if (animator != null)
        {
            animator.SetBool("hasGun", false);
            animator.SetBool("isAiming", false);
        }

        // Find UI elements recursively in Canvas (so we find them even if inactive at startup)
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            Debug.Log($"[PlayerGunController] Found Canvas: {canvas.name}");
            foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "GameOverBulletsPanel" || t.name == "CyberBulletsPanel" || t.name == "OnGameCyberBulletsPanel")
                {
                    ammoPanel = t.gameObject;
                    Debug.Log($"[PlayerGunController] Found ammoPanel: {t.name}");
                    break; // Prioritize the full bullets panel hierarchy
                }
            }

            if (ammoPanel == null)
            {
                // Fallback to the single image sidebar if the full hierarchy is not found
                foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Bullets" || t.name == "CyberBullets")
                    {
                        ammoPanel = t.gameObject;
                        Debug.Log($"[PlayerGunController] Found fallback ammoPanel: {t.name}");
                        break;
                    }
                }
            }

            foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "shooting" || t.name == "CyberShooting")
                {
                    shootButton = t.GetComponent<Button>();
                    Debug.Log($"[PlayerGunController] Found shootButton: {t.name} (has Button component: {shootButton != null})");
                    break;
                }
            }
        }
        else
        {
            Debug.LogWarning("[PlayerGunController] Canvas NOT found in scene!");
        }

        if (ammoPanel != null)
        {
            Transform bulletsPanel = ammoPanel.transform.Find("BulletsPanel");
            if (bulletsPanel == null)
            {
                bulletsPanel = ammoPanel.transform.Find("BulletPanel");
            }
            if (bulletsPanel == null)
            {
                bulletsPanel = ammoPanel.transform.Find("CyberBulletPanel");
            }
            Debug.Log($"[PlayerGunController] Resolved bulletsPanel: {(bulletsPanel != null ? bulletsPanel.name : "null")}");

            if (bulletsPanel != null)
            {
                Transform container = bulletsPanel.Find("BulletContainer");
                if (container == null)
                {
                    container = bulletsPanel.Find("CyberBulletContainer");
                }
                Debug.Log($"[PlayerGunController] Resolved container: {(container != null ? container.name : "null")}");

                if (container != null)
                {
                    bulletContainer = container;
                    if (container.childCount > 0)
                    {
                        bulletUIPrefab = container.GetChild(0).gameObject;
                        Debug.Log($"[PlayerGunController] Resolved bulletUIPrefab: {bulletUIPrefab.name}");
                    }
                    else
                    {
                        Debug.LogWarning("[PlayerGunController] bulletContainer has 0 children!");
                    }

                    // Programmatically add VerticalLayoutGroup if missing to ensure icons stack vertically
                    var layoutGroup = bulletContainer.GetComponent<VerticalLayoutGroup>();
                    if (layoutGroup == null)
                    {
                        layoutGroup = bulletContainer.gameObject.AddComponent<VerticalLayoutGroup>();
                        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
                        layoutGroup.spacing = 2f;
                        layoutGroup.childForceExpandWidth = false;
                        layoutGroup.childForceExpandHeight = false;
                        layoutGroup.childControlWidth = false;
                        layoutGroup.childControlHeight = false;
                        layoutGroup.childScaleWidth = false;
                        layoutGroup.childScaleHeight = false;
                        Debug.Log("[PlayerGunController] Programmatically added VerticalLayoutGroup to bulletContainer.");
                    }
                }

                Transform countTrans = bulletsPanel.Find("AmmoCount");
                if (countTrans == null)
                {
                    countTrans = bulletsPanel.Find("CyberAmmoCount");
                }
                Debug.Log($"[PlayerGunController] Resolved countTrans: {(countTrans != null ? countTrans.name : "null")}");

                if (countTrans != null)
                {
                    ammoText = countTrans.GetComponent<TextMeshProUGUI>();
                    Debug.Log($"[PlayerGunController] Resolved ammoText: {ammoText != null}");
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

        // Add default AudioSources if not set, and force playOnAwake to false to prevent gunshot sound playing on game startup
        if (gunShotAudio == null)
        {
            gunShotAudio = gameObject.AddComponent<AudioSource>();
            gunShotAudio.playOnAwake = false;
        }
        else
        {
            gunShotAudio.playOnAwake = false;
        }

        if (explosionAudio == null)
        {
            explosionAudio = gameObject.AddComponent<AudioSource>();
            explosionAudio.playOnAwake = false;
        }
        else
        {
            explosionAudio.playOnAwake = false;
        }

        if (gunInHand != null)
        {
            gunInHand.SetActive(false);
        }
    }

    void Update()
    {
        if (!hasGun)
        {
            SetAiming(false);
            return;
        }

        // Holding the right mouse button is the only action that enables the
        // aiming/shooting pose. Releasing it returns to the normal armed run.
        bool wantsToAim = Input.GetMouseButton(1);
        SetAiming(wantsToAim);

        // Space is reserved for jumping. Every mouse/UI firing path is validated
        // again inside Shoot(), so the player cannot fire without aiming.
        bool clickedUI = UnityEngine.EventSystems.EventSystem.current != null && 
                          UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        if (wantsToAim && Input.GetMouseButtonDown(0) && !clickedUI)
        {
            Shoot();
        }
    }

    public void EquipGun()
    {
        Debug.Log($"[PlayerGunController] EquipGun called! currentAmmo={currentAmmo}, maxAmmo={maxAmmo}");
        hasGun = true;
        currentAmmo = maxAmmo;

        if (gunInHand != null)
        {
            gunInHand.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[PlayerGunController] gunInHand is null when equipping!");
        }

        if (ammoPanel != null)
        {
            ammoPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[PlayerGunController] ammoPanel is null when equipping!");
        }

        if (shootButton != null)
        {
            shootButton.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[PlayerGunController] shootButton is null when equipping!");
        }

        if (animator != null)
        {
            animator.SetBool("hasGun", true);
            animator.SetBool("isAiming", false);
        }

        UpdateAmmoUI();
    }

    public void Shoot()
    {
        Debug.Log($"[PlayerGunController] Shoot clicked! hasGun={hasGun}, ammo={currentAmmo}");
        // Every firing path, including the on-screen button, must respect aiming.
        // This prevents a normal left click from bypassing the right-click aim hold.
        if (!hasGun || currentAmmo <= 0 || !isAiming)
        {
            if (hasGun && currentAmmo > 0 && !isAiming)
                Debug.Log("[PlayerGunController] Shot ignored because the player is not aiming.");
            return;
        }

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

        if (TryFindObstacleInFirePath(out GameObject targetObj, out Vector3 hitPoint))
        {
            if (explosionAudio != null && explosionAudio.clip != null)
                explosionAudio.Play();

            SpawnDestructionEffect(hitPoint, targetObj);
            Destroy(targetObj);
            AnalyticsStats.RegisterObstacleDestroyed();
            Debug.Log($"[PlayerGunController] Destroyed obstacle: {targetObj.name}");
        }
        else
        {
            Debug.Log("[PlayerGunController] No obstacle was found in the firing path.");
        }

        currentAmmo--;
        UpdateAmmoUI();

        if (currentAmmo <= 0)
        {
            Disarm();
        }
    }

    private bool TryFindObstacleInFirePath(out GameObject obstacle, out Vector3 hitPoint)
    {
        obstacle = null;
        hitPoint = Vector3.zero;

        Ray ray = new Ray(transform.position + Vector3.up, transform.forward);
        RaycastHit[] hits = Physics.SphereCastAll(
            ray,
            1f,
            35f,
            ~0,
            QueryTriggerInteraction.Collide);

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null ||
                hit.collider.transform == transform ||
                hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            GameObject resolvedObstacle = FindObstacleRoot(hit.collider.transform);
            if (resolvedObstacle == null)
                continue;

            obstacle = resolvedObstacle;
            hitPoint = hit.point;
            return true;
        }

        return false;
    }

    private static GameObject FindObstacleRoot(Transform hitTransform)
    {
        GameObject highestTaggedObstacle = null;
        Transform current = hitTransform;

        while (current != null)
        {
            if (current.CompareTag("Obstacle"))
                highestTaggedObstacle = current.gameObject;

            current = current.parent;
        }

        return highestTaggedObstacle;
    }

    void Disarm()
    {
        Debug.Log("[PlayerGunController] Disarming gun.");
        SetAiming(false);
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
            animator.SetBool("isAiming", false);
        }
    }

    private void SetAiming(bool aiming)
    {
        if (isAiming == aiming)
            return;

        isAiming = aiming;
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetBool("isAiming", isAiming);
    }

    private void OnDisable()
    {
        SetAiming(false);
    }

    void UpdateAmmoUI()
    {
        Debug.Log($"[PlayerGunController] UpdateAmmoUI: currentAmmo={currentAmmo}, maxAmmo={maxAmmo}, ammoTextIsNotNull={ammoText != null}, bulletContainerIsNotNull={bulletContainer != null}, bulletUIPrefabIsNotNull={bulletUIPrefab != null}");
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

        // Support vertical filled image updates (only if the full container/text hierarchy is not present)
        if (ammoPanel != null && bulletContainer == null && ammoText == null)
        {
            UnityEngine.UI.Image ammoImage = ammoPanel.GetComponent<UnityEngine.UI.Image>();
            if (ammoImage != null)
            {
                ammoImage.type = UnityEngine.UI.Image.Type.Filled;
                ammoImage.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
                ammoImage.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;
                ammoImage.fillAmount = (float)currentAmmo / (float)maxAmmo;
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
