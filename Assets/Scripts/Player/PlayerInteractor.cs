using UnityEngine;
using UnityEngine.InputSystem;

// The E key. Look at something usable and:
//   - tap E to do it at once, which is loud (the monster can hear it), or
//   - hold E until the ring fills to do it slowly and quietly.
// For now the only usable things are hiding spots; while hidden the same key gets you out.
// Draws the prompt, the hold ring, and the view through the hiding spot's slats.
[RequireComponent(typeof(FirstPersonController))]
public class PlayerInteractor : MonoBehaviour
{
    public float reach = 1.8f;
    public float quietHoldSeconds = 1.6f;
    public float tapSeconds = 0.25f;   // releasing before this counts as a tap

    FirstPersonController fps;
    HidingSpot target;     // what we're looking at, or the spot we're inside
    HidingSpot hiddenIn;
    float holdTime;
    bool holding;

    public HidingSpot HiddenIn => hiddenIn;

    void Awake() => fps = GetComponent<FirstPersonController>();

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        target = hiddenIn != null ? hiddenIn : FindTarget();

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

    HidingSpot FindTarget()
    {
        var eye = fps.cameraRoot;
        if (Physics.Raycast(eye.position, eye.forward, out var hit, reach, ~0, QueryTriggerInteraction.Ignore))
            return hit.collider.GetComponentInParent<HidingSpot>();
        return null;
    }

    void Use(HidingSpot spot, bool loud)
    {
        if (hiddenIn == null)
        {
            hiddenIn = spot;
            fps.EnterHiding(spot.InsidePoint, spot.Facing);
        }
        else
        {
            fps.ExitHiding(spot.OutsidePoint);
            hiddenIn = null;
        }
        if (loud)
            NoiseEvents.Emit(spot.transform.position, spot.loudNoiseRange);
        holdTime = 0f;
    }

    void OnGUI()
    {
        if (hiddenIn != null)
            DrawSlats(hiddenIn.kind);

        if (target == null)
            return;

        string action = hiddenIn != null ? "나오기" : $"{target.Label}에 숨기";
        string text = $"[E] {action}    (길게 누르면 조용히)";
        var style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        var rect = new Rect(0f, Screen.height * 0.72f, Screen.width, 30f);
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        GUI.color = Color.white;
        GUI.Label(rect, text, style);

        if (holding && holdTime >= tapSeconds)
            DrawHoldBar(holdTime / quietHoldSeconds);
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
    static void DrawSlats(HidingSpot.Kind kind)
    {
        float W = Screen.width, H = Screen.height;
        GUI.color = new Color(0.01f, 0.01f, 0.01f, 0.97f);
        if (kind == HidingSpot.Kind.TrashBin)
        {
            float gapTop = H * 0.42f, gapBottom = H * 0.5f;
            GUI.DrawTexture(new Rect(0f, 0f, W, gapTop), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, gapBottom, W, H - gapBottom), Texture2D.whiteTexture);
        }
        else
        {
            int slats = kind == HidingSpot.Kind.Locker ? 6 : 9;
            float top = H * (kind == HidingSpot.Kind.Locker ? 0.3f : 0.18f);
            float bottom = H * (kind == HidingSpot.Kind.Locker ? 0.62f : 0.82f);
            float left = W * (kind == HidingSpot.Kind.Locker ? 0.3f : 0.15f);
            float right = W - left;
            float pitch = (bottom - top) / slats;
            float gap = pitch * 0.45f;
            GUI.DrawTexture(new Rect(0f, 0f, W, top), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, bottom, W, H - bottom), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, top, left, bottom - top), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(right, top, W - right, bottom - top), Texture2D.whiteTexture);
            for (int i = 0; i < slats; i++)
                GUI.DrawTexture(new Rect(left, top + i * pitch + gap, right - left, pitch - gap), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }
}
