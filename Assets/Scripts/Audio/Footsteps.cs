using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The player's footsteps: one step each stride of ground covered, a random one of the steps cut out
// of the files, a little higher or lower each time.
//   walking   - Footstep_*
//   crouching - Footstep_Crouch_* (none yet: the walking steps, softly)
//   running   - Footstep_Run_*    (none yet: silent, as the walking steps got in the way when sped up)
// None while hidden or standing still. Added to the player at run time.
public class Footsteps : MonoBehaviour
{
    const float Stride = 0.75f;   // metres between steps (walking)
    const float WalkVolume = 0.55f, CrouchVolume = 0.4f, RunVolume = 0.8f;
    const float CrouchStandInVolume = 0.25f;

    FirstPersonController fps;
    AudioSource source;
    Vector3 last;
    float travelled;
    List<AudioClip> walk, crouch, run;
    int previous = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded += (scene, mode) => Attach();
        Attach();
    }

    static void Attach()
    {
        var player = FindFirstObjectByType<FirstPersonController>();
        if (player != null && player.GetComponent<Footsteps>() == null)
            player.gameObject.AddComponent<Footsteps>();
    }

    void Start()
    {
        fps = GetComponent<FirstPersonController>();
        source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        last = transform.position;
        var bank = Sfx.Bank;
        walk = Steps(bank != null ? bank.footsteps : null);
        crouch = Steps(bank != null ? bank.footstepsCrouch : null);
        Loudest(crouch, CrouchPeak);   // the crouching file came out far quieter than the others
        run = Steps(bank != null ? bank.footstepsRun : null);
    }

    // every single step in the files (each generated file holds a whole walk)
    static List<AudioClip> Steps(AudioClip[] files)
    {
        var list = new List<AudioClip>();
        if (files != null)
            foreach (var f in files)
                foreach (var piece in Sfx.Pieces(f))
                    if (piece.length >= 0.06f)
                        list.Add(piece);
        return list;
    }

    // brings a set of steps up (or down) so its loudest step peaks at the given level
    const float CrouchPeak = 0.45f;

    static void Loudest(List<AudioClip> set, float peak)
    {
        float top = 0f;
        var data = new List<float[]>();
        foreach (var c in set)
        {
            var d = new float[c.samples * c.channels];
            c.GetData(d, 0);
            data.Add(d);
            foreach (var x in d) top = Mathf.Max(top, Mathf.Abs(x));
        }
        if (top < 1e-4f)
            return;
        float gain = peak / top;
        for (int i = 0; i < set.Count; i++)
        {
            var d = data[i];
            for (int k = 0; k < d.Length; k++) d[k] *= gain;
            set[i].SetData(d, 0);
        }
    }

    void Update()
    {
        var now = transform.position;
        var moved = now - last;
        moved.y = 0f;
        last = now;
        bool running = fps.CurrentNoise == FirstPersonController.NoiseLevel.Loud;
        bool crouching = fps.IsCrouching;
        var set = running ? run : crouching && crouch.Count > 0 ? crouch : walk;
        if (fps.IsHidden || fps.inputLocked || set.Count == 0 || moved.magnitude > 2f)   // a teleport isn't a step
        {
            travelled = 0f;
            return;
        }
        travelled += moved.magnitude;
        float stride = crouching ? Stride * 0.8f : running ? Stride * 1.4f : Stride;
        if (travelled < stride)
            return;
        travelled = 0f;
        int i = Random.Range(0, set.Count);
        if (i == previous && set.Count > 1) i = (i + 1) % set.Count;
        previous = i;
        source.pitch = Random.Range(0.92f, 1.08f);
        float volume = running ? RunVolume : crouching ? (crouch.Count > 0 ? CrouchVolume : CrouchStandInVolume) : WalkVolume;
        source.PlayOneShot(set[i], volume);
    }
}
