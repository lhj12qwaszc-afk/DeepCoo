using UnityEngine;

// A spare battery lying in the maze. It slowly turns and gives off a faint glow so it can be
// spotted in the dark; walking up to it refills the flashlight.
public class BatteryPickup : MonoBehaviour
{
    public float charge = 0.5f;
    public float pickupDistance = 1.0f;
    public float spinSpeed = 40f;

    Transform player;
    FlashlightBattery battery;

    void Start()
    {
        battery = FindFirstObjectByType<FlashlightBattery>();
        if (battery != null)
            player = battery.GetComponentInParent<FirstPersonController>().transform;
    }

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        if (player == null)
            return;
        var offset = player.position - transform.position;
        offset.y = 0f;
        if (offset.magnitude < pickupDistance)
        {
            battery.AddCharge(charge);
            Destroy(gameObject);
        }
    }
}
