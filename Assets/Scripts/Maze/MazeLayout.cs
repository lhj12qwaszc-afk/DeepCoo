using System.Collections.Generic;
using UnityEngine;

// The maze floor plan, shared by the editor builder and runtime code (monster pathfinding).
// Cell (col, row) covers x in [col, col+1) * CellSize and z in (-(row+1), -row] * CellSize.
public static class MazeLayout
{
    // '#' = wall, '.' = floor. Row 0 is the north edge (top of the floor plan).
    public static readonly string[] Rows =
    {
        "###############",
        "#.............#",
        "#.#######.#####",
        "#...#...#.....#",
        "#.###.#.#####.#",
        "#.#...#...#...#",
        "#.#.#####.#.###",
        "#.#.#...#.#...#",
        "#.#.#.#.#.###.#",
        "#.#.#.#.#...#.#",
        "#.#.###.###.#.#",
        "#.#...#.#...#.#",
        "#####.#.#.###.#",
        "#.....#.......#",
        "###############",
    };

    public const float CellSize = 4f;

    public static int Width => Rows[0].Length;
    public static int Height => Rows.Length;

    public static bool IsFloor(Vector2Int cell) =>
        cell.y >= 0 && cell.y < Height && cell.x >= 0 && cell.x < Width && Rows[cell.y][cell.x] == '.';

    public static Vector3 CellCenter(Vector2Int cell) =>
        new Vector3((cell.x + 0.5f) * CellSize, 0f, -(cell.y + 0.5f) * CellSize);

    public static Vector2Int WorldToCell(Vector3 position) =>
        new Vector2Int(Mathf.FloorToInt(position.x / CellSize), Mathf.FloorToInt(-position.z / CellSize));

    public static List<Vector2Int> FloorCells()
    {
        var cells = new List<Vector2Int>();
        for (int r = 0; r < Height; r++)
            for (int c = 0; c < Width; c++)
                if (Rows[r][c] == '.')
                    cells.Add(new Vector2Int(c, r));
        return cells;
    }

    static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    // Shortest path between two floor cells (breadth-first), start and goal included.
    // Returns null when there is no path.
    public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        if (!IsFloor(start) || !IsFloor(goal))
            return null;
        var cameFrom = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (cell == goal)
                break;
            foreach (var step in Steps)
            {
                var next = cell + step;
                if (IsFloor(next) && !cameFrom.ContainsKey(next))
                {
                    cameFrom[next] = cell;
                    queue.Enqueue(next);
                }
            }
        }
        if (!cameFrom.ContainsKey(goal))
            return null;
        var path = new List<Vector2Int> { goal };
        while (path[path.Count - 1] != start)
            path.Add(cameFrom[path[path.Count - 1]]);
        path.Reverse();
        return path;
    }
}
