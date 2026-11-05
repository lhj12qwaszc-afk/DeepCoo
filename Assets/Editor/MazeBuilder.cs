using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the underground maze scene from the floor plan.
// Menu: DeepCoo > Build Maze Scene
public static class MazeBuilder
{
    static readonly string[] Layout = MazeLayout.Rows;

    const float CellSize = MazeLayout.CellSize;
    const float WallHeight = 4.5f;
    const string ScenePath = "Assets/Scenes/Underground.unity";

    // Wall texture set; one copy of the texture covers WallTileMeters x WallTileMeters of wall.
    const string WallTextureFolder = "Assets/Textures/Walls/SmoothConcrete";
    const string WallAlbedo = "smooth_concrete_floor_diff_2k.jpg";
    const string WallNormal = "smooth_concrete_floor_nor_gl_2k.exr";
    const string WallOcclusion = "";
    const float WallTileMeters = 2f;

    const string MonsterModelPath = "Assets/Characters/PigeonMonster/PigeonMonster.fbx";
    const float MonsterScale = 1.6f;  // the model is ~2.6 m tall; this makes it ~4.1 m
    const string FlashlightModelPath = "Assets/Props/Flashlight/Flashlight.fbx";
    const string BatteryModelPath = "Assets/Props/Battery/Battery.fbx";

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
        new Marker { Name = "PlayerStart",    Col = 2.5f,  Row = 22.5f, Color = Color.white,                  Shape = PrimitiveType.Capsule },
        new Marker { Name = "Key_A",          Col = 1.5f,  Row = 1.5f,  Color = new Color(1f, 0.85f, 0.2f),   Shape = PrimitiveType.Cube },
        new Marker { Name = "Clue_A",         Col = 11.5f, Row = 11.5f, Color = new Color(0.3f, 0.6f, 1f),    Shape = PrimitiveType.Cylinder },
        new Marker { Name = "MonsterSpawn",   Col = 19.5f, Row = 17.5f, Color = new Color(0.6f, 0.1f, 0.15f), Shape = PrimitiveType.Capsule },
        new Marker { Name = "ExitDoor",       Col = 19.5f, Row = 1.5f,  Color = new Color(0.2f, 0.9f, 0.4f),  Shape = PrimitiveType.Cube },
    };

    // Placeholder boxes for furniture whose models aren't made yet: real-world size, standing
    // against a wall and facing into the corridor. Swap in the real models later.
    struct PropSpec
    {
        public string Name;
        public int Count;
        public Vector3 Size;      // width, height, depth (depth runs away from the wall)
        public float Elevation;   // bottom edge above the floor (wall-mounted and hanging items)
        public Color Color;
        public bool Hanging;      // hangs from the ceiling on a chain
    }

    static readonly Color HideColor = new Color(0.25f, 0.45f, 0.85f);   // hiding spots
    static readonly Color ClueColor = new Color(0.9f, 0.75f, 0.2f);     // places clues are kept
    static readonly Color PuzzleColor = new Color(0.9f, 0.45f, 0.15f);  // puzzle objects

    static readonly PropSpec[] PropSpecs =
    {
        // hiding spots are half the wall height (2.25 m), keeping their proportions
        new PropSpec { Name = "Locker",        Count = 5, Size = new Vector3(0.6f, 2.25f, 0.6f),   Color = HideColor },
        new PropSpec { Name = "Wardrobe",      Count = 3, Size = new Vector3(1.35f, 2.25f, 0.68f), Color = HideColor },
        new PropSpec { Name = "TrashBin",      Count = 3, Size = new Vector3(1.88f, 2.25f, 1.5f),  Color = HideColor },
        new PropSpec { Name = "FilingCabinet", Count = 2, Size = new Vector3(0.5f, 1.35f, 0.65f),  Color = ClueColor },
        new PropSpec { Name = "CorkBoard",     Count = 1, Size = new Vector3(1.2f, 0.9f, 0.05f),   Elevation = 1.1f, Color = ClueColor },
        new PropSpec { Name = "PipeGauge",     Count = 2, Size = new Vector3(1.0f, 0.5f, 0.3f),    Elevation = 0.9f, Color = PuzzleColor },
        new PropSpec { Name = "Birdcage",      Count = 2, Size = new Vector3(0.5f, 0.7f, 0.5f),    Elevation = 1.3f, Color = PuzzleColor, Hanging = true },
        new PropSpec { Name = "Radio",         Count = 1, Size = new Vector3(0.35f, 0.22f, 0.12f), Color = PuzzleColor },
    };

    // Fixed layout so the props stay put between rebuilds; change the seed to reshuffle.
    const int PropSeed = 7;

    static void PlacePropPlaceholders(Transform parent)
    {
        var steps = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        var reserved = new System.Collections.Generic.HashSet<Vector2Int>();
        foreach (var m in Markers)
            reserved.Add(new Vector2Int(Mathf.FloorToInt(m.Col), Mathf.FloorToInt(m.Row)));
        // keep the start room and its doorway empty
        var startCell = new Vector2Int(Mathf.FloorToInt(Markers[0].Col), Mathf.FloorToInt(Markers[0].Row));
        foreach (var cell in MazeLayout.FloorCells())
        {
            var path = MazeLayout.FindPath(startCell, cell);
            if (path != null && path.Count <= 3)
                reserved.Add(cell);
        }

        // every (floor cell, wall side) pair, in a fixed shuffled order
        var spots = new System.Collections.Generic.List<(Vector2Int cell, Vector2Int side)>();
        foreach (var cell in MazeLayout.FloorCells())
            foreach (var step in steps)
                if (!reserved.Contains(cell) && !MazeLayout.IsFloor(cell + step))
                    spots.Add((cell, step));
        var rng = new System.Random(PropSeed);
        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (spots[i], spots[j]) = (spots[j], spots[i]);
        }

        var root = new GameObject("PropPlaceholders").transform;
        root.SetParent(parent);
        var used = new System.Collections.Generic.HashSet<Vector2Int>();
        int next = 0;
        foreach (var spec in PropSpecs)
        {
            var mat = CreateMaterial("Placeholder_" + spec.Name, spec.Color);
            for (int n = 0; n < spec.Count && next < spots.Count; next++)
            {
                var (cell, side) = spots[next];
                if (used.Contains(cell))
                    continue;   // one prop per cell keeps corridors passable
                used.Add(cell);
                n++;

                // MazeLayout rows grow southward (-Z), so a grid step (x, y) is world (x, 0, -y).
                var outward = new Vector3(side.x, 0f, -side.y);
                var pos = MazeLayout.CellCenter(cell) + outward * (CellSize / 2f - spec.Size.z / 2f - 0.02f);
                pos.y = spec.Elevation + spec.Size.y / 2f;

                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"{spec.Name}_{n}";
                box.transform.SetParent(root);
                box.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-outward, Vector3.up));
                box.transform.localScale = spec.Size;
                box.GetComponent<Renderer>().sharedMaterial = mat;
                if (System.Enum.TryParse<HidingSpot.Kind>(spec.Name, out var kind))
                    box.AddComponent<HidingSpot>().kind = kind;
                PlaceModel(box, spec, outward);
            }
        }
    }

    // Chests of drawers where spare batteries are found (Drawer, BatterySpawner). Most go in the
    // north-west of the maze, which has nothing else to find; a few elsewhere. Stand-in boxes
    // until there's a model. Can be re-run on its own: it replaces the old "Drawers" group.
    const int DrawerSeed = 11;
    const int DrawersNorthWest = 5;
    const int DrawersElsewhere = 3;
    const int DrawerSpacing = 3;   // cells between any two drawers
    static readonly Vector3 DrawerSize = new Vector3(0.7f, 0.8f, 0.5f);   // before DrawerScale
    const float DrawerScale = 1.5f;   // the whole cabinet, drawer travel included

    static bool InNorthWest(Vector3 p) => p.x < 56f && p.z > -38f;

    public static void PlaceDrawers(Transform maze)
    {
        var old = maze.Find("Drawers");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        var used = new System.Collections.Generic.HashSet<Vector2Int>();
        foreach (var m in Markers)
            used.Add(new Vector2Int(Mathf.FloorToInt(m.Col), Mathf.FloorToInt(m.Row)));
        var startCell = new Vector2Int(Mathf.FloorToInt(Markers[0].Col), Mathf.FloorToInt(Markers[0].Row));
        foreach (var cell in MazeLayout.FloorCells())
        {
            var path = MazeLayout.FindPath(startCell, cell);
            if (path != null && path.Count <= 3)
                used.Add(cell);
        }
        var props = maze.Find("PropPlaceholders");
        if (props != null)
            foreach (Transform p in props)
                used.Add(MazeLayout.WorldToCell(p.position));

        var steps = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        var spots = new System.Collections.Generic.List<(Vector2Int cell, Vector2Int side)>();
        foreach (var cell in MazeLayout.FloorCells())
            foreach (var step in steps)
                if (!used.Contains(cell) && !MazeLayout.IsFloor(cell + step))
                    spots.Add((cell, step));
        var rng = new System.Random(DrawerSeed);
        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (spots[i], spots[j]) = (spots[j], spots[i]);
        }

        var root = new GameObject("Drawers").transform;
        root.SetParent(maze);
        var bodyMat = CreateMaterial("Placeholder_Drawer", new Color(0.36f, 0.3f, 0.24f));
        var frontMat = CreateMaterial("Placeholder_DrawerFront", new Color(0.48f, 0.4f, 0.31f));
        var placed = new System.Collections.Generic.List<Vector2Int>();
        int northWest = 0, elsewhere = 0;
        foreach (var (cell, side) in spots)
        {
            bool nw = InNorthWest(MazeLayout.CellCenter(cell));
            if (nw ? northWest >= DrawersNorthWest : elsewhere >= DrawersElsewhere)
                continue;
            bool tooClose = false;
            foreach (var c in placed)
                if (Mathf.Abs(c.x - cell.x) + Mathf.Abs(c.y - cell.y) < DrawerSpacing)
                    tooClose = true;
            if (tooClose)
                continue;
            placed.Add(cell);
            if (nw) northWest++; else elsewhere++;

            var outward = new Vector3(side.x, 0f, -side.y);
            var pos = MazeLayout.CellCenter(cell) + outward * (CellSize / 2f - DrawerScale * DrawerSize.z / 2f - 0.02f);
            var drawer = new GameObject($"Drawer_{placed.Count}");
            drawer.transform.SetParent(root);
            drawer.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-outward, Vector3.up));
            drawer.transform.localScale = Vector3.one * DrawerScale;
            BuildDrawerStandIn(drawer.transform, bodyMat, frontMat);
            drawer.AddComponent<Drawer>();
            if (northWest >= DrawersNorthWest && elsewhere >= DrawersElsewhere)
                break;
        }
    }

    // A cabinet with three drawer fronts; the top one ("Tray") slides out and has a floor to
    // hold what's inside. Only the cabinet body has a collider.
    static void BuildDrawerStandIn(Transform drawer, Material bodyMat, Material frontMat)
    {
        Transform Part(Transform parent, string name, Vector3 position, Vector3 scale, Material mat, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }

        Vector3 s = DrawerSize;
        float face = s.z / 2f;
        Part(drawer, "Body", new Vector3(0f, s.y / 2f, 0f), s, bodyMat, collider: true);
        foreach (var y in new[] { 0.38f, 0.15f })
        {
            Part(drawer, "Front", new Vector3(0f, y, face + 0.012f), new Vector3(s.x * 0.86f, 0.18f, 0.024f), frontMat);
            Part(drawer, "Handle", new Vector3(0f, y, face + 0.035f), new Vector3(0.14f, 0.025f, 0.025f), bodyMat);
        }

        var tray = new GameObject("Tray").transform;
        tray.SetParent(drawer, false);
        tray.localPosition = new Vector3(0f, 0.62f, 0f);
        Part(tray, "Front", new Vector3(0f, 0f, face + 0.012f), new Vector3(s.x * 0.86f, 0.18f, 0.024f), frontMat);
        Part(tray, "Handle", new Vector3(0f, 0f, face + 0.035f), new Vector3(0.14f, 0.025f, 0.025f), bodyMat);
        Part(tray, "Bottom", new Vector3(0f, -0.07f, face - 0.2f), new Vector3(s.x * 0.8f, 0.02f, 0.4f), frontMat);
        Part(tray, "Back", new Vector3(0f, -0.02f, face - 0.39f), new Vector3(s.x * 0.8f, 0.1f, 0.02f), frontMat);
        Part(tray, "SideL", new Vector3(-s.x * 0.39f, -0.02f, face - 0.2f), new Vector3(0.02f, 0.1f, 0.4f), frontMat);
        Part(tray, "SideR", new Vector3(s.x * 0.39f, -0.02f, face - 0.2f), new Vector3(0.02f, 0.1f, 0.4f), frontMat);
    }

    // Swaps a placeholder box's look for the real model when there is one. The box stays as the
    // collider and hiding spot; the model is scaled so its longest side matches the box's, stood
    // on the box's bottom edge and pushed back against the wall.
    static void PlaceModel(GameObject box, PropSpec spec, Vector3 outward)
    {
        var asset = PropModels.Load(spec.Name);
        if (asset == null)
            return;
        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        model.name = "Model";
        // The import's own rotation stands the model up with its front along +Z; then turn that to
        // face the corridor.
        model.transform.SetPositionAndRotation(Vector3.zero,
            Quaternion.LookRotation(-outward, Vector3.up) * asset.transform.localRotation);
        var size = Bounds(model).size;
        float scale = Mathf.Max(spec.Size.x, spec.Size.y, spec.Size.z) / Mathf.Max(size.x, size.y, size.z);
        model.transform.localScale = Vector3.one * scale;

        var bounds = Bounds(model);
        var wall = box.transform.position + outward * (spec.Size.z / 2f);
        var shift = box.transform.position - bounds.center;
        shift -= outward * Vector3.Dot(shift, outward);                                    // centred along the wall
        shift += outward * (Vector3.Dot(wall - bounds.center, outward) - Vector3.Dot(bounds.extents, Abs(outward)));
        shift.y = spec.Elevation - bounds.min.y;                                           // standing on its bottom edge
        model.transform.position += shift;

        // parented to the box but keeping its own world size
        model.transform.SetParent(box.transform, true);
        box.GetComponent<Renderer>().enabled = false;
        foreach (var c in model.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(c);

        if (spec.Hanging)
            HangFromCeiling(box, Bounds(model));
        if (spec.Name == "PipeGauge")
            box.AddComponent<PipeGaugeNeedle>();
    }

    // A chain of links from the top of a hanging prop up to the ceiling.
    static void HangFromCeiling(GameObject box, Bounds bounds)
    {
        const float link = 0.07f;
        var mat = CreateMaterial("Prop_Chain", new Color(0.22f, 0.13f, 0.08f));
        mat.SetFloat("_Metallic", 0.6f);
        mat.SetFloat("_Smoothness", 0.3f);
        var chain = new GameObject("Chain").transform;
        chain.SetParent(box.transform, true);
        var top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        int count = Mathf.CeilToInt((WallHeight - top.y) / link);
        for (int i = 0; i < count; i++)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = "Link";
            Object.DestroyImmediate(piece.GetComponent<Collider>());
            piece.transform.SetParent(chain, true);
            piece.transform.position = top + Vector3.up * (link * (i + 0.5f));
            // alternate links turn 90 degrees, like a real chain
            piece.transform.rotation = Quaternion.Euler(0f, i % 2 == 0 ? 0f : 90f, 0f);
            piece.transform.localScale = new Vector3(0.035f, link * 1.1f, 0.012f);
            piece.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    static Bounds Bounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var b = renderers[0].bounds;
        foreach (var r in renderers)
            b.Encapsulate(r.bounds);
        return b;
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    [MenuItem("DeepCoo/Build Maze Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[MazeBuilder] Stop Play mode before building the maze.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int rows = Layout.Length;
        int cols = Layout[0].Length;
        float width = cols * CellSize;
        float depth = rows * CellSize;

        var floorMat = CreateMaterial("Maze_Floor", new Color(0.22f, 0.21f, 0.2f));
        var wallMat = CreateMaterial("Maze_Wall", new Color(0.38f, 0.37f, 0.35f));
        ApplyWallTextures(wallMat);
        var ceilingMat = CreateMaterial("Maze_Ceiling", new Color(0.15f, 0.15f, 0.15f));

        var root = new GameObject("Maze");

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(root.transform);
        floor.transform.localPosition = new Vector3(width / 2f, -0.05f, -depth / 2f);
        floor.transform.localScale = new Vector3(width, 0.1f, depth);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;
        GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);

        // The ceiling is on in the game (hanging props hang from it) but hidden in the Scene view
        // so the maze can still be seen from above.
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling";
        ceiling.transform.SetParent(root.transform);
        ceiling.transform.localPosition = new Vector3(width / 2f, WallHeight + 0.05f, -depth / 2f);
        ceiling.transform.localScale = new Vector3(width, 0.1f, depth);
        ceiling.GetComponent<Renderer>().sharedMaterial = ceilingMat;
        SceneVisibilityManager.instance.Hide(ceiling, true);

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
                ApplyWorldUVs(wall);
                GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            }
        }

        var markers = new GameObject("Markers");
        markers.transform.SetParent(root.transform);
        foreach (var m in Markers)
        {
            if (m.Name == "MonsterSpawn" && PlaceMonster(new Vector3(m.Col * CellSize, 0f, -m.Row * CellSize)))
                continue;
            if (m.Name == "ExitDoor" && PlaceExitHatch(markers.transform, new Vector3(m.Col * CellSize, 0f, -m.Row * CellSize)))
                continue;
            if (m.Name == "PlayerStart")
                continue;   // the player stands there; a marker would just get in the way
            var go = GameObject.CreatePrimitive(m.Shape);
            go.name = m.Name;
            go.transform.SetParent(markers.transform);
            float height = m.Shape == PrimitiveType.Capsule ? 1f : 0.4f;
            go.transform.localPosition = new Vector3(m.Col * CellSize, height, -m.Row * CellSize);
            go.transform.localScale = m.Shape == PrimitiveType.Capsule ? Vector3.one : Vector3.one * 0.6f;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Marker_" + m.Name, m.Color);
        }

        PlacePropPlaceholders(root.transform);
        PlaceDrawers(root.transform);

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

        CreatePlayer(Markers[0]);
        CreateBatterySpawner();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        Selection.activeGameObject = root;
        Debug.Log($"[MazeBuilder] Built {ScenePath} ({cols}x{rows} cells, {walls.transform.childCount} wall blocks).");
    }

    // Puts the rigged monster model at the spawn point, facing up the corridor (north),
    // with its movement, senses, walking and tentacle scripts.
    // Returns false when the model hasn't been imported, so the placeholder marker is used.
    static bool PlaceMonster(Vector3 position)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterModelPath);
        if (model == null)
            return false;
        var monster = (GameObject)PrefabUtility.InstantiatePrefab(model);
        monster.name = "PigeonMonster";
        monster.transform.SetPositionAndRotation(position, Quaternion.identity);
        monster.transform.localScale = Vector3.one * MonsterScale;
        monster.AddComponent<MonsterWander>();
        monster.AddComponent<MonsterSenses>();
        monster.AddComponent<MonsterGait>();
        monster.AddComponent<TentacleWiggle>();
        return true;
    }

    // The way out: a floor hatch with the padlock on its hasp, lying front face up with its hinges
    // toward the north wall. Size and place were set by hand in the editor (about 2 m square, in
    // the north-east corner of the exit cell). Returns false when the model hasn't been imported.
    static bool PlaceExitHatch(Transform parent, Vector3 position)
    {
        var asset = PropModels.Load("Hatch");
        if (asset == null)
            return false;
        var root = new GameObject("ExitDoor").transform;
        root.SetParent(parent);
        root.position = position + ExitHatchOffset;
        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        model.name = "Model";
        model.transform.SetParent(root, false);
        // the import's rotation stands it up with its front along +Z; lay that front face up
        model.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward) * asset.transform.localRotation;
        model.transform.localPosition = ExitHatchModelPosition;
        model.transform.localScale = ExitHatchModelScale;
        foreach (var c in model.GetComponentsInChildren<Collider>())
            Object.DestroyImmediate(c);
        return true;
    }

    static readonly Vector3 ExitHatchOffset = new Vector3(1.069f, 0f, 1.07f);          // from the exit cell's centre
    static readonly Vector3 ExitHatchModelPosition = new Vector3(-0.07f, 0.1382f, -1.0688f);
    static readonly Vector3 ExitHatchModelScale = new Vector3(2.1183f, 2.6278f, 2.1183f);

    static void CreatePlayer(Marker spawn)
    {
        var player = new GameObject("Player");
        player.transform.position = new Vector3(spawn.Col * CellSize, 0f, -spawn.Row * CellSize);
        player.transform.rotation = Quaternion.identity;   // facing north, out of the start room
        var controller = player.AddComponent<CharacterController>();
        controller.radius = 0.35f;
        controller.stepOffset = 0.3f;
        var fps = player.AddComponent<FirstPersonController>();
        player.AddComponent<PlayerInteractor>();
        player.AddComponent<MicrophoneNoise>();

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0f, fps.standEyeHeight, 0f);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.fieldOfView = 70f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.AddComponent<AudioListener>();

        // The flashlight held low in the right hand; the beam comes out just past its lens.
        var hand = new GameObject("FlashlightHand");
        hand.transform.SetParent(camGo.transform, false);
        hand.transform.localPosition = new Vector3(0.2f, -0.2f, 0.28f);
        hand.transform.localRotation = Quaternion.Euler(4f, -6f, 0f);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightModelPath);
        if (model != null)
        {
            var held = (GameObject)PrefabUtility.InstantiatePrefab(model);
            held.transform.SetParent(hand.transform, false);
            foreach (var r in held.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        var flashGo = new GameObject("Flashlight");
        flashGo.transform.SetParent(hand.transform, false);
        flashGo.transform.localPosition = new Vector3(0f, 0f, 0.14f);
        var flashlight = flashGo.AddComponent<Light>();
        flashlight.type = LightType.Spot;
        flashlight.range = 18f;
        flashlight.spotAngle = 55f;
        flashlight.innerSpotAngle = 30f;
        flashlight.intensity = 4f;
        flashlight.color = new Color(1f, 0.95f, 0.85f);
        flashlight.shadows = LightShadows.Soft;

        fps.cameraRoot = camGo.transform;
        fps.flashlight = flashlight;
        player.AddComponent<FlashlightBattery>().flashlight = flashlight;
        var dread = player.AddComponent<MonsterProximityFX>();
        dread.cameraRoot = camGo.transform;
        dread.flashlight = flashlight;
    }

    static void CreateBatterySpawner()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(BatteryModelPath);
        if (model == null)
            return;
        var spawner = new GameObject("BatterySpawner").AddComponent<BatterySpawner>();
        spawner.batteryModel = model;
    }

    // Replaces the cube's 0..1 UVs with world-space UVs so the texture keeps the same
    // size on every wall block and lines up across neighbouring blocks.
    static void ApplyWorldUVs(GameObject wall)
    {
        var filter = wall.GetComponent<MeshFilter>();
        var mesh = Object.Instantiate(filter.sharedMesh);
        mesh.name = wall.name + "_Mesh";

        var t = wall.transform;
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var uvs = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var p = t.TransformPoint(vertices[i]);
            var n = normals[i];
            Vector2 uv;
            if (Mathf.Abs(n.x) > 0.5f) uv = new Vector2(p.z, p.y);
            else if (Mathf.Abs(n.y) > 0.5f) uv = new Vector2(p.x, p.z);
            else uv = new Vector2(p.x, p.y);
            uvs[i] = uv / WallTileMeters;
        }
        mesh.uv = uvs;
        mesh.RecalculateTangents();
        filter.sharedMesh = mesh;
    }

    static void ApplyWallTextures(Material mat)
    {
        var albedo = LoadTexture(WallAlbedo, TextureImporterType.Default, true);
        var normal = LoadTexture(WallNormal, TextureImporterType.NormalMap, false);
        var occlusion = LoadTexture(WallOcclusion, TextureImporterType.Default, false);
        if (albedo == null)
            return;

        mat.SetTexture("_BaseMap", albedo);
        mat.SetColor("_BaseColor", Color.white);
        mat.color = Color.white;
        mat.SetFloat("_Smoothness", 0.15f);
        mat.SetTexture("_BumpMap", normal);
        mat.SetTexture("_OcclusionMap", occlusion);
        SetKeyword(mat, "_NORMALMAP", normal != null);
        SetKeyword(mat, "_OCCLUSIONMAP", occlusion != null);
        EditorUtility.SetDirty(mat);
    }

    static void SetKeyword(Material mat, string keyword, bool on)
    {
        if (on) mat.EnableKeyword(keyword);
        else mat.DisableKeyword(keyword);
    }

    static Texture2D LoadTexture(string file, TextureImporterType type, bool sRGB)
    {
        if (string.IsNullOrEmpty(file))
            return null;
        string path = $"{WallTextureFolder}/{file}";
        if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
            (importer.textureType != type || importer.sRGBTexture != sRGB))
        {
            importer.textureType = type;
            importer.sRGBTexture = sRGB;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
