using System.Collections.Generic;
using UnityEngine;

// Animates the monster's four back tentacles.
// - Normally they writhe loosely: a travelling wave runs along each bone chain in two
//   directions, stronger and faster while the monster walks.
// - While chasing they stretch out and work as extra legs: each tip plants on the floor ahead
//   and to the side, stays put while the body passes, then lifts and slams down further ahead.
//   At most two tentacles step at once so they alternate like a gait. A CCD IK pass bends each
//   chain toward its foot point.
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

    [Header("Chase")]
    public float chaseStretch = 2.3f;    // bones lengthen so the tips can reach the floor
    public float upperReach = 1.9f;      // how far ahead of the body the upper tentacles plant
    public float lowerReach = 1.0f;
    public float sideReach = 1.2f;
    public float stepDistance = 1.3f;    // a planted tip lifts once it is this far behind its spot
    public float stepDuration = 0.22f;
    public float liftHeight = 0.6f;
    public int maxSteppingAtOnce = 2;
    public int ikIterations = 8;
    public float ikMaxTurnPerStep = 25f;

    static readonly string[] Names = { "LeftTentacleUpper", "RightTentacleUpper", "LeftTentacleLower", "RightTentacleLower" };

    class Chain
    {
        public Transform[] bones;
        public Quaternion[] bindRotations;
        public Vector3[] bindPositions;
        public Vector3 tipLocal;          // the chain's tip, in the last bone's space
        public Quaternion[] poseLocal;
        public bool upper;
        public float side;                // +1 = monster's right, -1 = left
        public Vector3 foot, stepFrom, stepTo;
        public float stepT = 1f;
        public bool planted;
    }

    readonly List<Chain> chains = new List<Chain>();
    float walkBlend, chaseBlend;
    float time;

    void Awake()
    {
        if (mover == null)
            mover = GetComponent<MonsterWander>();
        var byName = new Dictionary<string, Transform>();
        foreach (var t in GetComponentsInChildren<Transform>())
            byName[t.name] = t;
        foreach (var name in Names)
        {
            var bones = new List<Transform>();
            for (int k = 1; byName.TryGetValue(name + k, out var bone); k++)
                bones.Add(bone);
            if (bones.Count < 2)
                continue;
            var last = bones[bones.Count - 1];
            var beforeLast = bones[bones.Count - 2];
            chains.Add(new Chain
            {
                bones = bones.ToArray(),
                bindRotations = bones.ConvertAll(b => b.localRotation).ToArray(),
                bindPositions = bones.ConvertAll(b => b.localPosition).ToArray(),
                tipLocal = last.InverseTransformPoint(last.position + (last.position - beforeLast.position)),
                poseLocal = new Quaternion[bones.Count],
                upper = name.Contains("Upper"),
                side = name.StartsWith("Left") ? -1f : 1f,
            });
        }
    }

    void LateUpdate()
    {
        float target = mover != null && mover.walkSpeed > 0f ? Mathf.Clamp01(mover.CurrentSpeed / mover.walkSpeed) : 0f;
        walkBlend = Mathf.MoveTowards(walkBlend, target, Time.deltaTime * 1.5f);
        chaseBlend = Mathf.MoveTowards(chaseBlend, mover != null && mover.IsChasing ? 1f : 0f, Time.deltaTime * 2f);
        float amplitude = Mathf.Lerp(idleAmplitude, walkAmplitude, walkBlend);
        time += Time.deltaTime * Mathf.Lerp(idleFrequency, walkFrequency, walkBlend) * Mathf.PI * 2f;

        int stepping = 0;
        foreach (var chain in chains)
            if (chain.stepT < 1f)
                stepping++;

        for (int c = 0; c < chains.Count; c++)
        {
            var chain = chains[c];
            Writhe(chain, c * 1.7f, amplitude);
            if (chaseBlend <= 0.001f)
            {
                chain.planted = false;
                continue;
            }
            Stretch(chain, Mathf.Lerp(1f, chaseStretch, chaseBlend));
            UpdateFoot(chain, ref stepping);

            for (int k = 0; k < chain.bones.Length; k++)
                chain.poseLocal[k] = chain.bones[k].localRotation;
            SolveIK(chain, chain.foot);
            for (int k = 0; k < chain.bones.Length; k++)
                chain.bones[k].localRotation = Quaternion.Slerp(chain.poseLocal[k], chain.bones[k].localRotation, chaseBlend);
        }
    }

    void Writhe(Chain chain, float offset, float amplitude)
    {
        var right = transform.right;
        var up = transform.up;
        var bones = chain.bones;
        for (int k = 0; k < bones.Length; k++)
        {
            bones[k].localPosition = chain.bindPositions[k];
            bones[k].localRotation = chain.bindRotations[k];
            // Bones near the root barely move; the tips do most of the writhing.
            float weight = Mathf.Lerp(0.25f, 1f, k / (float)Mathf.Max(1, bones.Length - 1));
            float wave = time - k * wavePerBone + offset;
            float pitch = Mathf.Sin(wave) * amplitude * weight;
            float yaw = Mathf.Sin(wave * 0.7f + 1.3f) * amplitude * 0.8f * weight;
            bones[k].rotation = Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(pitch, right) * bones[k].rotation;
        }
    }

    static void Stretch(Chain chain, float factor)
    {
        for (int k = 1; k < chain.bones.Length; k++)
            chain.bones[k].localPosition = chain.bindPositions[k] * factor;
    }

    Vector3 Tip(Chain chain)
    {
        var last = chain.bones[chain.bones.Length - 1];
        float factor = Mathf.Lerp(1f, chaseStretch, chaseBlend);
        return last.TransformPoint(chain.tipLocal * factor);
    }

    void UpdateFoot(Chain chain, ref int stepping)
    {
        var desired = transform.position
            + transform.forward * (chain.upper ? upperReach : lowerReach)
            + transform.right * chain.side * sideReach;
        desired.y = transform.position.y;

        if (!chain.planted)
        {
            chain.foot = desired;
            chain.stepT = 1f;
            chain.planted = true;
            return;
        }

        if (chain.stepT >= 1f && stepping < maxSteppingAtOnce
            && Vector3.Distance(chain.foot, desired) > stepDistance)
        {
            chain.stepFrom = chain.foot;
            // land a bit beyond the ideal spot so the tentacle stays planted for a while
            chain.stepTo = desired + transform.forward * stepDistance * 0.5f;
            chain.stepT = 0f;
            stepping++;
        }

        if (chain.stepT < 1f)
        {
            chain.stepT = Mathf.Min(1f, chain.stepT + Time.deltaTime / stepDuration);
            chain.foot = Vector3.Lerp(chain.stepFrom, chain.stepTo, chain.stepT)
                + Vector3.up * liftHeight * Mathf.Sin(chain.stepT * Mathf.PI);
        }
    }

    // Cyclic coordinate descent: from the tip back to the root, turn each bone so the tip
    // moves toward the target, a limited amount per pass.
    void SolveIK(Chain chain, Vector3 target)
    {
        var bones = chain.bones;
        for (int it = 0; it < ikIterations; it++)
        {
            for (int k = bones.Length - 1; k >= 0; k--)
            {
                var toTip = Tip(chain) - bones[k].position;
                var toTarget = target - bones[k].position;
                if (toTip.sqrMagnitude < 1e-6f || toTarget.sqrMagnitude < 1e-6f)
                    continue;
                var turn = Quaternion.FromToRotation(toTip, toTarget);
                turn = Quaternion.RotateTowards(Quaternion.identity, turn, ikMaxTurnPerStep);
                bones[k].rotation = turn * bones[k].rotation;
            }
            if ((Tip(chain) - target).sqrMagnitude < 0.0025f)
                break;
        }
    }
}
