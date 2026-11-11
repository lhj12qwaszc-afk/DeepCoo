using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Tab opens the player's belongings: the flashlight and tracker they carry, and every hint note
// they have read (to read again). The world doesn't stop while it's open: the monster keeps
// moving, only the player stands still. W A S D or the arrow keys pick a slot; its picture, name
// and text show on the right. Tab closes it.
// Lives on the scene's "InventoryView" canvas (laid out in the Scene view; the child "Root" is
// hidden until Tab), built by DeepCoo > Build Inventory View.
public class Inventory : MonoBehaviour
{
    public const int Columns = 4;
    public const int SlotCount = 8;   // 4 x 2

    public GameObject root;
    public RectTransform grid;
    public Image detailIcon;
    public TMP_Text detailTitle, detailBody;
    public Sprite slotSprite, slotSelectedSprite;
    public Sprite noteIcon, crumpledNoteIcon, flashlightIcon, trackerIcon;

    struct Entry
    {
        public string title, body;
        public Sprite icon;
        public Sprite picture;   // a picture hint's drawing, shown large instead of the icon
    }

    public static bool IsOpen { get; private set; }

    static Inventory instance;
    readonly List<Entry> entries = new List<Entry>();
    readonly List<Image> slots = new List<Image>();
    readonly List<Image> slotIcons = new List<Image>();
    int selected;
    FirstPersonController fps;
    bool busyLastFrame;

    void Awake()
    {
        instance = this;
        IsOpen = false;
        root.SetActive(false);
        BuildSlots();
    }

    // The flashlight and tracker go in once picked up (PlayerGear).
    public static void AddGear(PlayerGear.Item item)
    {
        if (instance == null)
            return;
        if (item == PlayerGear.Item.Flashlight)
            instance.entries.Add(new Entry { title = "손전등", icon = instance.flashlightIcon,
                body = "어둠을 비춘다.\n배터리가 닳으면 서랍을 뒤져 새 배터리를 찾자." });
        else
            instance.entries.Add(new Entry { title = "위치 추적기", icon = instance.trackerIcon,
                body = "주변의 벽과 내 위치(초록)를 보여준다.\n가까이 온 괴물은 빨간 점으로 나타난다." });
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            IsOpen = false;
        }
    }

    // Notes read from a clue spot are kept (once each) so they can be read again here.
    public static void Remember(IEnumerable<PlayerInteractor.Note> notes)
    {
        if (instance == null)
            return;
        foreach (var note in notes)
        {
            string title = string.IsNullOrEmpty(note.title) ? "쪽지" : note.title;
            // a picture hint is told apart by its drawing and box; its text is the row of boxes
            string body = note.picture == null ? note.text : PictureRow(note.box);
            if (instance.entries.Exists(e => e.title == title && e.body == body && e.picture == note.picture))
                continue;
            if (instance.entries.Count >= SlotCount)
                return;
            instance.entries.Add(new Entry { title = title, body = body, picture = note.picture,
                icon = title.Contains("구겨진") ? instance.crumpledNoteIcon : instance.noteIcon });
        }
    }

    // "□  □  ■  □": which of the four boxes the drawing was in
    static string PictureRow(int box)
    {
        var parts = new string[4];
        for (int i = 0; i < 4; i++)
            parts[i] = i == box ? "■" : "□";
        return "그림이 그려진 칸\n\n" + string.Join("   ", parts);
    }

    void BuildSlots()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            var slot = new GameObject($"Slot_{i + 1}", typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(grid, false);
            var image = slot.GetComponent<Image>();
            image.sprite = slotSprite;
            image.type = Image.Type.Sliced;          // the frame stretches to the tall slot
            image.pixelsPerUnitMultiplier = 5.5f;    // keeps the frame edge thin
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(slot.transform, false);
            var rt = (RectTransform)icon.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(24f, 24f); rt.offsetMax = new Vector2(-24f, -24f);
            var iconImage = icon.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            slots.Add(image);
            slotIcons.Add(iconImage);
        }
    }

    readonly Dictionary<Sprite, Sprite> whiteLines = new Dictionary<Sprite, Sprite>();

    // the drawing with its colours turned around (dark lines become light), its see-through parts kept;
    // made once per drawing (copied through the GPU, so the texture needn't be readable)
    Sprite WhiteLines(Sprite drawing)
    {
        if (whiteLines.TryGetValue(drawing, out var done))
            return done;
        var src = drawing.texture;
        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i++)
            px[i] = new Color32((byte)(255 - px[i].r), (byte)(255 - px[i].g), (byte)(255 - px[i].b), px[i].a);
        tex.SetPixels32(px);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        sprite.name = drawing.name + "_white";
        whiteLines[drawing] = sprite;
        return sprite;
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k == null)
            return;
        if (fps == null)
            fps = FindFirstObjectByType<FirstPersonController>();

        if (!IsOpen)
        {
            // busy last frame too: the Tab that just closed the padlock mustn't open the bag as well
            bool busy = busyLastFrame || Interactable.CloseUpOpen || (fps != null && fps.inputLocked);
            busyLastFrame = Interactable.CloseUpOpen || (fps != null && fps.inputLocked);
            if (k.tabKey.wasPressedThisFrame && !busy)
                Open();
            return;
        }

        if (k.tabKey.wasPressedThisFrame)
        {
            Close();
            return;
        }
        int move = 0;
        if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) move = 1;
        if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) move = -1;
        if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame) move = Columns;
        if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) move = -Columns;
        if (move != 0 && selected + move >= 0 && selected + move < SlotCount)
        {
            selected += move;
            Refresh();
        }
    }

    void Open()
    {
        IsOpen = true;
        root.SetActive(true);
        if (fps != null) fps.inputLocked = true;
        Refresh();
    }

    void Close()
    {
        IsOpen = false;
        root.SetActive(false);
        if (fps != null) fps.inputLocked = false;
    }

    void Refresh()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].sprite = i == selected ? slotSelectedSprite : slotSprite;
            slots[i].color = i == selected ? new Color(1f, 0.55f, 0.4f) : Color.white;   // the pick glows warm
            bool has = i < entries.Count;
            slotIcons[i].enabled = has;
            if (has) slotIcons[i].sprite = entries[i].icon;
        }
        bool any = selected < entries.Count;
        detailIcon.enabled = any;
        // a drawn hint is dark pencil on clear: here it's shown in white lines so it reads on the dark panel
        if (any) detailIcon.sprite = entries[selected].picture != null ? WhiteLines(entries[selected].picture) : entries[selected].icon;
        detailTitle.text = any ? entries[selected].title : "";
        detailBody.text = any ? entries[selected].body : "";
    }
}
