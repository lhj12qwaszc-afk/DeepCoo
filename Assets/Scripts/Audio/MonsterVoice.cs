using UnityEngine;
using UnityEngine.SceneManagement;

// The monster's voice, heard from where it is: a low coo now and then while it roams, a shriek the
// moment it starts chasing, and a ragged breathing-and-wings loop for as long as the chase lasts.
// Added to the monster at run time.
public class MonsterVoice : MonoBehaviour
{
    const float Range = 28f;   // metres it can be heard

    MonsterWander mover;
    AudioSource voice, chase;
    float nextCoo;
    bool wasChasing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded += (scene, mode) => Attach();
        Attach();
    }

    static void Attach()
    {
        foreach (var m in FindObjectsByType<MonsterWander>(FindObjectsSortMode.None))
            if (m.GetComponent<MonsterVoice>() == null)
                m.gameObject.AddComponent<MonsterVoice>();
    }

    void Start()
    {
        mover = GetComponent<MonsterWander>();
        voice = MakeSource(false);
        chase = MakeSource(true);
        chase.clip = Sfx.Bank != null ? Sfx.Bank.monsterChase : null;
        nextCoo = Time.time + Random.Range(4f, 10f);
    }

    AudioSource MakeSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 2f;
        s.maxDistance = Range;
        s.loop = loop;
        s.playOnAwake = false;
        return s;
    }

    void Update()
    {
        var bank = Sfx.Bank;
        if (bank == null)
            return;
        bool chasing = mover.IsChasing;
        if (chasing && !wasChasing)
        {
            voice.Stop();
            voice.pitch = 1f;
            voice.PlayOneShot(bank.monsterAlert, 1f);
            if (chase.clip != null) { chase.volume = 0.8f; chase.PlayDelayed(0.8f); }
        }
        else if (!chasing && wasChasing)
        {
            chase.Stop();
            nextCoo = Time.time + Random.Range(5f, 12f);
        }
        wasChasing = chasing;

        if (!chasing && Time.time >= nextCoo && bank.monsterCoo != null)
        {
            voice.pitch = Random.Range(0.9f, 1.05f);
            voice.PlayOneShot(bank.monsterCoo, 0.8f);
            nextCoo = Time.time + bank.monsterCoo.length + Random.Range(6f, 14f);
        }
    }
}
