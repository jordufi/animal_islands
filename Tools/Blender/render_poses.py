import bpy, os
from mathutils import Vector
out = r"C:\Users\Usuario\AppData\Local\Temp\claude\C--Users-Usuario-animal-island1\6f3f3173-8634-4c0e-b7b7-7a516ff743ee\scratchpad\poses"
os.makedirs(out, exist_ok=True)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.render.resolution_x = 360
sc.render.resolution_y = 360
sc.render.film_transparent = False
# side-ish camera so the arm swing and the fall read clearly
cam = bpy.data.objects["Cam_Hero"]
cam.location = Vector((2.6, -2.2, 1.0))
d = Vector((0, 0, 0.55)) - cam.location
cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
sc.camera = cam
rig = bpy.data.objects["OldMan_Rig"]
rig.animation_data_create()
for act, frame in (("OldMan_Idle", 1), ("OldMan_Attack", 9), ("OldMan_Attack", 14), ("OldMan_Death", 40), ("OldMan_Walk", 10)):
    a = bpy.data.actions[act]
    rig.animation_data.action = a
    rig.animation_data.action_slot = a.slots[0]
    sc.frame_set(frame)
    sc.render.filepath = os.path.join(out, "%s_%02d.png" % (act, frame))
    bpy.ops.render.render(write_still=True)
