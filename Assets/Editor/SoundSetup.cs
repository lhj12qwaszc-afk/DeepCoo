using System.IO;
using UnityEditor;
using UnityEngine;

// DeepCoo > Set Up Sounds: fills Assets/Resources/SoundBank from the files in Assets/Audio (by name:
// Menu_Music, Button_Hover, Button_Click, Footstep_*, Monster_Coo/Alert/Chase, Scare_Flap,
// Padlock_Wrong/Open). Short sounds are loaded whole into memory so they can be cut up at run time.
public static class SoundSetup
{
    const string Folder = "Assets/Audio";
    const string BankPath = "Assets/Resources/SoundBank.asset";

    [MenuItem("DeepCoo/Set Up Sounds")]
    public static void SetUp()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is AudioImporter ai))
                continue;
            bool music = Path.GetFileNameWithoutExtension(path) == "Menu_Music";
            var s = ai.defaultSampleSettings;
            var want = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            if (s.loadType != want)
            {
                s.loadType = want;
                ai.defaultSampleSettings = s;
                ai.SaveAndReimport();
            }
        }

        var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<SoundBank>();
            AssetDatabase.CreateAsset(bank, BankPath);
        }
        bank.menuMusic = Clip("Menu_Music");
        bank.buttonHover = Clip("Button_Hover");
        bank.buttonClick = Clip("Button_Click");
        bank.footsteps = Numbered("Footstep_");
        bank.footstepsCrouch = Numbered("Footstep_Crouch_");
        bank.footstepsRun = Numbered("Footstep_Run_");
        bank.monsterWalk = Numbered("Monster_Walk_");
        bank.monsterRun = Numbered("Monster_Run_");
        bank.monsterCoo = Clip("Monster_Coo");
        bank.monsterAlert = Clip("Monster_Alert");
        bank.monsterChase = Clip("Monster_Chase");
        bank.scareFlap = Clip("Scare_Flap");
        bank.padlockWrong = Clip("Padlock_Wrong");
        bank.padlockOpen = Clip("Padlock_Open");
        bank.radioStatic = Clip("Radio_Static");
        bank.radioDigits = new AudioClip[10];
        for (int i = 0; i < 10; i++)
            bank.radioDigits[i] = Clip("Radio_" + i);
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        Debug.Log("[SoundSetup] sound bank filled");
    }

    // name1, name2, ... up to name8, as many as there are
    static AudioClip[] Numbered(string prefix)
    {
        var list = new System.Collections.Generic.List<AudioClip>();
        for (int i = 1; i <= 8; i++)
        {
            var c = Clip(prefix + i);
            if (c != null) list.Add(c);
        }
        return list.ToArray();
    }

    static AudioClip Clip(string name)
    {
        foreach (var ext in new[] { ".wav", ".mp3", ".ogg" })
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Folder}/{name}{ext}");
            if (c != null) return c;
        }
        return null;
    }
}
