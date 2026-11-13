using UnityEngine;
using UnityEngine.SceneManagement;

// The monster's footfalls, heard from where it is, one per foot landing in MonsterGait's walk
// cycle: slow heavy steps while it roams (Monster_Walk_*), fast pounding ones while it chases
// (Monster_Run_*), each file cut into its separate steps. Until those files exist it uses the player's footstep sounds, pitched right
// down and played heavier, so the monster can already be heard coming.
// Added to the monster at run time.
public class MonsterSteps : MonoBehaviour
{
    const float Range = 22f;          // metres its steps can be heard
    const float WalkVolume = 0.55f;
    const float RunVolume = 0.9f;
    const float StandInPitch = 0.55f; // the player's steps, made big and heavy

    MonsterGait gait;
    MonsterWander mover;
    float lastHalfStep;
    int lastPick = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded += (scene, mode) => Attach();
        Attach();
    }

    static void Attach()
    {
        foreach (var g in FindObjectsByType<MonsterGait>(FindObjectsSortMode.None))
            if (g.GetComponent<MonsterSteps>() == null)
                g.gameObject.AddComponent<MonsterSteps>();
    }

    void Start()
    {
        gait = GetComponent<MonsterGait>();
        mover = GetComponent<MonsterWander>();
        lastHalfStep = Mathf.Floor(gait.Phase / Mathf.PI);
    }

    void LateUpdate()
    {
        if (gait == null || CaughtScene.Playing)
            return;
        float halfStep = Mathf.Floor(gait.Phase / Mathf.PI);
        if (halfStep == lastHalfStep)
            return;
        lastHalfStep = halfStep;
        if (mover != null && mover.CurrentSpeed < 0.2f)
            return;   // shuffling on the spot, kneeling at a hiding spot: no footfall
        if (gait.FloatBlend > 0.5f)
            return;   // floating while it roams: its feet don't touch the floor
        Step(gait.RunBlend > 0.5f);
    }

    void Step(bool running)
    {
        var bank = Sfx.Bank;
        if (bank == null)
            return;
        var own = running ? bank.monsterRun : bank.monsterWalk;
        float volume = running ? RunVolume : WalkVolume;
        float pitch = Random.Range(0.93f, 1.07f);
        var pool = Pool(own);
        if (pool.Count == 0)
        {
            pool = Pool(bank.footsteps);   // stand-in until Monster_Walk_ / Monster_Run_ files are added
            pitch *= running ? StandInPitch * 1.1f : StandInPitch;
        }
        if (pool.Count == 0)
            return;
        int pick;
        do pick = Random.Range(0, pool.Count); while (pool.Count > 1 && pick == lastPick);
        lastPick = pick;
        Sfx.Play(pool[pick], transform.position, volume, pitch, Range);
    }

    // Every single footfall in the files: the generated files hold a whole walk or run, so each
    // one is cut into its separate steps (Sfx.Pieces), and any scrap too short to be a step is dropped.
    const float ShortestStep = 0.09f;
    static readonly System.Collections.Generic.Dictionary<AudioClip[], System.Collections.Generic.List<AudioClip>> pools =
        new System.Collections.Generic.Dictionary<AudioClip[], System.Collections.Generic.List<AudioClip>>();

    static System.Collections.Generic.List<AudioClip> Pool(AudioClip[] files)
    {
        var list = new System.Collections.Generic.List<AudioClip>();
        if (files == null || files.Length == 0)
            return list;
        if (pools.TryGetValue(files, out var done))
            return done;
        foreach (var f in files)
            foreach (var piece in Sfx.Pieces(f))
                if (piece.length >= ShortestStep)
                    list.Add(piece);
        pools[files] = list;
        return list;
    }
}
