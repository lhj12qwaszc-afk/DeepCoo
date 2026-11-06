using System.IO;
using UnityEditor;
using UnityEngine;

// Real furniture models that replace the maze's placeholder boxes.
// Each prop lives in Assets/Props/Models/<Name>/ as <Name>.fbx, its textures, and materials.txt
// (one line per material: material name|base colour texture|normal texture|plain colour), written by the
// Blender export. Menu: DeepCoo > Set Up Prop Models makes a URP material for each line and
// hooks it onto the model.
public static class PropModels
{
    const string Folder = "Assets/Props/Models";

    public static GameObject Load(string name) =>
        AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{name}/{name}.fbx");

    // Models the game loads at run time live under Resources, laid out the same way.
    static readonly string[] RuntimeModels = { "Assets/Resources/Tracker" };

    [MenuItem("DeepCoo/Set Up Prop Models")]
    public static void SetUpAll()
    {
        foreach (var dir in Directory.GetDirectories(Folder))
            SetUp(Path.GetFileName(dir));
        foreach (var dir in RuntimeModels)
            SetUp(Path.GetFileName(dir), Path.GetDirectoryName(dir).Replace('\\', '/'));
        AssetDatabase.SaveAssets();
    }

    static void SetUp(string name, string folder = Folder)
    {
        string dir = $"{folder}/{name}";
        string list = $"{dir}/materials.txt";
        if (!File.Exists(list) || !(AssetImporter.GetAtPath($"{dir}/{name}.fbx") is ModelImporter model))
            return;

        int index = 0;
        foreach (var line in File.ReadAllLines(list))
        {
            var parts = line.Split('|');
            if (parts.Length < 3 || parts[0].Length == 0)
                continue;
            var mat = CreateMaterial($"{dir}/{name}_{index++}.mat",
                LoadTexture($"{dir}/{parts[1]}", false), LoadTexture($"{dir}/{parts[2]}", true),
                parts.Length > 3 ? ParseColor(parts[3]) : Color.white);
            model.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), parts[0]), mat);
        }
        model.SaveAndReimport();
        Debug.Log($"[PropModels] {name}: {index} materials");
    }

    static Texture2D LoadTexture(string path, bool normalMap)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            return null;
        var type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != type)
        {
            importer.textureType = type;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // "r,g,b" in Blender's linear 0..1 values
    static Color ParseColor(string text)
    {
        var c = text.Split(',');
        if (c.Length < 3)
            return Color.white;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return new Color(float.Parse(c[0], inv), float.Parse(c[1], inv), float.Parse(c[2], inv)).gamma;
    }

    static Material CreateMaterial(string path, Texture2D albedo, Texture2D normal, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetTexture("_BaseMap", albedo);
        mat.SetTexture("_BumpMap", normal);
        mat.SetFloat("_Smoothness", 0.2f);
        if (normal != null) mat.EnableKeyword("_NORMALMAP");
        else mat.DisableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
