using UnityEngine;
using UnityEngine.UI;

// The settings panel on the main menu (built by DeepCoo > Build Settings Panel, can be laid out
// by hand afterwards). Opening it shows the saved values. Moving a dial or the lever changes
// nothing yet: Save keeps the new values and closes the panel, Back closes it without saving,
// Reset puts every control back to the starting values (still to be saved).
// The red bar of each slider is an Image filled from the left up to the dial.
public class SettingsMenu : MonoBehaviour
{
    public Slider brightness, volume, micSensitivity;
    public Image brightnessFill, volumeFill, micSensitivityFill;
    public Toggle micOn;

    void OnEnable()
    {
        Show(GameSettings.Brightness, GameSettings.Volume, GameSettings.MicOn, GameSettings.MicSensitivity);
    }

    void Show(float bright, float vol, bool mic, float sens)
    {
        Hook(brightness, brightnessFill, bright);
        Hook(volume, volumeFill, vol);
        Hook(micSensitivity, micSensitivityFill, sens);
        if (micOn != null) micOn.isOn = mic;
    }

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
