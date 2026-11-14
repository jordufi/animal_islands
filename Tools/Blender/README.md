# Blender scripts (Animal Island)

Scripts used to create and export the game assets. Run them with Blender in background mode, for example:

```
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b <file.blend> --python <script.py> -- <args>
```

| Script | What it does |
|---|---|
| `make_map_props.py` | Low-poly props of the Isla Zombie map (faro, coche, autobús, puesto, tienda, caseta, contenedor, barco, avioneta, grada) → `Assets/Maps/IslaZombie/Models` |
| `make_anims.py` | Generates Idle / Run / Jump / Death from a character's Walk (old man: Run) and exports the FBX. Args: `<Name> <out.fbx> [oldman]` |
| `oldman_combat.py` | Old man: cane in the right hand + Idle / Attack / Death, exports `OldMan.fbx` |
| `make_pistol.py`, `make_weapons.py` | Low-poly pistol, shotgun, sniper and SMG (`.blend` + FBX) |
| `export_island.py` | Island.blend → one FBX per mesh + `Island_Layout.json` for Unity |
| `export_char.py` | Exports an animal (rig + meshes + actions) to Unity |
| `render_*.py`, `inspect_*.py` | Preview renders and rig / scene inspection used while working |
