using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class TrackTile : MonoBehaviour
{
    [Header("Lane Settings (Must Match PlayerMove Exactly)")]
    [SerializeField] float trackCenterXOffset = -15.0f;
    [SerializeField] float laneDistance = 0.2f;

    [Header("Prefabs to Spawn")]
    [SerializeField] GameObject coinPrefab;
    [SerializeField] GameObject[] obstaclePrefabs;
    [SerializeField] GameObject gunPickupPrefab;

    [Header("Spawn Logic")]
    [Range(0f, 100f)][SerializeField] float obstacleChance = 40f;
    [Range(0f, 100f)][SerializeField] float coinChance = 35f;
    [Range(0f, 100f)][SerializeField] float gunSpawnChance = 3.0f;

    [Tooltip("Local Z positions along this tile where items can spawn. Adjust based on tile length!")]
    [SerializeField] float[] localZPositions = new float[] { 10f, 20f, 30f };

    void Start()
    {
        // Automatically match the player's lane positioning settings at runtime if available
        PlayerMove player = FindFirstObjectByType<PlayerMove>();
        if (player != null)
        {
            trackCenterXOffset = player.TrackCenterXOffset;
            laneDistance = player.LaneDistance;
        }
        StartCoroutine(SpawnSequence());
    }

    IEnumerator SpawnSequence()
    {
        // Wait one physics frame so the newly instantiated tile colliders are registered
        yield return new WaitForFixedUpdate();
        GenerateObstaclesAndCoins();
    }

    void GenerateObstaclesAndCoins()
    {
        // Keep the first 100 meters of the track completely empty for a clean player start
        if (transform.position.z < 50f)
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

    void SpawnItem(GameObject prefab, int lane, float localZ)
    {
        if (prefab == null) return;

        // Calculates the absolute world X coordinate matching PlayerMove's grid layout
        float targetWorldX = trackCenterXOffset + ((lane - 1) * laneDistance);

        // 1. Raycast down BEFORE instantiating to find the exact road surface Y
        float groundY = transform.position.y;
        Vector3 rayOrigin = new Vector3(targetWorldX, transform.position.y + 25f, transform.position.z + localZ);
        
        int layerMask = ~0;

        RaycastHit hit;
        bool hitSomething = Physics.Raycast(rayOrigin, Vector3.down, out hit, 50f, layerMask, QueryTriggerInteraction.Ignore);
        if (hitSomething)
        {
            groundY = hit.point.y;
        }
        else
        {
            // If the raycast hits nothing, it means there is no road/ground in this lane at this Z coordinate.
            // We skip spawning the item entirely so it does not float in mid-air.
            Debug.LogWarning($"[TrackTile] Raycast hit NOTHING at X:{targetWorldX:F3} Z:{transform.position.z + localZ:F3} - skipping spawn.");
            return;
        }

        bool isCoin = prefab.name.ToLower().Contains("coin") || prefab == coinPrefab;
        bool isGunPickup = prefab.name.ToLower().Contains("gun") || prefab == gunPickupPrefab;

        // 2. Calculate the correct spawn Y position
        float spawnY;
        if (isCoin || isGunPickup)
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
        spawnedObj.transform.SetParent(transform, true);

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

        // 5. For obstacles: use bounds to align the bottom to ground level,
        //    and re-center colliders to match the visual mesh.
        spawnedObj.tag = "Obstacle";
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

            // Re-center colliders to match the visual mesh center
            foreach (Collider c in spawnedObj.GetComponentsInChildren<Collider>(true))
            {
                Vector3 localBoundsCenter = c.transform.InverseTransformPoint(bounds.center);
                if (c is CapsuleCollider capsule)
                {
                    capsule.center = new Vector3(capsule.center.x, localBoundsCenter.y, capsule.center.z);
                }
                else if (c is SphereCollider sphere)
                {
                    sphere.center = new Vector3(sphere.center.x, localBoundsCenter.y, sphere.center.z);
                }
                else if (c is BoxCollider box)
                {
                    box.center = new Vector3(box.center.x, localBoundsCenter.y, box.center.z);
                }
            }

            Debug.Log($"[TrackTile] Spawned OBSTACLE '{prefab.name}' at lane {lane} | worldPos: {spawnedObj.transform.position} | groundY: {groundY:F3}");
        }
        else
        {
            Debug.LogWarning($"[TrackTile] Spawned {prefab.name} but it has no bounds!");
        }
    }
}