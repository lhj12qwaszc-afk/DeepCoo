using System;
using UnityEngine;

// One-off loud sounds in the world (a slammed locker door, a dropped object) that the
// monster can hear if it is within the sound's range.
public static class NoiseEvents
{
    public static event Action<Vector3, float> Emitted;

    public static void Emit(Vector3 position, float range) => Emitted?.Invoke(position, range);
}
