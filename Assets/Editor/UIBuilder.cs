using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Builds the game's screens from the images in Assets/UI (made outside Unity):
//   DeepCoo > Build Main Menu Scene   new scene Assets/Scenes/MainMenu.unity, first in the build
//   DeepCoo > Build Inventory View    adds the "InventoryView" canvas to the open scene
// Both can be laid out further by hand in the Scene view afterwards; running a menu item again
// replaces what it built (asks first).
public static class UIBuilder
{
    const string MenuFolder = "Assets/UI/MainMenu";
    const string InventoryFolder = "Assets/UI/Inventory";
    const string MenuScene = "Assets/Scenes/MainMenu.unity";
    static readonly Color Ink = new Color(0.86f, 0.83f, 0.76f);

    static TMP_FontAsset Regular => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/MaruBuri-Regular TMP.asset");
    static TMP_FontAsset Bold => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/MaruBuri-Bold TMP.asset");

    // ---------- main menu ----------

    [MenuItem("DeepCoo/Build Main Menu Scene")]
    public static void BuildMainMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        MakeSprites(MenuFolder);
        MakeSprites(InventoryFolder);
        var previous = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.tag = "MainCamera";

        var canvas = MakeCanvas("MainMenuCanvas", 0);
        var menu = canvas.gameObject.AddComponent<MainMenu>();

        var bgSprite = Load(MenuFolder, "Background");
        var bg = MakeImage(canvas.transform, "Background", bgSprite);
        Stretch(bg.rectTransform);
        var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = bgSprite != null ? bgSprite.rect.width / bgSprite.rect.height : 16f / 9f;

        // the buttons, stacked down the left side
        var column = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        column.SetParent(canvas.transform, false);
        column.anchorMin = column.anchorMax = new Vector2(0f, 0.5f);
        column.pivot = new Vector2(0f, 0.5f);
        column.anchoredPosition = new Vector2(150f, -60f);
        column.sizeDelta = new Vector2(480f, 3 * 160f + 2 * 30f);
        var layout = column.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 30f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;

        var start = MakeButton(column, "StartButton", "게임 시작");
        var settings = MakeButton(column, "SettingsButton", "설정");
        var quit = MakeButton(column, "QuitButton", "종료");
        UnityEventTools.AddPersistentListener(start.onClick, menu.StartGame);
        UnityEventTools.AddPersistentListener(settings.onClick, menu.OpenSettings);
        UnityEventTools.AddPersistentListener(quit.onClick, menu.Quit);

        // settings: an empty panel for now, with a way back
        var panel = MakeImage(canvas.transform, "SettingsPanel", Load(InventoryFolder, "Panel"));
        panel.rectTransform.sizeDelta = new Vector2(1100f, 584f);
        var title = MakeText(panel.transform, "Title", "설정", Bold, 48, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(600f, 70f));
        var note = MakeText(panel.transform, "Body", "준비 중이에요.", Regular, 30, TextAlignmentOptions.Center);
        Place(note.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(800f, 120f));
        var back = MakeButton(panel.transform, "BackButton", "돌아가기");
        Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(360f, 120f));
        UnityEventTools.AddPersistentListener(back.onClick, menu.CloseSettings);
        menu.settingsPanel = panel.gameObject;

        var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        events.GetComponent<EventSystem>().firstSelectedGameObject = null;

        EditorSceneManager.SaveScene(scene, MenuScene);
        AddToBuildFirst(MenuScene);
        if (!string.IsNullOrEmpty(previous))
            EditorSceneManager.OpenScene(previous);
        Debug.Log("[UIBuilder] main menu scene built: " + MenuScene);
    }

    static Button MakeButton(Transform parent, string name, string label)
    {
        var image = MakeImage(parent, name, Load(MenuFolder, "Button"));
        var button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.SpriteSwap;
        var hover = Load(MenuFolder, "Button_Hover");
        button.spriteState = new SpriteState { highlightedSprite = hover, selectedSprite = hover, pressedSprite = hover };
        var text = MakeText(image.transform, "Label", label, Bold, 44, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        text.raycastTarget = false;
        return button;
    }

    static void AddToBuildFirst(string path)
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == path);
        list.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ---------- inventory ----------

    [MenuItem("DeepCoo/Build Inventory View")]
    public static void BuildInventoryView()
    {
        MakeSprites(InventoryFolder);
        var old = GameObject.Find("InventoryView");
        if (old != null)
        {
            if (!EditorUtility.DisplayDialog("Inventory", "The scene already has an InventoryView. Replace it?", "Replace", "Cancel"))
                return;
            Undo.DestroyObjectImmediate(old);
        }

        var canvas = MakeCanvas("InventoryView", 20);
        var inv = canvas.gameObject.AddComponent<Inventory>();

        var root = new GameObject("Root", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        Stretch(root);
        var dim = MakeImage(root, "Dim", null);
        dim.color = new Color(0f, 0f, 0f, 0.6f);
        Stretch(dim.rectTransform);

        // the panel: slots filling the left side (4 x 2), on the right the picture above and the
        // name and text below
        var panel = MakeImage(root, "Panel", Load(InventoryFolder, "Panel"));
        panel.rectTransform.sizeDelta = new Vector2(1500f, 797f);

        var grid = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<RectTransform>();
        grid.SetParent(panel.transform, false);
        Place(grid, new Vector2(0f, 1f), new Vector2(80f, -80f), new Vector2(760f, 640f), new Vector2(0f, 1f));   // inside the panel's rim
        var g = grid.GetComponent<GridLayoutGroup>();
        g.spacing = new Vector2(12f, 14f);
        g.cellSize = new Vector2((760f - 3 * 12f) / Inventory.Columns, (640f - 14f) / 2f);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = Inventory.Columns;

        var icon = MakeImage(panel.transform, "DetailIcon", Load(InventoryFolder, "Icon_Note"));
        icon.preserveAspect = true;
        Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(1145f, -235f), new Vector2(300f, 300f));
        var name = MakeText(panel.transform, "DetailTitle", "첫 번째 힌트", Bold, 36, TextAlignmentOptions.Left);
        Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(880f, -420f), new Vector2(560f, 60f), new Vector2(0f, 1f));
        var body = MakeText(panel.transform, "DetailBody", "첫 번째 숫자는 라디오가 들려준다.", Regular, 28, TextAlignmentOptions.TopLeft);
        Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(880f, -490f), new Vector2(560f, 260f), new Vector2(0f, 1f));

        var hint = MakeText(root, "Footer", "W A S D  고르기        [Tab]  닫기", Regular, 24, TextAlignmentOptions.Center);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(900f, 40f));

        inv.root = root.gameObject;
        inv.grid = grid;
        inv.detailIcon = icon;
        inv.detailTitle = name;
        inv.detailBody = body;
        inv.slotSprite = Load(InventoryFolder, "Slot");
        inv.slotSelectedSprite = Load(InventoryFolder, "Slot_Selected");
        inv.noteIcon = Load(InventoryFolder, "Icon_Note");
        inv.crumpledNoteIcon = Load(InventoryFolder, "Icon_CrumpledNote");
        inv.flashlightIcon = Load(InventoryFolder, "Icon_Flashlight");
        inv.trackerIcon = Load(InventoryFolder, "Icon_Tracker");

        Undo.RegisterCreatedObjectUndo(canvas.gameObject, "Build Inventory View");
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[UIBuilder] InventoryView added to " + canvas.gameObject.scene.name);
    }

    // ---------- helpers ----------

    static void MakeSprites(string folder)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti))
                continue;
            // the slot frames stretch to any shape without thickening their edges (9-slice)
            var border = System.IO.Path.GetFileName(path).StartsWith("Slot") ? new Vector4(160f, 160f, 160f, 160f) : Vector4.zero;
            if (ti.textureType == TextureImporterType.Sprite && ti.spriteBorder == border)
                continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.spriteBorder = border;
            ti.SaveAndReimport();
        }
    }

    static Sprite Load(string folder, string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{name}.png");

    static Canvas MakeCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    static Image MakeImage(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        return image;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, string text, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSize = size;
        t.color = Ink;
        t.alignment = align;
        t.text = text;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }
}
