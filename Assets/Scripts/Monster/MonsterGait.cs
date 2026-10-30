using UnityEngine;

// Procedural, unsettling walk for the pigeon monster, driven by MonsterWander.CurrentSpeed:
// hunched spine, long legs stepping, limp dangling arms, a pigeon-like head bob while
// walking, and sudden jerky head turns while standing still. While chasing it drops low,
// leans far forward, takes long strides and reaches ahead with its arms. While inspecting a
// hiding spot it kneels in front of it and bends down until its face is at the slats, peering
// in and snapping its head from side to side.
// Bones are reset to their bind pose every frame and then rotated around the monster's
// own axes, parents before children.
[DefaultExecutionOrder(100)]
public class MonsterGait : MonoBehaviour
{
    public MonsterWander mover;
    public float strideLength = 1.4f;   // metres per full step cycle (both feet), at scale 1
    public float legSwing = 24f;
    public float kneeBend = 40f;
    public float armSwing = 9f;
    public float hunch = 9f;
    public float headBob = 11f;
    public float hipBob = 0.05f;

    [Header("Chase")]
    public float runStrideLength = 2.8f;
    public float runLean = 40f;         // extra forward lean of the spine
    public float runCrouch = 0.35f;     // metres the hips drop
    public float runLegSwing = 38f;
    public float runKneeBend = 70f;
    public float runArmReach = 40f;     // arms thrust forward

    [Header("Inspecting a hiding spot")]
    public float squatThigh = 10f;      // kneeling: thighs nearly upright
    public float squatKnee = 100f;      // shins folded back along the floor
    public float squatDrop = 0.56f;     // metres the hips drop at scale 1 (one leg length + foot)
    public float peerLean = 45f;        // spine leans in toward the door
    public float peerHeadHeight = 1.35f; // world metres (head bone): bends further until the eyes are at the slats
    public float faceGlow = 0.9f;        // faint light on the face so it shows through the slats
    public float peerEyeHeight = 1.6f;   // the face aims at this height (the hidden player's eyes)

    Animator animator;
    Transform hips, spine, chest, upperChest, neck, head;
    Transform[] upLeg = new Transform[2], leg = new Transform[2], foot = new Transform[2];
    Transform[] arm = new Transform[2], foreArm = new Transform[2], hand = new Transform[2];
    Transform[] all;
    Quaternion[] bindRotations;
    Vector3 hipsBindPosition;

    float phase;
    float walkBlend;
    float runBlend;
    float inspectBlend;
    float headYaw, headYawTarget, nextHeadTwitch;
    float headCock, headCockTarget, nextCock;
    Light glow;
    Vector3 headFaceLocal;   // which way the face points, in the head bone's space

    void Awake()
    {
        if (mover == null)
            mover = GetComponent<MonsterWander>();
        animator = GetComponent<Animator>();
        hips = Bone(HumanBodyBones.Hips);
        spine = Bone(HumanBodyBones.Spine);
        chest = Bone(HumanBodyBones.Chest);
        upperChest = Bone(HumanBodyBones.UpperChest);
        neck = Bone(HumanBodyBones.Neck);
        head = Bone(HumanBodyBones.Head);
        upLeg[0] = Bone(HumanBodyBones.LeftUpperLeg); upLeg[1] = Bone(HumanBodyBones.RightUpperLeg);
        leg[0] = Bone(HumanBodyBones.LeftLowerLeg); leg[1] = Bone(HumanBodyBones.RightLowerLeg);
        foot[0] = Bone(HumanBodyBones.LeftFoot); foot[1] = Bone(HumanBodyBones.RightFoot);
        arm[0] = Bone(HumanBodyBones.LeftUpperArm); arm[1] = Bone(HumanBodyBones.RightUpperArm);
        foreArm[0] = Bone(HumanBodyBones.LeftLowerArm); foreArm[1] = Bone(HumanBodyBones.RightLowerArm);
        hand[0] = Bone(HumanBodyBones.LeftHand); hand[1] = Bone(HumanBodyBones.RightHand);

        all = new[] { hips, spine, chest, upperChest, neck, head,
            upLeg[0], upLeg[1], leg[0], leg[1], foot[0], foot[1],
            arm[0], arm[1], foreArm[0], foreArm[1], hand[0], hand[1] };
        bindRotations = new Quaternion[all.Length];
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null)
                bindRotations[i] = all[i].localRotation;
        hipsBindPosition = hips.localPosition;
        // in the bind pose the face looks along the monster's forward
        headFaceLocal = Quaternion.Inverse(head.rotation) * transform.forward;

        // Bones are driven from code, so the Animator must not write over them.
        animator.enabled = false;

        // A cold, dim light just in front of the face, only while peering into a hiding spot.
        glow = new GameObject("FaceGlow").AddComponent<Light>();
        glow.transform.SetParent(head, false);
        glow.transform.position = head.position + transform.forward * 0.5f * transform.lossyScale.y + Vector3.up * 0.1f;
        glow.type = LightType.Point;
        glow.range = 1.6f;
        glow.color = new Color(0.75f, 0.8f, 0.95f);
        glow.shadows = LightShadows.None;
        glow.enabled = false;
    }

    Transform Bone(HumanBodyBones b) => animator.GetBoneTransform(b);

    void LateUpdate()
    {
        float speed = mover != null ? mover.CurrentSpeed : 0f;
        float target = mover != null && mover.walkSpeed > 0f ? Mathf.Clamp01(speed / mover.walkSpeed) : 0f;
        walkBlend = Mathf.MoveTowards(walkBlend, target, Time.deltaTime * 2f);
        runBlend = Mathf.MoveTowards(runBlend, mover != null && mover.IsChasing ? 1f : 0f, Time.deltaTime * 2.5f);
        inspectBlend = Mathf.MoveTowards(inspectBlend, mover != null && mover.IsInspecting ? 1f : 0f, Time.deltaTime * 1.8f);
        float ib = inspectBlend * inspectBlend * (3f - 2f * inspectBlend);
        // Distances are tuned for the model at scale 1; a bigger monster takes bigger steps.
        float size = transform.lossyScale.y;
        float stride = Mathf.Lerp(strideLength, runStrideLength, runBlend) * size;
        float swingAmount = Mathf.Lerp(legSwing, runLegSwing, runBlend);
        float kneeAmount = Mathf.Lerp(kneeBend, runKneeBend, runBlend);
        phase += speed / stride * Mathf.PI * 2f * Time.deltaTime;

        for (int i = 0; i < all.Length; i++)
            if (all[i] != null)
                all[i].localRotation = bindRotations[i];
        hips.localPosition = hipsBindPosition;

        var right = transform.right;
        var up = transform.up;
        var forward = transform.forward;
        float t = Time.time;
        float s = Mathf.Sin(phase), c = Mathf.Cos(phase);

        // Body: hunched forward, bobbing and rolling with each step, slow breathing when still.
        hips.position += up * size * (walkBlend * hipBob * (1f + runBlend) * (Mathf.Abs(c) - 0.5f)
                                      - runCrouch * runBlend - squatDrop * ib);
        Turn(hips, forward, walkBlend * 4f * s);
        Turn(hips, right, runLean * 0.4f * runBlend);
        float breath = Mathf.Sin(t * (1.3f + ib * 1.2f)) * (1.5f + ib);   // breathes faster while peering
        Turn(spine, right, hunch * 0.4f + breath + runLean * 0.6f * runBlend + peerLean * 0.6f * ib);
        Turn(chest, right, peerLean * 0.4f * ib);
        Turn(chest, right, hunch * 0.3f);
        Turn(upperChest, right, hunch * 0.3f - breath * 0.5f);

        // Legs: positive angle around 'right' swings a limb backwards.
        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? 1f : -1f;
            float swing = -swingAmount * sign * s * walkBlend;
            float knee = kneeAmount * Mathf.Max(0f, sign * c) * walkBlend;
            // lift the thighs back up against the lean so the feet stay under the body
            Turn(upLeg[side], right, swing - runLean * runBlend - squatThigh * ib);
            Turn(leg[side], right, knee + squatKnee * ib);
            Turn(foot[side], right, -(swing + knee) * 0.5f - (squatKnee - squatThigh) * ib);
        }

        // Arms: long and limp, swinging opposite the legs and lagging behind.
        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? 1f : -1f;
            float sway = Mathf.Sin(t * 0.9f + side) * 2f;
            // while peering the long arms hang forward, claws almost touching the door
            Turn(arm[side], right, armSwing * (1f + runBlend) * sign * s * walkBlend + sway - runArmReach * runBlend - 35f * ib);
            Turn(foreArm[side], right, -armSwing * 0.7f * sign * Mathf.Sin(phase - 0.8f) * walkBlend - 6f - 15f * runBlend);
            Turn(hand[side], right, -8f * Mathf.Sin(phase - 1.4f) * sign * walkBlend);
        }

        // Peering: keep bending at the waist until the face is down at the hiding spot's slats.
        float extraBend = 0f;
        if (ib > 0.01f)
        {
            float targetY = Mathf.Lerp(head.position.y, peerHeadHeight, ib);
            while (head.position.y > targetY && extraBend < 120f)
            {
                Turn(spine, right, 2f);
                Turn(chest, right, 1f);
                extraBend += 3f;
            }
            // Shuffle the whole body back or forward so the face stops just outside the door.
            var toDoor = mover.InspectLook - head.position;
            toDoor.y = 0f;
            float overshoot = mover.InspectFaceGap - toDoor.magnitude;
            hips.position -= toDoor.normalized * overshoot * ib;
        }

        // Head: pigeon bob twice per stride while walking; sudden twitchy turns while standing.
        if (Time.time >= nextHeadTwitch)
        {
            headYawTarget = Random.Range(-35f, 35f) * (1f - walkBlend);
            nextHeadTwitch = Time.time + Random.Range(0.6f, 2.5f);
        }
        headYaw = Mathf.MoveTowards(headYaw, headYawTarget * (1f - ib), Time.deltaTime * 400f);
        // peering: snaps its head from one tilted side to the other, like a pigeon sizing something up
        if (Time.time >= nextCock)
        {
            headCockTarget = (headCockTarget > 0f ? -1f : 1f) * Random.Range(22f, 38f);
            nextCock = Time.time + Random.Range(0.5f, 1.4f);
        }
        headCock = Mathf.MoveTowards(headCock, headCockTarget, Time.deltaTime * 500f);
        float peck = Mathf.Max(0f, Mathf.Sin(t * 5f)) * 6f;
        float bob = Mathf.Sin(phase * 2f) * headBob * walkBlend * (1f - 0.5f * runBlend);
        // keep the eyes on the corridor ahead despite the lean
        // while peering, tip the head back up so the face looks straight through the slats
        Turn(neck, right, -hunch * 0.8f + bob - runLean * 0.9f * runBlend + (peck - peerLean * 0.8f) * ib - extraBend * 0.6f);
        Turn(head, right, -extraBend * 0.3f);
        Turn(head, right, -bob * 0.8f);
        Turn(head, up, headYaw);
        Turn(head, forward, Mathf.Sin(t * 0.7f) * 4f * (1f - walkBlend) * (1f - ib));

        // Still too tall once the neck has tipped the face up: sink the whole kneeling body. Only the
        // face is visible from inside the hiding spot, so the folded legs dipping into the floor
        // can't be seen.
        if (ib > 0.01f)
        {
            float remaining = head.position.y - Mathf.Lerp(head.position.y, peerHeadHeight, ib);
            if (remaining > 0f)
                hips.position -= up * Mathf.Min(remaining, 0.6f * size);
        }

        // Turn the face to stare straight through the slats at eye height, then cock it like a pigeon.
        if (ib > 0.01f)
        {
            var eyeTarget = mover.InspectLook;
            eyeTarget.y = peerEyeHeight;
            var want = (eyeTarget - head.position).normalized;
            var face = head.rotation * headFaceLocal;
            var aim = Quaternion.FromToRotation(face, want);
            head.rotation = Quaternion.Slerp(Quaternion.identity, aim, ib) * head.rotation;
            head.rotation = Quaternion.AngleAxis(headCock * ib, want) * head.rotation;
        }

        glow.intensity = faceGlow * ib;
        glow.enabled = ib > 0.01f;
    }

    static void Turn(Transform bone, Vector3 axis, float degrees)
    {
        if (bone != null && degrees != 0f)
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
    }
}
