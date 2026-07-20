using UnityEngine;

public class CyberTrackTile : TrackTile
{
    [SerializeField, Range(1f, 1.3f)] private float nonCarObstacleScale = 1.15f;

    protected override void Start()
    {
        // Apply cyberpunk defaults if they haven't been overridden in the inspector
        if (trackCenterXOffset == -15.0f)
        {
            trackCenterXOffset = -18.0f;
        }
        if (laneDistance == 0.2f)
        {
            laneDistance = 3.5f;
        }

        // Disable base TrackTile's automatic 30x car scaling
        scaleCars = false;

        base.Start();
    }

    protected override void SpawnItem(GameObject prefab, int lane, float localZ)
    {
        Transform container = GetItemContainer();
        int previousChildCount = container != null ? container.childCount : 0;

        base.SpawnItem(prefab, lane, localZ);

        // If spawning was skipped, do not modify an item that was already in the container.
        if (container == null || container.childCount <= previousChildCount)
            return;

        Transform spawned = container.GetChild(container.childCount - 1);
        if (spawned == null || !IsConfiguredObstaclePrefab(prefab))
            return;

        // Cars already use their authored Cyberpunk size. Slightly widen every other obstacle.
        if (prefab.name.ToLowerInvariant().Contains("car"))
            return;

        spawned.localScale *= nonCarObstacleScale;
        SeatOnRoad(spawned, localZ);
    }

    private void SeatOnRoad(Transform spawned, float localZ)
    {
        // The spawned obstacle can have "Road" in its name. Disable its colliders while
        // raycasting so it cannot be mistaken for the road and placed on top of itself.
        Collider[] spawnedColliders = spawned.GetComponentsInChildren<Collider>(true);
        bool[] colliderStates = new bool[spawnedColliders.Length];
        for (int i = 0; i < spawnedColliders.Length; i++)
        {
            colliderStates[i] = spawnedColliders[i].enabled;
            spawnedColliders[i].enabled = false;
        }

        bool foundRoad = TryGetRoadY(spawned.position.x, localZ, out float groundY);

        for (int i = 0; i < spawnedColliders.Length; i++)
            spawnedColliders[i].enabled = colliderStates[i];

        if (!foundRoad)
            return;

        bool hasBounds = false;
        Bounds bounds = new Bounds();
        foreach (Renderer renderer in spawned.GetComponentsInChildren<Renderer>(true))
        {
            string rendererName = renderer.name.ToLowerInvariant();
            if (rendererName.Contains("shadow") || rendererName.Contains("particle") || rendererName.Contains("effect"))
                continue;

            if (hasBounds) bounds.Encapsulate(renderer.bounds);
            else
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
        }

        if (hasBounds)
            spawned.position += Vector3.up * (groundY - bounds.min.y);
    }
}
