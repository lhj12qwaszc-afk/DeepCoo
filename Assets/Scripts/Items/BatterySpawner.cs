using System.Collections.Generic;
using UnityEngine;

// Scatters spare batteries around the maze at the start of every run, preferring dead ends,
// so their positions change each time the level restarts.
public class BatterySpawner : MonoBehaviour
{
    public GameObject batteryModel;
    public int count = 5;
    public int minDistanceFromPlayer = 3;   // in cells

    void Start()
    {
        var player = FindFirstObjectByType<FirstPersonController>();
        var start = player != null ? MazeLayout.WorldToCell(player.transform.position) : new Vector2Int(-100, -100);

        var deadEnds = new List<Vector2Int>();
        var others = new List<Vector2Int>();
        foreach (var cell in MazeLayout.FloorCells())
        {
            if (Mathf.Abs(cell.x - start.x) + Mathf.Abs(cell.y - start.y) < minDistanceFromPlayer)
                continue;
            int exits = 0;
            foreach (var step in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                if (MazeLayout.IsFloor(cell + step))
                    exits++;
            (exits == 1 ? deadEnds : others).Add(cell);
        }
        Shuffle(deadEnds);
        Shuffle(others);
        deadEnds.AddRange(others);

        for (int i = 0; i < count && i < deadEnds.Count; i++)
            Spawn(deadEnds[i]);
    }

    void Spawn(Vector2Int cell)
    {
        var jitter = new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f));
        var root = new GameObject("Battery");
        root.transform.SetParent(transform);
        root.transform.position = MazeLayout.CellCenter(cell) + jitter + Vector3.up * 0.03f;
        root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        var model = Instantiate(batteryModel, root.transform);
        model.transform.localPosition = Vector3.up * 0.031f;
        // lying on its side
        model.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        model.transform.localScale = Vector3.one * 2f;   // a little oversized so it reads in the dark

        var glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = Vector3.up * 0.4f;
        glow.type = LightType.Point;
        glow.range = 1.6f;
        glow.intensity = 0.6f;
        glow.color = new Color(0.6f, 1f, 0.6f);
        glow.shadows = LightShadows.None;

        root.AddComponent<BatteryPickup>();
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
