using UnityEngine;
using UnityEngine.SceneManagement;

// When the monster comes to peer into the hiding spot the player is in, it grabs the spot and
// shakes it, only to scare: the locker / wardrobe / bin rattles, the hidden player's view shakes
// with it, and the rattle sound (Hide_Rattle) plays. It still never finds the player and leaves.
// The shaking follows the sound's loudness, so every bang in the file is a jolt on screen.
// Added to the monster at run time.
public class HideRattle : MonoBehaviour
{
    const float StartAfter = 1.0f;     // seconds of peering before it grabs the spot
    const float SpotTilt = 2.5f;       // degrees the spot rocks at the loudest bang
    const float SpotShift = 0.035f;    // metres
    const float ViewShift = 0.09f;     // metres the hidden player's view jolts
    const float ViewTilt = 3f;         // degrees
    const float SlatJump = 0.03f;      // share of the screen the slatted door overlay jumps
    const float Volume = 1f;
    const float Range = 25f;

    MonsterWander mover;
    MonsterGait gait;
    FirstPersonController player;
    PlayerInteractor interactor;

    float[] envelope;            // the sound's loudness, 0..1, every EnvelopeStep seconds
    const float EnvelopeStep = 0.02f;
    float clipLength;

    float inspectTime;
    bool rattled;                // already shaken during this visit
    float rattleTime = -1f;      // seconds into the shaking, -1 when not shaking
    AudioSource sound;
    Transform spot;
    Vector3 spotPosition;
    Quaternion spotRotation;
    Vector3 viewOffset;
    Quaternion viewTilt = Quaternion.identity;

    public float Shake { get; private set; }   // 0..1 right now (MonsterGait jerks the arms with it)
    public bool Holding => rattleTime >= 0f;   // its hands are on the spot (MonsterGait reaches out)

    // How far the slatted door drawn over the hidden player's screen is knocked this frame, as a
    // share of the screen (PlayerInteractor.DrawSlats): the door is right in front of the eyes,
    // so it jumps much more than the view behind it.
    public static Vector2 SlatJolt { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded += (scene, mode) => Attach();
        Attach();
    }

    static void Attach()
    {
        foreach (var w in FindObjectsByType<MonsterWander>(FindObjectsSortMode.None))
            if (w.GetComponent<HideRattle>() == null)
                w.gameObject.AddComponent<HideRattle>();
    }

    void Start()
    {
        mover = GetComponent<MonsterWander>();
        gait = GetComponent<MonsterGait>();
        player = FindFirstObjectByType<FirstPersonController>();
        if (player != null)
            interactor = player.GetComponent<PlayerInteractor>();
        BuildEnvelope(Sfx.Bank != null ? Sfx.Bank.hideRattle : null);
    }

    void BuildEnvelope(AudioClip clip)
    {
        clipLength = 2.5f;
        if (clip == null || !clip.LoadAudioData() || clip.samples == 0)
            return;   // no sound file: shake in three bursts instead (see Level)
        var data = new float[clip.samples * clip.channels];
        if (!clip.GetData(data, 0))
            return;
        clipLength = clip.length;
        int window = Mathf.Max(1, Mathf.RoundToInt(EnvelopeStep * clip.frequency) * clip.channels);
        envelope = new float[data.Length / window + 1];
        float peak = 0f;
        for (int w = 0; w < envelope.Length; w++)
        {
            float sum = 0f;
            int from = w * window, to = Mathf.Min(data.Length, from + window);
            for (int i = from; i < to; i++)
                sum += data[i] * data[i];
            envelope[w] = to > from ? Mathf.Sqrt(sum / (to - from)) : 0f;
            peak = Mathf.Max(peak, envelope[w]);
        }
        if (peak > 0f)
            for (int w = 0; w < envelope.Length; w++)
                envelope[w] = Mathf.Clamp01(envelope[w] / peak);
    }

    // how hard to shake t seconds into the sound
    float Level(float t)
    {
        if (envelope != null)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt(t / EnvelopeStep), 0, envelope.Length - 1);
            float v = envelope[i];
            return v < 0.12f ? 0f : v;   // quiet tails don't shake
        }
        // stand-in: three jolts
        float phase = t % 0.8f;
        return phase < 0.35f ? Mathf.Sin(phase / 0.35f * Mathf.PI) : 0f;
    }

    void Update()
    {
        if (mover == null || player == null)
            return;

        var hiddenIn = player.IsHidden && interactor != null ? interactor.HiddenIn : null;
        if (!mover.IsInspecting || hiddenIn == null)
        {
            Stop();
            inspectTime = 0f;
            rattled = false;
            return;
        }

        inspectTime += Time.deltaTime;
        if (!rattled && inspectTime >= StartAfter)
            Begin(hiddenIn.transform);

        if (rattleTime < 0f)
            return;
        rattleTime += Time.deltaTime;
        if (rattleTime >= clipLength)
        {
            Stop();
            return;
        }
        mover.HoldInspect(0.8f);   // stays until it lets go, then a moment longer

        Shake = Level(rattleTime);
        float t = Time.time;
        // fast, rough jitter scaled by how loud the sound is right now
        var tilt = new Vector3(Noise(t, 1f), Noise(t, 2f) * 0.5f, Noise(t, 3f)) * SpotTilt * Shake;
        spot.SetPositionAndRotation(
            spotPosition + new Vector3(Noise(t, 4f), 0f, Noise(t, 5f)) * SpotShift * Shake,
            spotRotation * Quaternion.Euler(tilt));
    }

    // The hidden player's view jolts with the spot (after FirstPersonController has set it).
    void LateUpdate()
    {
        if (player == null || player.cameraRoot == null)
            return;
        var cam = player.cameraRoot;
        // take off last frame's jolt (unless the controller has set the view afresh since), then add this frame's
        if (cam.localPosition == joltedPosition)
            cam.localPosition -= viewOffset;
        if (cam.localRotation == joltedRotation)
            cam.localRotation = cam.localRotation * Quaternion.Inverse(viewTilt);
        if (rattleTime >= 0f && player.IsHidden)
        {
            float t = Time.time;
            viewOffset = new Vector3(Noise(t, 6f), Noise(t, 7f), Noise(t, 8f) * 0.5f) * ViewShift * Shake;
            viewTilt = Quaternion.Euler(Noise(t, 9f) * ViewTilt * Shake, Noise(t, 10f) * ViewTilt * Shake, Noise(t, 11f) * ViewTilt * 1.5f * Shake);
            SlatJolt = new Vector2(Noise(t, 12f), Noise(t, 13f)) * SlatJump * Shake;
        }
        else
        {
            viewOffset = Vector3.zero;
            viewTilt = Quaternion.identity;
            SlatJolt = Vector2.zero;
        }
        cam.localPosition += viewOffset;
        cam.localRotation = cam.localRotation * viewTilt;
        joltedPosition = cam.localPosition;
        joltedRotation = cam.localRotation;
    }

    Vector3 joltedPosition;
    Quaternion joltedRotation;

    static float Noise(float t, float seed) => Mathf.PerlinNoise(t * 22f, seed * 7.3f) * 2f - 1f;

    void Begin(Transform hidingSpot)
    {
        rattled = true;
        rattleTime = 0f;
        spot = hidingSpot;
        spotPosition = spot.position;
        spotRotation = spot.rotation;
        var clip = Sfx.Bank != null ? Sfx.Bank.hideRattle : null;
        sound = Sfx.Play(clip, spot.position, Volume, 1f, Range);
    }

    void Stop()
    {
        if (spot != null)
            spot.SetPositionAndRotation(spotPosition, spotRotation);
        spot = null;
        rattleTime = -1f;
        Shake = 0f;
        SlatJolt = Vector2.zero;
        if (sound != null)
            sound.Stop();
        sound = null;
    }

    void OnDisable() => Stop();
}
