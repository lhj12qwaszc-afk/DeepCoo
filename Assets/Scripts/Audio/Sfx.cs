using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Plays the game's sounds (from the SoundBank). Some generated files hold the sound several times
// over (a click clicked eight times, a walk of seven steps): First() cuts out just the first one,
// Pieces() each one separately, both worked out once from the waveform.
// Every button in every scene gets its hover and click sounds automatically.
public static class Sfx
{
    static SoundBank bank;
    public static SoundBank Bank => bank != null ? bank : bank = Resources.Load<SoundBank>("SoundBank");



    // a one-off sound at a place in the world (heard from where it is), or 2D when at is null
    public static AudioSource Play(AudioClip clip, Vector3? at = null, float volume = 1f, float pitch = 1f, float range = 25f)
    {
        if (clip == null)
            return null;
        var go = new GameObject("Sound " + clip.name);
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        if (at.HasValue)
        {
            go.transform.position = at.Value;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = range;
        }
        source.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        return source;
    }

    static readonly Dictionary<float, AudioSource> uiByPitch = new Dictionary<float, AudioSource>();

    // a menu sound: 2D and not stopped by the pause; pitch below 1 makes it lower and heavier
    public static void PlayUI(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null)
            return;
        if (!uiByPitch.TryGetValue(pitch, out var source) || source == null)
        {
            var go = new GameObject($"UI Sounds {pitch}");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.ignoreListenerPause = true;
            source.pitch = pitch;
            uiByPitch[pitch] = source;
        }
        source.PlayOneShot(clip, volume);
    }

    // ---------- cutting a file down to its sounds ----------

    static readonly Dictionary<AudioClip, List<AudioClip>> pieces = new Dictionary<AudioClip, List<AudioClip>>();

    public static AudioClip First(AudioClip clip)
    {
        var all = Pieces(clip);
        return all.Count > 0 ? all[0] : clip;
    }

    // each separate sound in the file: where it rises above a tenth of the loudest, until it has
    // stayed quiet for a tenth of a second
    public static List<AudioClip> Pieces(AudioClip clip)
    {
        if (clip == null)
            return new List<AudioClip>();
        if (pieces.TryGetValue(clip, out var done))
            return done;
        var result = new List<AudioClip>();
        pieces[clip] = result;
        var data = new float[clip.samples * clip.channels];
        if (!clip.GetData(data, 0))
        {
            result.Add(clip);
            return result;
        }
        int ch = clip.channels, rate = clip.frequency, frames = clip.samples;
        float peak = 0f;
        foreach (var s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
        float threshold = peak * 0.1f;
        int quietGap = rate / 10, lead = rate / 100;
        int start = -1, lastLoud = -1;
        for (int f = 0; f < frames; f++)
        {
            float a = 0f;
            for (int c = 0; c < ch; c++) a = Mathf.Max(a, Mathf.Abs(data[f * ch + c]));
            if (a > threshold)
            {
                if (start < 0) start = Mathf.Max(0, f - lead);
                lastLoud = f;
            }
            else if (start >= 0 && f - lastLoud > quietGap)
            {
                result.Add(Cut(clip, data, start, lastLoud + quietGap / 2, result.Count));
                start = -1;
            }
        }
        if (start >= 0)
            result.Add(Cut(clip, data, start, Mathf.Min(frames - 1, lastLoud + quietGap / 2), result.Count));
        if (result.Count == 0)
            result.Add(clip);
        return result;
    }

    static AudioClip Cut(AudioClip clip, float[] data, int from, int to, int index)
    {
        int ch = clip.channels, length = Mathf.Max(1, to - from);
        var part = new float[length * ch];
        System.Array.Copy(data, from * ch, part, 0, part.Length);
        // a short fade at the end so it doesn't click off
        int fade = Mathf.Min(length, clip.frequency / 50);
        for (int f = 0; f < fade; f++)
            for (int c = 0; c < ch; c++)
                part[(length - 1 - f) * ch + c] *= f / (float)fade;
        var cut = AudioClip.Create($"{clip.name}_{index}", length, ch, clip.frequency, false);
        cut.SetData(part, 0);
        return cut;
    }

    // ---------- buttons ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded += (scene, mode) => HookButtons();
        HookButtons();
    }

    static void HookButtons()
    {
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.GetComponent<ButtonSounds>() == null)
                b.gameObject.AddComponent<ButtonSounds>();
    }
}

// hover and click sounds on a button
public class ButtonSounds : MonoBehaviour, IPointerEnterHandler
{
    // under the mouse: the same clunk as a click, only softer, so the two belong together
    void Start() => GetComponent<Button>().onClick.AddListener(() =>
    {
        Sfx.PlayUI(Sfx.First(Sfx.Bank?.buttonClick), 0.8f);
        // a clicked button stays "selected" and kept its red-rim look; the menus are mouse-only,
        // so let go of it once it's been pressed
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);
    });

    public void OnPointerEnter(PointerEventData e)
    {
        var b = GetComponent<Button>();
        if (b != null && b.interactable)
            Sfx.PlayUI(Sfx.First(Sfx.Bank?.buttonClick), 0.35f);
    }
}
