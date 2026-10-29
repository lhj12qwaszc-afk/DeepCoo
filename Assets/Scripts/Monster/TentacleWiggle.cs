using System.Collections.Generic;
using UnityEngine;

// Makes the monster's four back tentacles writhe loosely: a travelling wave runs along each
// bone chain in two directions, stronger and faster while the monster walks.
// Chains are found by name: {Left|Right}Tentacle{Upper|Lower}{1..n}.
[DefaultExecutionOrder(110)]
public class TentacleWiggle : MonoBehaviour
{
    public MonsterWander mover;
    public float idleAmplitude = 7f;     // degrees per bone at the tip
    public float walkAmplitude = 13f;
    public float idleFrequency = 0.6f;   // waves per second
    public float walkFrequency = 1.3f;
    public float wavePerBone = 0.7f;     // phase shift between neighbouring bones

    static readonly string[] Chains = { "LeftTentacleUpper", "RightTentacleUpper", "LeftTentacleLower", "RightTentacleLower" };

    readonly List<Transform[]> chains = new List<Transform[]>();
    readonly List<Quaternion[]> bindRotations = new List<Quaternion[]>();
    float walkBlend;
    float time;

    void Awake()
    {
        if (mover == null)
            mover = GetComponent<MonsterWander>();
        var byName = new Dictionary<string, Transform>();
        foreach (var t in GetComponentsInChildren<Transform>())
            byName[t.name] = t;
        foreach (var chain in Chains)
        {
            var bones = new List<Transform>();
            for (int k = 1; byName.TryGetValue(chain + k, out var bone); k++)
                bones.Add(bone);
            if (bones.Count == 0)
                continue;
            chains.Add(bones.ToArray());
            bindRotations.Add(bones.ConvertAll(b => b.localRotation).ToArray());
        }
    }

    void LateUpdate()
    {
        float target = mover != null && mover.walkSpeed > 0f ? Mathf.Clamp01(mover.CurrentSpeed / mover.walkSpeed) : 0f;
        walkBlend = Mathf.MoveTowards(walkBlend, target, Time.deltaTime * 1.5f);
        float amplitude = Mathf.Lerp(idleAmplitude, walkAmplitude, walkBlend);
        time += Time.deltaTime * Mathf.Lerp(idleFrequency, walkFrequency, walkBlend) * Mathf.PI * 2f;

        var right = transform.right;
        var up = transform.up;
        for (int c = 0; c < chains.Count; c++)
        {
            var bones = chains[c];
            float offset = c * 1.7f;
            for (int k = 0; k < bones.Length; k++)
            {
                bones[k].localRotation = bindRotations[c][k];
                // Bones near the root barely move; the tips do most of the writhing.
                float weight = Mathf.Lerp(0.25f, 1f, k / (float)Mathf.Max(1, bones.Length - 1));
                float wave = time - k * wavePerBone + offset;
                float pitch = Mathf.Sin(wave) * amplitude * weight;
                float yaw = Mathf.Sin(wave * 0.7f + 1.3f) * amplitude * 0.8f * weight;
                bones[k].rotation = Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(pitch, right) * bones[k].rotation;
            }
        }
    }
}
