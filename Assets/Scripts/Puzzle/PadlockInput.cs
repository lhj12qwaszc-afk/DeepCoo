using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// The combination padlock on the exit hatch. E opens a close-up with four dials:
// A/D (or arrows) pick a dial, W/S turn it, number keys type straight in, E tries the code,
// Tab (or Esc, Backspace) steps back, shown at the bottom of the screen. The right code opens the hatch and ends the game; a wrong one
// rattles the lock loudly enough for the monster to hear.
public class PadlockInput : Interactable
{
    public PuzzleManager puzzle;
    public float rattleNoiseRange = 18f;

    readonly int[] dials = new int[4];
    int selected;
    bool open;
    bool escaped;
    float wrongUntil;
    FirstPersonController fps;
    int openedFrame = -1;

    // the number row runs 1..9, 0 in the Key enum
    static readonly Key[] DigitKeys = { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
                                        Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };

    public static bool IsOpen { get; private set; }

    public override string Prompt => "자물쇠 번호 맞추기";
    public override bool CanUse => !escaped;

    public override void Use(PlayerInteractor player)
    {
        fps = player.GetComponent<FirstPersonController>();
        openedFrame = Time.frameCount;   // the E that opened it must not also try the code
        SetOpen(true);
    }

    void SetOpen(bool value)
    {
        open = value;
        IsOpen = value || escaped;
        if (fps != null)
            fps.inputLocked = value || escaped;
    }

    void OnDestroy() => IsOpen = false;

    void Update()
    {
        var k = Keyboard.current;
        if (k == null)
            return;
        if (escaped || !open)   // once out, EscapeEnding takes over
            return;

        if (k.tabKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame)
        {
            SetOpen(false);
            return;
        }
        if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) selected = (selected + 3) % 4;
        if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) selected = (selected + 1) % 4;
        if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) dials[selected] = (dials[selected] + 1) % 10;
        if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame) dials[selected] = (dials[selected] + 9) % 10;
        for (int d = 0; d < 10; d++)
        {
            if (k[DigitKeys[d]].wasPressedThisFrame || k[Key.Numpad0 + d].wasPressedThisFrame)
            {
                dials[selected] = d;
                selected = Mathf.Min(selected + 1, 3);
            }
        }
        if (k.eKey.wasPressedThisFrame && Time.frameCount != openedFrame)   // E tries the code (was Enter)
            TryCode();
    }

    void TryCode()
    {
        if (puzzle != null && string.Concat(dials) == puzzle.CodeText)
        {
            escaped = true;
            if (Sfx.Bank != null) Sfx.Play(Sfx.Bank.padlockOpen);
            SetOpen(false);
            EscapeEnding.Play();   // out through the hatch, then the record
            return;
        }
        wrongUntil = Time.time + 2f;
        if (Sfx.Bank != null) Sfx.Play(Sfx.Bank.padlockWrong, transform.position, 0.9f);
        NoiseEvents.Emit(transform.position, rattleNoiseRange);
    }

    // Drawn on a 720-unit-high canvas scaled to the window, so it keeps its proportions (and
    // doesn't spill off a small window).
    const float RefHeight = 720f;
    static float W, H;

    void OnGUI()
    {
        if (!open)
            return;
        float k = Screen.height / RefHeight;
        W = Screen.width / k;
        H = RefHeight;
        var before = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
        Draw();
        GUI.matrix = before;
    }

    void Draw()
    {
        Shade(0.6f);
        const float size = 90f, gap = 20f;
        float total = 4 * size + 3 * gap;
        float x0 = (W - total) / 2f, y = H * 0.4f;
        Label(new Rect(0f, y - 70f, W, 40f), "자물쇠 번호", 28);
        for (int i = 0; i < 4; i++)
        {
            var r = new Rect(x0 + i * (size + gap), y, size, size);
            GUI.color = i == selected ? new Color(0.85f, 0.6f, 0.25f, 0.95f) : new Color(0.25f, 0.22f, 0.2f, 0.95f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            Label(r, dials[i].ToString(), 54);
        }
        if (Time.time < wrongUntil)
            Label(new Rect(0f, y + size + 80f, W, 34f), "철컥!  열리지 않는다…", 26);
        // how to get away from the lock in a hurry
        Label(new Rect(0f, H - 70f, W, 34f), "[E]  열기        [Tab]  나가기", 24);
    }

    static void Shade(float alpha)
    {
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    static void Label(Rect rect, string text, int fontSize)
    {
        var style = new GUIStyle(GUI.skin.label) { font = GameFont.Regular, fontSize = fontSize, alignment = TextAnchor.MiddleCenter };
        GUI.Label(rect, text, style);
    }
}
