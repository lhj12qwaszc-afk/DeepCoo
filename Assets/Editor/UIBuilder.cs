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

        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
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

        AddTitle(canvas.transform);

        // the buttons, stacked down the left side
        var column = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        column.SetParent(canvas.transform, false);
        column.anchorMin = column.anchorMax = new Vector2(0f, 0.5f);
        column.pivot = new Vector2(0f, 0.5f);
        column.anchoredPosition = new Vector2(110f, -90f);
        column.sizeDelta = new Vector2(720f, 3 * 240f - 2 * 20f);
        var layout = column.GetComponent<VerticalLayoutGroup>();
        layout.spacing = -20f;   // the pictures have see-through margins above and below
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

    // The game's title (Title.png) at the top left, over the dark side of the background and
    // above the buttons. Replaces an existing "Title".
    public static void AddTitle(Transform canvas)
    {
        MakeSprites(MenuFolder);
        var old = canvas.Find("Title");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var title = MakeImage(canvas, "Title", Load(MenuFolder, "Title"));
        title.preserveAspect = true;
        title.raycastTarget = false;
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(110f, -40f), new Vector2(720f, 270f), new Vector2(0f, 1f));
        title.transform.SetSiblingIndex(1);   // just above the background
    }

    // ---------- settings panel ----------

    const string SettingsFolder = "Assets/UI/Settings";

    // Replaces the main menu's "SettingsPanel" (the MainMenu scene must be open) with the real one:
    // brightness, sound volume, microphone on/off and microphone sensitivity, and a back button.
    [MenuItem("DeepCoo/Build Settings Panel")]
    public static void BuildSettingsPanel()
    {
        var canvasGo = GameObject.Find("MainMenuCanvas");
        var menu = canvasGo != null ? canvasGo.GetComponent<MainMenu>() : null;
        if (menu == null)
        {
            Debug.LogError("[UIBuilder] open the MainMenu scene first");
            return;
        }
        var dim = MakeSettingsPanel(canvasGo.transform, menu.CloseSettings);
        menu.settingsPanel = dim;
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        Debug.Log("[UIBuilder] settings panel built");
    }

    // The settings panel (replacing an existing "SettingsPanel" under the canvas), hidden. Back
    // calls close; Save saves and hides the panel itself. Shared by the main menu and the pause menu.
    static GameObject MakeSettingsPanel(Transform canvas, UnityEngine.Events.UnityAction close)
    {
        MakeSprites(SettingsFolder);
        var old = canvas.Find("SettingsPanel");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        // the menu behind is dimmed while the panel is up
        var dim = MakeImage(canvas, "SettingsPanel", null);
        Stretch(dim.rectTransform);
        dim.color = new Color(0f, 0f, 0f, 0.7f);
        var panel = MakeImage(dim.transform, "Plate", Load(SettingsFolder, "Panel"));
        panel.rectTransform.sizeDelta = new Vector2(1200f, 750f);   // the picture's own shape
        var settings = panel.gameObject.AddComponent<SettingsMenu>();

        var title = MakeText(panel.transform, "Title", "설정", Bold, 56, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(600f, 80f));

        settings.brightness = MakeSettingSlider(panel.transform, "Brightness", "밝기", 165f, out settings.brightnessFill);
        settings.volume = MakeSettingSlider(panel.transform, "Volume", "소리 크기", 60f, out settings.volumeFill);
        settings.micOn = MakeSettingToggle(panel.transform, "MicOn", "마이크", -50f);
        settings.micSensitivity = MakeSettingSlider(panel.transform, "MicSensitivity", "마이크 민감도", -160f, out settings.micSensitivityFill);

        // bottom row: reset (controls back to the start values), save (keep and close), back (close without saving)
        var reset = MakeButton(panel.transform, "ResetButton", "초기화");
        var save = MakeButton(panel.transform, "SaveButton", "저장");
        var back = MakeButton(panel.transform, "BackButton", "돌아가기");
        var row = new[] { reset, save, back };
        for (int i = 0; i < row.Length; i++)
        {
            Place((RectTransform)row[i].transform, new Vector2(0.5f, 0f), new Vector2((i - 1) * 320f, 105f), new Vector2(290f, 105f));
            row[i].GetComponentInChildren<TMP_Text>().fontSize = 36;
            UsePanelButton(row[i], new Vector2(290f, 80f));
        }
        UnityEventTools.AddPersistentListener(reset.onClick, settings.ResetToDefaults);
        UnityEventTools.AddPersistentListener(save.onClick, settings.Save);
        UnityEventTools.AddPersistentListener(back.onClick, close);

        dim.gameObject.SetActive(false);
        return dim.gameObject;
    }

    // ---------- pause menu (ESC in the game) ----------

    // Adds the "PauseMenu" canvas to the open game scene (replacing an old one): a dimmed screen
    // with a metal plate holding Continue, Settings, Main Menu and Quit, plus the settings panel.
    [MenuItem("DeepCoo/Build Pause Menu")]
    public static void BuildPauseMenu()
    {
        MakeSprites(MenuFolder);
        var old = GameObject.Find("PauseMenu");
        if (old != null)
            Object.DestroyImmediate(old);
        var canvas = MakeCanvas("PauseMenu", 50);   // over the game's other screens
        var pause = canvas.gameObject.AddComponent<PauseMenu>();

        var dim = MakeImage(canvas.transform, "Root", null);
        Stretch(dim.rectTransform);
        dim.color = new Color(0f, 0f, 0f, 0.7f);
        var plate = MakeImage(dim.transform, "Plate", Load(SettingsFolder, "Panel"));
        plate.rectTransform.sizeDelta = new Vector2(820f, 900f);
        plate.type = Image.Type.Sliced;            // a taller plate than the picture, rivets kept in the corners
        plate.pixelsPerUnitMultiplier = 1.5f;
        var title = MakeText(plate.transform, "Title", "일시 정지", Bold, 60, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(600f, 90f));

        var labels = new[] { ("ContinueButton", "계속하기"), ("SettingsButton", "설정"), ("MainMenuButton", "메인 메뉴로"), ("QuitButton", "게임 종료") };
        var buttons = new Button[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            buttons[i] = MakeButton(plate.transform, labels[i].Item1, labels[i].Item2);
            Place((RectTransform)buttons[i].transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f - i * 150f), new Vector2(600f, 200f));
            buttons[i].GetComponentInChildren<TMP_Text>().fontSize = 50;
            UsePanelButton(buttons[i], new Vector2(560f, 110f));
        }
        UnityEventTools.AddPersistentListener(buttons[0].onClick, pause.Resume);
        UnityEventTools.AddPersistentListener(buttons[1].onClick, pause.OpenSettings);
        UnityEventTools.AddPersistentListener(buttons[2].onClick, pause.ToMainMenu);
        UnityEventTools.AddPersistentListener(buttons[3].onClick, pause.Quit);

        pause.root = dim.gameObject;
        pause.settingsPanel = MakeSettingsPanel(canvas.transform, pause.CloseSettings);
        dim.gameObject.SetActive(false);

        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[UIBuilder] pause menu built");
    }

    static TextMeshProUGUI MakeRowLabel(Transform panel, string name, string label, float y)
    {
        var text = MakeText(panel, name + "Label", label, Bold, 36, TextAlignmentOptions.Left);
        Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-330f, y), new Vector2(340f, 60f));
        return text;
    }

    // a row: the label on the left, the slider (dark groove, red bar, dial) on the right
    static Slider MakeSettingSlider(Transform panel, string name, string label, float y, out Image fill)
    {
        MakeRowLabel(panel, name, label, y);
        var root = new GameObject(name, typeof(RectTransform), typeof(Slider)).GetComponent<RectTransform>();
        root.SetParent(panel, false);
        Place(root, new Vector2(0.5f, 0.5f), new Vector2(120f, y), new Vector2(690f, 230f));   // picture shape 3:1

        var track = MakeImage(root, "Track", Load(SettingsFolder, "Slider_Track"));
        Stretch(track.rectTransform);
        fill = MakeImage(root, "Fill", Load(SettingsFolder, "Slider_Fill"));
        Stretch(fill.rectTransform);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.raycastTarget = false;

        // the dial slides between the bar's ends
        var area = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
        area.SetParent(root, false);
        Stretch(area);
        area.offsetMin = new Vector2(20f, 0f);
        area.offsetMax = new Vector2(-20f, 0f);
        var handle = MakeImage(area, "Handle", Load(SettingsFolder, "Slider_Handle"));
        handle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(110f, 0f);   // the slider stretches it to full height...
        handle.preserveAspect = true;                              // ...but the dial stays round

        var slider = root.GetComponent<Slider>();
        slider.targetGraphic = handle;
        slider.handleRect = handle.rectTransform;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.5f;
        slider.transition = Selectable.Transition.ColorTint;
        var colors = slider.colors;
        colors.highlightedColor = new Color(1f, 0.85f, 0.8f);
        colors.pressedColor = new Color(1f, 0.7f, 0.6f);
        colors.selectedColor = Color.white;
        slider.colors = colors;
        return slider;
    }

    // a row: the label on the left, the lever plate on the right (lit red when on)
    static Toggle MakeSettingToggle(Transform panel, string name, string label, float y)
    {
        MakeRowLabel(panel, name, label, y);
        var off = MakeImage(panel, name, Load(SettingsFolder, "Toggle_Off"));
        Place(off.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-90f, y), new Vector2(260f, 130f));   // picture shape 2:1
        var on = MakeImage(off.transform, "On", Load(SettingsFolder, "Toggle_On"));
        Stretch(on.rectTransform);
        on.raycastTarget = false;
        var toggle = off.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = off;
        toggle.graphic = on;
        toggle.toggleTransition = Toggle.ToggleTransition.None;
        toggle.isOn = true;
        return toggle;
    }

    // ---------- buttons on the metal plates (pause menu, settings panel) ----------

    static readonly Color DarkInk = new Color(0.1f, 0.08f, 0.07f);

    // The silver PanelButton (and PanelButton_Hover, red-lined, under the mouse when it exists)
    // instead of the dark menu button, so it stands out on the dark plate. size = the plate as
    // seen on screen: the picture is cut down to its plate (no see-through margin) and 9-sliced so
    // the rivets stay round. The label turns dark to read on the silver.
    public static void UsePanelButton(Button button, Vector2 size)
    {
        var plain = PlateSprite("PanelButton");
        if (plain == null)
            return;
        var hover = PlateSprite("PanelButton_Hover");
        var glow = button.GetComponent<MenuButtonGlow>();
        if (glow != null)
            Object.DestroyImmediate(glow);
        var image = button.GetComponent<Image>();
        image.sprite = plain;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = plain.rect.height / size.y;   // rivets scale with the button's height
        ((RectTransform)button.transform).sizeDelta = size;
        if (hover != null)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = hover, selectedSprite = hover, pressedSprite = hover };
        }
        else
        {
            button.transition = Selectable.Transition.ColorTint;   // until the red-lined picture comes
            var c = button.colors;
            c.highlightedColor = c.selectedColor = new Color(1f, 0.75f, 0.7f);
            button.colors = c;
        }
        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.color = DarkInk;
        EditorUtility.SetDirty(button);
        EditorUtility.SetDirty(image);
        if (label != null) EditorUtility.SetDirty(label);
    }

    // a sprite of just the plate in Assets/UI/MainMenu/<name>.png: the see-through margin is cut off
    // (found from the picture's opaque middle row and column), the rivet corners 9-sliced
    static Sprite PlateSprite(string name)
    {
        var path = $"{MenuFolder}/{name}.png";
        if (!System.IO.File.Exists(path))
            return null;
        if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.spriteImportMode != SpriteImportMode.Multiple)
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(System.IO.File.ReadAllBytes(path));
            int w = tex.width, h = tex.height, cx = w / 2, cy = h / 2;
            bool Solid(int x, int y) => tex.GetPixel(x, y).a > 0.8f;
            int left = 0, right = w - 1, bottom = 0, top = h - 1;
            while (left < cx && !Solid(left, cy)) left++;
            while (right > cx && !Solid(right, cy)) right--;
            while (bottom < cy && !Solid(cx, bottom)) bottom++;
            while (top > cy && !Solid(cx, top)) top--;
            Object.DestroyImmediate(tex);
            const int pad = 8;
            var rect = Rect.MinMaxRect(Mathf.Max(0, left - pad), Mathf.Max(0, bottom - pad), Mathf.Min(w, right + pad), Mathf.Min(h, top + pad));
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
#pragma warning disable 618
            ti.spritesheet = new[] { new SpriteMetaData { name = name, rect = rect, border = new Vector4(150f, 95f, 150f, 95f), alignment = 0 } };
#pragma warning restore 618
            ti.SaveAndReimport();
        }
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite s)
                return s;
        return null;
    }

    static Button MakeButton(Transform parent, string name, string label)
    {
        var image = MakeImage(parent, name, Load(MenuFolder, "Button"));
        var button = image.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.SpriteSwap;
        var hover = Load(MenuFolder, "MenuButton_Red") ?? Load(MenuFolder, "Button_Hover");   // red-rimmed under the mouse
        button.spriteState = new SpriteState { highlightedSprite = hover, selectedSprite = hover, pressedSprite = hover };
        var red = Load(MenuFolder, "MenuButton_Red");
        if (red != null)
            button.gameObject.AddComponent<MenuButtonGlow>().glow = red;   // a bigger red plate under the mouse
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

        var hint = MakeText(root, "Footer", "[Tab]  닫기", Regular, 24, TextAlignmentOptions.Center);
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
            var file = System.IO.Path.GetFileName(path);
            if (file.StartsWith("PanelButton"))
                continue;   // cut to their plate by PlateSprite
            var border = file.StartsWith("Slot") ? new Vector4(160f, 160f, 160f, 160f)
                : file.StartsWith("MenuButton_Red") ? new Vector4(260f, 324f, 260f, 340f)   // rivets stay in the corners
                : file == "Panel.png" && path.Contains("/Settings/") ? new Vector4(120f, 120f, 120f, 120f)
                : Vector4.zero;
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
