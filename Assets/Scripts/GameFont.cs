using UnityEngine;

// The game's lettering (MaruBuri), for text drawn from code with OnGUI. The fonts live in
// Assets/Resources/Fonts so they can be loaded at run time; the note paper (NoteView) uses the
// same files directly.
public static class GameFont
{
    static Font regular, bold;

    public static Font Regular => regular != null ? regular : regular = Resources.Load<Font>("Fonts/MaruBuri-Regular");
    public static Font Bold => bold != null ? bold : bold = Resources.Load<Font>("Fonts/MaruBuri-Bold");
}
