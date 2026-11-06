using UnityEngine;

// Something the player can look at and press E on (besides hiding spots, which PlayerInteractor
// handles itself). Needs a collider on this object or a child for the look ray to hit.
public abstract class Interactable : MonoBehaviour
{
    // A note, the birdcage, the padlock or the inventory fills the screen: the HUD (battery,
    // microphone) hides.
    public static bool CloseUpOpen => PlayerInteractor.ReadingNote || BirdcageInspect.IsOpen || PadlockInput.IsOpen || Inventory.IsOpen;

    public abstract string Prompt { get; }      // shown after "[E] "
    public virtual bool CanUse => true;         // false: ignored, no prompt
    public abstract void Use(PlayerInteractor player);
}
