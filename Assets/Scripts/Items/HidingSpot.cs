using UnityEngine;

// A locker, wardrobe or bin the player can climb into. The object's +Z faces out into the
// corridor and its scale is its size in metres. From inside, the box's own faces are culled,
// so the player sees out; PlayerInteractor draws the slatted view over the screen.
public class HidingSpot : MonoBehaviour
{
    public enum Kind { Locker, Wardrobe, TrashBin }

    public Kind kind;
    public float loudNoiseRange = 15f;   // slamming in or out

    public string Label => kind switch
    {
        Kind.Locker => "사물함",
        Kind.Wardrobe => "옷장",
        _ => "쓰레기통",
    };

    // Where the player's feet go: just behind the front face, so the eye sits at the opening.
    public Vector3 InsidePoint
    {
        get
        {
            var p = transform.position + transform.forward * (transform.localScale.z / 2f - 0.15f);
            p.y = transform.position.y - transform.localScale.y / 2f;
            return p;
        }
    }

    public Vector3 OutsidePoint
    {
        get
        {
            var p = transform.position + transform.forward * (transform.localScale.z / 2f + 0.7f);
            p.y = transform.position.y - transform.localScale.y / 2f;
            return p;
        }
    }

    public Quaternion Facing => Quaternion.LookRotation(transform.forward, Vector3.up);
}
