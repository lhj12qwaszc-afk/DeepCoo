using UnityEngine;

// Battery for the player's flashlight. Drains while the light is on; below the low mark the
// beam dims and flickers, and at zero it goes dark until a battery is picked up.
// Shows a small battery gauge in the bottom-left corner.
public class FlashlightBattery : MonoBehaviour
{
    public Light flashlight;
    [Range(0f, 1f)] public float charge = 1f;
    public float secondsOfLight = 150f;   // a full battery lasts this long
    public float lowCharge = 0.25f;

    float baseIntensity;
    float flickerTimer;
    float flickerFactor = 1f;

    void Awake()
    {
        if (flashlight == null)
            flashlight = GetComponentInChildren<Light>();
        baseIntensity = flashlight.intensity;
    }

    public void AddCharge(float amount)
    {
        charge = Mathf.Clamp01(charge + amount);
    }

    void Update()
    {
        if (flashlight.enabled)
            charge = Mathf.Max(0f, charge - Time.deltaTime / secondsOfLight);

        float level = charge >= lowCharge ? 1f : Mathf.Lerp(0.25f, 1f, charge / lowCharge);
        if (charge <= 0f)
        {
            level = 0f;
        }
        else if (charge < lowCharge)
        {
            // short random dropouts, more often the emptier the battery gets
            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0f)
            {
                bool drop = Random.value < Mathf.Lerp(0.6f, 0.15f, charge / lowCharge);
                flickerFactor = drop ? Random.Range(0f, 0.3f) : 1f;
                flickerTimer = drop ? Random.Range(0.03f, 0.12f) : Random.Range(0.2f, 1.2f);
            }
            level *= flickerFactor;
        }
        flashlight.intensity = baseIntensity * level;
    }

    void OnGUI()
    {
        const float x = 20f, y = 20f, w = 70f, h = 18f;
        float top = Screen.height - y - h;
        GUI.color = new Color(1f, 1f, 1f, 0.6f);
        GUI.DrawTexture(new Rect(x - 2f, top - 2f, w + 4f, h + 4f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + w + 2f, top + 5f, 4f, h - 10f), Texture2D.whiteTexture);
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(x, top, w, h), Texture2D.whiteTexture);
        GUI.color = charge < lowCharge ? new Color(0.9f, 0.2f, 0.15f) : new Color(0.85f, 0.85f, 0.75f);
        GUI.DrawTexture(new Rect(x + 2f, top + 2f, (w - 4f) * charge, h - 4f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
