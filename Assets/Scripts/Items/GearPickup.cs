using UnityEngine;

// The flashlight or the tracker lying in the start room, taken with E (see PlayerGear).
// The model is a child; the collider on this object is what the look ray hits.
public class GearPickup : Interactable
{
    public PlayerGear.Item item;

    public override string Prompt => item == PlayerGear.Item.Flashlight ? "손전등 줍기" : "위치 추적기 줍기";

    public override void Use(PlayerInteractor player)
    {
        PlayerGear.Give(item);
        Destroy(gameObject);
    }
}
