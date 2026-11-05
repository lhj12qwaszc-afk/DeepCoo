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
        _ => "구겨진 쪽지에 적혀 있다.",
    };

    // Always the same places (only what the notes say changes): the board has the hints for the
    // first and second digits, the first filing cabinet the third, the second cabinet the fourth
    // and the number note.
    void PlaceNotes(Dictionary<string, List<Transform>> props)
    {
        var boards = props.GetValueOrDefault("CorkBoard", new List<Transform>());
        var cabinets = props.GetValueOrDefault("FilingCabinet", new List<Transform>());
        cabinets.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        var spots = new List<ClueSpot>();
        if (boards.Count > 0)
            spots.Add(AddClueSpot(boards[0], "게시판 메모 읽기"));
        foreach (var t in cabinets)
            spots.Add(AddClueSpot(t, "서랍 뒤지기"));
        if (spots.Count == 0)
            return;

        // board, board, cabinet 1, cabinet 2; with fewer spots the last one takes the rest
        int[] spotFor = { 0, 0, 1, 2 };
        for (int i = 0; i < 4; i++)
            spots[Mathf.Min(spotFor[i], spots.Count - 1)].notes.Add(
                new PlayerInteractor.Note($"{Ordinals[i]} 힌트", $"{Ordinals[i]} 숫자는 {Where(SourceAt[i])}"));
        spots[spots.Count - 1].notes.Add(
            new PlayerInteractor.Note("구겨진 쪽지", $"숫자 하나만 적혀 있다.\n\n{DigitOf(Source.NumberNote)}"));
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
