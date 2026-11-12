using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// What happens after the padlock opens. The screen goes black, and the player climbs out of the
// hatch into the night: a field ringed with dark pines, a moon, stars. Two lines fade in
// ("빠져나왔다…" / "정말 끝난 걸까?"), something coos far behind, and then the run's record
// (time, times caught) with [R] to play again and [M] for the main menu.
// The maze is stopped (time scale 0) the whole while, so everything here runs on real time.
// The outside is built on the spot, far above the maze, out of plain shapes.
public class EscapeEnding : MonoBehaviour
{
    static readonly Vector3 Outside = new Vector3(0f, 600f, 0f);   // the hatch, up in the open
    const float RefHeight = 720f;

    float black;            // 0..1 black over the screen
    float dim;              // 0..1 darkening behind the record
    float line1, line2;     // the two lines' fade
    float record;           // the record's fade
    bool canLeave;
    float seconds;
    int deaths;

    public static void Play()
    {
        if (FindFirstObjectByType<EscapeEnding>() == null)
            new GameObject("EscapeEnding").AddComponent<EscapeEnding>();
    }

    void Start()
    {
        seconds = RunStats.Seconds;
        deaths = RunStats.Deaths;
        Time.timeScale = 0f;   // the monster stops too
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return Fade(v => black = v, 0f, 1f, 0.8f);
        Camera cam = BuildOutside();
        yield return new WaitForSecondsRealtime(0.6f);

        // up through the hatch: looking up at the sky, then out across the field
        var from = Outside + new Vector3(0f, -1.3f, 0f);
        var to = Outside + new Vector3(0f, 1.7f, 0.6f);
        const float rise = 5f;
        StartCoroutine(Fade(v => black = v, 1f, 0f, 1.8f));
        for (float t = 0f; t < rise; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / rise);
            cam.transform.position = Vector3.Lerp(from, to, k);
            cam.transform.rotation = Quaternion.Euler(Mathf.Lerp(-75f, -6f, k), Mathf.Lerp(-20f, 0f, k), 0f);
            yield return null;
        }
        StartCoroutine(Drift(cam));

        yield return new WaitForSecondsRealtime(0.8f);
        yield return Fade(v => line1 = v, 0f, 1f, 1.2f);
        yield return new WaitForSecondsRealtime(1.2f);
        yield return Fade(v => line2 = v, 0f, 1f, 1.2f);
        yield return new WaitForSecondsRealtime(0.9f);
        // ...far behind, under the ground
        var bank = Sfx.Bank;
        if (bank != null && bank.monsterCoo != null)
        {
            var coo = Sfx.Play(bank.monsterCoo, null, 0.45f, 0.75f);
            if (coo != null) coo.ignoreListenerPause = true;
        }
        yield return new WaitForSecondsRealtime(2.6f);

        StartCoroutine(Fade(v => { line1 = line2 = 1f - v; }, 0f, 1f, 0.8f));
        yield return Fade(v => dim = v, 0f, 0.75f, 1.2f);
        yield return Fade(v => record = v, 0f, 1f, 1.2f);
        canLeave = true;
    }

    // a slow look around once outside
    IEnumerator Drift(Camera cam)
    {
        var start = cam.transform.rotation.eulerAngles;
        for (float t = 0f; cam != null; t += Time.unscaledDeltaTime)
        {
            cam.transform.rotation = Quaternion.Euler(start.x - Mathf.Sin(t * 0.15f) * 1.5f, start.y + Mathf.Sin(t * 0.1f) * 12f, 0f);
            yield return null;
        }
    }

    static IEnumerator Fade(System.Action<float> set, float a, float b, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            set(Mathf.Lerp(a, b, t / seconds));
            yield return null;
        }
        set(b);
    }

    void Update()
    {
        var k = Keyboard.current;
        if (!canLeave || k == null)
            return;
        if (k.rKey.wasPressedThisFrame)
        {
            RunStats.StartRun();
            Leave(SceneManager.GetActiveScene().name);
        }
        else if (k.mKey.wasPressedThisFrame)
            Leave("MainMenu");
    }

    static void Leave(string scene)
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(scene);
    }

    // ---------- the outside ----------

    Camera BuildOutside()
    {
        // the maze stops showing and stops being heard
        foreach (var dark in FindObjectsByType<FlashlightDarkness>(FindObjectsSortMode.None))
            Destroy(dark);
        foreach (var mic in FindObjectsByType<MicrophoneNoise>(FindObjectsSortMode.None))
            mic.enabled = false;   // its "마이크 없음" corner text
        foreach (var voice in FindObjectsByType<MonsterVoice>(FindObjectsSortMode.None))
            voice.gameObject.SetActive(false);
        foreach (var src in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if (!src.ignoreListenerPause) src.Stop();
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            c.enabled = false;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) l.enabled = false;

        var root = new GameObject("Outside").transform;

        // night sky, fog and moonlight
        var sky = new Material(Shader.Find("Skybox/Procedural"));
        sky.SetFloat("_SunSize", 0f);
        sky.SetFloat("_AtmosphereThickness", 0.55f);
        sky.SetColor("_SkyTint", new Color(0.25f, 0.32f, 0.55f));
        sky.SetColor("_GroundColor", new Color(0.02f, 0.02f, 0.03f));
        sky.SetFloat("_Exposure", 0.35f);
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.12f, 0.18f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.05f, 0.07f, 0.12f);
        RenderSettings.fogDensity = 0.006f;

        var moonLight = new GameObject("Moonlight").AddComponent<Light>();
        moonLight.transform.SetParent(root);
        moonLight.type = LightType.Directional;
        moonLight.color = new Color(0.65f, 0.75f, 1f);
        moonLight.intensity = 0.5f;
        moonLight.shadows = LightShadows.Soft;
        moonLight.transform.rotation = Quaternion.Euler(8f, 200f, 0f);   // low, behind and to the left: the horizon glows ahead
        RenderSettings.sun = moonLight;

        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        Material Mat(Shader s, Color c, bool fogless = false)
        {
            var m = new Material(s) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            return m;
        }
        var grass = Mat(lit, new Color(0.10f, 0.13f, 0.09f));
        var pine = Mat(lit, new Color(0.03f, 0.05f, 0.04f));
        var bark = Mat(lit, new Color(0.06f, 0.045f, 0.035f));
        var metal = Mat(lit, new Color(0.22f, 0.22f, 0.23f));
        var hill = Mat(lit, new Color(0.04f, 0.05f, 0.06f));
        var star = Mat(unlit, new Color(0.9f, 0.92f, 1f));
        var moon = Mat(unlit, new Color(0.95f, 0.93f, 0.82f));

        GameObject Shape(PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Quaternion? rot = null)
        {
            var g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(root);
            g.transform.SetPositionAndRotation(pos, rot ?? Quaternion.identity);
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        // the field, with the hatch's open mouth in the middle (the ground has a hole there)
        const float hole = 0.7f, size = 400f;
        float side = (size - hole) / 2f;
        Shape(PrimitiveType.Cube, Outside + new Vector3(0f, -0.05f, (hole + side) / 2f), new Vector3(size, 0.1f, side), grass);
        Shape(PrimitiveType.Cube, Outside + new Vector3(0f, -0.05f, -(hole + side) / 2f), new Vector3(size, 0.1f, side), grass);
        Shape(PrimitiveType.Cube, Outside + new Vector3((hole + side) / 2f, -0.05f, 0f), new Vector3(side, 0.1f, hole), grass);
        Shape(PrimitiveType.Cube, Outside + new Vector3(-(hole + side) / 2f, -0.05f, 0f), new Vector3(side, 0.1f, hole), grass);
        // the shaft below and the hatch's rim and thrown-back lid
        Shape(PrimitiveType.Cube, Outside + new Vector3(0f, -1.6f, -hole / 2f - 0.05f), new Vector3(hole + 0.2f, 3f, 0.1f), metal);
        Shape(PrimitiveType.Cube, Outside + new Vector3(0f, -1.6f, hole / 2f + 0.05f), new Vector3(hole + 0.2f, 3f, 0.1f), metal);
        Shape(PrimitiveType.Cube, Outside + new Vector3(-hole / 2f - 0.05f, -1.6f, 0f), new Vector3(0.1f, 3f, hole), metal);
        Shape(PrimitiveType.Cube, Outside + new Vector3(hole / 2f + 0.05f, -1.6f, 0f), new Vector3(0.1f, 3f, hole), metal);
        Shape(PrimitiveType.Cube, Outside + new Vector3(0f, 0.04f, -hole / 2f - 0.06f), new Vector3(hole + 0.3f, 0.08f, 0.12f), metal);
        Shape(PrimitiveType.Cube, Outside + new Vector3(0f, 0.04f, hole / 2f + 0.06f), new Vector3(hole + 0.3f, 0.08f, 0.12f), metal);
        Shape(PrimitiveType.Cube, Outside + new Vector3(-hole / 2f - 0.06f, 0.04f, 0f), new Vector3(0.12f, 0.08f, hole), metal);
        Shape(PrimitiveType.Cube, Outside + new Vector3(hole / 2f + 0.06f, 0.04f, 0f), new Vector3(0.12f, 0.08f, hole), metal);
        Shape(PrimitiveType.Cube, Outside + new Vector3(0f, 0.08f, -hole - 0.05f), new Vector3(hole + 0.1f, 0.06f, hole + 0.1f), metal,
              Quaternion.Euler(-12f, 0f, 0f));

        // pines all around, thicker further out; an open stretch straight ahead. The fir model
        // (Resources/Escape/Tree, made in VARCO) when it's there, otherwise stacked cones.
        var rng = new System.Random(7);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        var treeModel = Resources.Load<GameObject>("Escape/Tree");
        var treeMat = Resources.Load<Material>("Escape/Tree_mat");
        for (int i = 0; i < 260; i++)
        {
            float angle = R(0f, 360f), dist = R(14f, 150f);
            if (Mathf.Abs(Mathf.DeltaAngle(angle, 0f)) < 22f && dist < 60f)
                continue;   // the clearing ahead
            var at = Outside + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * dist;
            float h = R(6f, 13f), w = h * R(0.28f, 0.38f);
            if (treeModel != null)
            {
                var tree = Instantiate(treeModel, at, Quaternion.Euler(0f, R(0f, 360f), 0f), root);
                // the model stands ModelHeight tall; a little wider or narrower each time
                float k = h / ModelHeight;
                tree.transform.localScale = Vector3.Scale(tree.transform.localScale, new Vector3(k * R(0.85f, 1.15f), k, k * R(0.85f, 1.15f)));
                if (treeMat != null)
                    foreach (var r in tree.GetComponentsInChildren<Renderer>())
                        r.sharedMaterial = treeMat;
                continue;
            }
            Shape(PrimitiveType.Cylinder, at + Vector3.up * 1f, new Vector3(0.35f, 1f, 0.35f), bark);
            // three stacked cones, each narrower, the classic fir outline
            for (int tier = 0; tier < 3; tier++)
            {
                float k = tier / 3f;
                var cone = new GameObject("Pine");
                cone.transform.SetParent(root);
                cone.transform.SetPositionAndRotation(at + Vector3.up * (1.2f + h * k * 0.62f), Quaternion.Euler(0f, R(0f, 360f), 0f));
                cone.transform.localScale = new Vector3(w * (1f - k * 0.45f), h * 0.5f, w * (1f - k * 0.45f));
                cone.AddComponent<MeshFilter>().sharedMesh = Cone;
                cone.AddComponent<MeshRenderer>().sharedMaterial = pine;
            }
        }
        // low hills on the horizon
        for (int i = 0; i < 9; i++)
        {
            var at = Outside + Quaternion.Euler(0f, i * 40f + R(-10f, 10f), 0f) * Vector3.forward * R(170f, 210f);
            Shape(PrimitiveType.Sphere, at + Vector3.down * 8f, new Vector3(R(90f, 140f), R(30f, 45f), R(60f, 90f)), hill);
        }

        // the moon (up ahead, to the right) and stars
        var moonDir = Quaternion.Euler(-20f, 22f, 0f) * Vector3.forward;
        var moonBall = Shape(PrimitiveType.Sphere, Outside + moonDir * 150f, Vector3.one * MoonSize, moon);
        var moonTex = Resources.Load<Texture2D>("Escape/Moon");   // a flat map of the moon's surface
        if (moonTex != null)
        {
            moon.mainTexture = moonTex;
            if (moon.HasProperty("_BaseMap")) moon.SetTexture("_BaseMap", moonTex);
            // brighter than white, so it still shines through the night haze
            var glowTint = new Color(1.9f, 1.85f, 1.7f);
            moon.color = glowTint;
            if (moon.HasProperty("_BaseColor")) moon.SetColor("_BaseColor", glowTint);
            // turn it so the dark "seas" of the near side face the player
            moonBall.transform.rotation = Quaternion.LookRotation(moonDir) * Quaternion.Euler(0f, MoonTurn, 0f);
        }
        // a soft glow around it
        var halo = Shape(PrimitiveType.Quad, Outside + moonDir * (150f + MoonSize), Vector3.one * MoonSize * 5.5f, HaloMaterial());
        halo.transform.rotation = Quaternion.LookRotation(moonDir);
        for (int i = 0; i < 420; i++)
        {
            var dir = Quaternion.Euler(-R(8f, 88f), R(0f, 360f), 0f) * Vector3.forward;
            var s = Shape(PrimitiveType.Quad, Outside + dir * 160f, Vector3.one * R(0.25f, 0.6f), star);
            s.transform.rotation = Quaternion.LookRotation(dir);
        }

        var cam = new GameObject("EscapeCamera").AddComponent<Camera>();
        cam.transform.SetParent(root);
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 800f;
        cam.fieldOfView = 65f;
        cam.gameObject.AddComponent<AudioListener>();
        foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (l.gameObject != cam.gameObject) l.enabled = false;
        return cam;
    }

    const float ModelHeight = 5f;   // the fir model's height in metres as imported
    const float MoonTurn = 90f;     // degrees round its axis, to show the familiar face
    const float MoonSize = 16f;     // metres across, 150 m away (was 6: too small)

    // pale light fading out from the middle, drawn see-through
    static Material HaloMaterial()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f).magnitude / (size / 2f);
                float a = Mathf.Clamp01(1f - r);
                px[y * size + x] = new Color(0.8f, 0.85f, 1f, a * a * a * 0.35f);
            }
        tex.SetPixels(px);
        tex.Apply();
        return new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
    }

    // a cone 1 wide and 1 tall, base at the origin
    static Mesh cone;
    static Mesh Cone
    {
        get
        {
            if (cone != null)
                return cone;
            const int n = 12;
            var v = new Vector3[n + 2];
            var tris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                v[i] = new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f);
            }
            v[n] = Vector3.up;          // tip
            v[n + 1] = Vector3.zero;    // middle of the base
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                tris[i * 6 + 0] = i; tris[i * 6 + 1] = n; tris[i * 6 + 2] = j;
                tris[i * 6 + 3] = i; tris[i * 6 + 4] = j; tris[i * 6 + 5] = n + 1;
            }
            cone = new Mesh { name = "Cone", vertices = v, triangles = tris };
            cone.RecalculateNormals();
            cone.RecalculateBounds();
            return cone;
        }
    }

    // ---------- the words ----------

    void OnGUI()
    {
        float s = Screen.height / RefHeight;
        float W = Screen.width / s, H = RefHeight;
        var before = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

        Shade(W, H, Mathf.Max(black, dim));
        if (line1 > 0f) Label(new Rect(0f, H * 0.40f, W, 60f), "빠져나왔다…", 40, line1, false);
        if (line2 > 0f) Label(new Rect(0f, H * 0.40f + 64f, W, 50f), "정말 끝난 걸까?", 30, line2, false);
        if (record > 0f)
        {
            float y = H * 0.24f;
            Label(new Rect(0f, y, W, 80f), "탈출 성공", 60, record, true);
            int m = Mathf.FloorToInt(seconds / 60f), sec = Mathf.FloorToInt(seconds % 60f);
            Label(new Rect(0f, y + 120f, W, 40f), $"걸린 시간      {m:00}:{sec:00}", 28, record, false);
            Label(new Rect(0f, y + 165f, W, 40f), $"잡힌 횟수      {deaths}번", 28, record, false);
            if (canLeave)
                Label(new Rect(0f, H - 90f, W, 36f), "[R] 처음부터 다시          [M] 메인 메뉴", 24, record * 0.85f, false);
        }
        GUI.matrix = before;
    }

    static void Shade(float W, float H, float alpha)
    {
        if (alpha <= 0f)
            return;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    static void Label(Rect rect, string text, int size, float alpha, bool bold)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            font = bold ? GameFont.Bold : GameFont.Regular,
            fontSize = size,
            alignment = TextAnchor.MiddleCenter,
        };
        // a soft shadow so the words read over the sky
        style.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.7f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        style.normal.textColor = new Color(0.93f, 0.9f, 0.84f, alpha);
        GUI.Label(rect, text, style);
    }
}
