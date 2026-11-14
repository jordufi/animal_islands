import bpy, math
sc = bpy.context.scene
for c in bpy.data.collections:
    if c.name in ("Characters", "Assets", "Workshop"):
        c.hide_render = True
cam_data = bpy.data.cameras.new("TopCam")
cam_data.type = 'ORTHO'
cam_data.ortho_scale = 72
cam_data.clip_end = 500
cam = bpy.data.objects.new("TopCam", cam_data)
sc.collection.objects.link(cam)
# Unity (x, z) -> Blender (-x, -z); centre Unity (-4, -2)
cam.location = (4, 2, 80)
cam.rotation_euler = (0, 0, 0)
sc.camera = cam
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x = 1100
sc.render.resolution_y = 1100
sc.render.filepath = r"C:\Users\Usuario\AppData\Local\Temp\claude\C--Users-Usuario-animal-island1\6f3f3173-8634-4c0e-b7b7-7a516ff743ee\scratchpad\top_raw.png"
bpy.ops.render.render(write_still=True)
