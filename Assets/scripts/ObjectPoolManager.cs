using System.Collections.Generic;
using UnityEngine;

public class CanyonPoolManager : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform;      // Reference to your Player object
    public GameObject[] tilePrefabs;       // Array holding your Tile_01 and Tile_02 prefabs

    [Header("Configuration")]
    public float tileLength = 120f;         // Must match your Blender Y-dimension exactly (50)
    public int totalTilesOnScreen = 4;     // How many tiles track blocks exist at once
    public float safetyBuffer = 40f;        // Extra distance player must travel before tile is recycled

    private List<GameObject> activeTiles = new List<GameObject>();
    private float spawnZ = 0f;             // Tracks the current horizon line for spawning

    void Start()
    {
        // Check to ensure everything is assigned safely before starting layout math
        if (playerTransform == null || tilePrefabs.Length == 0)
        {
            Debug.LogError("Please assign the Player and Tile Prefabs in the Inspector!");
            return;
        }

        // Initialize our scene track by spawning a string of tiles back-to-back
        for (int i = 0; i < totalTilesOnScreen; i++)
        {
            // Pick a random tile prefab index from our array
            int randomIndex = Random.Range(0, tilePrefabs.Length);
            SpawnInitialTile(randomIndex);
        }
    }

    void Update()
    {
        // Safe tracking guard clause
        if (activeTiles.Count == 0) return;

        // Check if the player's Z position has crossed past the endpoint of our oldest tile plus the safety buffer
        // activeTiles[0] is always the tile furthest behind the player
        if (playerTransform.position.z - tileLength - safetyBuffer > activeTiles[0].transform.position.z)
        {
            LeapfrogTile();
        }
    }

  void SpawnInitialTile(int prefabIndex)
{
    // Fix: Instead of using Quaternion.identity (0,0,0), we use the prefab's native flat rotation!
    GameObject newTile = Instantiate(tilePrefabs[prefabIndex], Vector3.forward * spawnZ, tilePrefabs[prefabIndex].transform.rotation);
    activeTiles.Add(newTile);
    
    spawnZ += tileLength; 
}

void LeapfrogTile()
{
    GameObject recycledTile = activeTiles[0];
    activeTiles.RemoveAt(0);

    // Moves it cleanly straight forward along the global Z track
    recycledTile.transform.position = new Vector3(0, 0, spawnZ);
    
    activeTiles.Add(recycledTile);
    spawnZ += tileLength;
}
}