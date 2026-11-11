using UnityEngine;

// Every sound the game plays, in one asset (Assets/Resources/SoundBank, filled from Assets/Audio by
// DeepCoo > Set Up Sounds). Sfx reads it.
[CreateAssetMenu(menuName = "DeepCoo/Sound Bank")]
public class SoundBank : ScriptableObject
{
    public AudioClip menuMusic;
    public AudioClip buttonHover, buttonClick;
    public AudioClip[] footsteps;
    public AudioClip monsterCoo, monsterAlert, monsterChase;
    public AudioClip scareFlap;
    public AudioClip padlockWrong, padlockOpen;
    public AudioClip radioStatic;
    public AudioClip[] radioDigits = new AudioClip[10];   // Radio_0 .. Radio_9, a voice reading the digit
}
