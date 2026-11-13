using UnityEngine;
using UnityEngine.UI;

// The settings panel on the main menu (built by DeepCoo > Build Settings Panel, can be laid out
// by hand afterwards). Opening it shows the saved values. Moving a dial or the lever changes
// at once as a preview (volume, brightness, sensitivity): Save keeps the new values and
// closes the panel, Back closes it without saving (the old values come back),
// Reset puts every control back to the starting values (still to be saved).
// The red bar of each slider is an Image filled from the left up to the dial.
public class SettingsMenu : MonoBehaviour
{
    public Slider brightness, volume, micSensitivity;
    public Image brightnessFill, volumeFill, micSensitivityFill;
    public Toggle micOn;

    void OnEnable()
    {
        foreach (var s in new[] { brightness, volume, micSensitivity })
            NarrowHitArea(s);
        Show(GameSettings.Brightness, GameSettings.Volume, GameSettings.MicOn, GameSettings.MicSensitivity);
    }

    // The slider pictures have tall see-through margins, so a slider's own box reaches into the rows
    // above and below and a click meant for one slider grabbed its neighbour. Only a thin strip
    // along the bar (and the dial itself) takes clicks now.
    const float HitHeight = 0.32f;   // share of the slider box's height that is clickable

    static void NarrowHitArea(Slider slider)
    {
        if (slider == null || slider.transform.Find("HitArea") != null)
            return;
        foreach (var img in slider.GetComponentsInChildren<Image>(true))
            if (slider.handleRect == null || img.transform != slider.handleRect)
                img.raycastTarget = false;
        var hit = new GameObject("HitArea", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        hit.SetParent(slider.transform, false);
        hit.SetAsFirstSibling();
        hit.anchorMin = new Vector2(0f, 0.5f - HitHeight / 2f);
        hit.anchorMax = new Vector2(1f, 0.5f + HitHeight / 2f);
        hit.offsetMin = hit.offsetMax = Vector2.zero;
        var image = hit.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);   // invisible, but it catches the mouse
        if (slider.handleRect != null)
        {
            // the dial too only as tall as the strip
            var h = slider.handleRect.GetComponent<Image>();
            if (h != null) h.raycastTarget = false;
        }
        slider.targetGraphic = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : image;
    }

    void Show(float bright, float vol, bool mic, float sens)
    {
        Hook(brightness, brightnessFill, bright);
        Hook(volume, volumeFill, vol);
        Hook(micSensitivity, micSensitivityFill, sens);
        if (micOn != null) micOn.isOn = mic;
        // every dial is tried out straight away while it moves (Save keeps it, Back undoes it)
        AudioListener.volume = vol;
        GameSettings.PreviewBrightness(bright);
        GameSettings.PreviewSensitivity(sens);
        if (volume != null) volume.onValueChanged.AddListener(v => AudioListener.volume = v);
        if (brightness != null) brightness.onValueChanged.AddListener(GameSettings.PreviewBrightness);
        if (micSensitivity != null) micSensitivity.onValueChanged.AddListener(GameSettings.PreviewSensitivity);
    }

    // closing the panel (Back, or after Save) leaves the saved values in use
    void OnDisable() => GameSettings.EndPreview();

    static void Hook(Slider slider, Image fill, float value)
    {
        if (slider == null)
            return;
        slider.onValueChanged.RemoveAllListeners();
        slider.value = value;
        if (fill != null)
        {
            fill.fillAmount = Fill(value);
            slider.onValueChanged.AddListener(v => fill.fillAmount = Fill(v));
        }
    }

    // the bar's pointed ends take up a little of the picture on each side
    static float Fill(float v) => Mathf.Lerp(0.03f, 0.97f, v);

    public void Save()
    {
        if (brightness != null) GameSettings.Brightness = brightness.value;
        if (volume != null) GameSettings.Volume = volume.value;
        if (micSensitivity != null) GameSettings.MicSensitivity = micSensitivity.value;
        if (micOn != null) GameSettings.MicOn = micOn.isOn;
        // the panel's dimmed backdrop is its parent: hide the whole thing
        (transform.parent != null ? transform.parent.gameObject : gameObject).SetActive(false);
    }

    public void ResetToDefaults()
    {
        Show(GameSettings.DefaultBrightness, GameSettings.DefaultVolume, GameSettings.DefaultMicOn, GameSettings.DefaultMicSensitivity);
    }
}
