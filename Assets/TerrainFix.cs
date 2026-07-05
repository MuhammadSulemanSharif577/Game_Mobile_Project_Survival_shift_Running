using UnityEngine;

[ExecuteAlways] // This makes the script run while you are editing
public class TerrainFix : MonoBehaviour
{
    public Terrain terrain;

    // This creates a button you can click in the Inspector
    [ContextMenu("Force Sync Terrain Holes")]
    public void ForceSync()
    {
        if (terrain == null) terrain = GetComponent<Terrain>();

        if (terrain != null && terrain.terrainData != null)
        {
            terrain.terrainData.SyncTexture(TerrainData.HolesTextureName);
            Debug.Log("Terrain Holes Synced Successfully.");
        }
        else
        {
            Debug.LogError("No Terrain component found!");
        }
    }
}