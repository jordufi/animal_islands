import bpy, os
from mathutils import Vector
OUT = r"C:\Users\Usuario\AppData\Local\Temp\claude\C--Users-Usuario-animal-island1\6f3f3173-8634-4c0e-b7b7-7a516ff743ee\scratchpad"
sc = bpy.context.scene
for c in bpy.data.collections:
    if c.name in ("Characters", "Assets", "Workshop"):
        c.hide_render = True
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x = 1600
sc.render.resolution_y = 900

def U(x, y, z):   # Unity (x, y, z) -> Blender
    return Vector((-x, -z, y))

# green beacon at the end of the dock (where the rescue boat / extraction is in the game)
ext = U(18.2, 0.0, 24.6)
bpy.ops.mesh.primitive_cylinder_add(radius=0.6, depth=30, location=ext + Vector((0, 0, 15)))
beacon = bpy.context.active_object
mat = bpy.data.materials.new("BeaconMat")
mat.diffuse_color = (0.2, 1.0, 0.35, 1)
beacon.data.materials.append(mat)
beacon.color = (0.2, 1.0, 0.35, 1)
sc.display.shading.color_type = 'TEXTURE'

cam_data = bpy.data.cameras.new("ShotCam")
cam = bpy.data.objects.new("ShotCam", cam_data)
sc.collection.objects.link(cam)
sc.camera = cam

def shot(pos, look, lens, name):
    cam.location = pos
    cam.rotation_euler = (look - pos).to_track_quat('-Z', 'Y').to_euler()
    cam_data.lens = lens
    sc.render.filepath = os.path.join(OUT, name)
    bpy.ops.render.render(write_still=True)

shot(U(-22, 32, -30), U(6, 0, 6), 24, "extraction_overview.png")
shot(U(9.2, 5, 16.6), U(18.2, 1.0, 24.6), 30, "extraction_closeup.png")
