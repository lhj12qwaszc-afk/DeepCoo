using UnityEngine;
using UnityEngine.SceneManagement;

// The monster's hearing and sight. Hearing range depends on how loud the player is right now
// (FirstPersonController.CurrentNoise); sight needs the player in front and not behind a wall.
// Either one makes the monster chase, as do loud one-off noises (NoiseEvents) within their
// range. A player hiding in a locker, wardrobe or bin can't be seen, heard or caught; if the
// monster was after them, it comes to the hiding spot, crouches to peer in, then leaves.
// Getting caught restarts the level from scratch, which is the game's one-life rule.
public class MonsterSenses : MonoBehaviour
{
    public MonsterWander mover;
    // metres, indexed by FirstPersonController.NoiseLevel: Silent, Quiet, Normal, Loud
    public float[] hearingRange = { 0f, 3f, 9f, 20f };
    public float sightRange = 22f;
    public float sightAngle = 75f;      // half-angle either side of where it faces
    public float eyeHeight = 2.2f;      // at scale 1; grows with the monster
    public float catchDistance = 1.3f;  // at scale 1; a bigger monster grabs from further away

    public float inspectDistance = 1.2f;   // at scale 1, in front of the hiding spot's door

    FirstPersonController player;
    PlayerInteractor interactor;
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
        {
            playerEye = player.cameraRoot != null ? player.cameraRoot : player.transform;
            interactor = player.GetComponent<PlayerInteractor>();
        }
    }

    void OnEnable() => NoiseEvents.Emitted += OnNoise;
    void OnDisable() => NoiseEvents.Emitted -= OnNoise;

    void OnNoise(Vector3 position, float range)
    {
        var offset = position - transform.position;
        offset.y = 0f;
        if (offset.magnitude <= range)
            mover.Pursue(position);
    }

    void Update()
    {
        if (player == null)
            return;
        if (player.IsHidden)
        {
            // It was coming for the player (or heard them from inside): go and look at the spot.
            var spot = interactor != null ? interactor.HiddenIn : null;
            if (mover.IsChasing && spot != null)
            {
                var s = spot.transform;
                var stand = s.position + s.forward * (s.localScale.z / 2f + inspectDistance * transform.lossyScale.y);
                stand.y = transform.position.y;
                mover.Investigate(stand, s.position);
            }
            return;
        }

        var toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        if (mover.IsChasing && distance < catchDistance * transform.lossyScale.y)
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
        var eye = transform.position + Vector3.up * eyeHeight * transform.lossyScale.y;
        if (Physics.Linecast(eye, playerEye.position, out var hit, ~0, QueryTriggerInteraction.Ignore))
            return hit.transform == player.transform || hit.transform.IsChildOf(player.transform);
        return true;
    }
}
