using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the underground maze scene from the floor plan.
// Menu: DeepCoo > Build Maze Scene
public static class MazeBuilder
{
    // '#' = wall, '.' = floor. Row 0 is the north edge (top of the floor plan).
    static readonly string[] Layout =
    {
        "###########################",
        "###########################",
        "##.......................##",
        "##.......................##",
        "######.####.###########..##",
        "##.###.####.###.....###..##",
        "##.###.####.###.#######..##",
        "##.....####........####..##",
        "######...#########.####..##",
        "########.#########.####..##",
        "##.......................##",
        "##.......................##",
        "##..####.###########.######",
        "##..####.##.....####.######",
        "##..#....##.###.####.######",
        "##..#.##.##.###.####.######",
        "##..#.##.##.#####.##.##.###",
        "##..#.##......###.##.##.###",
        "##..#..######.###....##.###",
        "##..#########.#########.###",
        "##..#...#####.###.......###",
        "##..###.#####.###.#########",
        "##.......................##",
        "##.......................##",
        "###########################",
        "###########################",
    };

    const float CellSize = 2f;
    const float WallHeight = 3f;
    const string ScenePath = "Assets/Scenes/Underground.unity";

    // Marker positions in grid units (column, row), taken from the floor plan icons.
    struct Marker
    {
        public string Name;
        public float Col, Row;
        public Color Color;
        public PrimitiveType Shape;
    }

    static readonly Marker[] Markers =
    {
        new Marker { Name = "PlayerStart",    Col = 3.0f,  Row = 3.0f,  Color = Color.white,                  Shape = PrimitiveType.Capsule },
        new Marker { Name = "Key_A",          Col = 2.5f,  Row = 5.5f,  Color = new Color(1f, 0.85f, 0.2f),   Shape = PrimitiveType.Cube },
        new Marker { Name = "Clue_A",         Col = 19.5f, Row = 5.5f,  Color = new Color(0.3f, 0.6f, 1f),    Shape = PrimitiveType.Cylinder },
        new Marker { Name = "Key_B",          Col = 15.5f, Row = 15.5f, Color = new Color(1f, 0.85f, 0.2f),   Shape = PrimitiveType.Cube },
        new Marker { Name = "MonsterSpawn",   Col = 17.5f, Row = 16.5f, Color = new Color(0.6f, 0.1f, 0.15f), Shape = PrimitiveType.Capsule },
        new Marker { Name = "Clue_B",         Col = 5.5f,  Row = 18.5f, Color = new Color(0.3f, 0.6f, 1f),    Shape = PrimitiveType.Cylinder },
        new Marker { Name = "ExitDoor",       Col = 24.5f, Row = 23.0f, Color = new Color(0.2f, 0.9f, 0.4f),  Shape = PrimitiveType.Cube },
    };

    [MenuItem("DeepCoo/Build Maze Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int rows = Layout.Length;
        int cols = Layout[0].Length;
        float width = cols * CellSize;
        float depth = rows * CellSize;

        var floorMat = CreateMaterial("Maze_Floor", new Color(0.22f, 0.21f, 0.2f));
        var wallMat = CreateMaterial("Maze_Wall", new Color(0.38f, 0.37f, 0.35f));
        var ceilingMat = CreateMaterial("Maze_Ceiling", new Color(0.15f, 0.15f, 0.15f));

        var root = new GameObject("Maze");

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(root.transform);
        floor.transform.localPosition = new Vector3(width / 2f, -0.05f, -depth / 2f);
        floor.transform.localScale = new Vector3(width, 0.1f, depth);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;
        GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);

        // Ceiling starts disabled so the maze is visible from the Scene view; enable it for play.
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling";
        ceiling.transform.SetParent(root.transform);
        ceiling.transform.localPosition = new Vector3(width / 2f, WallHeight + 0.05f, -depth / 2f);
        ceiling.transform.localScale = new Vector3(width, 0.1f, depth);
        ceiling.GetComponent<Renderer>().sharedMaterial = ceilingMat;
        ceiling.SetActive(false);

        // Merge horizontal runs of wall cells into one block each to keep the object count low.
        var walls = new GameObject("Walls");
        walls.transform.SetParent(root.transform);
        for (int r = 0; r < rows; r++)
        {
            int c = 0;
            while (c < cols)
            {
                if (Layout[r][c] != '#') { c++; continue; }
                int start = c;
                while (c < cols && Layout[r][c] == '#') c++;
                int length = c - start;

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wall_r{r}_c{start}";
                wall.transform.SetParent(walls.transform);
                wall.transform.localPosition = new Vector3((start + length / 2f) * CellSize, WallHeight / 2f, -(r + 0.5f) * CellSize);
                wall.transform.localScale = new Vector3(length * CellSize, WallHeight, CellSize);
                wall.GetComponent<Renderer>().sharedMaterial = wallMat;
                GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            }
        }

        var markers = new GameObject("Markers");
        markers.transform.SetParent(root.transform);
        foreach (var m in Markers)
        {
            var go = GameObject.CreatePrimitive(m.Shape);
            go.name = m.Name;
            go.transform.SetParent(markers.transform);
            float height = m.Shape == PrimitiveType.Capsule ? 1f : 0.4f;
            go.transform.localPosition = new Vector3(m.Col * CellSize, height, -m.Row * CellSize);
            go.transform.localScale = m.Shape == PrimitiveType.Capsule ? Vector3.one : Vector3.one * 0.6f;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Marker_" + m.Name, m.Color);
        }

        // Dim, cold lighting for an underground space.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.08f, 0.08f, 0.1f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.03f);
        RenderSettings.fogDensity = 0.06f;

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.3f;
        light.color = new Color(0.7f, 0.75f, 0.9f);
        lightGo.transform.rotation = Quaternion.Euler(60f, -30f, 0f);

        var spawn = Markers[0];
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(spawn.Col * CellSize, 1.6f, -spawn.Row * CellSize);
        camGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        Selection.activeGameObject = root;
        Debug.Log($"[MazeBuilder] Built {ScenePath} ({cols}x{rows} cells, {walls.transform.childCount} wall blocks).");
    }

    static Material CreateMaterial(string name, Color color)
    {
        const string folder = "Assets/Materials/Maze";
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Materials", "Maze");

        string path = $"{folder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void AddSceneToBuildSettings(string path)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == path))
            return;
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
