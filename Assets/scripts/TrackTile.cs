using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class TrackTile : MonoBehaviour
{
    [Header("Lane Settings (Must Match PlayerMove Exactly)")]
    [SerializeField] protected float trackCenterXOffset = -15.0f;
    [SerializeField] protected float laneDistance = 0.2f;

    [Header("Prefabs to Spawn")]
    [SerializeField] protected GameObject coinPrefab;
    [SerializeField] protected GameObject[] obstaclePrefabs;
    [SerializeField] protected GameObject gunPickupPrefab;
    [SerializeField] protected GameObject waterBottlePrefab;

    [Header("Spawn Logic")]
    [Range(0f, 100f)][SerializeField] protected float obstacleChance = 40f;
    [Range(0f, 100f)][SerializeField] protected float coinChance = 35f;
    [Range(0f, 100f)][SerializeField] protected float gunSpawnChance = 3.0f;
    [Range(0f, 100f)][SerializeField] protected float waterBottleSpawnChance = 12f;

    [Tooltip("Local Z positions along this tile where items can spawn. Adjust based on tile length!")]
    [SerializeField] protected float[] localZPositions = new float[] { 10f, 20f, 30f };

    [SerializeField] protected float emptyStartDistance = 150f;
    [SerializeField] protected bool scaleCars = true;

    protected virtual void Start()
    {
        // Automatically match the player's lane positioning settings at runtime if available
        IRunnerController player = RunnerControllerLocator.Find();
        if (player != null)
        {
            trackCenterXOffset = player.TrackCenterXOffset;
            laneDistance = player.LaneDistance;
        }
        StartCoroutine(SpawnSequence());
    }

    protected virtual IEnumerator SpawnSequence()
    {
        // Wait one physics frame so the newly instantiated tile colliders are registered
        yield return new WaitForFixedUpdate();
        GenerateObstaclesAndCoins();
    }

    protected virtual void GenerateObstaclesAndCoins()
    {
        float playerStartingZ = 0f;
        IRunnerController player = RunnerControllerLocator.Find();
        if (player != null)
        {
            playerStartingZ = player.StartingZ;
        }

        float distanceFromStart = transform.position.z - playerStartingZ;
        if (distanceFromStart < emptyStartDistance)
        {
            return;
        }

        float tileLength = 120f;
        Renderer tileRenderer = GetComponent<Renderer>();
        if (tileRenderer == null) tileRenderer = GetComponentInChildren<Renderer>();
        if (tileRenderer != null)
        {
            tileLength = tileRenderer.bounds.size.z;
        }
        if (tileLength < 40f)
        {
            tileLength = 120f;
        }

        List<float> finalZPositions = new List<float>();
        if (localZPositions == null || localZPositions.Length <= 3)
        {
            for (float z = 10f; z < tileLength - 5f; z += 12f)
            {
                finalZPositions.Add(z);
            }
        }
        else
        {
            foreach (float z in localZPositions)
            {
                if (z < tileLength)
                    finalZPositions.Add(z);
            }
        }

        foreach (float zPos in finalZPositions)
        {
            float randomizedZ = zPos + Random.Range(-2.5f, 2.5f);
            List<int> availableLanes = new List<int> { 0, 1, 2 };

            // 1. Roll for a Gun Pickup
            if (gunPickupPrefab != null && Random.Range(0f, 100f) < gunSpawnChance)
            {
                int randomLaneIndex = Random.Range(0, availableLanes.Count);
                int chosenLane = availableLanes[randomLaneIndex];
                SpawnItem(gunPickupPrefab, chosenLane, randomizedZ);
                availableLanes.Remove(chosenLane);
            }

            // 1b. Roll for a Water Bottle
            if (waterBottlePrefab != null && Random.Range(0f, 100f) < waterBottleSpawnChance && availableLanes.Count > 0)
            {
                int randomLaneIndex = Random.Range(0, availableLanes.Count);
                int chosenLane = availableLanes[randomLaneIndex];
                SpawnItem(waterBottlePrefab, chosenLane, randomizedZ);
                availableLanes.Remove(chosenLane);
            }

            // 2. Roll for an Obstacle
            if (Random.Range(0f, 100f) < obstacleChance && obstaclePrefabs.Length > 0 && availableLanes.Count > 0)
            {
                int randomLaneIndex = Random.Range(0, availableLanes.Count);
                int chosenLane = availableLanes[randomLaneIndex];

                GameObject chosenObstacle = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
                SpawnItem(chosenObstacle, chosenLane, randomizedZ);

                availableLanes.Remove(chosenLane);
            }

            // 3. Spawn coins in remaining lanes
            foreach (int lane in availableLanes)
            {
                if (Random.Range(0f, 100f) < coinChance && coinPrefab != null)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        float coinZ = randomizedZ + (c * 1.8f);
                        if (coinZ < tileLength - 2f)
                        {
                            SpawnItem(coinPrefab, lane, coinZ);
                        }
                    }
                }
            }
        }
    }

    protected virtual void SpawnItem(GameObject prefab, int lane, float localZ)
    {
        if (prefab == null) return;

        // Calculates the absolute world X coordinate matching PlayerMove's grid layout
        float targetWorldX = trackCenterXOffset + ((lane - 1) * laneDistance);

        // 1. Raycast down BEFORE instantiating to find the exact road surface Y
        float groundY;
        if (!TryGetRoadY(targetWorldX, localZ, out groundY))
        {
            // If the raycast hits nothing, it means there is no road/ground in this lane at this Z coordinate.
            // We skip spawning the item entirely so it does not float in mid-air.
            Debug.LogWarning($"[TrackTile] Raycast hit NOTHING at X:{targetWorldX:F3} Z:{transform.position.z + localZ:F3} - skipping spawn.");
            return;
        }

        // The inspector arrays are authoritative. A newly added obstacle must stay
        // an obstacle even if its asset name contains words such as gun or bottle.
        bool isObstacle = IsConfiguredObstaclePrefab(prefab);
        bool isCoin = !isObstacle &&
            (prefab == coinPrefab || prefab.GetComponentInChildren<CoinScript>(true) != null);
        bool isGunPickup = !isObstacle &&
            (prefab == gunPickupPrefab || prefab.GetComponentInChildren<GunPickup>(true) != null);
        bool isWaterBottle = !isObstacle &&
            (prefab == waterBottlePrefab || prefab.GetComponentInChildren<WaterBottleItem>(true) != null);

        // 2. Calculate the correct spawn Y position
        float spawnY;
        if (isCoin || isGunPickup || isWaterBottle)
        {
            // Place coin/gun center at waist height (1.0m above ground)
            spawnY = groundY + 1.0f;
        }
        else
        {
            // Place obstacle at ground level (will be fine-tuned by bounds below)
            spawnY = groundY;
        }

        // 3. Instantiate the object
        Vector3 spawnPosition = new Vector3(targetWorldX, spawnY, transform.position.z + localZ);
        GameObject spawnedObj = Instantiate(prefab, spawnPosition, Quaternion.identity);
        
        // Scale up car obstacles that are exported in tiny centimeter units
        if (scaleCars && prefab.name.ToLower().Contains("car"))
        {
            spawnedObj.transform.localScale = new Vector3(30f, 30f, 30f);
        }

        spawnedObj.transform.SetParent(GetItemContainer(), true);

        // 4. For Gun Pickups:
        if (isGunPickup)
        {
            Animator gunAnim = spawnedObj.GetComponent<Animator>();
            if (gunAnim == null) gunAnim = spawnedObj.GetComponentInChildren<Animator>();
            if (gunAnim != null) gunAnim.enabled = false;

            Rigidbody gunRb = spawnedObj.GetComponent<Rigidbody>();
            if (gunRb == null) gunRb = spawnedObj.AddComponent<Rigidbody>();
            gunRb.isKinematic = true;
            gunRb.useGravity = false;

            foreach (Collider c in spawnedObj.GetComponentsInChildren<Collider>(true))
            {
                c.isTrigger = true;
            }
            Debug.Log($"[TrackTile] Spawned GUN PICKUP '{prefab.name}' at lane {lane} | worldPos: {spawnedObj.transform.position} | groundY: {groundY:F3}");
            return;
        }

        // 5. For coins: ensure the tag is "Coin" and the trigger collider is properly sized.
        if (isCoin)
        {
            // FIX: The coin prefab's tag might be "Untagged" since the FBX base model doesn't
            // have a tag set. We force it to "Coin" so OnTriggerEnter tag checks work.
            spawnedObj.tag = "Coin";

            // Disable Animator controller so CoinScript has full control over transform
            Animator coinAnim = spawnedObj.GetComponent<Animator>();
            if (coinAnim == null) coinAnim = spawnedObj.GetComponentInChildren<Animator>();
            if (coinAnim != null) coinAnim.enabled = false;

            // Add a kinematic Rigidbody so the coin is a "Kinematic Trigger Collider"
            Rigidbody coinRb = spawnedObj.GetComponent<Rigidbody>();
            if (coinRb == null) coinRb = spawnedObj.AddComponent<Rigidbody>();
            coinRb.isKinematic = true;
            coinRb.useGravity = false;

            // Ensure the trigger collider is large enough for reliable detection.
            foreach (Collider c in spawnedObj.GetComponentsInChildren<Collider>(true))
            {
                c.isTrigger = true;
                if (c is CapsuleCollider capsule)
                {
                    capsule.radius = Mathf.Max(capsule.radius, 0.5f);
                    capsule.height = Mathf.Max(capsule.height, 1.0f);
                }
            }

            Debug.Log($"[TrackTile] Spawned COIN '{prefab.name}' at lane {lane} | worldPos: {spawnedObj.transform.position} | groundY: {groundY:F3}");
            return;
        }

        // 5b. For water bottles:
        if (isWaterBottle)
        {
            spawnedObj.tag = "Untagged"; // WaterBottleItem trigger checks are component/tag based
            
            // Disable Animator controller so WaterBottleItem has full control over transform rotation
            Animator bottleAnim = spawnedObj.GetComponent<Animator>();
            if (bottleAnim == null) bottleAnim = spawnedObj.GetComponentInChildren<Animator>();
            if (bottleAnim != null) bottleAnim.enabled = false;

            Rigidbody bottleRb = spawnedObj.GetComponent<Rigidbody>();
            if (bottleRb == null) bottleRb = spawnedObj.AddComponent<Rigidbody>();
            bottleRb.isKinematic = true;
            bottleRb.useGravity = false;

            foreach (Collider c in spawnedObj.GetComponentsInChildren<Collider>(true))
            {
                c.isTrigger = true;
            }
            
            Debug.Log($"[TrackTile] Spawned WATER BOTTLE '{prefab.name}' at lane {lane} | worldPos: {spawnedObj.transform.position} | groundY: {groundY:F3}");
            return;
        }

        // 5. For obstacles: use bounds to align the bottom to ground level,
        //    and re-center colliders to match the visual mesh.
        spawnedObj.tag = "Obstacle";

        foreach (Rigidbody obstacleRb in spawnedObj.GetComponentsInChildren<Rigidbody>(true))
        {
            obstacleRb.isKinematic = true;
            obstacleRb.useGravity = false;
        }

        Bounds bounds = new Bounds();
        bool hasBounds = false;
        foreach (Renderer r in spawnedObj.GetComponentsInChildren<Renderer>(true))
        {
            string rName = r.name.ToLower();
            if (rName.Contains("shadow") || rName.Contains("blob") || rName.Contains("projector") || rName.Contains("particle") || rName.Contains("effect"))
                continue;

            if (hasBounds) bounds.Encapsulate(r.bounds);
            else { bounds = r.bounds; hasBounds = true; }
        }

        if (hasBounds)
        {
            // Align obstacle bottom to touch the ground perfectly
            float offsetY = groundY - bounds.min.y;
            spawnedObj.transform.position += Vector3.up * offsetY;

            // Update bounds to match the new position
            bounds.center += Vector3.up * offsetY;

            // Resolve colliders on the obstacle (programmatically add if missing)
            Collider[] colliders = spawnedObj.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                BoxCollider newBox = spawnedObj.AddComponent<BoxCollider>();
                colliders = new Collider[] { newBox };
                Debug.Log($"[TrackTile] Obstacle '{prefab.name}' had no colliders. Programmatically added BoxCollider.");
            }

            // Re-center and resize colliders to match the visual mesh bounds
            foreach (Collider c in colliders)
            {
                Vector3 localCenter = c.transform.InverseTransformPoint(bounds.center);
                Vector3 localSize = c.transform.InverseTransformVector(bounds.size);
                localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

                if (c is BoxCollider box)
                {
                    box.center = localCenter;
                    box.size = localSize;
                }
                else if (c is CapsuleCollider capsule)
                {
                    capsule.center = localCenter;
                    capsule.height = localSize.y;
                    capsule.radius = Mathf.Min(localSize.x, localSize.z) * 0.5f;
                }
                else if (c is SphereCollider sphere)
                {
                    sphere.center = localCenter;
                    sphere.radius = Mathf.Max(localSize.x, Mathf.Max(localSize.y, localSize.z)) * 0.5f;
                }
            }

            Debug.Log($"[TrackTile] Spawned OBSTACLE '{prefab.name}' at lane {lane} | worldPos: {spawnedObj.transform.position} | groundY: {groundY:F3}");
        }
        else
        {
            Debug.LogWarning($"[TrackTile] Spawned {prefab.name} but it has no bounds!");
        }
    }

    protected bool IsConfiguredObstaclePrefab(GameObject prefab)
    {
        if (prefab == null || obstaclePrefabs == null)
            return false;

        foreach (GameObject obstaclePrefab in obstaclePrefabs)
        {
            if (obstaclePrefab == prefab)
                return true;
        }

        return false;
    }

    protected virtual bool TryGetRoadY(float worldX, float localZ, out float roadY)
    {
        Vector3 rayOrigin = new Vector3(worldX, transform.position.y + 25f, transform.position.z + localZ);
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (IsGroundCollider(hit.collider))
            {
                roadY = hit.point.y;
                return true;
            }
        }

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != null && !hit.collider.isTrigger &&
                !IsSpawnedItemCollider(hit.collider) &&
                hit.collider.GetComponentInParent<TrackTile>() != null)
            {
                roadY = hit.point.y;
                return true;
            }
        }

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != null && !hit.collider.isTrigger && !IsSpawnedItemCollider(hit.collider))
            {
                roadY = hit.point.y;
                return true;
            }
        }

        roadY = transform.position.y;
        return false;
    }

    protected virtual bool IsGroundCollider(Collider col)
    {
        if (col == null || col.isTrigger) return false;
        if (IsSpawnedItemCollider(col)) return false;
        if (col.CompareTag("Ground")) return true;
        if (col.transform.parent != null && col.transform.parent.CompareTag("Ground")) return true;

        string name = col.name.ToLower();

        // Exclude common overhead decorations/signs/buildings
        if (name.Contains("sign") || name.Contains("building") || name.Contains("prop") || 
            name.Contains("wire") || name.Contains("light") || name.Contains("wall") || 
            name.Contains("roof") || name.Contains("bridge") || name.Contains("arch") || name.Contains("tunnel"))
        {
            return false;
        }

        return name.Contains("tile") ||
               name.Contains("plane") ||
               name.Contains("road") ||
               name.Contains("terrain") ||
               name.Contains("ground") ||
               name.Contains("pavement") ||
               name.Contains("shoulder") ||
               name.Contains("scut");
    }

    private bool IsSpawnedItemCollider(Collider col)
    {
        if (col == null)
            return false;

        if (RunnerControllerLocator.GetFrom(col) != null ||
            col.GetComponentInParent<WaterBottleItem>() != null ||
            col.GetComponentInParent<CoinScript>() != null)
        {
            return true;
        }

        Transform current = col.transform;
        while (current != null && current != transform)
        {
            if (current.CompareTag("Obstacle") || current.CompareTag("Coin"))
                return true;
            current = current.parent;
        }

        return false;
    }

    private Transform itemContainer;

    protected Transform GetItemContainer()
    {
        if (itemContainer == null)
        {
            itemContainer = transform.Find("UnscaledItemContainer");
            if (itemContainer == null)
            {
                GameObject containerObj = new GameObject("UnscaledItemContainer");
                itemContainer = containerObj.transform;
                itemContainer.SetParent(transform, false);

                // Negate the parent's non-uniform scaling
                itemContainer.localScale = new Vector3(
                    1f / Mathf.Max(0.001f, transform.localScale.x),
                    1f / Mathf.Max(0.001f, transform.localScale.y),
                    1f / Mathf.Max(0.001f, transform.localScale.z)
                );
            }
        }
        return itemContainer;
    }
}
