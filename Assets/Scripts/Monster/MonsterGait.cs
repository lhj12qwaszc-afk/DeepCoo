using UnityEngine;

// Procedural, unsettling walk for the pigeon monster, driven by MonsterWander.CurrentSpeed:
// hunched spine, long legs stepping, limp dangling arms, a pigeon-like head bob while
// walking, and sudden jerky head turns while standing still. While chasing it drops low,
// leans far forward, takes long strides and reaches ahead with its arms.
// Bones are reset to their bind pose every frame and then rotated around the monster's
// own axes, parents before children.
[DefaultExecutionOrder(100)]
public class MonsterGait : MonoBehaviour
{
    public MonsterWander mover;
    public float strideLength = 1.4f;   // metres per full step cycle (both feet)
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
    float headYaw, headYawTarget, nextHeadTwitch;

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

        // Bones are driven from code, so the Animator must not write over them.
        animator.enabled = false;
    }

    Transform Bone(HumanBodyBones b) => animator.GetBoneTransform(b);

    void LateUpdate()
    {
        float speed = mover != null ? mover.CurrentSpeed : 0f;
        float target = mover != null && mover.walkSpeed > 0f ? Mathf.Clamp01(speed / mover.walkSpeed) : 0f;
        walkBlend = Mathf.MoveTowards(walkBlend, target, Time.deltaTime * 2f);
        runBlend = Mathf.MoveTowards(runBlend, mover != null && mover.IsChasing ? 1f : 0f, Time.deltaTime * 2.5f);
        float stride = Mathf.Lerp(strideLength, runStrideLength, runBlend);
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
        hips.position += up * (walkBlend * hipBob * (1f + runBlend) * (Mathf.Abs(c) - 0.5f) - runCrouch * runBlend);
        Turn(hips, forward, walkBlend * 4f * s);
        Turn(hips, right, runLean * 0.4f * runBlend);
        float breath = Mathf.Sin(t * 1.3f) * 1.5f;
        Turn(spine, right, hunch * 0.4f + breath + runLean * 0.6f * runBlend);
        Turn(chest, right, hunch * 0.3f);
        Turn(upperChest, right, hunch * 0.3f - breath * 0.5f);

        // Legs: positive angle around 'right' swings a limb backwards.
        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? 1f : -1f;
            float swing = -swingAmount * sign * s * walkBlend;
            float knee = kneeAmount * Mathf.Max(0f, sign * c) * walkBlend;
            // lift the thighs back up against the lean so the feet stay under the body
            Turn(upLeg[side], right, swing - runLean * runBlend);
            Turn(leg[side], right, knee);
            Turn(foot[side], right, -(swing + knee) * 0.5f);
        }

        // Arms: long and limp, swinging opposite the legs and lagging behind.
        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? 1f : -1f;
            float sway = Mathf.Sin(t * 0.9f + side) * 2f;
            Turn(arm[side], right, armSwing * (1f + runBlend) * sign * s * walkBlend + sway - runArmReach * runBlend);
            Turn(foreArm[side], right, -armSwing * 0.7f * sign * Mathf.Sin(phase - 0.8f) * walkBlend - 6f - 15f * runBlend);
            Turn(hand[side], right, -8f * Mathf.Sin(phase - 1.4f) * sign * walkBlend);
        }

        // Head: pigeon bob twice per stride while walking; sudden twitchy turns while standing.
        if (Time.time >= nextHeadTwitch)
        {
            headYawTarget = Random.Range(-35f, 35f) * (1f - walkBlend);
            nextHeadTwitch = Time.time + Random.Range(0.6f, 2.5f);
        }
        headYaw = Mathf.MoveTowards(headYaw, headYawTarget, Time.deltaTime * 400f);
        float bob = Mathf.Sin(phase * 2f) * headBob * walkBlend * (1f - 0.5f * runBlend);
        // keep the eyes on the corridor ahead despite the lean
        Turn(neck, right, -hunch * 0.8f + bob - runLean * 0.9f * runBlend);
        Turn(head, right, -bob * 0.8f);
        Turn(head, up, headYaw);
        Turn(head, forward, Mathf.Sin(t * 0.7f) * 4f * (1f - walkBlend));
    }

    static void Turn(Transform bone, Vector3 axis, float degrees)
    {
        if (bone != null && degrees != 0f)
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
    }
}
