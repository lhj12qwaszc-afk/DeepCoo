using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// A handheld tracker the player starts with. M raises it into the left hand and lowers it again;
// it can be used while walking. Its screen is a small radar, turned so "up" is the way the player
// faces: the maze walls around the player, the player as a green arrow in the middle, and the
// monster as a red dot when it comes within range. No battery. One thing in hand at a time: while
// the tracker is up the flashlight goes down and dark, and comes back as it was afterwards.
// The device is the model Resources/Tracker/Tracker (its screen part is named "Screen"; the radar
// is drawn on it), or a plain stand-in box if that's missing.
public class PositionTracker : MonoBehaviour
{
    const int Pixels = 128;           // radar resolution
    const float Range = 12f;          // metres from the middle to the edge of the radar
    const float RefreshRate = 12f;    // radar redraws per second
    const float RaiseSpeed = 4f;      // raise/lower: 1 / seconds

    static readonly Vector3 RaisedPosition = new Vector3(-0.085f, -0.04f, 0.22f);   // close enough to read
    static readonly Vector3 LoweredPosition = new Vector3(-0.1f, -0.36f, 0.18f);
    static readonly Quaternion HeldRotation = Quaternion.Euler(22f, -14f, 0f);   // screen tipped toward the eye

    static readonly Color32 Floor = new Color32(4, 14, 6, 255);
    static readonly Color32 Wall = new Color32(16, 52, 22, 255);
    static readonly Color32 Edge = new Color32(60, 190, 80, 255);
    static readonly Color32 Player = new Color32(90, 255, 110, 255);
    static readonly Color32 Monster = new Color32(255, 40, 30, 255);

    FirstPersonController fps;
    Transform device;
    Texture2D screen;
    Color32[] pixels;
    bool held;
    float raised;      // 0 lowered (out of sight) .. 1 up in front of the eyes

    // the tracker is (partly) up in front of the eyes; the battery gauge hides then
    public static bool InView { get; private set; }
    float nextDraw;
    MonsterWander monster;
    Transform flashlightHand;
    Vector3 flashlightHome;
    bool holdingLight;     // the flashlight was put away for the tracker...
    bool lightWasOn;       // ...and whether it was on then

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded += (scene, mode) => Attach();
        Attach();
    }

    static void Attach()
    {
        var player = FindFirstObjectByType<FirstPersonController>();
        if (player != null && player.GetComponent<PositionTracker>() == null)
            player.gameObject.AddComponent<PositionTracker>();
    }

    void Start()
    {
        fps = GetComponent<FirstPersonController>();
        screen = new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        pixels = new Color32[Pixels * Pixels];
        device = BuildDevice();
        device.SetParent(fps.cameraRoot, false);
        device.localPosition = LoweredPosition;
        device.localRotation = HeldRotation;
        device.gameObject.SetActive(false);
    }

    Transform BuildDevice()
    {
        Renderer screenRenderer;
        GameObject root;
        var model = Resources.Load<GameObject>("Tracker/Tracker");
        if (model != null)
        {
            // the import faces the model's front along +Z; turn it round to face the eye
            root = new GameObject();
            var body = Instantiate(model, root.transform);
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation;
            var part = FindDeep(root.transform, "Screen");
            screenRenderer = part != null ? part.GetComponent<Renderer>() : null;
        }
        else
        {
            root = new GameObject();
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, -0.02f, 0.0145f);   // front face just behind the screen
            body.transform.localScale = new Vector3(0.11f, 0.16f, 0.025f);
            body.GetComponent<Renderer>().material.color = new Color(0.1f, 0.1f, 0.11f);
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = "Screen";
            face.transform.SetParent(root.transform, false);
            face.transform.localScale = new Vector3(0.085f, 0.085f, 1f);
            screenRenderer = face.GetComponent<Renderer>();
        }
        root.name = "Tracker";
        foreach (var c in root.GetComponentsInChildren<Collider>())
            Destroy(c);
        foreach (var r in root.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (screenRenderer != null)
        {
            // unlit, so it glows in the dark
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            var mat = new Material(shader);
            mat.mainTexture = screen;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", screen);
            screenRenderer.material = mat;
        }
        return root.transform;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var found = FindDeep(c, name);
            if (found != null) return found;
        }
        return null;
    }

    void Update()
    {
        var k = Keyboard.current;
        bool busy = fps.inputLocked || Interactable.CloseUpOpen;
        if (k != null && !busy && PlayerGear.HasTracker && k.mKey.wasPressedThisFrame)
            held = !held;
        bool up = held && !busy;

        raised = Mathf.MoveTowards(raised, up ? 1f : 0f, RaiseSpeed * Time.deltaTime);
        InView = raised > 0f;
        float t = Mathf.SmoothStep(0f, 1f, raised);
        SwapWithFlashlight(t);
        device.localPosition = Vector3.Lerp(LoweredPosition, RaisedPosition, t);
        device.gameObject.SetActive(raised > 0f);

        if (raised > 0f && Time.time >= nextDraw)
        {
            nextDraw = Time.time + 1f / RefreshRate;
            Draw();
        }
    }

    // the flashlight hand drops out of sight as the tracker comes up, its light off; when the
    // tracker is put away the flashlight comes back up, on again if it was on
    void SwapWithFlashlight(float t)
    {
        if (flashlightHand == null)
        {
            flashlightHand = fps.cameraRoot.Find("FlashlightHand");
            if (flashlightHand == null)
                return;
            flashlightHome = flashlightHand.localPosition;
        }
        flashlightHand.localPosition = flashlightHome + Vector3.down * (0.4f * t);
        var light = fps.flashlight;
        if (light == null)
            return;
        if (raised > 0f)
        {
            if (!holdingLight)
            {
                holdingLight = true;
                lightWasOn = light.enabled;
            }
            light.enabled = false;
        }
        else if (holdingLight)
        {
            holdingLight = false;
            if (!fps.IsHidden)
                light.enabled = lightWasOn;
        }
    }

    void Draw()
    {
        var me = transform.position;
        var forward = Vector3.ProjectOnPlane(fps.cameraRoot.forward, Vector3.up).normalized;
        if (forward == Vector3.zero) forward = transform.forward;
        var right = Vector3.Cross(Vector3.up, forward);
        float half = Pixels / 2f;
        float metresPerPixel = Range / half;

        bool IsWall(float px, float py)
        {
            var p = me + right * ((px - half) * metresPerPixel) + forward * ((py - half) * metresPerPixel);
            return !MazeLayout.IsFloor(MazeLayout.WorldToCell(p));
        }

        for (int y = 0; y < Pixels; y++)
            for (int x = 0; x < Pixels; x++)
            {
                Color32 c;
                if (IsWall(x + 0.5f, y + 0.5f))   // the whole square screen, edge to edge
                    c = IsWall(x + 1.5f, y + 0.5f) && IsWall(x - 0.5f, y + 0.5f) && IsWall(x + 0.5f, y + 1.5f) && IsWall(x + 0.5f, y - 0.5f) ? Wall : Edge;
                else
                    c = Floor;
                pixels[y * Pixels + x] = c;
            }

        // the monster, when it's in range
        if (monster == null || !monster.isActiveAndEnabled)
            monster = FindFirstObjectByType<MonsterWander>();
        if (monster != null)
        {
            var offset = monster.transform.position - me;
            float mx = Vector3.Dot(offset, right) / metresPerPixel + half;
            float my = Vector3.Dot(offset, forward) / metresPerPixel + half;
            if (mx > 3f && mx < Pixels - 3f && my > 3f && my < Pixels - 3f)
                Dot(mx, my, 3.5f, Monster);
        }

        // the player: an arrow pointing up (the way they face)
        for (int row = 0; row < 9; row++)
        {
            int width = row / 2;
            for (int col = -width; col <= width; col++)
                Set((int)half + col, (int)half + 4 - row, Player);
        }

        screen.SetPixels32(pixels);
        screen.Apply(false);
    }

    void Dot(float cx, float cy, float radius, Color32 color)
    {
        for (int y = Mathf.FloorToInt(cy - radius); y <= Mathf.CeilToInt(cy + radius); y++)
            for (int x = Mathf.FloorToInt(cx - radius); x <= Mathf.CeilToInt(cx + radius); x++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                    Set(x, y, color);
    }

    void Set(int x, int y, Color32 color)
    {
        if (x >= 0 && x < Pixels && y >= 0 && y < Pixels)
            pixels[y * Pixels + x] = color;
    }

    void OnDestroy()
    {
        InView = false;
        if (screen != null) Destroy(screen);
    }
}
