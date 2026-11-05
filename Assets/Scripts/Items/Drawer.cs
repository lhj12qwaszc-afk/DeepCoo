using UnityEngine;

// A chest of drawers against a wall. E pulls the top drawer out; if a spare battery was put in it
// (BatterySpawner picks a few drawers each run) E again takes it and tops up the flashlight; once
// it's empty E pushes it shut again (and it can be reopened). Opening and closing are silent. The drawer that slides is the child "Tray"; anything put in it rides along.
public class Drawer : Interactable
{
    public float slide = 0.3f;        // how far the drawer comes out
    public float openSpeed = 2.5f;    // drawer lengths per second
    public float charge = 0.5f;       // how much a battery refills

    Transform tray;
    Vector3 closedPosition;
    GameObject battery;
    bool open;
    float shown;          // 0 closed .. 1 fully out
    string toast;
    float toastUntil;

    public override string Prompt => !open ? "서랍 열기" : battery != null ? "배터리 꺼내기" : "서랍 닫기";

    void Awake()
    {
        tray = transform.Find("Tray");
        closedPosition = tray.localPosition;
    }

    public void PutBattery(GameObject model)
    {
        battery = Instantiate(model, tray);
        battery.name = "Battery";
        // the model's long axis is its Z; turn it to lie across the drawer, bigger than life so it
        // reads (kept the same size however big the cabinet is scaled; it rests on the tray floor)
        float cabinet = tray.lossyScale.x;
        float radius = 0.0155f * BatteryScale;   // the model is about 3 cm thick
        battery.transform.localPosition = new Vector3(0f, -0.06f + radius / cabinet, BatterySpot);
        battery.transform.localRotation = Quaternion.Euler(0f, 90f + Random.Range(-15f, 15f), 0f);
        battery.transform.localScale = model.transform.localScale * (BatteryScale / cabinet);
        foreach (var c in battery.GetComponentsInChildren<Collider>())
            Destroy(c);
    }

    const float BatteryScale = 2.2f;

    // where a battery lies, along the tray (it's inside the cabinet until the drawer comes out)
    const float BatterySpot = 0.12f;

    public override void Use(PlayerInteractor player)
    {
        if (!open)
        {
            open = true;
            if (battery == null)
                Toast("비어 있다.");
            return;
        }
        if (battery != null)
        {
            var flashlight = player.GetComponentInChildren<FlashlightBattery>();
            if (flashlight != null)
                flashlight.AddCharge(charge);
            Destroy(battery);
            battery = null;
            Toast("배터리를 챙겼다.");
            return;
        }
        open = false;   // nothing (left) inside: push it shut
    }

    void Toast(string text)
    {
        toast = text;
        toastUntil = Time.time + 2.2f;
    }

    void Update()
    {
        shown = Mathf.MoveTowards(shown, open ? 1f : 0f, openSpeed * Time.deltaTime);
        tray.localPosition = closedPosition + Vector3.forward * (slide * Mathf.SmoothStep(0f, 1f, shown));
    }

    void OnGUI()
    {
        if (Time.time > toastUntil || Interactable.CloseUpOpen)
            return;
        var style = new GUIStyle(GUI.skin.label) { font = GameFont.Regular, fontSize = 20, alignment = TextAnchor.MiddleCenter };
        var rect = new Rect(0f, Screen.height * 0.64f, Screen.width, 30f);
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), toast, style);
        GUI.color = Color.white;
        GUI.Label(rect, toast, style);
    }
}
