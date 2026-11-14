# OldMan combat prep: cane in the right hand + Idle / Attack / Death actions, then FBX export.
import bpy, json, os, math
from mathutils import Vector, Euler, Matrix

rig = bpy.data.objects["OldMan_Rig"]
cane = bpy.data.objects["OldMan_Cane"]
vl = bpy.context.view_layer
for o in vl.objects:
    o.select_set(False)
vl.objects.active = rig
rig.select_set(True)
pbs = rig.pose.bones

# Armature space: character faces -Y, up +Z, character right = -X
F, U, R = Vector((0, -1, 0)), Vector((0, 0, 1)), Vector((-1, 0, 0))
L = -R
ORDER = ["Hips", "Spine", "Chest", "UpperChest", "Neck", "Head",
         "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
         "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
         "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes",
         "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes"]


def reset():
    for pb in pbs:
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)
    vl.update()


def bend(name, x=0.0, y=0.0, z=0.0):
    # local rotation in degrees (spine / leg bones: x = pitch forward, y = twist)
    pbs[name].rotation_quaternion = Euler((math.radians(x), math.radians(y), math.radians(z))).to_quaternion()
    vl.update()


def aim(name, direction):
    # point the bone Y axis along an armature-space direction (minimal twist), respecting posed parents
    vl.update()
    pb = pbs[name]
    cur = pb.matrix.copy()
    rd = cur.col[1].xyz.normalized().rotation_difference(Vector(direction).normalized())
    new = rd.to_matrix().to_4x4() @ cur
    new.translation = cur.translation
    pb.matrix = new
    vl.update()


def hips(pitch_deg=0.0, drop=0.0, back=0.0):
    # pitch around the character left axis (negative = lean back), translate in armature space
    vl.update()
    pb = pbs["Hips"]
    cur = pb.matrix.copy()
    rot = Matrix.Rotation(math.radians(-pitch_deg), 4, 'X')
    new = rot @ cur
    new.translation = cur.translation + Vector((0, back, -drop))
    pb.matrix = new
    vl.update()


def base_pose(hunch=8.0):
    bend("Spine", hunch * 0.4)
    bend("Chest", hunch * 0.3)
    bend("UpperChest", hunch * 0.3)
    bend("Neck", -hunch * 0.6)
    aim("LeftUpperArm", L * 0.25 + F * 0.05 - U)
    aim("LeftLowerArm", L * 0.1 + F * 0.35 - U)
    aim("LeftHand", F * 0.3 - U)
    # right arm leaning on the cane: forearm forward, hand pointing down so the cane reaches the ground
    aim("RightUpperArm", R * 0.2 + F * 0.15 - U)
    aim("RightLowerArm", R * 0.1 + F - U * 0.25)
    aim("RightHand", -U)


def key(frame):
    for n in ORDER:
        pbs[n].keyframe_insert("rotation_quaternion", frame=frame, group=n)
    pbs["Hips"].keyframe_insert("location", frame=frame, group="Hips")


def new_action(name):
    old = bpy.data.actions.get(name)
    if old:
        bpy.data.actions.remove(old)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = act
    return act


def death_lying(pitch, drop, legs):
    reset()
    hips(pitch, drop, 0.35)
    bend("Neck", 10)
    aim("RightUpperArm", R + U * 0.2)
    aim("LeftUpperArm", L + U * 0.2)
    aim("RightLowerArm", R + U * 0.5)
    aim("LeftLowerArm", L + U * 0.5)
    bend("LeftUpperLeg", 25 + legs)
    bend("LeftLowerLeg", -40 - legs)
    bend("RightUpperLeg", 10 + legs)


# ---- cane into the right hand (rest pose) ----
rig.animation_data_create()
rig.animation_data.action = None
reset()
cane_mw = cane.matrix_world.copy()
cane_top_local = max((Vector(c) for c in cane.bound_box), key=lambda v: (cane_mw @ v).z)
hand = rig.data.bones["RightHand"]
hand_mid = rig.matrix_world @ ((hand.head_local + hand.tail_local) / 2)
hand_dir = (rig.matrix_world.to_3x3() @ (hand.tail_local - hand.head_local)).normalized()
# cane "down" (-Z) follows the hand bone direction, handle at the palm
rot = Vector((0, 0, -1)).rotation_difference(hand_dir).to_matrix().to_4x4() @ cane_mw.to_3x3().to_4x4()
cane.matrix_world = rot
vl.update()
top_world = cane.matrix_world @ cane_top_local
cane.matrix_world = Matrix.Translation(hand_mid - top_world + hand_dir * 0.03) @ cane.matrix_world
vl.update()
mw = cane.matrix_world.copy()
cane.parent = rig
cane.parent_type = 'BONE'
cane.parent_bone = "RightHand"
vl.update()
cane.matrix_world = mw
vl.update()

# ---- Idle: 60 frames, breathing ----
new_action("OldMan_Idle")
for f, breath in ((1, 0.0), (31, 1.0), (61, 0.0)):
    reset()
    base_pose()
    bend("UpperChest", 8 * 0.3 - 2.5 * breath)
    bend("Head", 3 * breath)
    pbs["Hips"].location = (0, -0.006 * breath, 0)  # local Y = up
    key(f)

# ---- Attack: 26 frames, overhead cane strike (impact around frame 14) ----
new_action("OldMan_Attack")
reset(); base_pose(); key(1)
reset(); base_pose(hunch=-6)
bend("UpperChest", -6, -20)
aim("RightUpperArm", R * 0.35 + U - F * 0.25)
aim("RightLowerArm", U * 0.5 - F)
aim("RightHand", U * 0.2 - F)
aim("LeftUpperArm", L * 0.5 + F * 0.4 - U * 0.4)
aim("LeftLowerArm", L * 0.2 + F - U * 0.2)
key(9)
reset(); base_pose(hunch=22)
bend("UpperChest", 22 * 0.3, 15)
aim("RightUpperArm", R * 0.15 + F - U * 0.25)
aim("RightLowerArm", F - U * 0.55)
aim("RightHand", F * 0.6 - U)
aim("LeftUpperArm", L * 0.4 - F * 0.2 - U)
aim("LeftLowerArm", L * 0.2 - U)
bend("LeftUpperLeg", -15)
bend("LeftLowerLeg", 10)
key(14)
key(18)
reset(); base_pose(); key(26)

# ---- Death: 40 frames, stagger and fall on the back ----
new_action("OldMan_Death")
reset(); base_pose(); key(1)
reset(); base_pose(hunch=-10)
hips(-12, 0.03, 0.04)
aim("RightUpperArm", R + U * 0.4)
aim("LeftUpperArm", L + U * 0.4)
bend("LeftUpperLeg", 20)
bend("LeftLowerLeg", -30)
bend("RightUpperLeg", -10)
key(8)
death_lying(-88, 0.15, 0); key(22)
death_lying(-80, 0.12, 5); key(28)
death_lying(-88, 0.15, 0); key(34); key(40)

rig.animation_data.action = None
reset()

# ---- save + export ----
bpy.ops.wm.save_mainfile()
out_dir = r"C:\Users\Usuario\animal_island1\Assets\Characters\OldMan"
for o in vl.objects:
    o.select_set(False)
names = ["OldMan_Rig", "OldMan_Body", "OldMan_Hair", "OldMan_Glasses", "OldMan_Shirt", "OldMan_Shorts", "OldMan_Vest", "OldMan_Cane"]
for n in names:
    bpy.data.objects[n].select_set(True)
vl.objects.active = rig
fbx = os.path.join(out_dir, "OldMan.fbx")
bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'ARMATURE', 'MESH'},
    apply_scale_options='FBX_SCALE_ALL', apply_unit_scale=True, axis_forward='-Z', axis_up='Y',
    bake_space_transform=False, use_mesh_modifiers=False, mesh_smooth_type='FACE', add_leaf_bones=False,
    primary_bone_axis='Y', secondary_bone_axis='X', bake_anim=True, bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
    path_mode='STRIP', embed_textures=False)

# sanity: cane tip height in the idle pose
rig.animation_data.action = bpy.data.actions["OldMan_Idle"]
rig.animation_data.action_slot = bpy.data.actions["OldMan_Idle"].slots[0]
bpy.context.scene.frame_set(1)
vl.update()
dg = bpy.context.evaluated_depsgraph_get()
ce = cane.evaluated_get(dg)
tip = min((ce.matrix_world @ Vector(c) for c in ce.bound_box), key=lambda v: v.z)
rig.animation_data.action = None
print("JSONOUT" + json.dumps({"actions": [(a.name, list(a.frame_range)) for a in bpy.data.actions],
    "cane_parent": [cane.parent.name if cane.parent else None, cane.parent_bone],
    "cane_tip_idle_z": round(tip.z, 3), "fbx_size": os.path.getsize(fbx)}))
