using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The exit padlock's code and the clues to it, made fresh every time the level starts (so after
// every death too, since getting caught reloads the scene).
//   - Four random digits.
//   - Each digit is hidden in one kind of object, in a random order: the pipe gauges' needles,
//     the radio (turn it on), the feathers in the birdcages (count them; so 1-9, never 0), or a
//     note with just the number on it.
//   - Hint notes say which object holds which digit ("두 번째 숫자는 압력계 바늘이 가리킨다").
//     They and the number note sit in fixed places on the cork board and in the filing cabinets'
//     drawers (see PlaceNotes). Spots without notes don't respond to E.
// Sets itself up in any scene that has an "ExitDoor".
public class PuzzleManager : MonoBehaviour
{
    public enum Source { Gauge, Radio, Birdcage, NumberNote }

    static readonly string[] Ordinals = { "첫 번째", "두 번째", "세 번째", "네 번째" };
    static readonly string[] DigitWords = { "공", "일", "이", "삼", "사", "오", "육", "칠", "팔", "구" };

    public int[] Code { get; private set; }
    public Source[] SourceAt { get; private set; }   // which object holds each digit

    public string CodeText => string.Concat(Code);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded += (scene, mode) => Create();
        Create();
    }

    static void Create()
    {
        if (FindFirstObjectByType<PuzzleManager>() == null && GameObject.Find("ExitDoor") != null)
            new GameObject("PuzzleManager").AddComponent<PuzzleManager>();
    }

    void Awake()
    {
        Code = new int[4];
        for (int i = 0; i < 4; i++)
            Code[i] = Random.Range(0, 10);
        SourceAt = new[] { Source.Gauge, Source.Radio, Source.Birdcage, Source.NumberNote };
        Shuffle(SourceAt);
        int cageAt = System.Array.IndexOf(SourceAt, Source.Birdcage);
        Code[cageAt] = Random.Range(1, 10);   // counted in feathers, so never 0
        Debug.Log($"[Puzzle] code {CodeText}  ({string.Join(", ", SourceAt)})");

        var props = FindProps();
        foreach (var gauge in FindObjectsByType<PipeGaugeNeedle>(FindObjectsSortMode.None))
            gauge.SetDigit(DigitOf(Source.Gauge));
        foreach (var radio in props.GetValueOrDefault("Radio", new List<Transform>()))
        {
            var d = DigitOf(Source.Radio);
            AddSource(radio, "라디오 켜기", $"지지직…  …{DigitWords[d]}…  지지직…  …{DigitWords[d]}…", 12f);
        }
        foreach (var cage in props.GetValueOrDefault("Birdcage", new List<Transform>()))
            cage.gameObject.AddComponent<BirdcageInspect>().SetFeathers(DigitOf(Source.Birdcage));

        PlaceNotes(props);
        SetUpHatch();
    }

    int DigitOf(Source source) => Code[System.Array.IndexOf(SourceAt, source)];

    static string Where(Source source) => source switch
    {
        Source.Gauge => "압력계 바늘이 가리킨다.",
        Source.Radio => "라디오가 들려준다.",
        Source.Birdcage => "새장 속 깃털의 개수다.",
        _ => "어느 서랍 속 구겨진 쪽지에 적혀 있다.",
    };

    // The drawing used on a picture hint, from Resources/Hints: any picture whose name starts
    // with Hint_Radio, Hint_Gauge, Hint_Birdcage or Hint_Note (Hint_Radio, Hint_Radio_2, ...);
    // with several for one source a run picks one at random. Plain textures work too.
    static Sprite PictureOf(Source source)
    {
        string prefix = source switch
        {
            Source.Gauge => "Hint_Gauge",
            Source.Radio => "Hint_Radio",
            Source.Birdcage => "Hint_Birdcage",
            _ => "Hint_Note",
        };
        var choices = System.Array.FindAll(Resources.LoadAll<Texture2D>("Hints"), t => t.name.StartsWith(prefix));
        if (choices.Length == 0)
            return null;
        var tex = choices[Random.Range(0, choices.Length)];
        var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        sprite.name = tex.name;   // the note view darkens some drawings by name
        return sprite;
    }

    // Where the sheets go: see below.
    void PlaceNotes(Dictionary<string, List<Transform>> props)
    {
        var boards = props.GetValueOrDefault("CorkBoard", new List<Transform>());
        var cabinets = props.GetValueOrDefault("FilingCabinet", new List<Transform>());
        cabinets.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        // places: 0 the board, 1.. the filing cabinets (a sheet goes in one of a cabinet's drawers)
        var places = new List<Transform>();
        if (boards.Count > 0) places.Add(boards[0]);
        places.AddRange(cabinets);
        if (places.Count == 0)
            return;

        static bool Free(Drawer d) => !d.HasBattery && !d.HasNote;
        void AtPlace(PlayerInteractor.Note note, int place)
        {
            var t = places[Mathf.Clamp(place, 0, places.Count - 1)];
            var inside = new List<Drawer>(t.GetComponentsInChildren<Drawer>());
            inside.RemoveAll(d => !Free(d));
            if (inside.Count > 0)
            {
                inside[Random.Range(0, inside.Count)].PutNote(note);
                return;
            }
            var spot = t.GetComponent<ClueSpot>();
            if (spot == null)
                spot = AddClueSpot(t, t == (boards.Count > 0 ? boards[0] : null) ? "게시판 메모 읽기" : "서랍 뒤지기");
            spot.notes.Add(note);
        }

        // Each hint is written out or drawn (a picture of the source in the digit's box of four);
        // the board's two are always written, the cabinets' two picked anew each run (at least one drawn).
        // Written hints go on the board (first and
        // second digits) and in a drawer of filing cabinet 1 (third) and 2 (fourth); drawn hints
        // and the number note each lie in some other drawer, picked anew each run among those
        // without a battery (no free drawer left: they go with the written ones).
        int[] placeFor = boards.Count > 0 ? new[] { 0, 0, 1, 2 } : new[] { 0, 0, 0, 1 };
        var drawn = new bool[4];
        for (int i = 0; i < 4; i++)
            drawn[i] = Random.value < 0.5f;
        if (boards.Count > 0)
        {
            // the board's hints (first and second) are always written, so the board is never bare;
            // of the cabinets' two at least one is drawn, so there's always a picture to find
            drawn[0] = drawn[1] = false;
            if (!drawn[2] && !drawn[3])
                drawn[Random.Range(2, 4)] = true;
        }
        else if (System.Array.TrueForAll(drawn, d => d) || System.Array.TrueForAll(drawn, d => !d))
        {
            int flip = Random.Range(0, 4);
            drawn[flip] = !drawn[flip];
        }
        var pictures = new PlayerInteractor.Note?[4];
        for (int i = 0; i < 4; i++)
        {
            var picture = drawn[i] ? PictureOf(SourceAt[i]) : null;
            if (picture != null)
                pictures[i] = new PlayerInteractor.Note("힌트", picture, i);
            else
                AtPlace(new PlayerInteractor.Note($"{Ordinals[i]} 힌트", $"{Ordinals[i]} 숫자는 {Where(SourceAt[i])}"), placeFor[i]);
        }

        var drawers = new List<Drawer>(FindObjectsByType<Drawer>(FindObjectsSortMode.None));
        drawers.RemoveAll(d => !Free(d) || d.InStartRoom || cabinets.Exists(c => d.transform.IsChildOf(c)));
        for (int i = drawers.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (drawers[i], drawers[j]) = (drawers[j], drawers[i]);
        }
        void Stash(PlayerInteractor.Note note, int place)
        {
            drawers.RemoveAll(d => !Free(d));
            if (drawers.Count > 0)
            {
                drawers[0].PutNote(note);
                drawers.RemoveAt(0);
            }
            else
                AtPlace(note, place);
        }
        Stash(new PlayerInteractor.Note("구겨진 쪽지", $"숫자 하나만 적혀 있다.\n\n{DigitOf(Source.NumberNote)}"), places.Count - 1);
        for (int i = 0; i < 4; i++)
            if (pictures[i].HasValue)
                Stash(pictures[i].Value, placeFor[i]);
    }

    void SetUpHatch()
    {
        var exit = GameObject.Find("ExitDoor");
        var lid = exit.GetComponentInChildren<Renderer>();
        if (exit.GetComponent<Collider>() == null && lid != null)
        {
            var box = exit.AddComponent<BoxCollider>();
            var b = lid.bounds;
            box.center = exit.transform.InverseTransformPoint(b.center);
            box.size = exit.transform.InverseTransformVector(b.size);
            box.size = new Vector3(Mathf.Abs(box.size.x), Mathf.Abs(box.size.y), Mathf.Abs(box.size.z));
        }
        exit.AddComponent<PadlockInput>().puzzle = this;
    }

    static DigitSource AddSource(Transform t, string prompt, string message, float noise)
    {
        var s = t.gameObject.AddComponent<DigitSource>();
        s.prompt = prompt;
        s.message = message;
        s.noiseRange = noise;
        return s;
    }

    static ClueSpot AddClueSpot(Transform t, string prompt)
    {
        var s = t.gameObject.AddComponent<ClueSpot>();
        s.prompt = prompt;
        return s;
    }

    // The maze's props by kind: "Radio_1" -> "Radio".
    static Dictionary<string, List<Transform>> FindProps()
    {
        var found = new Dictionary<string, List<Transform>>();
        var root = GameObject.Find("PropPlaceholders");
        if (root == null)
            return found;
        foreach (Transform t in root.transform)
        {
            int cut = t.name.LastIndexOf('_');
            string kind = cut > 0 ? t.name.Substring(0, cut) : t.name;
            if (!found.TryGetValue(kind, out var list))
                found[kind] = list = new List<Transform>();
            list.Add(t);
        }
        return found;
    }

    static void Shuffle<T>(T[] items)
    {
        for (int i = items.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
