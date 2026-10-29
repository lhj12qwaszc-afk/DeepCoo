using UnityEngine;
using UnityEngine.SceneManagement;

// The monster's hearing and sight. Hearing range depends on how loud the player is right now
// (FirstPersonController.CurrentNoise); sight needs the player in front and not behind a wall.
// Either one makes the monster chase. Getting caught restarts the level from scratch,
// which is the game's one-life rule.
public class MonsterSenses : MonoBehaviour
{
    public MonsterWander mover;
    // metres, indexed by FirstPersonController.NoiseLevel: Silent, Quiet, Normal, Loud
    public float[] hearingRange = { 0f, 3f, 9f, 20f };
    public float sightRange = 22f;
    public float sightAngle = 75f;      // half-angle either side of where it faces
    public float eyeHeight = 2.2f;
    public float catchDistance = 1.3f;

    FirstPersonController player;
    Transform playerEye;

    void Awake()
    {
        if (mover == null)
            mover = GetComponent<MonsterWander>();
    }

    void Start()
    {
        player = FindFirstObjectByType<FirstPersonController>();
        if (player != null)
            playerEye = player.cameraRoot != null ? player.cameraRoot : player.transform;
    }

    void Update()
    {
        if (player == null)
            return;

        var toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        if (mover.IsChasing && distance < catchDistance)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        if (CanHear(distance) || CanSee(toPlayer, distance))
            mover.Pursue(player.transform.position);
    }

    bool CanHear(float distance)
    {
        int level = (int)player.CurrentNoise;
        return level < hearingRange.Length && distance <= hearingRange[level];
    }

    bool CanSee(Vector3 toPlayer, float distance)
    {
        if (distance > sightRange || Vector3.Angle(transform.forward, toPlayer) > sightAngle)
            return false;
        var eye = transform.position + Vector3.up * eyeHeight;
        if (Physics.Linecast(eye, playerEye.position, out var hit, ~0, QueryTriggerInteraction.Ignore))
            return hit.transform == player.transform || hit.transform.IsChildOf(player.transform);
        return true;
    }
}
