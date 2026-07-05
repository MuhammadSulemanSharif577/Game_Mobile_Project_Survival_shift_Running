using UnityEngine;
using System.Collections.Generic;

public class TrackManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform playerTransform;
    [SerializeField] GameObject[] tilePrefabs;

    [Header("Spawning Settings")]
    [SerializeField] float tileLength = 40f;
    [SerializeField] int numberOfTilesOnScreen = 5;
    [SerializeField] float safeDeleteDistance = 20f; // Distance behind the player before a tile is removed

    private float nextSpawnZ = 0f;
    private float targetY = 0f;
    private float trackCenterX = 0f;
    private List<GameObject> activeTiles = new List<GameObject>();

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null)
            {
                playerObj = GameObject.FindGameObjectWithTag("player");
            }
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }

        if (playerTransform != null)
        {
            // Cache the player's initial X coordinate (which is snapped to the track center in PlayerMove.Start())
            trackCenterX = playerTransform.position.x;
            targetY = playerTransform.position.y;
            nextSpawnZ = playerTransform.position.z;
        }
        else
        {
            Debug.LogError("[TrackManager] Player Transform could not be resolved! Track spawning will fail.");
            return;
        }

        for (int i = 0; i < numberOfTilesOnScreen; i++)
        {
            SpawnTile(false);
        }
    }

    void Update()
    {
        if (playerTransform == null || activeTiles.Count == 0) return;

        // FIXED: Calculates the back edge of the oldest plane.
        // It will only spawn a new plane and delete the old one when the player is safely past it.
        float oldestTileEndZ = activeTiles[0].transform.position.z + tileLength;

        if (playerTransform.position.z > oldestTileEndZ + safeDeleteDistance)
        {
            SpawnTile(false);
            DeleteOldestTile();
        }
    }

    void SpawnTile(bool emptyTile)
    {
        if (tilePrefabs == null || tilePrefabs.Length == 0) return;

        GameObject prefabToSpawn = tilePrefabs[Random.Range(0, tilePrefabs.Length)];

        // Use cached trackCenterX instead of playerTransform.position.x
        Vector3 spawnPos = new Vector3(trackCenterX, targetY, nextSpawnZ);

        GameObject spawnedTile = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        activeTiles.Add(spawnedTile);

        if (emptyTile)
        {
            TrackTile tileScript = spawnedTile.GetComponent<TrackTile>();
            if (tileScript != null) tileScript.enabled = false;
        }

        nextSpawnZ += tileLength;
    }

    void DeleteOldestTile()
    {
        if (activeTiles.Count > 0)
        {
            Destroy(activeTiles[0]);
            activeTiles.RemoveAt(0);
        }
    }
}