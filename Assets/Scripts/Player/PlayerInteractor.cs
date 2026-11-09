using UnityEngine;
using UnityEngine.InputSystem;

// The E key. Look at a hiding spot and:
//   - tap E to get in at once, which is loud (the monster can hear it), or
//   - hold E until the ring fills to do it slowly and quietly.
// While hidden the same key gets you out. Other usable things (Interactable: puzzle clues, the
// padlock) just take a tap. Notes they show hold the view still and stay on screen until E is
// pressed again; a stack of notes is read one sheet at a time, A/D or the arrow keys turning
// the pages. The paper is the scene's "NoteView" canvas (Paper with Title, Body, Footer,
// LeftArrow, RightArrow, PageHint), laid out by hand in the Scene view; it's hidden until a
// note is read. Draws the prompt, the hold ring, and the view through the slats.
[RequireComponent(typeof(FirstPersonController))]
public class PlayerInteractor : MonoBehaviour
{
    public float reach = 1.8f;
    public float quietHoldSeconds = 1.6f;
    public float tapSeconds = 0.25f;   // releasing before this counts as a tap

    FirstPersonController fps;
    GameObject noteView;
    TMPro.TMP_Text noteTitle, noteBody, noteFooter;
    GameObject leftArrow, rightArrow, pageHint;
    HidingSpot target;     // what we're looking at, or the spot we're inside
    HidingSpot hiddenIn;
    Interactable usable;   // a non-hiding thing we're looking at
    float holdTime;
    bool holding;
    Note[] notes;          // the sheets being read, if any
    int page;

    // A sheet: a title and text, or (a picture hint) a row of four boxes with a picture in one.
    public struct Note
    {
        public string title;   // may be empty
        public string text;
        public Sprite picture; // set: shown in box `box` (0-3) instead of the text
        public int box;
        public Note(string title, string text) { this.title = title; this.text = text; picture = null; box = 0; }
        public Note(string title, Sprite picture, int box) { this.title = title; text = ""; this.picture = picture; this.box = box; }
    }

    public HidingSpot HiddenIn => hiddenIn;
    public static bool ReadingNote { get; private set; }

    void Awake()
    {
        fps = GetComponent<FirstPersonController>();
        noteView = GameObject.Find("NoteView");
        if (noteView == null)
        {
            Debug.LogWarning("[PlayerInteractor] No NoteView in the scene; notes can't be shown.");
            return;
        }
        var paper = noteView.transform.Find("Paper");
        noteTitle = paper.Find("Title").GetComponent<TMPro.TMP_Text>();
        noteBody = paper.Find("Body").GetComponent<TMPro.TMP_Text>();
        noteFooter = paper.Find("Footer").GetComponent<TMPro.TMP_Text>();
        leftArrow = paper.Find("LeftArrow").gameObject;
        rightArrow = paper.Find("RightArrow").gameObject;
        pageHint = paper.Find("PageHint").gameObject;
        var row = paper.Find("Pictures");
        if (row != null)
        {
            pictureRow = row.gameObject;
            for (int i = 0; i < row.childCount; i++)
                pictureBoxes.Add(row.GetChild(i).Find("Picture").GetComponent<UnityEngine.UI.Image>());
        }
        noteView.SetActive(false);
    }

    GameObject pictureRow;
    readonly System.Collections.Generic.List<UnityEngine.UI.Image> pictureBoxes = new System.Collections.Generic.List<UnityEngine.UI.Image>();

    // Keeps the paper in step with the sheet being read.
    void LateUpdate()
    {
        if (noteView == null)
            return;
        bool show = notes != null;
        if (noteView.activeSelf != show)
            noteView.SetActive(show);
        if (!show)
            return;
        bool many = notes.Length > 1;
        var sheet = notes[page];
        noteTitle.text = sheet.title;
        bool drawn = sheet.picture != null && pictureRow != null;
        noteBody.text = drawn ? "" : sheet.text;
        if (pictureRow != null)
        {
            pictureRow.SetActive(drawn);
            for (int i = 0; i < pictureBoxes.Count; i++)
            {
                pictureBoxes[i].enabled = drawn && i == sheet.box;
                if (drawn && i == sheet.box) pictureBoxes[i].sprite = sheet.picture;
            }
        }
        noteFooter.text = many ? $"{page + 1} / {notes.Length}        [E] 닫기" : "[E] 닫기";
        leftArrow.SetActive(many && page > 0);
        rightArrow.SetActive(many && page < notes.Length - 1);
        pageHint.SetActive(many);
    }

    public void ShowNote(string text) => ShowNotes(new[] { new Note("", text) });

    public void ShowNotes(Note[] sheets)
    {
        notes = sheets;
        page = 0;
        ReadingNote = true;
        fps.inputLocked = true;
    }

    void CloseNotes()
    {
        if (notes == null)
            return;
        notes = null;
        ReadingNote = false;
        fps.inputLocked = false;
    }

    void OnDestroy() => ReadingNote = false;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (PadlockInput.IsOpen || BirdcageInspect.IsOpen || Inventory.IsOpen)
        {
            target = null;
            usable = null;
            if (notes != null)   // a close-up replaces any note being read
            {
                notes = null;
                ReadingNote = false;
            }
            return;
        }
        if (notes != null)
        {
            if (keyboard.eKey.wasPressedThisFrame)
                CloseNotes();
            else if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                page = Mathf.Min(page + 1, notes.Length - 1);
            else if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                page = Mathf.Max(page - 1, 0);
            return;
        }

        target = hiddenIn != null ? hiddenIn : FindTarget(out usable);
        if (target != null)
            usable = null;
        else if (usable != null && usable.CanUse && keyboard.eKey.wasPressedThisFrame)
        {
            usable.Use(this);
            usable = null;
            return;
        }

        if (target == null)
        {
            holding = false;
            holdTime = 0f;
            return;
        }

        if (keyboard.eKey.wasPressedThisFrame)
        {
            holding = true;
            holdTime = 0f;
        }
        if (!holding)
            return;

        if (keyboard.eKey.isPressed)
        {
            holdTime += Time.deltaTime;
            if (holdTime >= quietHoldSeconds)
            {
                holding = false;
                Use(target, loud: false);
            }
        }
        else
        {
            holding = false;
            if (holdTime < tapSeconds)
                Use(target, loud: true);
            holdTime = 0f;
        }
    }

    HidingSpot FindTarget(out Interactable other)
    {
        other = null;
        var eye = fps.cameraRoot;
        if (!Physics.Raycast(eye.position, eye.forward, out var hit, reach, ~0, QueryTriggerInteraction.Ignore))
            return null;
        var spot = hit.collider.GetComponentInParent<HidingSpot>();
        if (spot == null)
        {
            other = hit.collider.GetComponentInParent<Interactable>();
            if (other != null && !other.CanUse)
                other = null;
        }
        return spot;
    }

    void Use(HidingSpot spot, bool loud)
    {
        if (hiddenIn == null)
        {
            hiddenIn = spot;
            fps.EnterHiding(spot.InsidePoint, spot.Facing, spot.EyeRaise);
            ShowInsideOf(spot, false);
        }
        else
        {
            fps.ExitHiding(spot.OutsidePoint);
            ShowInsideOf(spot, true);
            hiddenIn = null;
        }
        if (loud)
            NoiseEvents.Emit(spot.transform.position, spot.loudNoiseRange);
        holdTime = 0f;
    }

    // From inside, the hiding spot's own walls would show through the gaps between the slats: they
    // aren't drawn while the player is in it (it still casts its shadow).
    readonly System.Collections.Generic.Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode> hiddenRenderers =
        new System.Collections.Generic.Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode>();

    void ShowInsideOf(HidingSpot spot, bool show)
    {
        if (!show)
        {
            hiddenRenderers.Clear();
            foreach (var r in spot.GetComponentsInChildren<Renderer>())
            {
                hiddenRenderers[r] = r.shadowCastingMode;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
            return;
        }
        foreach (var pair in hiddenRenderers)
            if (pair.Key != null)
                pair.Key.shadowCastingMode = pair.Value;
        hiddenRenderers.Clear();
    }

    void OnGUI()
    {
        if (PauseMenu.IsOpen)
            return;   // drawn over the pause menu otherwise (OnGUI is on top of every canvas)
        if (hiddenIn != null)
            DrawSlats(hiddenIn.kind);

        if (PadlockInput.IsOpen || BirdcageInspect.IsOpen || Inventory.IsOpen)
            return;   // the close-up draws itself
        if (notes != null)
            return;   // the NoteView paper is up
        if (usable != null)
        {
            DrawPrompt($"[E] {usable.Prompt}");
            return;
        }
        if (target == null)
            return;

        string action = hiddenIn != null ? "나오기" : $"{target.Label}에 숨기";
        DrawPrompt($"[E] {action}    (길게 누르면 조용히)");

        if (holding && holdTime >= tapSeconds)
            DrawHoldBar(holdTime / quietHoldSeconds);
    }

    static void DrawPrompt(string text)
    {
        var style = new GUIStyle(GUI.skin.label) { font = GameFont.Regular, fontSize = 20, alignment = TextAnchor.MiddleCenter };
        var rect = new Rect(0f, Screen.height * 0.72f, Screen.width, 30f);
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        GUI.color = Color.white;
        GUI.Label(rect, text, style);
    }


    static void DrawHoldBar(float t)
    {
        const float w = 220f, h = 8f;
        var x = (Screen.width - w) / 2f;
        var y = Screen.height * 0.72f + 38f;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = new Color(0.85f, 0.85f, 0.8f, 0.9f);
        GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(t), h), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    // Dark overlay with gaps: horizontal vents for lockers and wardrobes, a thin gap under the
    // lid for the bin.
    public static readonly Color SlatColor = new Color(0.01f, 0.01f, 0.01f, 0.97f);

    static void DrawSlats(HidingSpot.Kind kind)
    {
        float W = Screen.width, H = Screen.height;
        GUI.color = SlatColor;
        foreach (var r in SlatRects(kind))
            GUI.DrawTexture(new Rect(r.x * W, r.y * H, r.width * W, r.height * H), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    // the dark parts of the overlay as fractions of the screen (0..1, y down), also used by the
    // pause menu to draw the same slats behind itself
    public static System.Collections.Generic.List<Rect> SlatRects(HidingSpot.Kind kind)
    {
        var rects = new System.Collections.Generic.List<Rect>();
        if (kind == HidingSpot.Kind.TrashBin)
        {
            float gapTop = 0.36f, gapBottom = 0.5f;
            rects.Add(new Rect(0f, 0f, 1f, gapTop));
            rects.Add(new Rect(0f, gapBottom, 1f, 1f - gapBottom));
        }
        else
        {
            int slats = kind == HidingSpot.Kind.Locker ? 7 : 9;
            float top = 0.12f;
            float bottom = kind == HidingSpot.Kind.Locker ? 0.66f : 0.68f;
            float left = kind == HidingSpot.Kind.Locker ? 0.14f : 0.08f;
            float right = 1f - left;
            float pitch = (bottom - top) / slats;
            float gap = pitch * 0.45f;
            rects.Add(new Rect(0f, 0f, 1f, top));
            rects.Add(new Rect(0f, bottom, 1f, 1f - bottom));
            rects.Add(new Rect(0f, top, left, bottom - top));
            rects.Add(new Rect(right, top, 1f - right, bottom - top));
            for (int i = 0; i < slats; i++)
                rects.Add(new Rect(left, top + i * pitch + gap, right - left, pitch - gap));
        }
        return rects;
    }
}
