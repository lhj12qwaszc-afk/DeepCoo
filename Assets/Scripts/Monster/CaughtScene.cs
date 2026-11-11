using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Getting caught, seen through the player's eyes:
//   1. the monster's two upper tentacles shoot out and wrap round the player,
//   2. lift them high up, looking down at the monster's face,
//   3. fling them at the nearest wall,
//   4. the hit shakes the view, they drop to the floor and lie on their side (the flashlight
//      skitters away across the floor),
//   5. the screen slowly goes black and the level starts again (the game's one-life rule).
// The player can't move or look round meanwhile. Everything is procedural: the monster has no
// animation clips, so the tentacles are bent by TentacleWiggle's IK toward points set here,
// and the "player" is the camera (there is no body or arms model).
public class CaughtScene : MonoBehaviour
{
    public static bool Playing { get; private set; }

    const float GrabSeconds = 0.55f;
    const float LiftSeconds = 1.2f;
    const float HoldSeconds = 0.5f;
    const float ThrowSeconds = 0.32f;
    const float HitSeconds = 0.22f;
    const float FallSeconds = 0.5f;
    const float LieSeconds = 0.9f;
    const float FadeSeconds = 2.0f;
    const float HeadShare = 0.93f;      // the face's height, as a share of the monster's full height
    const float LiftClearance = 0.45f;  // the raised eye stays this far under the ceiling
    const float CeilingHeight = 4.5f;   // the maze's walls (MazeBuilder.WallHeight)
    const float WallGap = 0.35f;        // the eye stops this far off the wall
    const float LyingEyeHeight = 0.22f;

    MonsterSenses monster;
    FirstPersonController player;
    TentacleWiggle tentacles;
    float black;
    Transform root, eye;
    Vector3 eyeLocal;
    Vector3 shake;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init() => SceneManager.sceneLoaded += (s, m) => Playing = false;

    public static void Play(MonsterSenses monster, FirstPersonController player)
    {
        if (Playing)
            return;
        Playing = true;
        var scene = new GameObject("CaughtScene").AddComponent<CaughtScene>();
        scene.monster = monster;
        scene.player = player;
    }

    void Start() => StartCoroutine(Run());

    IEnumerator Run()
    {
        // ---- everything stops: the player's controls, every monster's walking and senses ----
        player.inputLocked = true;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        foreach (var mic in FindObjectsByType<MicrophoneNoise>(FindObjectsSortMode.None)) mic.enabled = false;
        foreach (var tr in FindObjectsByType<PositionTracker>(FindObjectsSortMode.None)) tr.enabled = false;   // it steers the flashlight hand
        foreach (var w in FindObjectsByType<MonsterWander>(FindObjectsSortMode.None)) w.enabled = false;
        foreach (var s in FindObjectsByType<MonsterSenses>(FindObjectsSortMode.None)) s.enabled = false;
        var agent = monster.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        tentacles = monster.GetComponent<TentacleWiggle>();

        root = player.transform;
        eye = player.cameraRoot != null ? player.cameraRoot : root;
        eyeLocal = eye == root ? Vector3.zero : eye.localPosition;
        var m = monster.transform;
        float size = m.lossyScale.y;
        float faceHeight = FaceHeight(m);
        Vector3 Face() => m.position + Vector3.up * faceHeight;

        // the monster squares up to the player
        var toPlayer = Flat(EyePos() - m.position);
        if (toPlayer.sqrMagnitude < 0.01f) toPlayer = -root.forward;
        toPlayer.Normalize();
        m.rotation = Quaternion.LookRotation(toPlayer);

        Shriek();

        // a dim light that travels with the eye, so the monster is seen even with the flashlight dead
        var glow = new GameObject("CaughtLight").AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.75f, 0.8f, 0.9f);
        glow.range = 6f;
        glow.intensity = 1.2f;
        glow.transform.SetParent(eye, false);
        glow.transform.localPosition = new Vector3(0f, 0.3f, 0.2f);

        // where the tentacle tips hold on: either side of the chest, just below the view
        Vector3 HoldPoint(float side)
        {
            var e = EyePos();
            var ahead = Flat(Face() - e).normalized;
            var right = Vector3.Cross(Vector3.up, ahead);
            return e + ahead * 0.3f + right * side * 0.36f + Vector3.down * 0.45f;   // in the bottom corners of the view
        }
        void Hold(float blend)
        {
            if (tentacles == null) return;
            tentacles.grabLeft = HoldPoint(-1f);
            tentacles.grabRight = HoldPoint(1f);
            tentacles.grabBlend = blend;
        }

        // ---- 1. grabbed: the head snaps to the monster as the tentacles whip round ----
        var startEye = EyePos();
        var startLook = eye.forward;
        for (float t = 0f; t < GrabSeconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / GrabSeconds);
            Hold(Mathf.Clamp01(t / (GrabSeconds * 0.7f)));
            SetEye(startEye, Vector3.Slerp(startLook, Face() - startEye, Mathf.Clamp01(k * 1.6f)), 0f, 0.02f * k);
            yield return null;
        }

        // ---- 2. lifted up, looking down at it ----
        float liftY = Mathf.Min(faceHeight + 0.9f, CeilingHeight - LiftClearance) + m.position.y;
        var liftEye = m.position + toPlayer * (0.8f * size) + Vector3.up * (liftY - m.position.y);
        // held up close above it: the gaze drops past the beak to its chest
        Vector3 Below() => m.position + Vector3.up * faceHeight * 0.62f;
        for (float t = 0f; t < LiftSeconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / LiftSeconds);
            var at = Vector3.Lerp(startEye, liftEye, k);
            float sway = Mathf.Sin(t * 9f) * 4f * (1f - k * 0.5f);
            SetEye(at, Vector3.Lerp(Face(), Below(), k) - at, sway, 0.015f);
            Hold(1f);
            yield return null;
        }
        for (float t = 0f; t < HoldSeconds; t += Time.deltaTime)
        {
            SetEye(liftEye, Below() - liftEye, Mathf.Sin(t * 7f) * 3f, 0.01f);
            Hold(1f);
            yield return null;
        }

        // ---- 3. thrown at the nearest wall ----
        Shriek();
        var (wallPoint, wallNormal) = FindWall(liftEye, toPlayer, m);
        var hitEye = wallPoint + wallNormal * WallGap;
        hitEye.y = Mathf.Clamp(liftY - 1.4f, 1.6f, liftY);
        var throwFrom = liftEye;
        var lookFrom = eye.forward;
        for (float t = 0f; t < ThrowSeconds; t += Time.deltaTime)
        {
            float k = t / ThrowSeconds;
            if (tentacles != null) tentacles.grabBlend = Mathf.Max(0f, 1f - k * 3f);   // let go at once
            var at = Vector3.Lerp(throwFrom, hitEye, k * k * (3f - 2f * k) * 0.3f + k * 0.7f);
            at += Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.4f;                         // a short arc
            SetEye(at, Vector3.Slerp(lookFrom, -wallNormal + Vector3.down * 0.15f, k), Mathf.Lerp(0f, 25f, k), 0.02f);
            yield return null;
        }
        if (tentacles != null) tentacles.grabBlend = 0f;

        // ---- 4. the hit ----
        Thud(1f, 0.55f);
        for (float t = 0f; t < HitSeconds; t += Time.deltaTime)
        {
            float k = 1f - t / HitSeconds;
            SetEye(hitEye, -wallNormal + Vector3.down * 0.15f, 25f, 0.12f * k);
            yield return null;
        }

        // ...dropping to the floor, ending on one side, facing back down the corridor
        float floor = FloorBelow(hitEye);
        var lieEye = hitEye + wallNormal * 0.25f;
        lieEye.y = floor + LyingEyeHeight;
        var towardMonster = Flat(m.position - lieEye);
        if (towardMonster.sqrMagnitude < 0.01f) towardMonster = wallNormal;
        // meanwhile the monster has drawn back a few steps: a shape standing further down the
        // corridor, the way the fallen player ends up facing
        var monsterFrom = m.position;
        var (monsterTo, lookDir) = StandOff(lieEye, towardMonster.normalized, m);
        towardMonster = lookDir;
        DropFlashlight(lieEye, towardMonster, floor);
        var fallFrom = hitEye;
        var fallLookFrom = eye.forward;
        for (float t = 0f; t < FallSeconds; t += Time.deltaTime)
        {
            float k = t / FallSeconds;
            m.position = Vector3.Lerp(monsterFrom, monsterTo, Mathf.SmoothStep(0f, 1f, k));
            m.rotation = Quaternion.LookRotation(Flat(lieEye - m.position).normalized);
            var at = Vector3.Lerp(fallFrom, lieEye, k * k);   // gravity
            SetEye(at, Vector3.Slerp(fallLookFrom, towardMonster.normalized + Vector3.up * 0.12f, Mathf.SmoothStep(0f, 1f, k)),
                   Mathf.Lerp(25f, 78f, k), 0.01f);
            yield return null;
        }
        Thud(0.8f, 0.7f);
        for (float t = 0f; t < 0.18f; t += Time.deltaTime)
        {
            SetEye(lieEye, towardMonster.normalized + Vector3.up * 0.12f, 78f, 0.05f * (1f - t / 0.18f));
            yield return null;
        }

        // ---- 5. lying there; everything goes dark ----
        glow.intensity = 0.6f;
        for (float t = 0f; t < LieSeconds; t += Time.deltaTime)
        {
            SetEye(lieEye, towardMonster.normalized + Vector3.up * 0.12f, 78f, 0f);
            yield return null;
        }
        float volume = AudioListener.volume;
        for (float t = 0f; t < FadeSeconds; t += Time.deltaTime)
        {
            black = t / FadeSeconds;
            AudioListener.volume = volume * (1f - black);
            yield return null;
        }
        black = 1f;
        yield return new WaitForSeconds(0.4f);
        AudioListener.volume = volume;
        RunStats.Deaths++;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ---------- the camera ----------

    Vector3 EyePos() => eye.position - shake;

    // puts the eye at a world point looking along dir, tilted sideways by roll degrees,
    // jolted at random by up to jolt metres
    void SetEye(Vector3 at, Vector3 dir, float roll, float jolt)
    {
        var flat = Flat(dir);
        if (flat.sqrMagnitude < 1e-4f) flat = root.forward;
        root.rotation = Quaternion.LookRotation(flat);
        float pitch = -Mathf.Atan2(dir.y, flat.magnitude) * Mathf.Rad2Deg;
        shake = Random.insideUnitSphere * jolt;
        root.position = at - root.rotation * eyeLocal + shake;
        if (eye != root)
            eye.localRotation = Quaternion.Euler(pitch, 0f, roll);
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    // ---------- the room ----------

    // the closest wall within reach, in any direction except back toward the monster
    (Vector3, Vector3) FindWall(Vector3 from, Vector3 toPlayer, Transform monsterRoot)
    {
        var origin = from;
        origin.y = Mathf.Max(from.y - 1.4f, 1.6f);
        float best = float.MaxValue;
        var point = origin + toPlayer * 3f;
        var normal = -toPlayer;
        for (int i = 0; i < 24; i++)
        {
            var dir = Quaternion.Euler(0f, i * 15f, 0f) * Vector3.forward;
            if (Vector3.Angle(dir, -toPlayer) < 70f)
                continue;   // not back into the monster
            foreach (var hit in Physics.RaycastAll(origin, dir, 8f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(monsterRoot) || hit.transform.IsChildOf(root))
                    continue;
                if (hit.distance < best && hit.distance > 0.6f)
                {
                    best = hit.distance;
                    point = hit.point;
                    normal = Flat(hit.normal).normalized;
                }
            }
        }
        if (normal.sqrMagnitude < 0.5f) normal = -toPlayer;
        return (point, normal);
    }

    // where the monster stands to watch the fallen player, and which way the player looks at it:
    // of the directions roughly toward the monster, the one with the most open floor (down the
    // corridor rather than across it), StandOffDistance along it or short of the next wall
    const float StandOffDistance = 4.5f;

    (Vector3, Vector3) StandOff(Vector3 lieEye, Vector3 toward, Transform monsterRoot)
    {
        var from = new Vector3(lieEye.x, monsterRoot.position.y + 1f, lieEye.z);
        float bestReach = -1f;
        var bestDir = toward;
        for (int i = 0; i < 24; i++)
        {
            var dir = Quaternion.Euler(0f, i * 15f, 0f) * Vector3.forward;
            if (Vector3.Angle(dir, toward) > 100f)
                continue;
            float reach = StandOffDistance;
            foreach (var hit in Physics.RaycastAll(from, dir, StandOffDistance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(monsterRoot) && !hit.transform.IsChildOf(root))
                    reach = Mathf.Min(reach, hit.distance - 0.9f);
            // prefer open floor, then the way it already is
            float score = reach + Vector3.Dot(dir, toward) * 0.5f;
            if (score > bestReach)
            {
                bestReach = score;
                bestDir = dir;
            }
        }
        float go = StandOffDistance;
        foreach (var hit in Physics.RaycastAll(from, bestDir, StandOffDistance, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(monsterRoot) && !hit.transform.IsChildOf(root))
                go = Mathf.Min(go, hit.distance - 0.9f);
        var at = from + bestDir * Mathf.Max(go, 1.5f);
        at.y = monsterRoot.position.y;
        return (at, bestDir);
    }

    float FloorBelow(Vector3 at)
    {
        foreach (var hit in Physics.RaycastAll(at, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(root) && hit.normal.y > 0.7f)
                return hit.point.y;
        return 0f;
    }

    static float FaceHeight(Transform m)
    {
        float top = 0f;
        foreach (var r in m.GetComponentsInChildren<Renderer>())
            top = Mathf.Max(top, r.bounds.max.y - m.position.y);
        var senses = m.GetComponent<MonsterSenses>();
        float eyeHeight = senses != null ? senses.eyeHeight * m.lossyScale.y : 2f;
        return top > eyeHeight ? top * HeadShare : eyeHeight;
    }

    // the flashlight flies out of the hand and ends up on the floor beside the player, still on
    void DropFlashlight(Vector3 lieEye, Vector3 toward, float floor)
    {
        var hand = eye.Find("FlashlightHand");
        if (hand == null || !hand.gameObject.activeInHierarchy)
            return;
        hand.SetParent(null, true);
        var side = Vector3.Cross(Vector3.up, toward);
        hand.position = new Vector3(lieEye.x, floor + 0.06f, lieEye.z) + toward * 1.0f + side * 0.35f;
        hand.rotation = Quaternion.LookRotation(Quaternion.Euler(0f, -10f, 0f) * toward, Vector3.up);   // its beam rolls round toward the monster
    }

    // ---------- sounds ----------

    static void Shriek()
    {
        var bank = Sfx.Bank;
        if (bank != null)
            Sfx.Play(bank.monsterAlert != null ? bank.monsterAlert : bank.monsterCoo, null, 1f, Random.Range(1.0f, 1.15f));
    }

    // a body hitting something hard: a footstep sound, pitched right down and played loud
    static void Thud(float volume, float pitch)
    {
        var bank = Sfx.Bank;
        if (bank == null || bank.footsteps == null || bank.footsteps.Length == 0)
            return;
        Sfx.Play(Sfx.First(bank.footsteps[0]), null, volume, pitch);
    }

    void OnGUI()
    {
        if (black <= 0f)
            return;
        GUI.color = new Color(0f, 0f, 0f, black);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
