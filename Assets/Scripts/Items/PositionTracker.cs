using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// A handheld tracker the player starts with. M raises it into the left hand and lowers it again;
// it can be used while walking. Its screen is a small radar, turned so "up" is the way the player
// faces: the maze walls around the player, the player as a green arrow in the middle, and the
// monster as a red dot when it comes within range. No battery.
// The device is Resources/Tracker if that model exists (its screen part must be named "Screen"),
// otherwise a plain stand-in box.
public class PositionTracker : MonoBehaviour
{
    const int Pixels = 128;           // radar resolution
    const float Range = 12f;          // metres from the middle to the edge of the radar
    const float RefreshRate = 12f;    // radar redraws per second
    const float RaiseSpeed = 4f;      // raise/lower: 1 / seconds

    static readonly Vector3 RaisedPosition = new Vector3(-0.12f, -0.075f, 0.32f);
    static readonly Vector3 LoweredPosition = new Vector3(-0.13f, -0.42f, 0.24f);
    static readonly Quaternion HeldRotation = Quaternion.Euler(22f, -14f, 0f);   // screen tipped toward the eye

    static readonly Color32 Off = new Color32(0, 0, 0, 255);
    static readonly Color32 Floor = new Color32(4, 14, 6, 255);
    static readonly Color32 Wall = new Color32(16, 52, 22, 255);
    static readonly Color32 Edge = new Color32(60, 190, 80, 255);
    static readonly Color32 Player = new Color32(90, 255, 110, 255);
    static readonly Color32 Monster = new Color32(255, 40, 30, 255);
    static readonly Color32 Ring = new Color32(30, 90, 40, 255);

    FirstPersonController fps;
    Transform device;
    Texture2D screen;
    Color32[] pixels;
    bool held;
    float raised;      // 0 lowered (out of sight) .. 1 up in front of the eyes
    float nextDraw;
    MonsterWander monster;

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
        var model = Resources.Load<GameObject>("Tracker");
        if (model != null)
        {
            root = Instantiate(model);
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
        if (k != null && !busy && k.mKey.wasPressedThisFrame)
            held = !held;
        bool up = held && !busy;

        raised = Mathf.MoveTowards(raised, up ? 1f : 0f, RaiseSpeed * Time.deltaTime);
        float t = Mathf.SmoothStep(0f, 1f, raised);
        device.localPosition = Vector3.Lerp(LoweredPosition, RaisedPosition, t);
        device.gameObject.SetActive(raised > 0f);

        if (raised > 0f && Time.time >= nextDraw)
        {
            nextDraw = Time.time + 1f / RefreshRate;
            Draw();
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
                float dx = x + 0.5f - half, dy = y + 0.5f - half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                Color32 c;
                if (r > half)
                    c = Off;
                else if (r > half - 2f)
                    c = Ring;
                else if (IsWall(x + 0.5f, y + 0.5f))
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
            if (new Vector2(mx - half, my - half).magnitude < half - 4f)
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
        if (screen != null) Destroy(screen);
    }
}
