using UnityEngine;

// One drawer of a filing cabinet (the model's Drawer_1, Drawer_2...; they all open on their own). E pulls it
// out; if a spare battery was put in it (BatterySpawner picks a few drawers each run) E again takes
// it and tops up the flashlight; if the puzzle put a sheet in it (a hint, a drawn hint or the
// crumpled number note; never with a battery) E reads it, and it's kept in the inventory. Once
// it's empty E pushes it shut again (and it can be reopened). Opening and closing are silent,
// except for the scare drawer, which bursts open in a flurry of feathers the first time.
// Things put in it lie on its child "Floor" and ride along.
public class Drawer : Interactable
{
    public float slide = 0.32f;       // how far the drawer comes out, in metres
    public float openSpeed = 2.5f;    // drawer lengths per second
    public float charge = 0.5f;       // how much a battery refills
    public float scareNoiseRange = 12f;

    // A scare: the first time this drawer is opened it bursts open and a flurry of dark
    // feathers flies out at the player, loud enough for the monster to hear.
    [System.NonSerialized] public bool scare;

    Transform floor;
    Vector3 closedPosition, openOffset;   // local to the cabinet
    GameObject battery;
    GameObject paper;     // the sheet lying in the drawer, if any
    PlayerInteractor.Note note;
    bool open;
    float shown;          // 0 closed .. 1 fully out
    string toast;
    float toastUntil;

    const float BatteryScale = 2.2f;

    public bool HasBattery => battery != null;
    public bool HasNote => paper != null;

    // the start room's cabinet (under "StartGear"): its drawers always stay empty
    public bool InStartRoom
    {
        get
        {
            for (var t = transform; t != null; t = t.parent)
                if (t.name == "StartGear") return true;
            return false;
        }
    }

    public override string Prompt =>
        !open ? "서랍 열기" : battery != null ? "배터리 꺼내기" : paper != null ? "쪽지 읽기" : "서랍 닫기";

    void Awake()
    {
        floor = transform;
        foreach (Transform c in transform)
            if (c.name.StartsWith("Floor"))   // Floor, Floor.001, ... as Blender named them
                floor = c;
        // out is the way from the middle of the cabinet to this drawer's front, level
        var cabinet = transform.parent;
        var mine = GetComponent<Renderer>().bounds.center;
        var whole = cabinet.GetComponent<Renderer>().bounds.center;
        var outward = Vector3.ProjectOnPlane(mine - whole, Vector3.up).normalized;
        closedPosition = transform.localPosition;
        openOffset = cabinet.InverseTransformVector(outward * slide);
    }

    public void PutBattery(GameObject model)
    {
        battery = Instantiate(model);
        battery.name = "Battery";
        // the model's long axis is its Z; it lies across the drawer, bigger than life so it reads
        float radius = 0.0155f * BatteryScale;   // the model is about 3 cm thick
        battery.transform.position = floor.position + Vector3.up * radius;
        battery.transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, Outward()), Vector3.up)
            * Quaternion.Euler(0f, Random.Range(-15f, 15f), 0f);
        battery.transform.localScale = model.transform.localScale * BatteryScale;
        battery.transform.SetParent(floor, true);
        foreach (var c in battery.GetComponentsInChildren<Collider>())
            Destroy(c);
    }

    public void PutNote(PlayerInteractor.Note sheet)
    {
        note = sheet;
        // a scrap of paper (a stand-in shape) lying on the drawer floor
        paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
        paper.name = "Note";
        Destroy(paper.GetComponent<Collider>());
        paper.transform.position = floor.position + Vector3.up * 0.006f;
        paper.transform.rotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f));
        paper.transform.localScale = new Vector3(0.11f, 0.012f, 0.08f);
        paper.transform.SetParent(floor, true);
        paper.GetComponent<Renderer>().material.color = new Color(0.78f, 0.72f, 0.58f);
    }

    Vector3 Outward() => transform.parent.TransformVector(openOffset).normalized;

    public override void Use(PlayerInteractor player)
    {
        if (!open)
        {
            open = true;
            if (scare)
            {
                scare = false;
                shown = 1f;   // flung open at once
                Burst(player);
                NoiseEvents.Emit(transform.position, scareNoiseRange);
                return;
            }
            if (battery == null && paper == null)
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
            RunStats.Batteries++;
            Toast("배터리를 챙겼다.");
            return;
        }
        if (paper != null)
        {
            Destroy(paper);
            paper = null;
            var sheets = new[] { note };
            player.ShowNotes(sheets);
            Inventory.Remember(sheets);
            return;
        }
        open = false;   // nothing (left) inside: push it shut
    }

    // dark feathers bursting out of the drawer up into the player's face (stand-in particles)
    void Burst(PlayerInteractor player)
    {
        if (Sfx.Bank != null) Sfx.Play(Sfx.Bank.scareFlap, transform.position, 1f);
        var go = new GameObject("FeatherBurst");
        go.transform.position = floor.position + Outward() * slide + Vector3.up * 0.1f;
        var toEye = player.GetComponent<FirstPersonController>().cameraRoot.position - go.transform.position;
        go.transform.rotation = Quaternion.LookRotation(Vector3.Lerp(Vector3.up, toEye.normalized, 0.6f));
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.3f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.7f, 0.72f), Color.white);   // tints over the feather picture
        main.gravityModifier = 0.25f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.15f;
        var drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.drag = 1.5f;
        var spin = ps.rotationOverLifetime;
        spin.enabled = true;
        spin.separateAxes = true;
        spin.x = spin.y = spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;
        var stretch = ps.sizeOverLifetime;   // feathers are longer than wide
        stretch.enabled = true;
        stretch.separateAxes = true;
        stretch.x = new ParticleSystem.MinMaxCurve(0.9f);   // the picture is 2:3; drawn a bit plumper
        stretch.y = new ParticleSystem.MinMaxCurve(1f);
        stretch.z = new ParticleSystem.MinMaxCurve(1f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        var featherMat = Resources.Load<Material>("Feather/FeatherParticle");   // the feather picture, see-through
        r.material = featherMat != null ? featherMat
            : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.alignment = ParticleSystemRenderSpace.Local;
        ps.Play();
    }

    void Toast(string text)
    {
        toast = text;
        toastUntil = Time.time + 2.2f;
    }

    void Update()
    {
        shown = Mathf.MoveTowards(shown, open ? 1f : 0f, openSpeed * Time.deltaTime);
        transform.localPosition = closedPosition + openOffset * Mathf.SmoothStep(0f, 1f, shown);
    }

    void OnGUI()
    {
        if (PauseMenu.IsOpen)
            return;   // drawn over the pause menu otherwise (OnGUI is on top of every canvas)
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
