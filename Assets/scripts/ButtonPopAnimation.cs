using System.Collections;
using UnityEngine;

/// <summary>Plays a short, unscaled-time pop sequence whenever the button is shown.</summary>
public sealed class ButtonPopAnimation : MonoBehaviour
{
    [SerializeField, Min(1)] private int popCount = 3;
    [SerializeField, Range(1.01f, 1.5f)] private float popScale = 1.12f;
    [SerializeField, Min(0.05f)] private float popDuration = 0.2f;

    private Vector3 restingScale;
    private Coroutine popRoutine;

    private void Awake()
    {
        restingScale = transform.localScale;
    }

    private void OnEnable()
    {
        if (restingScale == Vector3.zero)
            restingScale = transform.localScale;

        popRoutine = StartCoroutine(PopSequence());
    }

    private void OnDisable()
    {
        if (popRoutine != null)
            StopCoroutine(popRoutine);

        transform.localScale = restingScale;
        popRoutine = null;
    }

    private IEnumerator PopSequence()
    {
        // Let the layout system finish placing the button before animating it.
        yield return null;

        for (int pop = 0; pop < popCount; pop++)
        {
            float elapsed = 0f;
            while (elapsed < popDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / popDuration);
                float pulse = Mathf.Sin(progress * Mathf.PI);
                transform.localScale = restingScale * Mathf.Lerp(1f, popScale, pulse);
                yield return null;
            }

            transform.localScale = restingScale;
        }

        popRoutine = null;
    }
}
