using System.Collections;
using UnityEngine;

// A radio or birdcage that gives away one digit of the padlock code when used.
// The radio (digit set) crackles with static when switched on. With a voice for every digit
// (Radio_0..9 in the sound bank) it only speaks: 지지직.. 지직.. the digit once ... 지지직,
// heard from the radio. Until those voices exist, the paper shows the number spinning, slowing,
// then settling on its digit, as if the dial were being tuned in.
public class DigitSource : Interactable
{
    public string prompt;
    public string message;
    public float noiseRange;   // 0 = silent; the radio is loud enough for the monster to hear
    public int digit = -1;     // >= 0: tune in to this digit instead of showing message

    const float TuneSeconds = 1.8f;
    const float Range = 10f;   // metres the radio can be heard

    bool playing;

    public override string Prompt => prompt;
    public override bool CanUse => !playing;

    public override void Use(PlayerInteractor player)
    {
        if (noiseRange > 0f)
            NoiseEvents.Emit(transform.position, noiseRange);
        if (digit < 0)
        {
            player.ShowNote(message);
            return;
        }
        var bank = Sfx.Bank;
        var voice = bank != null && bank.radioDigits != null && bank.radioDigits.Length > digit ? bank.radioDigits[digit] : null;
        if (voice != null)
        {
            StartCoroutine(Speak(bank.radioStatic, voice));
            return;
        }
        if (bank != null && bank.radioStatic != null)
            Sfx.Play(bank.radioStatic, transform.position, StaticVolume, 1f, Range);
        var sheets = new[] { new PlayerInteractor.Note("", Screen(Random.Range(0, 10))) };
        player.ShowNotes(sheets);
        StartCoroutine(Tune(sheets));
    }

    static string Screen(int shown) => $"지지직…\n\n{shown}\n\n…지지직";

    const float StaticVolume = 1f;

    // 지지직.. 지직.. (the digit) ... 지지직
    IEnumerator Speak(AudioClip staticClip, AudioClip voice)
    {
        playing = true;
        yield return Hiss(staticClip, 0.9f);
        yield return new WaitForSeconds(0.3f);
        yield return Hiss(staticClip, 0.35f);
        yield return new WaitForSeconds(0.35f);
        Sfx.Play(voice, transform.position, 1f, 1f, Range);
        yield return new WaitForSeconds(voice.length + 0.4f);
        yield return Hiss(staticClip, 0.9f);
        playing = false;
    }

    // a burst of static this long, from a random spot in the clip, cut off with a quick fade
    IEnumerator Hiss(AudioClip clip, float seconds)
    {
        if (clip == null)
            yield break;
        var source = Sfx.Play(clip, transform.position, StaticVolume, 1f, Range);
        source.time = Random.Range(0f, Mathf.Max(0f, clip.length - seconds - 0.1f));
        yield return new WaitForSeconds(seconds);
        const float fade = 0.06f;
        for (float t = 0f; t < fade && source != null; t += Time.deltaTime)
        {
            source.volume = StaticVolume * (1f - t / fade);
            yield return null;
        }
        if (source != null)
            Destroy(source.gameObject);
    }

    // the number flicks through digits, quickly at first, slower and slower, and stops on the real one
    IEnumerator Tune(PlayerInteractor.Note[] sheets)
    {
        float t = 0f;
        int last = -1;
        while (t < TuneSeconds)
        {
            int shown;
            do shown = Random.Range(0, 10); while (shown == last);
            last = shown;
            sheets[0].text = Screen(shown);
            float wait = Mathf.Lerp(0.05f, 0.3f, t / TuneSeconds);
            t += wait;
            yield return new WaitForSeconds(wait);
        }
        sheets[0].text = Screen(digit);
    }
}
