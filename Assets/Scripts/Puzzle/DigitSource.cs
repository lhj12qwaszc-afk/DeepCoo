using UnityEngine;

// A radio or birdcage that gives away one digit of the padlock code when used.
public class DigitSource : Interactable
{
    public string prompt;
    public string message;
    public float noiseRange;   // 0 = silent; the radio is loud enough for the monster to hear

    public override string Prompt => prompt;

    public override void Use(PlayerInteractor player)
    {
        player.ShowNote(message);
        if (noiseRange > 0f)
            NoiseEvents.Emit(transform.position, noiseRange);
    }
}
