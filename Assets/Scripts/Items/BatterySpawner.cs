using System.Collections.Generic;
using UnityEngine;

// Hides spare batteries in a few of the maze's drawers at the start of every run, so which
// drawers hold one changes each time the level restarts (never one with a note in it). It also
// picks a few drawers, any of them, to hold a scare: feathers burst out the first time they open.
public class BatterySpawner : MonoBehaviour
{
    public GameObject batteryModel;
    public int drawersWithBattery = 4;
    public int drawersWithScare = 1;

    void Start()
    {
        var all = new List<Drawer>(FindObjectsByType<Drawer>(FindObjectsSortMode.None));
        Shuffle(all);
        for (int i = 0; i < drawersWithScare && i < all.Count; i++)
            all[i].scare = true;

        var drawers = new List<Drawer>(all);
        drawers.RemoveAll(d => d.HasNote);   // a note's drawer holds nothing else
        Shuffle(drawers);
        for (int i = 0; i < drawersWithBattery && i < drawers.Count; i++)
            drawers[i].PutBattery(batteryModel);
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
