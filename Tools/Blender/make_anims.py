# Generates gameplay animations from each character's own Walk cycle.
#   animals: <Name>_Idle, <Name>_Run, <Name>_Jump, <Name>_Death
#   old man: OldMan_Run
# Usage: blender -b <Name>.blend --python make_anims.py -- <Name> <out_fbx> [oldman]
import bpy, sys, os, json, math
from mathutils import Vector, Quaternion, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
NAME, OUT_FBX = args[0], args[1]
OLDMAN = len(args) > 2 and args[2] == "oldman"

rig = bpy.data.objects[NAME + "_Rig"]
vl = bpy.context.view_layer
sc = bpy.context.scene
pbs = rig.pose.bones
BONES = [pb.name for pb in pbs]
F, U, R = Vector((0, -1, 0)), Vector((0, 0, 1)), Vector((-1, 0, 0))
L = -R
rig.animation_data_create()


def use_action(act):
    rig.animation_data.action = act
    if act.slots:
        rig.animation_data.action_slot = act.slots[0]


# ---------------------------------------------------------------- sample the walk cycle
walk = next(a for a in bpy.data.actions if a.name.endswith("_Walk"))
w0, w1 = int(walk.frame_range[0]), int(walk.frame_range[1])
cycle = w1 - w0
use_action(walk)
samples = []   # per frame: {bone: quat}, hips loc, left/right foot y
for f in range(w0, w1 + 1):
    sc.frame_set(f)
    vl.update()
    q = {b: pbs[b].rotation_quaternion.copy() for b in BONES}
    samples.append({"q": q, "hips": pbs["Hips"].location.copy(),
                    "lf": pbs["LeftFoot"].head.y, "rf": pbs["RightFoot"].head.y})
rig.animation_data.action = None


def average(quats):
    ref = quats[0]
    acc = Vector((0.0, 0.0, 0.0, 0.0))
    for q in quats:
        v = Vector((q.w, q.x, q.y, q.z))
        if v.dot(Vector((ref.w, ref.x, ref.y, ref.z))) < 0: v = -v
        acc += v
    acc.normalize()
    return Quaternion((acc[0], acc[1], acc[2], acc[3]))


cyc = samples[:-1]
neutral = {b: average([s["q"][b] for s in cyc]) for b in BONES}
neutral_hips = sum((s["hips"] for s in cyc), Vector()) / len(cyc)
hips_height = rig.data.bones["Hips"].head_local.z


def sample_at(t):
    """walk pose at fractional cycle position t (0..1)"""
    x = (t % 1.0) * cycle
    i = int(x)
    a, b = samples[i], samples[min(i + 1, len(samples) - 1)]
    k = x - i
    return {bn: a["q"][bn].slerp(b["q"][bn], k) for bn in BONES}, a["hips"].lerp(b["hips"], k)


def amplify(q_neutral, q, k):
    d = q_neutral.inverted() @ q
    axis, angle = d.to_axis_angle()
    if angle > math.pi: angle -= 2 * math.pi
    return q_neutral @ Quaternion(axis, angle * k)


# ---------------------------------------------------------------- pose helpers
def set_pose(quats, hips_loc):
    for b in BONES:
        pbs[b].rotation_quaternion = quats[b]
        pbs[b].location = (0, 0, 0)
        pbs[b].scale = (1, 1, 1)
    pbs["Hips"].location = hips_loc
    vl.update()


def rotate_world(name, axis, deg):
    """rotate a bone around an armature-space axis through its head (keeps children following)"""
    vl.update()
    pb = pbs[name]
    m = pb.matrix.copy()
    head = m.translation.copy()
    r = Matrix.Rotation(math.radians(deg), 4, axis)
    new = r @ m
    new.translation = head
    pb.matrix = new
    vl.update()


def aim(name, direction):
    vl.update()
    pb = pbs[name]
    cur = pb.matrix.copy()
    rd = cur.col[1].xyz.normalized().rotation_difference(Vector(direction).normalized())
    new = rd.to_matrix().to_4x4() @ cur
    new.translation = cur.translation
    pb.matrix = new
    vl.update()


def move_hips(pitch_deg=0.0, drop=0.0, back=0.0):
    vl.update()
    pb = pbs["Hips"]
    cur = pb.matrix.copy()
    new = Matrix.Rotation(math.radians(-pitch_deg), 4, 'X') @ cur
    new.translation = cur.translation + Vector((0, back, -drop))
    pb.matrix = new
    vl.update()


def key(frame):
    for b in BONES:
        pbs[b].keyframe_insert("rotation_quaternion", frame=frame, group=b)
    pbs["Hips"].keyframe_insert("location", frame=frame, group="Hips")


def new_action(suffix):
    name = NAME + "_" + suffix
    old = bpy.data.actions.get(name)
    if old: bpy.data.actions.remove(old)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    rig.animation_data.action = act
    return act


TAILS = [b for b in BONES if b.startswith("Tail")]
EARS = [b for b in BONES if b.endswith("Ear")]
LIMBS = [b for b in BONES if any(k in b for k in ("Arm", "Leg", "Foot", "Toes", "Hand", "Shoulder"))]
created = []

# ---------------------------------------------------------------- RUN (all characters)
RUN_FRAMES = 20 if not OLDMAN else 26
new_action("Run")
for i in range(RUN_FRAMES + 1):
    q, hips = sample_at(i / RUN_FRAMES)
    out = {}
    for b in BONES:
        k = 1.85 if b in LIMBS else (1.4 if b in TAILS else 1.2)
        out[b] = amplify(neutral[b], q[b], k)
    set_pose(out, neutral_hips + (hips - neutral_hips) * 1.8)
    rotate_world("Spine", 'X', 16)            # lean forward
    rotate_world("Neck", 'X', -8)             # keep the eyes on the road
    key(i + 1)
created.append(NAME + "_Run")

if not OLDMAN:
    # ------------------------------------------------------------ IDLE
    new_action("Idle")
    for f, breath, look in ((1, 0, 0), (21, 1, 1), (41, 0.3, -1), (61, 0, 0)):
        set_pose(neutral, neutral_hips + Vector((0, -0.006 * breath, 0)))
        rotate_world("UpperChest", 'X', -2.5 * breath)
        rotate_world("Head", 'Z', 6 * look)
        for i, t in enumerate(TAILS):
            rotate_world(t, 'Z', 7 * math.sin(f / 60 * 2 * math.pi + i * 0.6))
        for e in EARS:
            rotate_world(e, 'X', 5 * breath)
        key(f)
    created.append(NAME + "_Idle")

    # ------------------------------------------------------------ JUMP (held pose)
    fwd = min(range(len(cyc)), key=lambda i: cyc[i]["lf"])    # left foot most forward
    back = max(range(len(cyc)), key=lambda i: cyc[i]["rf"])   # right foot most back
    new_action("Jump")
    for f in (1, 11):
        pose = dict(neutral)
        for b in BONES:
            if b.startswith("Left") and ("Leg" in b or "Foot" in b): pose[b] = amplify(neutral[b], cyc[fwd]["q"][b], 1.4)
            if b.startswith("Right") and ("Leg" in b or "Foot" in b): pose[b] = amplify(neutral[b], cyc[back]["q"][b], 1.3)
        set_pose(pose, neutral_hips)
        rotate_world("Spine", 'X', -5)
        aim("LeftUpperArm", L * 0.7 + U * 0.6 + F * 0.15)
        aim("RightUpperArm", R * 0.7 + U * 0.6 + F * 0.15)
        aim("LeftLowerArm", L * 0.3 + U)
        aim("RightLowerArm", R * 0.3 + U)
        for t in TAILS: rotate_world(t, 'X', 10 if f == 1 else 14)
        for e in EARS: rotate_world(e, 'X', -12)
        key(f)
    created.append(NAME + "_Jump")

    # ------------------------------------------------------------ DEATH
    drop = max(0.05, hips_height - 0.2)

    def lying(pitch, extra):
        set_pose(neutral, neutral_hips)
        move_hips(pitch, drop, 0.3)
        rotate_world("Neck", 'X', 10)
        aim("LeftUpperArm", L + U * 0.2)
        aim("RightUpperArm", R + U * 0.2)
        aim("LeftLowerArm", L + U * 0.5)
        aim("RightLowerArm", R + U * 0.5)
        rotate_world("LeftUpperLeg", 'X', -(20 + extra))
        rotate_world("RightUpperLeg", 'X', -(8 + extra))
        for t in TAILS: rotate_world(t, 'X', 6)

    new_action("Death")
    set_pose(neutral, neutral_hips); key(1)
    set_pose(neutral, neutral_hips)
    move_hips(-12, 0.02, 0.04)
    aim("LeftUpperArm", L + U * 0.5)
    aim("RightUpperArm", R + U * 0.5)
    rotate_world("LeftUpperLeg", 'X', -20)
    key(8)
    lying(-88, 0); key(22)
    lying(-80, 6); key(28)
    lying(-88, 0); key(34); key(40)
    created.append(NAME + "_Death")

rig.animation_data.action = None
set_pose({b: Quaternion() for b in BONES}, Vector())

# ---------------------------------------------------------------- save + export
bpy.ops.wm.save_mainfile()
for o in vl.objects: o.select_set(False)
export_objs = [rig] + [o for o in rig.children_recursive if o.type == 'MESH']
for o in export_objs: o.select_set(True)
vl.objects.active = rig
bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, object_types={'ARMATURE', 'MESH'},
    apply_scale_options='FBX_SCALE_ALL', apply_unit_scale=True, axis_forward='-Z', axis_up='Y',
    bake_space_transform=False, use_mesh_modifiers=False, mesh_smooth_type='FACE', add_leaf_bones=False,
    primary_bone_axis='Y', secondary_bone_axis='X', bake_anim=True, bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
    path_mode='STRIP', embed_textures=False)
print("JSONOUT" + json.dumps({"name": NAME, "created": created, "actions": sorted(a.name for a in bpy.data.actions),
                              "exported": [o.name for o in export_objs], "fbx_kb": os.path.getsize(OUT_FBX) // 1024}))
