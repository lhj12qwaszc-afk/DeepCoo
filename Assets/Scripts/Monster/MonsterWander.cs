using System.Collections.Generic;
using UnityEngine;

// Walks the monster around the maze: picks a random floor cell, follows the corridor
// path to it, pauses for a moment, then picks another.
public class MonsterWander : MonoBehaviour
{
    public float walkSpeed = 1.1f;     // m/s, slow and deliberate
    public float turnSpeed = 120f;     // degrees per second
    public Vector2 pauseSeconds = new Vector2(1.5f, 4f);
    public int minTargetDistance = 4;  // in cells, so it actually travels somewhere

    // Read by the procedural animation scripts.
    public float CurrentSpeed { get; private set; }

    List<Vector2Int> path;
    int pathIndex;
    float pauseTimer;
    List<Vector2Int> floorCells;

    void Start()
    {
        floorCells = MazeLayout.FloorCells();
        pauseTimer = Random.Range(pauseSeconds.x, pauseSeconds.y);
    }

    void Update()
    {
        if (path == null || pathIndex >= path.Count)
        {
            CurrentSpeed = 0f;
            pauseTimer -= Time.deltaTime;
            if (pauseTimer <= 0f)
                PickNewTarget();
            return;
        }

        var target = MazeLayout.CellCenter(path[pathIndex]);
        target.y = transform.position.y;
        var toTarget = target - transform.position;
        if (toTarget.magnitude < 0.1f)
        {
            pathIndex++;
            if (pathIndex >= path.Count)
                pauseTimer = Random.Range(pauseSeconds.x, pauseSeconds.y);
            return;
        }

        var wanted = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, turnSpeed * Time.deltaTime);

        // Slow down while turning so it doesn't slide sideways around corners.
        float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, toTarget.normalized));
        CurrentSpeed = walkSpeed * facing * facing;
        transform.position = Vector3.MoveTowards(transform.position, target, CurrentSpeed * Time.deltaTime);
    }

    void PickNewTarget()
    {
        var here = MazeLayout.WorldToCell(transform.position);
        for (int attempt = 0; attempt < 20; attempt++)
        {
            var goal = floorCells[Random.Range(0, floorCells.Count)];
            if (Mathf.Abs(goal.x - here.x) + Mathf.Abs(goal.y - here.y) < minTargetDistance)
                continue;
            var found = MazeLayout.FindPath(here, goal);
            if (found != null)
            {
                path = found;
                pathIndex = 1;
                return;
            }
        }
        pauseTimer = 1f;
    }
}
