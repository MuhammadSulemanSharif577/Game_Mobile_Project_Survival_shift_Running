using UnityEngine;
using UnityEngine.Rendering;

public class CyberWaterBottleItem : WaterBottleItem
{
    [Header("Cyber Rendering")]
    [Tooltip("Bottle meshes farther than this distance from the player are hidden until the tile approaches.")]
    [SerializeField, Min(20f)] private float visibleDistance = 110f;

    private Renderer[] bottleRenderers;
    private bool renderersVisible = true;

    protected override void Start()
    {
        // Match the Desert pickup size exactly.
        targetScaleFactor = 0.18f;

        base.Start();

        bottleRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer bottleRenderer in bottleRenderers)
        {
            // A small pickup does not need to cast or receive expensive realtime shadows.
            bottleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            bottleRenderer.receiveShadows = false;
        }

        UpdateRendererVisibility();
    }

    protected override void Update()
    {
        base.Update();

        if (!collected)
        {
            UpdateRendererVisibility();
        }
    }

    protected override void CollectBottle()
    {
        SetRenderersVisible(false);
        base.CollectBottle();
    }

    private void UpdateRendererVisibility()
    {
        if (playerTransform == null || bottleRenderers == null)
        {
            return;
        }

        float distanceAlongTrack = Mathf.Abs(transform.position.z - playerTransform.position.z);
        SetRenderersVisible(distanceAlongTrack <= visibleDistance);
    }

    private void SetRenderersVisible(bool visible)
    {
        if (bottleRenderers == null || renderersVisible == visible)
        {
            return;
        }

        foreach (Renderer bottleRenderer in bottleRenderers)
        {
            if (bottleRenderer != null)
            {
                bottleRenderer.enabled = visible;
            }
        }

        renderersVisible = visible;
    }
}
