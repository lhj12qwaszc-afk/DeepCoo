using UnityEngine;
using UnityEngine.InputSystem;

// A birdcage with some feathers in it (up to four on the floor, the rest stuck in the bars); how many there are is one digit of the
// padlock code (1-9). E brings the cage up close against a black screen, like a note being read;
// W/A/S/D turn it to count the feathers from every side (the mouse does nothing), E or Esc puts
// it back. The close-up is a second camera that sees only the cage (moved to InspectLayer).
// Feathers come from Resources/Feather/Feather if that model exists, otherwise a plain stand-in shape.
public class BirdcageInspect : Interactable
{
    const float FloorHeight = 0.048f;   // the cage's inner floor, as a share of the model's height (its roof is solid)
    const float ScreenShare = 0.7f;     // the upright cage, hook included, fills this much of the screen's height
    const float KeyTurnSpeed = 120f;    // degrees per second
    const int InspectLayer = 31;        // an unused layer only the close-up camera draws
    const float FeatherWiden = 1.5f;    // the feather made this much wider than the model...
    const float FeatherThicken = 2f;    // ...and this much thicker

    public int Feathers { get; private set; }

    public static bool IsOpen { get; private set; }

    bool open;
    int openedFrame;
    float yaw, pitch;
    float holdDistance;   // from the eye, so the cage fills ScreenShare of the screen
    FirstPersonController fps;
    Vector3 homePosition;
    Quaternion homeRotation;
    Transform chain;
    Collider[] colliders;
    Light lamp;   // soft light on the cage while it's held up, so the feathers can be seen
    Camera closeUp;
    readonly System.Collections.Generic.Dictionary<Transform, int> homeLayers = new System.Collections.Generic.Dictionary<Transform, int>();

    public override string Prompt => "새장 들여다보기";

    // how the feathers are arranged (being chosen): 1 all on the floor, 2 floor + standing inside the
    // bars, 3 floor + poked through the bars, 4 floor + both
    public static int Style = 4;

    public void SetFeathers(int count)
    {
        Feathers = count;
        var model = transform.Find("Model");
        var renderer = model != null ? model.GetComponentInChildren<Renderer>() : null;
        if (renderer == null)
            return;
        var b = renderer.bounds;
        float floor = b.min.y + FloorHeight * b.size.y;
        float width = Mathf.Min(b.size.x, b.size.z);
        var centre = new Vector3(b.center.x, floor, b.center.z);

        // At most four lie on the floor, one in each quarter so none touch; from five on, the rest
        // are stuck upright between the bars round the sides. Every feather stands apart to be counted.
        int onFloor = Style == 1 ? count : Mathf.Min(count, 4);
        int inBars = count - onFloor;

        if (Style == 1)
        {
            // all on the floor, smaller, one per spot of a 3x3 grid so none touch
            var spots = new System.Collections.Generic.List<Vector2>();
            for (int gx = -1; gx <= 1; gx++) for (int gz = -1; gz <= 1; gz++) spots.Add(new Vector2(gx, gz));
            for (int i = spots.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (spots[i], spots[j]) = (spots[j], spots[i]); }
            for (int i = 0; i < count; i++)
            {
                var s = spots[i];
                var pos = centre + new Vector3(s.x * 0.25f * b.size.x + Random.Range(-0.02f, 0.02f), 0f, s.y * 0.25f * b.size.z + Random.Range(-0.02f, 0.02f));
                var feather = MakeFeather(0.22f * width);
                feather.transform.rotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f)) * feather.transform.rotation;
                Place(feather, pos, floor);
                feather.transform.SetParent(model, true);
            }
            return;
        }

        var quarters = new System.Collections.Generic.List<Vector2> { new(-1, -1), new(1, -1), new(-1, 1), new(1, 1) };
        for (int i = quarters.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (quarters[i], quarters[j]) = (quarters[j], quarters[i]); }
        for (int i = 0; i < onFloor; i++)
        {
            var q = quarters[i];
            var pos = centre + new Vector3(q.x * 0.22f * b.size.x, 0f, q.y * 0.22f * b.size.z);
            var feather = MakeFeather(FloorLength * width);
            // lying flat, turned any which way
            feather.transform.rotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f)) * feather.transform.rotation;
            Place(feather, pos, floor);
            feather.transform.SetParent(model, true);
        }

        // round the inside of the cage's sides, evenly spaced, starting anywhere
        float px = b.size.x * 0.34f, pz = b.size.z * 0.34f;   // just inside the bars
        float perimeter = 4f * (px + pz);
        float offset = Random.Range(0f, perimeter);
        for (int i = 0; i < inBars; i++)
        {
            float t = (offset + i * perimeter / inBars) % perimeter;
            Vector3 spot, outward;
            if (t < 2f * px) { spot = new Vector3(-px + t, 0f, -pz); outward = Vector3.back; }
            else if ((t -= 2f * px) < 2f * pz) { spot = new Vector3(px, 0f, -pz + t); outward = Vector3.right; }
            else if ((t -= 2f * pz) < 2f * px) { spot = new Vector3(px - t, 0f, pz); outward = Vector3.forward; }
            else { t -= 2f * px; spot = new Vector3(-px, 0f, pz - t); outward = Vector3.left; }
            var feather = MakeFeather(BarLength * width);
            var pos = centre + spot + Vector3.up * (Random.Range(0.32f, 0.45f) * b.size.y);
            bool poke = Style == 3 || (Style == 4 && i % 2 == 0);   // style 2: none poked through
            if (poke)
            {
                // every other one is poked through a gap between the bars, half in and half out
                PokeThrough(feather, outward);
                pos += outward * (BarsAt - 0.34f) * (outward.x != 0f ? b.size.x : b.size.z);
            }
            else
                StandUp(feather, outward);   // the rest stand just inside the bars
            feather.transform.position = pos - (Bounds(feather).center - feather.transform.position);
            feather.transform.SetParent(model, true);
        }
    }

    const float FloorLength = 0.32f;   // a feather on the floor, as a share of the cage's width
    const float BarLength = 0.45f;     // a feather stuck in the bars
    const float BarsAt = 0.44f;        // the bars, as a share of the cage's size out from its middle

    // turn a flat-lying feather so it pokes out through the bars: pointing outward and a little
    // up, its flat face turned sideways so it slips between two bars
    static void PokeThrough(GameObject feather, Vector3 outward)
    {
        var fb = Bounds(feather);
        var along = fb.size.x >= fb.size.z ? Vector3.right : Vector3.forward;   // its length, lying down
        var point = Quaternion.FromToRotation(along, outward);
        var sideways = Quaternion.AngleAxis(90f, outward);                      // face to the side
        var up = Quaternion.AngleAxis(-Random.Range(15f, 30f), Vector3.Cross(Vector3.up, outward));
        feather.transform.rotation = up * sideways * point * feather.transform.rotation;
    }

    // one feather lying flat, `length` long, plumper than the model
    GameObject MakeFeather(float length)
    {
        var prefab = Resources.Load<GameObject>("Feather/Feather");
        var feather = prefab != null ? Instantiate(prefab) : MakeStandInFeather();
        feather.name = "Feather";
        if (prefab != null)
        {
            feather.transform.rotation = prefab.transform.rotation;
            var fb = Bounds(feather);
            feather.transform.localScale *= length / Mathf.Max(fb.size.x, fb.size.z);
            feather.transform.localScale = Vector3.Scale(feather.transform.localScale, new Vector3(FeatherWiden, 1f, FeatherThicken));
        }
        foreach (var c in feather.GetComponentsInChildren<Collider>())
            Destroy(c);
        return feather;
    }

    // rest it on the floor at `pos`
    static void Place(GameObject feather, Vector3 pos, float floor)
    {
        var fb = Bounds(feather);
        feather.transform.position += new Vector3(pos.x - fb.center.x, floor + 0.002f - fb.min.y, pos.z - fb.center.z);
    }

    // turn a flat-lying feather to stand on end, its face to the outside, leaning a little
    static void StandUp(GameObject feather, Vector3 outward)
    {
        var fb = Bounds(feather);
        var along = fb.size.x >= fb.size.z ? Vector3.right : Vector3.forward;   // its length, lying down
        var rise = Quaternion.FromToRotation(along, Vector3.up);
        var face = rise * Vector3.up;                                          // where its flat face looks now
        var turn = Quaternion.FromToRotation(Vector3.ProjectOnPlane(face, Vector3.up).normalized, outward);
        var lean = Quaternion.AngleAxis(Random.Range(-15f, 15f), outward) * Quaternion.AngleAxis(-12f, Vector3.Cross(Vector3.up, outward));
        feather.transform.rotation = lean * turn * rise * feather.transform.rotation;
    }

    static GameObject MakeStandInFeather()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.transform.localScale = new Vector3(0.02f, 0.055f, 0.004f);
        var r = go.GetComponent<Renderer>();
        r.material.color = new Color(0.82f, 0.8f, 0.76f);
        // lying down: the capsule's long axis along the floor
        var holder = new GameObject();
        go.transform.SetParent(holder.transform, false);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        return holder;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = System.Array.FindAll(go.GetComponentsInChildren<Renderer>(), r => r.enabled);   // not the hidden placeholder box
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    public override void Use(PlayerInteractor player)
    {
        fps = player.GetComponent<FirstPersonController>();
        homePosition = transform.position;
        homeRotation = transform.rotation;
        chain = transform.Find("Chain");
        if (chain != null) chain.gameObject.SetActive(false);
        colliders = GetComponentsInChildren<Collider>();
        foreach (var c in colliders) c.enabled = false;
        // face the player at first, upright
        var flat = Vector3.ProjectOnPlane(fps.cameraRoot.forward, Vector3.up).normalized;
        yaw = Vector3.SignedAngle(transform.forward, -flat, Vector3.up);
        pitch = 0f;
        var cam = fps.cameraRoot.GetComponent<Camera>();
        float halfFov = 0.5f * (cam != null ? cam.fieldOfView : 70f) * Mathf.Deg2Rad;
        var size = Bounds(gameObject).size;   // upright, as it hangs
        holdDistance = 0.5f * size.y / (Mathf.Tan(halfFov) * ScreenShare);
        if (lamp == null)
        {
            lamp = new GameObject("InspectLight").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 2f;
            lamp.intensity = 3f;
            lamp.color = new Color(1f, 0.92f, 0.8f);
            lamp.cullingMask = 1 << InspectLayer;
        }
        lamp.transform.SetParent(fps.cameraRoot, false);
        lamp.transform.localPosition = new Vector3(0f, 0.25f, 0.1f);
        lamp.enabled = true;

        if (closeUp == null)
        {
            var main = fps.cameraRoot.GetComponent<Camera>();
            closeUp = new GameObject("CloseUpCamera").AddComponent<Camera>();
            closeUp.clearFlags = CameraClearFlags.SolidColor;
            closeUp.backgroundColor = Color.black;
            closeUp.cullingMask = 1 << InspectLayer;
            closeUp.fieldOfView = main != null ? main.fieldOfView : 70f;
            closeUp.nearClipPlane = 0.05f;
            closeUp.depth = (main != null ? main.depth : 0f) + 1f;   // drawn over the game view
        }
        closeUp.transform.SetParent(fps.cameraRoot, false);
        closeUp.enabled = true;
        homeLayers.Clear();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            homeLayers[t] = t.gameObject.layer;
            t.gameObject.layer = InspectLayer;
        }
        open = true;
        openedFrame = Time.frameCount;
        IsOpen = true;
        fps.inputLocked = true;
    }

    void Close()
    {
        open = false;
        IsOpen = false;
        transform.SetPositionAndRotation(homePosition, homeRotation);
        if (chain != null) chain.gameObject.SetActive(true);
        foreach (var c in colliders) c.enabled = true;
        if (lamp != null) lamp.enabled = false;
        if (closeUp != null) closeUp.enabled = false;
        foreach (var pair in homeLayers)
            if (pair.Key != null) pair.Key.gameObject.layer = pair.Value;
        if (fps != null) fps.inputLocked = false;
    }

    void OnDestroy()
    {
        if (open) IsOpen = false;
    }

    void LateUpdate()
    {
        if (!open)
            return;
        var k = Keyboard.current;
        if (k != null && Time.frameCount != openedFrame && (k.eKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame))
        {
            Close();
            return;
        }
        if (k != null)
        {
            if (k.aKey.isPressed) yaw += KeyTurnSpeed * Time.deltaTime;
            if (k.dKey.isPressed) yaw -= KeyTurnSpeed * Time.deltaTime;
            if (k.wKey.isPressed) pitch = Mathf.Min(70f, pitch + KeyTurnSpeed * Time.deltaTime);
            if (k.sKey.isPressed) pitch = Mathf.Max(-30f, pitch - KeyTurnSpeed * Time.deltaTime);
        }

        // turn about the cage's own middle, held in front of the eyes
        var cam = fps.cameraRoot;
        var flat = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        var right = Vector3.Cross(Vector3.up, flat);
        var rot = Quaternion.AngleAxis(-pitch, right) * Quaternion.AngleAxis(yaw, Vector3.up) * homeRotation;   // +pitch tips the top toward you
        transform.rotation = rot;
        var center = Bounds(gameObject).center;
        transform.position += cam.position + cam.forward * holdDistance - center;
    }

    void OnGUI()
    {
        if (!open)
            return;
        var style = new GUIStyle(GUI.skin.label) { font = GameFont.Regular, fontSize = 18, alignment = TextAnchor.MiddleCenter };
        var rect = new Rect(0f, Screen.height - 40f, Screen.width, 30f);
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), "[E] 닫기", style);
        GUI.color = Color.white;
        GUI.Label(rect, "[E] 닫기", style);
    }
}
