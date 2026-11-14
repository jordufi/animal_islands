import bpy, json, sys, os
name = sys.argv[sys.argv.index("--") + 1]
rig = bpy.data.objects[name + "_Rig"]
bones = []
for b in rig.data.bones:
    bones.append(b.name)
walk = [a for a in bpy.data.actions if "Walk" in a.name]
info = {"name": name, "n_bones": len(bones), "bones": bones,
        "actions": [(a.name, list(a.frame_range)) for a in bpy.data.actions],
        "fps": bpy.context.scene.render.fps,
        "modes": sorted({pb.rotation_mode for pb in rig.pose.bones}),
        "rig_rot": [round(v, 3) for v in rig.rotation_euler], "rig_scale": [round(v, 3) for v in rig.scale]}
# facing: where do the feet/toes point (armature space)
lt = rig.data.bones.get("LeftToes") or rig.data.bones.get("LeftFoot")
if lt:
    info["toe_dir"] = [round(v, 2) for v in (lt.tail_local - lt.head_local).normalized()]
hips = rig.data.bones.get("Hips")
info["hips_head"] = [round(v, 3) for v in hips.head_local] if hips else None
head = rig.data.bones.get("Head")
info["head_tail"] = [round(v, 3) for v in head.tail_local] if head else None
print("JSONOUT" + json.dumps(info))
