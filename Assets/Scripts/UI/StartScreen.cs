using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// The first thing the game shows, before the main menu (scene "Start"):
//   1. a warning about scares and loud sounds, on black (any key moves on early),
//   2. the DeepCoo title fading in, with the menu music rising (it carries on into the menu),
//   3. "아무 키나 누르세요" blinking under it; a key or a click fades out to the main menu.
public class StartScreen : MonoBehaviour
{
    public Texture2D title;              // Assets/UI/MainMenu/Title.png
    public string nextScene = "MainMenu";

    const float RefHeight = 720f;
    const string Warning =
        "이 게임에는 갑자기 놀라게 하는 장면과 큰 소리가 있습니다.\n\n헤드폰을 쓰고, 불을 끈 어두운 곳에서 플레이하기를 권합니다.";
    const string PressAnyKey = "아무 키나 누르세요";

    float warning, logo, prompt, black;  // fades, 0..1
    bool waitingForKey;
    bool skip;

    void Start()
    {
        Cursor.visible = false;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        yield return Fade(v => warning = v, 0f, 1f, 1f);
        for (float t = 0f; t < 3.5f && !skip; t += Time.unscaledDeltaTime)
            yield return null;
        skip = false;
        yield return Fade(v => warning = v, 1f, 0f, 0.8f);
        yield return new WaitForSecondsRealtime(0.4f);
        MenuMusic.Play(2.5f);   // the menu tune rises with the title and carries on into the menu
        yield return Fade(v => logo = v, 0f, 1f, 1.8f);
        yield return new WaitForSecondsRealtime(0.6f);
        waitingForKey = true;
        skip = false;
        while (!skip)
        {
            prompt = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * 3f));   // slow blink
            yield return null;
        }
        waitingForKey = false;
        yield return Fade(v => black = v, 0f, 1f, 0.7f);
        Cursor.visible = true;
        SceneManager.LoadScene(nextScene);
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
        bool key = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool click = Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame);
        if (key || click)
            skip = true;   // during the warning: move on; at the title: start
    }

    void OnGUI()
    {
        float s = Screen.height / RefHeight;
        float W = Screen.width / s, H = RefHeight;
        var before = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (warning > 0f)
        {
            Label(new Rect(0f, H * 0.36f, W, 50f), "경고", 34, warning, true, new Color(0.82f, 0.33f, 0.23f));
            Label(new Rect(W * 0.1f, H * 0.36f + 60f, W * 0.8f, 120f), Warning, 24, warning, false, new Color(0.9f, 0.87f, 0.82f));
        }
        if (logo > 0f && title != null)
        {
            float h = H * 0.34f, w = h * title.width / title.height;
            if (w > W * 0.8f) { w = W * 0.8f; h = w * title.height / title.width; }
            GUI.color = new Color(1f, 1f, 1f, logo);
            GUI.DrawTexture(new Rect((W - w) / 2f, H * 0.42f - h / 2f, w, h), title, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
        }
        if (waitingForKey || prompt > 0f && logo >= 1f)
            Label(new Rect(0f, H * 0.74f, W, 40f), PressAnyKey, 26, prompt * logo, false, new Color(0.9f, 0.87f, 0.82f));

        if (black > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, black);
            GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        GUI.matrix = before;
    }

    static void Label(Rect rect, string text, int size, float alpha, bool bold, Color colour)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            font = bold ? GameFont.Bold : GameFont.Regular,
            fontSize = size,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
        };
        colour.a = alpha;
        style.normal.textColor = colour;
        GUI.Label(rect, text, style);
    }
}
