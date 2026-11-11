using UnityEngine;

// The numbers shown when the player gets out: how long the run took, how often the monster
// caught them (each catch restarts the level, but not the run) and how many batteries they took.
// A run starts at "게임 시작" on the main menu, or with [R] on the escape screen.
public static class RunStats
{
    static float startTime = -1f;
    public static int Deaths;
    public static int Batteries;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Clear()
    {
        startTime = -1f;
        Deaths = 0;
        Batteries = 0;
    }

    public static void StartRun()
    {
        startTime = Time.time;
        Deaths = 0;
        Batteries = 0;
    }

    // the level was opened some other way (straight from the editor): count from now
    public static void EnsureStarted()
    {
        if (startTime < 0f)
            StartRun();
    }

    // game time, so time spent in the pause menu doesn't count
    public static float Seconds => startTime < 0f ? 0f : Time.time - startTime;
}
