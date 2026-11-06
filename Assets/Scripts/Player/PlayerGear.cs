using UnityEngine;
using UnityEngine.SceneManagement;

// What the player carries. A run starts empty-handed: the flashlight and the tracker lie on a
// cabinet in the start room (GearPickup) and are taken with E. Until then the flashlight hand is
// hidden and dark (F does nothing, no battery gauge) and M does nothing. In a scene without
// those pickups the player simply starts with both.
public class PlayerGear : MonoBehaviour
{
    public enum Item { Flashlight, Tracker }

    public static bool HasFlashlight { get; private set; }
    public static bool HasTracker { get; private set; }

    static PlayerGear instance;
    FirstPersonController fps;
    GameObject hand;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded += (scene, mode) => Attach();
        Attach();
    }

    static void Attach()
    {
        var player = FindFirstObjectByType<FirstPersonController>();
        if (player != null && player.GetComponent<PlayerGear>() == null)
            player.gameObject.AddComponent<PlayerGear>();
    }

    void Awake()
    {
        instance = this;
        HasFlashlight = HasTracker = false;
    }

    void Start()
    {
        fps = GetComponent<FirstPersonController>();
        var h = fps.cameraRoot.Find("FlashlightHand");
        hand = h != null ? h.gameObject : null;
        if (FindObjectsByType<GearPickup>(FindObjectsSortMode.None).Length == 0)
        {
            Give(Item.Flashlight);
            Give(Item.Tracker);
            return;
        }
        if (hand != null) hand.SetActive(false);
        if (fps.flashlight != null) fps.flashlight.enabled = false;
    }

    public static void Give(Item item)
    {
        if (instance == null)
            return;
        if (item == Item.Flashlight && !HasFlashlight)
        {
            HasFlashlight = true;
            if (instance.hand != null) instance.hand.SetActive(true);
            if (instance.fps.flashlight != null) instance.fps.flashlight.enabled = true;   // picked up switched on
        }
        else if (item == Item.Tracker && !HasTracker)
            HasTracker = true;
        else
            return;
        Inventory.AddGear(item);
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
