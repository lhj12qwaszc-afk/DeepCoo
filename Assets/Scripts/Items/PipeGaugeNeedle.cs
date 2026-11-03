using UnityEngine;

// Points the pipe gauge's needle at one digit of its 0-9 dial. The dial's digits run clockwise
// from 0 at -135 degrees to 9 at +135 degrees (0 = straight up), 30 degrees apart.
// The puzzle sets Digit; until it does, a random digit is shown.
public class PipeGaugeNeedle : MonoBehaviour
{
    public int digit = -1;   // -1 = pick one at random

    Transform needle;
    Quaternion restRotation;   // needle pointing straight up

    public int Digit => digit;

    void Awake()
    {
        foreach (var t in GetComponentsInChildren<Transform>())
            if (t.name == "Needle")
                needle = t;
        if (needle != null)
            restRotation = needle.rotation;
    }

    void Start()
    {
        SetDigit(digit >= 0 ? digit : Random.Range(0, 10));
    }

    public void SetDigit(int value)
    {
        digit = Mathf.Clamp(value, 0, 9);
        if (needle == null)
            return;
        // this object faces the corridor, so its forward points at whoever reads the gauge;
        // a positive turn about it looks clockwise from there
        needle.rotation = Quaternion.AngleAxis(-135f + 30f * digit, transform.forward) * restRotation;
    }
}
