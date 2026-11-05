using System.Collections.Generic;
using UnityEngine;

// Hides spare batteries in a few of the maze's drawers at the start of every run, so which
// drawers hold one changes each time the level restarts. The rest are empty.
public class BatterySpawner : MonoBehaviour
{
    public GameObject batteryModel;
    public int drawersWithBattery = 4;

    void Start()
    {
        var drawers = new List<Drawer>(FindObjectsByType<Drawer>(FindObjectsSortMode.None));
        for (int i = drawers.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (drawers[i], drawers[j]) = (drawers[j], drawers[i]);
        }
        for (int i = 0; i < drawersWithBattery && i < drawers.Count; i++)
            drawers[i].PutBattery(batteryModel);
    }
}
