using UnityEngine;
using UnityEngine.SceneManagement;

// The player's footsteps: one step each stride of ground covered, a random one of the steps cut out
// of the Footstep files, a little higher or lower each time. Quiet when crouching; none while
// running (it got in the way), hidden or standing still. Added to the player at run time.
public class Footsteps : MonoBehaviour
{
    const float Stride = 0.75f;   // metres between steps (walking)

    FirstPersonController fps;
    AudioSource source;
    Vector3 last;
    float travelled;
    System.Collections.Generic.List<AudioClip> steps;
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
        steps = new System.Collections.Generic.List<AudioClip>();
        var bank = Sfx.Bank;
        if (bank != null && bank.footsteps != null)
            foreach (var f in bank.footsteps)
                steps.AddRange(Sfx.Pieces(f));
    }

    void Update()
    {
        var now = transform.position;
        var moved = now - last;
        moved.y = 0f;
        last = now;
        bool running = fps.CurrentNoise == FirstPersonController.NoiseLevel.Loud;
        if (fps.IsHidden || fps.inputLocked || running || steps.Count == 0 || moved.magnitude > 2f)   // a teleport isn't a step; none while running
        {
            travelled = 0f;
            return;
        }
        bool crouching = fps.IsCrouching;
        bool sprinting = fps.CurrentNoise == FirstPersonController.NoiseLevel.Loud;
        travelled += moved.magnitude;
        float stride = crouching ? Stride * 0.8f : sprinting ? Stride * 1.4f : Stride;
        if (travelled < stride)
            return;
        travelled = 0f;
        int i = Random.Range(0, steps.Count);
        if (i == previous && steps.Count > 1) i = (i + 1) % steps.Count;
        previous = i;
        source.pitch = Random.Range(0.92f, 1.08f);
        source.PlayOneShot(steps[i], crouching ? 0.25f : sprinting ? 0.9f : 0.55f);
    }
}
