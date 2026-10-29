using System.Collections.Generic;
using UnityEngine;

// Moves the monster around the maze. While wandering it picks a random floor cell, follows
// the corridor path to it, pauses, then picks another. When MonsterSenses calls Pursue() it
// runs after the last known player position, re-planning the path as that position updates,
// and gives up after losing track for a while.
public class MonsterWander : MonoBehaviour
{
    public float walkSpeed = 1.1f;     // m/s, slow and deliberate
    public float runSpeed = 4.2f;      // just under the player's sprint
    public float turnSpeed = 120f;     // degrees per second while walking
    public float chaseTurnSpeed = 360f;
    public Vector2 pauseSeconds = new Vector2(1.5f, 4f);
    public int minTargetDistance = 4;  // in cells, so it actually travels somewhere
    public float repathInterval = 0.4f;
    public float loseTrackSeconds = 5f;

    // Read by the procedural animation scripts.
    public float CurrentSpeed { get; private set; }
    public bool IsChasing { get; private set; }

    List<Vector2Int> path;
    int pathIndex;
    float pauseTimer;
    List<Vector2Int> floorCells;
    Vector3 chaseTarget;
    float lastSighting;
    float repathTimer;

    void Start()
    {
        floorCells = MazeLayout.FloorCells();
        pauseTimer = Random.Range(pauseSeconds.x, pauseSeconds.y);
    }

    // Called every frame the monster hears or sees the player.
    public void Pursue(Vector3 worldPosition)
    {
        chaseTarget = worldPosition;
        lastSighting = Time.time;
        if (!IsChasing)
        {
            IsChasing = true;
            repathTimer = 0f;
        }
    }

    void Update()
    {
        if (IsChasing)
            UpdateChase();
        else
            UpdateWander();
    }

    void UpdateWander()
    {
        if (path == null || pathIndex >= path.Count)
        {
            CurrentSpeed = 0f;
            pauseTimer -= Time.deltaTime;
            if (pauseTimer <= 0f)
                PickNewTarget();
            return;
        }
        if (MoveTowards(MazeLayout.CellCenter(path[pathIndex]), walkSpeed, turnSpeed))
        {
            pathIndex++;
            if (pathIndex >= path.Count)
                pauseTimer = Random.Range(pauseSeconds.x, pauseSeconds.y);
        }
    }

    void UpdateChase()
    {
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            path = MazeLayout.FindPath(MazeLayout.WorldToCell(transform.position), MazeLayout.WorldToCell(chaseTarget));
            pathIndex = 1;
        }

        bool lastLegReached = path == null || pathIndex >= path.Count;
        if (!lastLegReached)
        {
            if (MoveTowards(MazeLayout.CellCenter(path[pathIndex]), runSpeed, chaseTurnSpeed))
                pathIndex++;
        }
        else if (MoveTowards(chaseTarget, runSpeed, chaseTurnSpeed))
        {
            CurrentSpeed = 0f;
        }

        if (Time.time - lastSighting > loseTrackSeconds && lastLegReached)
        {
            IsChasing = false;
            path = null;
            pauseTimer = Random.Range(pauseSeconds.x, pauseSeconds.y);
        }
    }

    // Steps toward a point on the floor; returns true once it is reached.
    bool MoveTowards(Vector3 target, float speed, float turnRate)
    {
        target.y = transform.position.y;
        var toTarget = target - transform.position;
        if (toTarget.magnitude < 0.1f)
            return true;

        var wanted = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, turnRate * Time.deltaTime);

        // Slow down while turning so it doesn't slide sideways around corners.
        float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, toTarget.normalized));
        CurrentSpeed = speed * facing * facing;
        transform.position = Vector3.MoveTowards(transform.position, target, CurrentSpeed * Time.deltaTime);
        return false;
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
