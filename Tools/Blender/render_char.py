import bpy, os, sys
from mathutils import Vector
args = sys.argv[sys.argv.index("--") + 1:]
NAME, OUT = args[0], args[1]
frames = [tuple(x.split(":")) for x in args[2:]]   # Action:frame
os.makedirs(OUT, exist_ok=True)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.render.resolution_x = 320
sc.render.resolution_y = 360
for c in bpy.data.collections:
    if "Source" in c.name: c.hide_render = True
cam_data = bpy.data.cameras.new("C")
cam = bpy.data.objects.new("C", cam_data)
sc.collection.objects.link(cam)
cam.location = Vector((2.6, -2.4, 1.0))
cam.rotation_euler = (Vector((0, 0, 0.55)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
sc.camera = cam
rig = bpy.data.objects[NAME + "_Rig"]
rig.animation_data_create()
for act_suffix, f in frames:
    a = bpy.data.actions[NAME + "_" + act_suffix]
    rig.animation_data.action = a
    rig.animation_data.action_slot = a.slots[0]
    sc.frame_set(int(f))
    sc.render.filepath = os.path.join(OUT, "%s_%s_%02d.png" % (NAME, act_suffix, int(f)))
    bpy.ops.render.render(write_still=True)
