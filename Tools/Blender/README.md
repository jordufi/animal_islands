# Blender scripts (Animal Island)

Scripts used to create and export the game assets. Run them with Blender in background mode, for example:

```
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b <file.blend> --python <script.py> -- <args>
```

| Script | What it does |
|---|---|
| `make_map_props.py` | Low-poly props of the Isla Zombie map (faro, coche, autobús, puesto, tienda, caseta, contenedor, barco, avioneta, grada) → `Assets/Maps/IslaZombie/Models` |
| `make_hotel_props.py` | Kit of the Hotel Jubiles map: 87 `HJ_*` FBX (tower facade strips, loose neon letters, lobby, lifts, stairs, buffet / Salón Dorado, basement, rooms, pool, roof, pickups, old-men accessories, figures) → `Assets/Maps/HotelJubiles/Models` + `Documents/BeachAnimals/HotelJubiles/HotelJubiles_Props.blend`. Reuses the Builder of `make_venecia_props.py`. Glowing parts are in `HJ_Emit_<Name>` slots (on colours in its `EMIT` table). Prints a coplanar-face report; `-- sheet` also renders the contact sheet `Renders/HotelKit_*.png` |
| `make_crucero_props.py` | Kit of the cruise map "Reina del Imserso": 82 `CR_*` FBX (hull strips, bow, stern with the marina, rails, lifeboats and davits, funnel, bridge, watertight doors, deckhouse / corridor wall strips, sun deck with pool, slides, cabanas and shuffleboard, casino, theatre, dining room, cabins, engine room, port pieces, gangway, the receding port + city and the Benidorm skyline with the HOTEL JUBILES neon, boarding launches and hooks, mission items, old-men accessories) → `Assets/Maps/Crucero/Models` + `Documents/BeachAnimals/Crucero/Crucero_Props.blend`. Reuses the Builder of `make_venecia_props.py`. Glowing parts are in `CR_Emit_<Name>` slots (on colours in its `EMIT` table). Prints a coplanar-face report; `-- sheet` also renders `Renders/CruceroKit_*.png` (`only=A,B out=<dir> per=4` for a closer look) |
| `make_anims.py` | Generates Idle / Run / Jump / Death from a character's Walk (old man: Run) and exports the FBX. Args: `<Name> <out.fbx> [oldman]` |
| `oldman_combat.py` | Old man: cane in the right hand + Idle / Attack / Death, exports `OldMan.fbx` |
| `make_pistol.py`, `make_weapons.py` | Low-poly pistol, shotgun, sniper and SMG (`.blend` + FBX) |
| `export_island.py` | Island.blend → one FBX per mesh + `Island_Layout.json` for Unity |
| `export_char.py` | Exports an animal (rig + meshes + actions) to Unity |
| `render_*.py`, `inspect_*.py` | Preview renders and rig / scene inspection used while working |
