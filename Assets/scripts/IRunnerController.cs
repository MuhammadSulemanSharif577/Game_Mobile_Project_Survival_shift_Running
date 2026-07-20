using UnityEngine;

public interface IRunnerController
{
    Transform RunnerTransform { get; }
    float TrackCenterXOffset { get; }
    float LaneDistance { get; }
    float StartingZ { get; }
    bool IsDead { get; }

    void SetScoreSpeedMultiplier(float multiplier);
    void KnockoutByEnemy();
    void TriggerObstacleDeath();
}

public static class RunnerControllerLocator
{
    public static IRunnerController Find()
    {
        CyberPlayerMove cyberPlayer = Object.FindAnyObjectByType<CyberPlayerMove>();
        if (cyberPlayer != null)
            return cyberPlayer;

        PlayerMove desertPlayer = Object.FindAnyObjectByType<PlayerMove>();
        return desertPlayer;
    }

    public static IRunnerController GetFrom(Component component)
    {
        if (component == null)
            return null;

        Transform current = component.transform;
        while (current != null)
        {
            foreach (MonoBehaviour behaviour in current.GetComponents<MonoBehaviour>())
            {
                if (behaviour is IRunnerController runner)
                    return runner;
            }

            current = current.parent;
        }

        return null;
    }
}
