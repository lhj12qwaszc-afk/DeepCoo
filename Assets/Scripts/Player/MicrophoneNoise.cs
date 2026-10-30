using UnityEngine;
using UnityEngine.InputSystem;

// Listens to the player's real microphone. The louder they are, the further away the monster
// hears it: a whisper carries a few metres, a shout carries across the maze. Even inside a
// hiding spot a loud voice draws the monster to it (though it still can't get in).
// M toggles the microphone. Shows a small level meter next to the battery gauge.
public class MicrophoneNoise : MonoBehaviour
{
    public float quietDb = -45f;          // at or below this the monster hears nothing
    public float loudDb = -8f;            // this loud carries the full range
    public float minRange = 3f;           // metres, just above quietDb
    public float maxRange = 30f;          // metres, at loudDb
    public float emitInterval = 0.25f;
    [Tooltip("For testing without a microphone: 0..1 fakes the input level, -1 uses the mic.")]
    public float testLevel = -1f;

    public float Level { get; private set; }      // 0..1, smoothed
    public float HearingRange { get; private set; }
    public bool HasMicrophone => device != null;

    const int SampleRate = 16000;
    const int Window = 512;
    string device;
    AudioClip clip;
    readonly float[] samples = new float[Window];
    float emitTimer;
    bool muted;

    void Start()
    {
        if (Microphone.devices.Length > 0)
        {
            device = Microphone.devices[0];
            clip = Microphone.Start(device, true, 1, SampleRate);
        }
    }

    void OnDestroy()
    {
        if (device != null)
            Microphone.End(device);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            muted = !muted;

        float raw = muted ? 0f : testLevel >= 0f ? testLevel : ReadMicLevel();
        // rise fast, fall slowly, so a shout lingers on the meter
        Level = raw > Level ? raw : Mathf.MoveTowards(Level, raw, Time.deltaTime * 1.5f);
        HearingRange = Level > 0f ? Mathf.Lerp(minRange, maxRange, Level) : 0f;

        emitTimer -= Time.deltaTime;
        if (HearingRange > 0f && emitTimer <= 0f)
        {
            emitTimer = emitInterval;
            NoiseEvents.Emit(transform.position, HearingRange);
        }
    }

    float ReadMicLevel()
    {
        if (clip == null)
            return 0f;
        int pos = Microphone.GetPosition(device) - Window;
        if (pos < 0)
            return 0f;
        clip.GetData(samples, pos);
        float sum = 0f;
        foreach (var s in samples)
            sum += s * s;
        float rms = Mathf.Sqrt(sum / Window);
        float db = 20f * Mathf.Log10(Mathf.Max(rms, 1e-6f));
        return Mathf.Clamp01(Mathf.InverseLerp(quietDb, loudDb, db));
    }

    void OnGUI()
    {
        const float x = 110f, w = 60f, h = 10f;
        float top = Screen.height - 20f - 14f;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        if (!HasMicrophone && testLevel < 0f)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.Label(new Rect(x, top - 4f, 120f, 22f), "마이크 없음", style);
            GUI.color = Color.white;
            return;
        }
        GUI.color = new Color(1f, 1f, 1f, 0.6f);
        GUI.Label(new Rect(x, top - 4f, 40f, 22f), muted ? "끔" : "MIC", style);
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x + 30f, top, w, h), Texture2D.whiteTexture);
        // red once it is loud enough for the monster to hear
        GUI.color = Level > 0f ? Color.Lerp(new Color(1f, 0.8f, 0.3f), new Color(0.9f, 0.15f, 0.1f), Level) : Color.gray;
        GUI.DrawTexture(new Rect(x + 30f, top, w * Level, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
