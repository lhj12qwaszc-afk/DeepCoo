using UnityEngine;
using UnityEngine.InputSystem;

// A birdcage with some feathers lying on its floor; how many there are is one digit of the
// padlock code (1-9). E brings the cage up close against a black screen, like a note being read;
// W/A/S/D turn it to count the feathers from every side (the mouse does nothing), E or Esc puts
// it back. The close-up is a second camera that sees only the cage (moved to InspectLayer).
// Feathers come from Resources/Feather/Feather if that model exists, otherwise a plain stand-in shape.
public class BirdcageInspect : Interactable
{
    const float FloorHeight = 0.048f;   // the cage's inner floor, as a share of the model's height (its roof is solid)
    const float FloorReach = 0.3f;      // how far from the centre feathers lie, as a share of its width
    const float ScreenShare = 0.7f;     // the upright cage, hook included, fills this much of the screen's height
    const float KeyTurnSpeed = 120f;    // degrees per second
    const int InspectLayer = 31;        // an unused layer only the close-up camera draws
    const float FeatherLength = 0.5f;  // a feather's length, as a share of the cage's width
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

    public void SetFeathers(int count)
    {
        Feathers = count;
        var model = transform.Find("Model");
        var renderer = model != null ? model.GetComponentInChildren<Renderer>() : null;
        if (renderer == null)
            return;
        var b = renderer.bounds;
        var prefab = Resources.Load<GameObject>("Feather/Feather");
        float floor = b.min.y + FloorHeight * b.size.y;
        float width = Mathf.Min(b.size.x, b.size.z);
        // spread evenly round the floor like the spokes of a wheel, so none overlap and they can be
        // counted; each pointing outward, with a little wobble so it doesn't look laid out by hand
        float start = Random.Range(0f, 360f);
        float wobble = 0.25f * 360f / Mathf.Max(count, 1);
        for (int i = 0; i < count; i++)
        {
            float heading = start + i * 360f / count + Random.Range(-wobble, wobble);
            var dir = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            var pos = new Vector3(b.center.x, floor, b.center.z) + dir * (Random.Range(0.78f, 0.84f) * FloorReach * width);
            var feather = prefab != null ? Instantiate(prefab) : MakeStandInFeather();
            feather.name = "Feather";
            // lying on the floor along its spoke, tipped a little (the model is made lying flat)
            var turn = Quaternion.Euler(Random.Range(-8f, 8f), heading + Random.Range(-12f, 12f), Random.Range(-8f, 8f));
            feather.transform.SetPositionAndRotation(pos + Vector3.up * 0.004f,
                prefab != null ? turn * prefab.transform.rotation : turn);
            if (prefab != null)
            {
                // FeatherLength of the cage's width long, resting on the floor
                var fb = Bounds(feather);
                feather.transform.localScale *= FeatherLength * width / Mathf.Max(fb.size.x, fb.size.z);
                // plumper than the model: wider (its X) and thicker (its Z, which lies up)
                feather.transform.localScale = Vector3.Scale(feather.transform.localScale, new Vector3(FeatherWiden, 1f, FeatherThicken));
                fb = Bounds(feather);
                feather.transform.position += Vector3.up * (floor + 0.002f - fb.min.y);
            }
            feather.transform.SetParent(model, true);
            foreach (var c in feather.GetComponentsInChildren<Collider>())
                Destroy(c);
        }
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
