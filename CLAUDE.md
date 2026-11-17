# Animal Island — project context for Claude

Co-op-ready (currently single-player) third-person **Zombies-style round survival** game in Unity, with
animal characters and an ironic / shitpost tone (think Black Ops Zombies × Borderlands humour).
The user talks to Claude mostly in **Spanish** (sometimes English): answer in the language they write in.
In-game text is in Spanish. The user wants big multi-step work done in one go and verified by play tests.
**Git/GitHub (since 2026-11-14):** repo https://github.com/jordufi/animal_islands (branch `main`, local repo in the
project root, `.gitignore` for Unity: Library, Temp, Builds, Logs, UserSettings, Renders, *.zip, csproj/sln). ~1355
files / ~21 MB, no Git LFS (nothing >40 MB). First commit `84e9104`, README + CLAUDE.md `e7c1f41`; pushed OK
(the first push got HTTP 403 from a stale Windows credential; fixed by the user signing in again, so push works now).
Commit / push **only when the user asks**; end commit messages with the Co-Authored-By line from the system
reminder. Use `git` from Git Bash / PowerShell; never run `du` over the whole project (Library is 3.3 GB, very slow).
Before large risky edits it is still fine to copy the touched files to the session scratchpad. `README.md` has the
public summary (Spanish): keep it in sync with big changes.

## The game (decided design — don't re-ask)
- **Premise:** a beach resort island taken over by hostile retirees ("jubilados"). You play resort animals.
- **Loop:** rounds of old men (health grows each round) → earn **conchas** (points) → open doors to unlock
  map zones → reach the **dock (Muelle)**, beat the final boss, **extract** on the rescue boat. No timer.
- **Each door opened = choose 1 of 3 cards** (blessing + curse, stacking for the whole run).
- **Weapons from the mystery box** ("Máquina de Granizados"): **one use per zone opened**; after use it
  vanishes and reappears at a random box spot of an already-open zone when a new zone opens. On Isla del Faro it
  is **not on the map at the start**: it first appears in **Parking or Volley** when one of them is opened (zones
  flagged `firstbox`; never first in the Chiringuito or the Templo), then the normal rule.
  Exception: the **Lentos-peta** (slow shotgun, shells one by one) hangs on a Camping wall: **500**, ammo 250.
  Progression: pistol → shotgun → sniper → SMG. **Pack-a-Punch** = "El Microondas del Chiringuito".
  Every animal also carries a **ukulele** for melee (R3).
- **Drinks** (perks) with side effects. **5 animals**, each with a passive + active ability (L1).
- **Enemies:** only the old man model, with stat variants that change clothing colour
  (normal; red tracksuit runner from round 4; purple "culturista" tank from round 7). **Every 5th round is a
  night round**: it gets dark and only **green "abuelos senderistas"** come (fewer, **at most 5 alive at once**
  (`maxAliveAtOnce` on the variant); they walk over props and throw chanclas from mid range); dawn drops a Nevera de munición. Boss: "Don Anselmo, presidente de la comunidad"
  (giant old man).
- **Spawns are visible and predictable:** old men come out of entrances (WC cabin, bus del Imserso, tent, rock
  crack, pool ladder from the sea...), ~1 s in sight before they are in play, and they can come from any side
  (no safe spots, sometimes from behind).
- **Game modes** (main menu, like the animal): **Clásico** and **"Anselmo cagón"**: every old man gets at spawn a
  fixed 50 % flag (`spawn_through`, `EnemyAI.Tunneler`); tunnelers take shortcuts through the spawn entrances (WC,
  crack, bus...) into another zone when that is faster than walking to the player; the rest chase normally. Only one
  old man at a time per entrance (in or out), with the climb/door animation. Numbers in `Data/AnselmoCagon.asset`.
- **Easter egg:** 3 fake Julio Iglesias records → sing at the karaoke → Golden Disc (carryable, hands busy,
  drops when hit) → extract with it for ×2 score. More extractable items planned for other maps.
- **Runs start from zero** (no meta progression). Target run length **20–40 min**. It must be **hard**
  (an easy game gets boring). The economy is currently too generous (doors open by round 2): tune it later
  with the run logs.
- **Multiple maps planned**. Current: **Isla del Faro** (files: IslaZombie) and **Original** (old test map).
- **Priorities agreed with the user:** solid base first (done 2026-10-07) → the user will design the actual
  cards / power-ups / curses → multiplayer (Netcode for GameObjects, 2–4 co-op) → graphics.
  Keep gameplay logic separated from input/visuals so multiplayer is not a rewrite.

## Multiplayer (decided with the user 2026-10-08 — don't re-ask)
Plan: Phase 1 "N players" refactor → Phase 2 split-screen (2–4, `PlayerInputManager`) → Phase 3 online 4 players
(Netcode for GameObjects + Unity Relay, host-authoritative, join by code). **All three phases done (online 2026-10-12).**
Online = each friend at home with the same .zip; one creates the room (6-letter Relay code), the others join with it;
no servers of ours (the host's PC runs the match, Unity Relay only forwards packets; free tier). Online cards don't
pause (timer 15 s, chooser frozen); online pause menu doesn't stop the world.
- **Conchas: one shared pot** for the team (anyone earns into it, anyone spends from it).
- **Down / revive exactly like Black Ops Zombies:** downed = last stand with the pistol, bleed-out timer, a teammate
  revives by holding □ near you; everyone down = game over; a bled-out player comes back next round.
- **Cards:** the player who opens the door chooses; blessing + curse apply to **everyone**. **Changed 2026-10-11:
  the game PAUSES for everyone and the cards cover the whole screen** ("ELIGE J2 (TUCÁN)"); only the chooser's
  devices move / take them (stick or D-pad, X / □ / R2). `CoopData.pauseForCards` (off = old no-pause + timer,
  kept for online later).
- **Split-screen lobby** = the screen where everyone picks their animal; **J1 also sets difficulty and mode there**
  (▲ ▼ moves J1's cursor animal / dificultad / modo, ◄ ► changes it).
- **Scale with player count:** more old men / boss health with 2–4 players.
- **Box weapon offer can be taken by anyone** (others can steal it).
- Same animal can be picked by several players. **No joining mid-game.** **No friendly fire.**
  Split-screen + online mixed: later. Host leaves = game over.
- Defaults accepted: bleed-out 30 s, revive hold □ ~4 s (Tinto = faster revive; solo keeps the Tinto self-revive);
  (the card timer only applies without the pause); split-screen pause stops
  everyone, online pause doesn't; +50 % old men per extra player (max 24 alive), boss health scales; old men chase the
  nearest reachable player; llave / disc can be picked up by anyone and drop where the holder goes down.

## Tech
- **Unity 6.6 (6000.6.4f1)**, URP, Input System 1.20 (Input System only), AI Navigation 2.0.14, uGUI
  (legacy `Text` + `LegacyRuntime.ttf`, no TextMeshPro).
- **Netcode for GameObjects 2.13.3** (only as transport / connection: no NetworkObjects, one unnamed message per
  `NetMsg`) + **Multiplayer Services 2.3.3** (Unity Relay + anonymous Authentication). The project is linked to Unity
  Cloud (project id 675cce7c-…) and **Relay already works** (tested 2026-10-12, codes like DKM7BT).
- **Blender 5.2**: `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`.
- PS4 controller (DualShock 4) is the main input; keyboard/mouse also bound.
- Windows 11, shells: PowerShell 5.1 and Git Bash.

## Where things are
| Path | What |
|---|---|
| `C:\Users\Usuario\animal_island1` | Unity project |
| `Assets/Scenes/MainMenu.unity` | **Start here** (build index 0): 3D title/menu diorama |
| `Assets/Scenes/IslaZombie.unity` | **Isla del Faro** (build index 1) + `IslaZombie_NavMesh.asset` and `IslaZombie_NavMesh_Saltador.asset` |
| `Assets/Scenes/BeachIsland.unity` | The original small test map, **"Original"** in the map selector (build index 3; build order follows the `Data/Maps` asset names) |
| `Assets/Scenes/IslaFaroAlturas.unity` | **"Isla del Faro (alturas)"** (build index 2): Phase 3 copy with heights, from `Assets/Maps/IslaFaroAlturas/IslaFaroAlturas_Layout.txt` |
| `Assets/Scenes/Villajubilada.unity` | **"Fiestas de Villajubilada"** (map 5 in the selector): a village in two acts, see its section |
| `Assets/Scenes/Venecia.unity` | **"La Serenísima"** (map 4 in the selector): Venice + cemetery, see its section |
| `Assets/Data/` | ScriptableObjects — **all balance numbers live here**: `Weapons/` (+ `Ukulele.asset` = `MeleeData`, `LentosPeta.asset` = wall buy), `AnselmoCagon.asset` (`TunnelData`), `Drinks/`, `Cards/`, `Animals/`, `Enemies/` (Jubilado, JubiladoChandal, AbueloCulturista, **AbueloSenderista**), `Maps/` (`MapData` for the map selector), `Economy.asset`, `Materials/` |
| `Assets/Scripts/` | Game code (see Architecture) |
| `Assets/Scripts/Editor/TestMapSetup.cs` | **Tools > Island > Setup Test Map** (`SetupAll`: data assets, prefabs, Original scene, menu) and **Rebuild Main Menu**; shared builders (`BuildDoor`, `BuildSystems`, shop, karaoke, ukulele, `SetBuildScenes`, `SetupMenuMaps`, `EnsureLayers`) |
| `Assets/Maps/IslaZombie/IslaZombie_Layout.txt` | **The Isla del Faro map as an ASCII grid** (zones, walls, props, doors, spawns, machines, llave spots...) — edit this to change the map |
| `Assets/Scripts/Editor/IslaZombieSetup.cs` | **Tools > Island > Setup Isla Zombie** (reads the layout, builds the scene, prop colliders, two NavMeshes, zone-graph check) and **Capture Isla Zombie Map** (PNGs in `Renders/`) |
| `Assets/Scripts/Editor/IslaZombieSetup.Entrances.cs` | Spawn entrances (partial class) + **spawn coverage** check ("safe cells" reached from a single direction) |
| `Assets/Scripts/Editor/IslaZombiePlayTest.cs` | **Tools > Island > Play Test Isla Zombie**: round 1, every door, llave, walks, boss, extraction. EditorPrefs `IslaZombiePlayTest.Log` / `.Status` |
| `Assets/Scripts/Editor/BasePlayTest.cs` | **Tools > Island > Play Test Base**: collision audit + 90 random walks, sprint toggle (virtual gamepad), camera against a wall, ukulele + lunge, floating points, shotgun pays once per shot, spawn entrances (time in sight, side vs camera), night round, run log. EditorPrefs `BasePlayTest.Log` / `.Status`, screenshots `Renders/Base_*.png`. `BasePlayTest.Probe()` = short frame-timing probe |
| `Assets/Scripts/Editor/FeedbackPlayTest.cs` | **Tools > Island > Play Test Feedback** (2026-10-08 pass): FPS counter, box first in Parking, Lentos-peta (buy, shell-by-shell reload, shot stops it, ammo), headshot 65, tree trunks solid where visible, spawn colour, night cap 5, Anselmo cagón (50 % split, shortcuts, one per entrance, stress 14). EditorPrefs `FeedbackPlayTest.Log` / `.Status`, screenshots `Renders/Feedback_*.png` |
| `Assets/Scripts/Editor/CoopPlayTest.cs` | **Tools > Island > Play Test Coop**: split-screen co-op (see Status). Starts in the MainMenu scene with virtual pads. EditorPrefs `CoopPlayTest.Log` / `.Status`, screenshots `Renders/Coop_*.png` |
| `Assets/Scripts/Net/` | **Online**: `Net` (flags + `NetMsg`), `NetSession` (room, Relay / direct IP, flow, messaging), `NetGame` (match sync), `NetPlayer`, `NetEnemy`, `NetInterp`, `NetBuf`, `NetTestBot` (only with `-netbot`) |
| `Assets/Scripts/UI/MainMenu.Online.cs` | "JUGAR ONLINE" screen (create / join by code / IP) and the online room (partial of `MainMenu`) |
| `Assets/Scripts/Editor/OnlinePlayTest.cs` | **Tools > Island > Build Online Test Client** (`BuildClient()`, Windows player in `Builds/NetTest`, ~15 s incremental) and **Play Test Online** (`RunDirect()`: editor = host, the built exe = client J2 by 127.0.0.1) / **(Relay)** (`RunRelay()`: same through a real Relay code) / **(alturas)** (`RunDirectAlturas()`: RunDirect on the heights map). EditorPrefs `OnlinePlayTest.Log` / `.Status`, screenshots `Renders/Online_*.png` (host and client), client log `Builds/NetTest/client.log` |
| `Assets/Scripts/Editor/MapSelectorPlayTest.cs` | Loads each map from the menu selector (`RunOriginal()` …): scene loads, player spawns, round 1, 0 errors |
| `Assets/Maps/IslaZombie/Models/` | Low-poly props (IZ_Faro, IZ_Coche, IZ_Autobus, IZ_Puesto, IZ_Tienda, IZ_Caseta, IZ_Contenedor, IZ_Barco, IZ_Avioneta, IZ_Grada) made by `Tools/Blender/make_map_props.py` (blend: `Documents/BeachAnimals/IslaZombie/IslaZombie_Props.blend`) |
| `Assets/Maps/IslaZombie/IslaZombie_Map.png`, `Signs/` | Generated: top-down map (pause menu) and one "ESTÁS AQUÍ" picture per door |
| `Assets/Resources/Shaders/WorldText.shader` | Depth-tested text for `WorldLabel` |
| `Assets/Prefabs/` | `Players/Player_<Animal>`, `Enemy_OldMan`, `Boss_DonAnselmo`, `GoldenDisc`, `IslandEnvironment` |
| `Assets/Characters/<Name>/` | `<Name>.fbx` (rig + clips), `<Name>_Player.controller`; OldMan also `OldMan_Enemy.controller` |
| `Assets/Weapons/<Name>/` | Pistol, Shotgun, Sniper, SMG FBX (low-poly, made by script). The ukulele is built from primitives in `TestMapSetup.BuildUkulele` |
| `Assets/BeachIsland/` | Island models (107 FBX), textures, shared materials, `Island_Layout.json`, `Editor/IslandBuilder.cs`, `Editor/BeachIslandImportSettings.cs` |
| `Renders/` (project root) | Map captures and play-test screenshots |
| `<persistentDataPath>/runs/` = `AppData/LocalLow/DefaultCompany/animal_island1/runs/` | **Run logs** (`run_<date>.txt`) for balancing |
| `Tools/Blender/` | Blender Python scripts used to make/animate/export assets (see its README) |
| `Assets/Scripts/Editor/BuildGame.cs` | **Tools > Island > Build Windows (.zip)** (and `-executeMethod BuildGame.CommandLine`): Windows 64-bit player of the enabled build scenes → `Builds/AnimalIsland_<date>_<time>/AnimalIsland.exe` + `LEEME.txt` (Spanish instructions) + `Builds/AnimalIsland_<date>_<time>.zip` (skips Unity's DoNotShip / DontShip folders). Doesn't touch project settings (window title stays `animal_island1`). `BuildGame.Status/Result` in EditorPrefs. First build ~4 min, zip ~80 MB |
| `Tools/Build-Game.ps1` | Same from PowerShell with **Unity closed** (detects `Temp/UnityLockfile` and tells you to use the menu): `powershell -ExecutionPolicy Bypass -File Tools\Build-Game.ps1` (log `Builds/build.log`). Saved with UTF-8 BOM (Spanish text in PS 5.1) |
| `Builds/` (project root) | Exported games (not in Assets) |
| `Tools/Backup-Project.ps1` | Backup to a drive: `powershell -ExecutionPolicy Bypass -File Tools\Backup-Project.ps1 -Destination F:\` |
| `C:\Users\Usuario\Documents\BeachAnimals\` | Blender sources: `Island/Island.blend` (user's original — don't touch), `Island/Island_TestMap.blend`, `<Animal>/<Animal>.blend` (+ backups), `Weapons/*.blend` |

Characters: RedPanda, Panda, Frog, Crocodile, Toucan, OldMan. All share the same humanoid rig
(Hips…Spine…RightHand, Left/Right UpperLeg…Toes; RedPanda/Crocodile/Toucan have Tail bones, RedPanda ears),
face −Y in Blender, 30 fps. Clips per animal: `_Idle _Walk _Run _Jump _Death`; OldMan: `_Idle _Walk _Run _Attack _Death`.
Blender→Unity axis: Unity (x, y, z) = Blender (−x, z, −y).

## Architecture (Assets/Scripts)
- **Core:** `GameManager` (spawns the players: solo or the `Party` (2–4, own devices / camera / HUD, see Status),
  shared points, run stats, states Playing / ChoosingCard / Paused /
  GameOver / Victory / Debug, score; `AddPoints(raw, PointSource)`, `TrySpend(cost, what)`, `PointsDelta` event),
  `GameSettings` (PlayerPrefs: difficulty, animal, map, sensitivity, invert Y, FOV, **SprintToggle**, **ShowFps**,
  **AnselmoCagon** = game mode), `Stats.cs`
  (`Stat` enum + `RunModifiers`: difficulty + cards), `EconomyData` (`Data/Economy.asset`: start 500, hit 10, kill 60,
  headshot **65**, melee 130, ability 60, boss 2000) + `PointSource`, `RunLog` (per round: duration, kills, conchas by
  source, purchases; written at death / victory / restart / leaving), `MapData`.
- **Player:** `PlayerController` (CharacterController movement, strafe while aiming, **sprint latch**: one L3 press
  keeps sprinting while the stick is pushed, ends on stick release / aim / shot / swing / second press; "hold" option;
  Shift always holds; `Dash` for the melee lunge), `ThirdPersonCamera` (over-the-shoulder, aim zoom, recoil, shake;
  never placed past a hit, rises over the shoulder when pushed in, hides the character (shadow only) closer than
  0.95 m, ignores thin colliders), `PlayerStats` (per-player multipliers).
- **Combat:** `PlayerShooter` (2 slots, ammo, reload (or **one shell at a time** when `WeaponData.reloadOneByOne`;
  firing with shells loaded stops it), pellets, penetration, headshots, PaP, recoil, muzzle flash, arm
  aims the gun in LateUpdate; hit points **once per enemy per shot**), `PlayerMelee` (ukulele on the back, swung
  procedurally in the right hand after the Animator, lunge ≤3.5 m in front of the camera, 350 dmg = one-hit kill up
  to round 3, `MeleeData`), `PlayerHealth` (regen, Tinto self-revive (solo), co-op last stand / revive / bleed-out /
  respawn, hit flinch, `GodMode`), `EnemyHealth`,
  `EnemyAI` (NavMesh chase, cane attack, variants, stun/knockback, flinch, `EmergeFrom(SpawnEntrance)`, night
  variant: `UpdateHop` rises onto props, `ThrowChancla`; **Anselmo cagón**: `EnableTunnels`, `PlanTunnel` (walk to entrance + `travelTime` + walk out
  vs. direct path, nearest `candidates` entrances each side, ≥ `minSaving` m), `GoThrough` (climb in, hidden and
  untouchable, out of the free entrance nearest the player in another open zone)), `EnemyVariant` (+ `jumper`,
  `throwsChanclas`, throw numbers, `maxAliveAtOnce`), `ChanclaProjectile`, `NavAgents` (agent type **"Saltador"**; `NavAgents.Sample / Filter` query ONE agent
  type — **always use these**: plain `NavMesh.SamplePosition(.., AllAreas)` may snap onto the Saltador mesh under a
  car), `HitZone` (head collider).
- **Rounds:** `RoundManager` (HP 150+100·(r−1) until r9, then ×1.1/round; count 6+2.2r+0.08r²; max 24 alive;
  spawn point weighted by **walking distance** (≤28 m ×1, ≤50 m ×0.35, more ×0.06) × **angle to the camera** (front
  ×0.45, close front ×0.2, sides ×1.2, back ×1.6) × recently-used ×0.3; night round every 5 = only `nightVariant`,
  ×0.6 count; `DebugEndRound`, `ForceNextNight`, `EnemySpawned` event), `SpawnEntrance` (Cabina / Grieta /
  Escalerilla / Objeto: door rattles → opens → walk out → climb the barrier → land on the NavMesh; per-entrance
  walk/climb times; `All` registry, `Zone`, `Enter()` for tunnellers, `Busy` = one old man at a time, `Overlaps`
  counter for tests), `TunnelData`, `DayNightCycle` (sun, ambient, skybox exposure (copied material), fog, camera torch, lighthouse
  beam; dawn drops a Nevera de munición).
- **Zones:** `Zone` (children `SpawnPoints`, `BoxSpots`), `ZoneManager`, `ZoneDoor` (paid barrier with carving
  NavMeshObstacle → opens zone(s) → card offer; `DebugOpen`), `MasterKey` (Llave Maestra).
- **Interaction:** `Interactable` base (`Pay(cost)` logs what was bought) + `PlayerInteractor` (□ uses the closest
  interactable; otherwise reload / drop carried item).
- **Shop:** `MysteryBox` (`firstZones`), `WallWeapon` (wall buy: `WeaponData.wallCost` / `wallAmmoCost`, ×2 ammo
  if PaP'd), `DrinkMachine` / `DrinkData` / `PlayerDrinks`, `PackAPunch`.
- **Cards:** `CardData`, `CardManager` (offers 3 untaken cards to the door opener, pauses the game, reads only the
  chooser's devices incl. D-pad / arrow keys).
- **Co-op:** `Party`, `PlayerSlot`, `CoopData` (`Data/Coop.asset`), `ReviveTarget`, `PlayerHUD`.
- **Animals:** `AnimalData`, `PlayerAbility` (RedFury, BearHug, SuperJump, Armor, EagleEye).
- **EasterEgg:** `EasterEgg`, `DiscPickup`, `Karaoke`, `Carryable`, `PlayerCarry`, `ExtractionPoint`.
- **Boss:** `BossEncounter` (opening the Muelle pauses rounds and spawns the boss), `BossAI`.
- **PowerUps:** `PowerUpManager` (only "Nevera de munición" = max ammo for now).
- **UI:** `UIKit` (uGUI from code), `GameHUD` (everything in-game incl. cards / pause / end screens, pause-menu map,
  night label, **floating points** next to the counter: white hit, yellow kill / melee, orange headshot, red spend,
  fast streams merge), `DebugMenu` (**F1 / Share**: +5000, god mode, end round, next round night, see colliders
  (`ColliderViewer`), open all doors), `MainMenu` (title → menu (start, **split-screen lobby**, animal, difficulty, **mode**) → **map selector**), `SettingsPanel`
  (sensitivity, FOV, invert Y, sprint mode, **FPS counter**), `FpsCounter` (DontDestroyOnLoad overlay, top centre), `WorldLabel`, `MapInfo`.

- **Online (Net/):** host-authoritative world, owner-authoritative players. `Net.Authority` = offline or host (runs
  the world); `Net.IsClient` = pure client; hooks are `NetGame.H?.X()` (host broadcasts) / `NetGame.C` (client sends).
  - `NetSession` (DontDestroyOnLoad): `HostRelay()` / `HostDirect()` / `Join(code or ip[:port])`, connection approval
    (protocol `Version` string — **bump it when messages change**; full room 4; no joining mid-match), `Roster`
    (index J1–J4, animal), host settings (difficulty / mode / map), `StartMatch` → everyone loads → `Loaded` → `Go`
    (rounds wait for it: `RoundManager.WaitingForPlayers`), `RestartMatch`, `BackToLobby`, `Leave`, host leaving =
    clients back to the menu with `LastEndReason`. Messages: `NetBuf` / `NetRead` payloads, `ToHost / ToAll / ToPlayer`
    (host relays client messages), handlers via `NetSession.On(NetMsg, ...)`.
  - `NetGame` (added by `GameManager.Start` online, before the players): scene objects matched by hierarchy path
    (interactables, entrances, box spots); enemies get `EnemyHealth.NetId`; on clients old men / boss are **puppets**
    (`EnemyAI/BossAI.MakePuppet`, `NetEnemy` interpolates 20 Hz snapshots, keeps colliders so they can be shot);
    events: spawn, attack, death, entrance doors, chanclas (visual only), rounds (`RoundManager.NetRoundStarted/
    NetTick`), points (`GameManager.NetSetPoints`), toasts (host's `ShowMessage` is broadcast; `ShowMessageTo` = one
    player), doors (`ZoneDoor.NetOpened`), box (`MysteryBox.WriteNet/ReadNet`), key, records, karaoke, Golden Disc carry,
    coolers, cards (`CardManager.NetOffer/NetChosen`, chooser's machine picks), boss, game end summary, `WorldInit` at Go
    / for late loaders. Client requests: `RequestInteract` (host runs `Interact` with the client's proxy),
    `SendHit` (all player damage goes through `EnemyHits.Apply`: client predicts, host applies + pays), drop carry,
    revive. Host → owner forwards (`NetGame.Fwd`): a proxy's `GiveWeapon / UpgradeCurrent / RefillAmmo /
    RefillAllAmmo / PlayerDrinks.Add / PlayerHealth.TakeDamage / Revive` act on the proxy AND tell the owner.
  - Players: `GameManager.SpawnOnline` spawns everyone; the local one gets the camera / HUD, the others are proxies
    (`PlayerSlot.IsRemote`, actions bound to no device, controller / interactor / ability / drinks disabled,
    `PlayerSlot.LookOf` = remote camera direction). `NetPlayer`: local sends snapshot 20 Hz (pos, yaw, look, aim point,
    anim, hp, reviving), status (downed / dead / ignored), loadout (weapons + drinks), shot / swing fx; proxies apply
    (`PlayerHealth.NetApplyStatus`, `PlayerShooter.NetMirror / PlayShotFx`, `PlayerMelee.PlaySwingFx`).
  - **Adding a gameplay feature now means deciding its online side**: world state only on `Net.Authority`, broadcast
    it from the host, apply it on clients (with `Replay` so they don't toast twice); player-owned stuff on the owner.

**Input** (`Assets/InputSystem_Actions.inputactions`, map "Player"): Move, Look, Jump (X), Sprint (L3 / Shift),
Fire (R2), Aim (L2), Use (□ / F / R), SwitchWeapon (Triangle / Q), Ability (L1 / E), **Melee (R3 / V / middle mouse)**,
Pause (Options / Esc). Debug menu reads F1 / gamepad Select (Share) directly.

**Layers:** 4 Water (water blockers, volley net: block walking, bullets and camera pass), 8 Player, 9 Enemy,
10 Door, 11 Wall, 12 Prop (every prop collider).

## Isla del Faro (files: IslaZombie) — decided with the user, don't re-ask
References: `C:\Users\Usuario\Downloads\ref1.png` (night aerial) and `ref2.jpg` (labelled diorama). Daylight (except
night rounds), all in-game text in Spanish.
**Feel:** packed, chaotic, claustrophobic, lots of bottlenecks inside every zone, you must not see zombies coming
from far away. **Round island** ~70 m across (+ pier), ~2,000 m² walkable, lanes 2–4 m, walls 3.5 m, rocks 6 m,
props mostly 2.4 m tall.
Zones: Chiringuito (W beach, start) · Camping (W centre) · Parking (NW) · Volley (SW) · Templo (centre) · Cueva Secreta (N) ·
Faro (NE, lighthouse) · Lago + Bosque (E centre) · Mercadillo (SE) · Muelle (E, boss + extraction at the pier end).
Graph (7 doors, cost): Chiringuito→Camping 750 · Camping→Parking 1000 (also opens Cueva+Faro) · Camping→Volley 1000 ·
Camping→Templo 5000 (also opens Lago) · Faro↔Lago 1500 · Lago→Mercadillo 1250 · Faro→Muelle 10000 (or the Llave).
- **Two ways from left to right only:** Camping→Templo→Lago, or Parking→Cueva→Faro. Never Camping↔Lago, Volley↔Mercadillo.
- **Parking and Faro touch** (wall) with no way through; the **Cueva Secreta** is a dark roofed **labyrinth** north of
  them, entered from the Parking NE corner and the Faro NW corner. No doors inside it.
- **Templo = small temple in a rock ridge, a big bottleneck**: 2 m corridor and a 3×3-cell hall (Pack-a-Punch); two
  dead-end chambers; you pay 5000 once and walk through.
- Rewards: box spots in most zones, drinks in Camping / Parking / Faro / Lago, PaP in the temple hall, karaoke at the
  Chiringuito, Julio records in Volley / Cueva / Mercadillo.
- **Llave Maestra** (`Zones/MasterKey.cs`): one pickup at a random `L` spot (Volley, Mercadillo, Cueva dead ends); in
  the pocket (hands free), HUD line, drops where the holder dies; opens the `llave` door (Muelle) for free.
- **"ESTÁS AQUÍ" signs** beside every door (top-down map, red arrow, SALIDA at the boat); the same map in the pause
  menu with a red dot for the player.
- **The map is the text file `Assets/Maps/IslaZombie/IslaZombie_Layout.txt`** (1 char = 2 m; legend, [zones],
  [doors], [map]). Edit it by hand and re-run the setup. `group` = zones connected without doors.
- **Collision:** walls / rocks / temple are grid boxes (Wall layer); every prop model gets **one box collider per mesh
  part** (Prop layer, at least 0.5 m tall so NavMesh climb 0.45 and player step 0.3 agree); single meshes >2.6 m
  (crashed plane, bus) get a **footprint** of 0.4 m columns from the ground (nothing passes under a wing); tree trunks
  are 0.3 m capsules; decoration has no colliders and nothing that looks solid (no crates / barrels / logs).
- **Spawn entrances:** every `s` marker becomes a `SpawnEntrance` built into the solid cell next to it, preference
  bus (W) > beach hut (h) > tent / stall / container (o in Camping / Mercadillo / Muelle) > wall (#, a cabin with a
  real doorway: WC on the beach and volley, DUCHAS camping, TAQUILLA parking, ALMACÉN faro, CASETA lago,
  TRASTIENDA mercadillo, LONJA muelle) > rock / temple (crack) > sea (pool ladder). A wall cell used by a cabin is
  replaced by the cabin (still solid). 69 entrances now. **All entrances wear one colour** (`SpawnColor`, portaloo
  green: cabins, cabin doors, fences, ladder rails) so they are never confused with the paid doors (red / blue /
  yellow / gold / purple cloths) or towels.
- **Trees:** the tree models are centred on their canopy; `TrunkCollider` moves each tree so the bark base sits on
  the cell centre and follows the trunk with a chain of capsules (0.2 / 1.2 / 2.2 / 3.0 m), leaning / curving with it.
- Layout markers added 2026-10-08: `G` = wall weapon (floor cell next to a wall; Camping east wall), zone flag
  `firstbox` (Parking, Volley), new `B` in Volley.
- **Two NavMeshes:** default (props block) and **"Saltador"** (agent type id 1479372276, created by the setup in
  `ProjectSettings/NavMeshAreas.asset`; props ignored) for the night old men.
- **Setup checks** (fail loudly): groups sealed with doors closed, each door joins exactly its two groups (doors are
  NavMesh areas 3–9 "Puerta1–7"), every spawn exit / box spot / llave spot reachable inside its zone, route lengths,
  **spawn coverage** per zone. Watch out for props blocking 1-cell lanes (a prop diagonal to another closes it;
  machines are ~1.4 m wide). A spawn marker needs something solid / sea / a prop next to it.
- Last results (2026-10-07): all OK; Templo route 97 m (~19 s sprinting), Cueva route 123 m (~25 s); coverage
  16/519 "safe" cells (single direction), mostly dead ends (pier tip).

## How to work on this project (lessons learned)
- **Making a new map from scratch: read `Docs/GUIA_NUEVO_MAPA.md` first** (design slots, files, step by step, every
  test to run and when, how to make a map fun, the mistakes already made). Villajubilada is the worked example.
- **`Docs/mapas.md` (Spanish) = every map's features, mechanics, secrets, decisions, discarded ideas and proposals.**
  Read it before touching a map's design and keep it updated when a map changes (it is the user-facing design record).
- **After changing the map or `IslaZombieSetup*`:** run `IslaZombieSetup.Setup()` (idempotent: replaces the
  `IslaZombie` root, rebakes both NavMeshes, re-checks, points the menu at the scene, sets build scenes), read
  `IslaZombieSetup.LastReport`, then the play tests.
- **After changing prefab / data-building code:** run `TestMapSetup.SetupAll()` (idempotent; **never overwrites
  existing data assets**, only fills defaults when created; opens the scene first on purpose because opening a scene
  unloads assets).
- **Play tests** (run from a RunCommand, then read the EditorPrefs log from later RunCommands; each takes 1–3 min):
  `BasePlayTest.Run()`, `IslaZombiePlayTest.Run()`, `MapSelectorPlayTest.RunOriginal()`, `FeedbackPlayTest.Run()`,
  `CoopPlayTest.Run()`; online: **after changing runtime code rebuild the client first** (`OnlinePlayTest.BuildClient()`,
  ~15 s) then `OnlinePlayTest.RunDirect()` (~4 min) / `RunRelay()` (needs internet). While a test runs, poll its
  status from Git Bash instead of sleeping: `reg query "HKCU\Software\Unity Technologies\Unity Editor 5.x"` and look for
  `<Test>.Status_h…` (hex of "running" = `72756E6E696E6700`, "done" = `646F6E6500`).
- **Never edit scripts while a play test is running**: Unity recompiles, reloads the domain and the test dies silently
  (statics reset, play mode keeps going). Stop play mode and re-run.
- Test drivers that step an `IEnumerator` by hand must run nested `yield return Wait(..)` themselves (stack of
  enumerators) — otherwise every wait lasts one frame and timing checks fail for no real reason.
- **Blender work:** prefer the CLI in background (`blender.exe -b file.blend --python script.py -- args`) so the
  user's open Blender session is never touched. The Blender MCP `*_for_cli` tools fail (no BLENDER_PATH).
  Don't modify `Island.blend` (user's file) — use `Island_TestMap.blend`. Back up a .blend before editing it.
- **Unity MCP quirks:**
  - After editing scripts Unity recompiles and the MCP answers "Unity not detected" for ~20–60 s. Wait with a Git
    Bash loop until `Library/ScriptAssemblies/Assembly-CSharp(-Editor).dll` is newer than the edited file and a few
    seconds old, then retry. `AssetDatabase.Refresh()` from a RunCommand if Unity doesn't notice the change.
  - RunCommand scripts: class `internal class CommandScript : IRunCommand`; no `System.Reflection`;
    `GetInstanceID()` is obsolete (use `GetEntityId()`); dialogs/user interaction are not allowed.
  - Play mode does not advance while the editor is in the background: test scripts call
    `EditorApplication.QueuePlayerLoopUpdate()` from an `EditorApplication.update` callback and set
    `Application.runInBackground = true`. Store test output in `EditorPrefs` (the console overflows).
  - Simulate the controller with `InputSystem.AddDevice<Gamepad>()` + `QueueStateEvent(pad, GamepadState)`.
  - `Unity_Camera_Capture` fails; to screenshot, render a camera into a RenderTexture and `File.WriteAllBytes` a PNG
    (switch overlay canvases to ScreenSpaceCamera to include the UI).
  - Check `EditorApplication.isPlaying` first: the user may be playing (even idling in the menu) — ask before
    stopping their session.
  - **Enter Play Mode Options are on (no domain reload)**: statics survive entering play mode (reset them with
    `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`) and `[InitializeOnLoad]` constructors don't run again —
    hook `EditorApplication.update` directly before `EnterPlaymode()`.
  - **Carving `NavMeshObstacle`s carve the NavMesh in the editor too**: add door obstacles after any edit-time path
    check (`TestMapSetup.BuildDoor(..., obstacle: false)` + `AddDoorObstacle`).
  - `TextMesh` labels need the WorldText shader (`WorldLabel`) or they show through walls.
  - `System.Diagnostics.Stopwatch` isn't available in RunCommand; use `EditorApplication.timeSinceStartup`.
  - If everything renders whitish/lilac with coloured distance bands, the URP Rendering Debugger is on:
    `DebugManager.instance.Reset()` + `UniversalRenderPipelineDebugDisplaySettings.Instance.Reset()` (view setting only).
  - Map props: Blender script → FBX in `Assets/Maps/<Map>/Models/`; `BeachIslandImportSettings` remaps materials named
    like the island's (`M_Wood`, `M_Canvas`, `M_Rock`...) to the shared textured .mat files; `IZ_*Paint` slots are
    recoloured per instance (`Repaint`). `PlaceInRect` fits any model to a cell rectangle (auto 0/90° turn).
  - Unity null: never use `??` / `?.` on UnityEngine.Object.
- **Editing scripts through Python heredocs:** an unquoted heredoc (`<<EOF`) or `'\n'` inside the Python text turns C#
  `"
"` into a real newline and breaks compilation (happened 4 times: "Newline in constant" in `Logs/Editor.log`).
  Use `<<'EOF'` or the Edit tool, and after every edit check the compile really happened (`Assembly-CSharp(-Editor).dll`
  newer than the script and no `error CS` at the end of `Logs/Editor.log`; or a RunCommand that references the new symbol)
  **before** starting a play test, otherwise `EnterPlaymode` is refused ("All compiler errors have to be fixed") and the
  test stays "running" forever with an empty log. Also run `SetupAll` and the play test in **separate** RunCommands.
- Keep gameplay numbers in `Assets/Data` ScriptableObjects, not hard-coded.
- Verify changes by playing (scripted play tests) before reporting them as done; report test results honestly.
- Keep this file updated after each step.

**Full screen (2026-11-02):** exported builds opened as a 1280×720 window because Unity restores the window mode
from the registry (`HKCU\Software\DefaultCompany\animal_island1`, shared by every build incl. the windowed NetTest
client). `Core/ScreenMode.cs` now forces `GameSettings.Fullscreen` (default on = borderless native) at startup
(skipped in the editor and with `-screen-fullscreen` on the command line); toggle in Ajustes > "Pantalla completa";
window resizable. Verified: `Builds/AnimalIsland_2026-11-02_1312` opens 2560×1440 borderless on the user's monitor.

**Windows export (2026-10-11):** first build OK (`Builds/AnimalIsland_2026-10-11_1353.zip`, 83 MB); the exported
exe was launched and showed the title screen correctly (Player.log: only URP depth-of-field / panini shaders
stripped, unused). The PowerShell path was only tested up to the "Unity is open" check (the editor was open).

## La Serenísima (third map, files: Venecia) — first playable version 2026-11-15 (Fable), fixes from the first real game 2026-11-16 (Opus)
Decided with the user (don't re-ask): Venice + Milan's Monumental cemetery; ~30 % bigger than Isla del Faro; **paid bridges**
are the doors; **acqua alta instead of night rounds**; the boat mission reused with other names (campanas = dar la luz,
vaporetto = barco); gondolas and falling into canals are a trial ("si no, serán barrera"); models 30-50 % more detailed
than the Isla props but not final; barrios delivered already dressed with objects (no graybox phase).
- **Layout is generated:** edit `Tools/Maps/make_venecia_layout.py` (rectangles per barrio, it checks spawns have something
  solid beside them) -> `Assets/Maps/Venecia/Venecia_Layout.txt` -> `IslaZombieSetup.SetupVenecia()` (Tools > Island > Setup La
  Serenisima). 70 x 48 cells, origin cell (34, 24). 8 zones, each its own group: Ferrovia `a` (start, SW), Mercato `r`,
  Cannaregio `g` (maze + high street), Carnevale `n` (stage), Cimitero `i` (island in the basin: ring, terrace level 1, tower
  top level 2 with the Microondas), Piazza `e` (campanile, loggias; campanas E), Arsenale `u` (darsena = water inside, rampart,
  emisora C), Vaporetto `d` (pier: boss + extraction). 9 doors: 1 a-r 750, 2 r-g 1000, 3 a-n 1500 (big arched bridge), 4 r-i 1250,
  5 i-e 1500, 6 e-n 1250, 7 g-u 2000 (big arched bridge), 8 e-u 1500, 9 e-d (mission). Two loops through the island (figure 8).
  A door never touches a ramp: the big bridges have the door between two pillar cells on the west bank, then flat / ramps / top.
- **Theme code:** `MapConfig.theme = "venecia"` + `menu = true`; `IslaZombieSetup.Venecia.cs` (`Ven`, `VenPlace` = model origin
  at a point, facades, monuments, props, quay lips, balustrades on bridge cells, lamps, boats, runtime objects, gondolas).
  Hooks in `BuildGround / BuildSolids / BuildProps / BuildDecor / BuildCoast (skipped) / BuildDoors / PlaceSignBoards (boards on a
  post at bridges) / PlanEntrances + BuildEntrances ('#' = dark doorway in the facade, no cabin)`. Kit: 31 FBX in
  `Assets/Maps/Venecia/Models` from `Tools/Blender/make_venecia_props.py` (blend: `Documents/BeachAnimals/Venecia/Venecia_Props.blend`).
- **New runtime rules** (all local, no new network message; the task list below did bump the protocol):
  - `Zones/CanalRescue`: no invisible coast walls; a player below the water line, or grounded in a zone that is still closed
    (long jump / Frog over a canal), is carried back to the last cell of an open zone he stood on (15 damage for the bath,
    never lethal). Door cells are neutral ('+').
  - `Rounds/AcquaAlta`: every 5 rounds (or `ForceNext`, debug menu toggle) the sea plane and the `Flotantes` rise to +0.45 m;
    players with their feet under water move x0.6 (`PlayerController.ExternalSpeedFactor`), old men don't; a Nevera when it ends.
    This scene's RoundManager has `nightVariant` = null (no senderistas).
  - Carnival masks: `EnemyVariant.maskHits / maskShare / maskPrefab`; `EnemyHealth.Masked` = the top share of the health bar,
    each hit takes exactly one mask segment and is never a headshot (`TakeDamage(.., pierce)` for insta-kill / nuke / NetKill).
    Variants `Data/Enemies/AnselmoBauta` (2 hits, from round 2) and `AnselmoPeste` (3 hits, faster, from round 6), only in this
    scene's `specialVariants`. The plague doctor leaves a gas cloud where he dies (`Combat/GasCloud`, `EnemyVariant.deathCloud*`).
  - `Zones/GondolaStop` (layout marker `N`, 7 stops, `EconomyData.gondolaCost` 250): carries to the next stop round the map
    whose zone is open, along baked canal paths. Online clients ride too (`NetGame.Fwd.Gondola`: the host charges, the owner
    rides). Since 2026-11-16 the routes only sail open water (see the fixes below).
- **Verified 2026-11-15:** setup 0 errors (9 doors join their groups, 71 entrances, 1376 cells walkable without drops, 123 m to
  the extraction; coverage 52/1376 safe cells, warning in Cimitero); `VeneciaPlayTest` (Tools > Island > Play Test La Serenisima)
  26 OK incl. 8 walks over every bridge / ramp with the player's body; `SmokePlayTest.RunVenecia()` 15 OK; regression on Isla
  del Faro: Smoke 15 OK, Feedback 26 OK. Captures `Renders/Venecia_*.png`, `Renders/VeneciaTest_*.png`.
- Later the same day (user: more graves you can't walk through): a wall of tall tombs down the middle of the west and east
  rings of the Cimitero (two 1-cell aisles each), clusters on the terrace, the spiral tower on the top (29,24). Setup 0 errors
  (1362 cells, coverage 58 safe, Cimitero 16), `VeneciaPlayTest` 26 OK again. Street-level captures `Renders/Venecia_Calle_*.png`.
  Cosmetic: a decoration gondola and a taxi overlap beside the pier; a crate of the pier stack floats.
- User (same day): "it is Venice: one zone must be big and wide, another narrow with alleys". **Piazza = the wide one**: one
  open square 38 x 28 m (no inner blocks; campanile, two columns, tables, loggias), it floods whole in the acqua alta.
  **Cannaregio = the narrow one**: no campo, only 1-cell alleys that turn and dead-end (drink machine in a dead end).
  Keep this contrast. Setup 0 errors (75 entrances, 1378 cells, coverage 58 safe), `VeneciaPlayTest` 26 OK.
- **Vaporetto task list** (user, same day; replaces gasolina / radio / emisora on this map, don't re-ask): 5 jobs in different
  barrios, shown top-left with checks (`GameHUD`, solo screen; split-screen shows "Tareas del vaporetto n/5" in the strip):
  1 the 3 Julio records to the karaoke (Ferrovia), 2 ring the bell on top of the campanile (= dar la luz, Piazza), 3 find the
  golden gondolier (one of 3 dead ends, picked by the host since 2026-11-16), 4 dig up Giuseppe in the Cimitero (needs the shovel: stall in the
  Mercato, `EconomyData.shovelCost` 500), 5 find the key. Jobs 1-4 done -> vaporetto on its way, arrives at the end of the next
  round (`missionDefenseRounds`) -> the last door is free but wants the key in the opener's pocket (`BoatMission.DoorNeedsKey`).
  Code: `BoatMission.HasTasks` (a map with `MissionTask` objects) + `Shovel / Giuseppe / Gondolier / TaskList()`,
  `Zones/MissionTask` (Shovel / Grave / Gondolier), `Zones/TowerClimb` (walk into the campanile door -> carried up to the
  belfry; jump off any side, the game has no fall damage; after 20 s up there you are thrown off, old men can't climb),
  the bell is a `PowerSwitch` with `action` "Tocar la campana". Layout markers `S` shovel stall, `F` grave, `O` gondolier
  (`VenBuildTasks`); `E Y T C` are gone from this layout. **Protocol bumped to `animal-island-online-3`, now `-4`** (the Quest message
  carries the three new flags): rebuild the online test client before any online test.
  Verified: `VeneciaPlayTest` 42 OK (list, shovel, grave refused without shovel, gondolier, climb, bell, 16 m jump without damage,
  thrown off, records, key, vaporetto after the round, last door with the key, plus everything from before),
  `SmokePlayTest.RunVenecia` 15 OK, `Phase1PlayTest` 21 OK on Isla del Faro. The karaoke song itself is skipped in that test
  (`EasterEgg.Completed` set by hand). Captures `Renders/Venecia_Tarea_*.png`. Giuseppe and the gondolier are primitive figures.
- Later on 2026-11-15 (Fable, commits `5a97f23`, `480e098`): gas cloud, the boss as the Dux with a golden mask (`Boss/DuxMask`,
  10 hits, share 0.3), gondolas for online clients, sunset light (warm low sun + haze), colonnades in the Piazza, and the
  editor tool `Editor/WallpaperShots.cs` (Open / Animal / OldMan / Render / Close: posed characters, phone-size renders in
  `Renders/Wallpaper_*`; used for the phone wallpapers in `Renders/Wallpapers/` and for quick visual checks).
- **Fixes after the user's first real game (2026-11-16, plan `Docs/VENECIA_FALLOS_TRASPASO.md`, Fable planned, Opus built):**
  - **Design rule (user, all maps, don't re-ask): the old men must always be able to follow the player's route, except the
    gondola and the campanile** (old men don't climb it; its 20 s limit stays). New traversals need their enemy side.
  - A1 floating crates: the top crate of a stack added its height twice (`CrateStack` in `IslaZombieSetup.cs`, all maps).
    Setup check `CheckProps`: no prop collider floats (all maps).
  - A2 door tops: the dark lunette over doors was a full disc overlapping the door (z-fighting). `Builder.half_disc` in the
    Blender script, stone transom; same for round windows and mausoleums.
  - A3 every `x` (low cover) can be jumped: graves rebuilt to 0.9 / 0.85 m, colliders capped at 0.9 m (`CapColliders`, the
    cafe parasol has none), no `x` in 1-cell aisles; setup check (Venice only: on Isla del Faro `x` is something else).
  - A4 gondola: passenger gondola at scale 0.5 (4.7 m), moored along the quay if there is room, else bow out; poles outside
    the hull; routes only through open water (the bridges are solid down to the canal bed, B4 not done), centred in the
    canal, Chaikin-rounded, trimmed where the hull would hit a quay (stops facing each other across a canal); decoration
    boats removed from routes and moorings. The 7 stops form 3 canal networks. Setup sweeps the hull along every route
    (4/298 samples still graze a corner) and `VeneciaPlayTest` samples it during a ride (0 hits).
  - A5 market: stalls are no longer spawn entrances in the Mercato; the spawn at (14,22) moved to (16,22) (a facade doorway);
    the shovel stays at (12,22). NavMesh islands nobody can reach (stall counters, roofs, the tops of tall props) are cut
    with Not Walkable `NavMeshModifierVolume`s (`CutNavIslands`, 168 on this map; **the volume's default `center` is (0,1,0)**,
    set it to zero) and re-baked; setup check "none left".
  - A6 masks: new bauta (wedge jaw, brows, gold filigree, tricorn with a feather), plague doctor (curved beak, brass-ringed
    lenses, wide hat, leather collar) and a Dux mask (gold, corno ducale, gems). `Combat/MaskLook`: the `VN_MaskPaint` slot
    gets ivory / black / red per old man (seed = `EnemyHealth.NetId`, same on every machine); a broken mask flies off in
    4 shards.
  - A7 acqua alta: posts with red tops on every low quay edge (`Balizas`, 2 combined meshes). While it floods the canals can
    be swum: a hidden floor (`SueloNado`, top at -0.25 m, 0.25 under the quays so you walk out) is switched on, baked as
    NavMesh area 14 "Canal" (cost 3, `NavAgents.CanalArea`), which old men only get while it floods (`NavAgents.SwimOpen`,
    `EnemyAI.SetCanalAccess`; `NavAgents.Filter` excludes it otherwise). Players and old men move x0.5 in a canal (x0.6
    wading in the street). When the tide drops: players still in a canal go back to the quay without damage
    (`CanalRescue.ToShore`), old men are warped to dry NavMesh, the Nevera lands on dry ground. Setup checks ignore area 14.
  - A8 closed barrios have a 3.2 m iron fence (`VN_Verja`, group `Vallas/Valla_<zone>`, `Zones/ZoneFence`) on every edge
    with the water; its collider is 6 m (the Frog jumps 4 m) and each straight run also carves the NavMesh (old men ignore
    colliders). Not baked; gone when the zone opens (clients too, through `ZoneDoor.NetOpened`). `CanalRescue` stays as
    the safety net.
  - B1 sun raised to 22 degrees (shorter shadows). B2 golden gondolier at one of 3 dead ends (57,42), (57,3), (57,36) picked by the
    host, sent in the quest state: **protocol `animal-island-online-4`** (rebuild the test client). B3 real models
    `VN_Giuseppe` (skeleton with beret and cane) and `VN_Gondolero` (gold, boater, striped shirt, oar). B5 Venetian spawn
    doorways: flood board (paratia) to climb + half-raised shutter, same spawn colour.
  - Verified 2026-11-16: setup 0 errors (1372 cells walkable, 123 m to the extraction, 168 islands cut, no floating props,
    every `x` jumpable); `VeneciaPlayTest` 56 OK / 0 FAIL (fences up / gone, 4 m jump stopped, swimming, an old man follows
    into the canal and reaches the player, walk out of the canal, the Carnevale fence keeps him in the water, tide down
    without damage, gondola ride with 0 hull hits, 3 gondolier spots); `SmokePlayTest.RunVenecia` 15 OK; Isla del Faro
    rebuilt (same report: 69 entrances, routes 97 / 123 m) + Alturas rebuilt (crates); Smoke Isla 15 OK, Feedback 26 OK,
    Coop 39 OK, Online RunDirect 69 / 0 with protocol 4. Captures `Renders/VeneciaTest_*.png`, `Renders/Wallpaper_Check_*`.
- **Low-poly water (2026-11-16, user: "not realistic, but I don't like it; better, still optimised and low poly"):** the sea
  plane is now one mesh built from the layout (`VenBuildWater`, under `Ground/Sea`, which AcquaAlta still moves): a vertex
  every metre (jittered away from the quays), a strip to the horizon, ~14k vertices / 21k triangles, 1 draw call, no
  depth / opaque texture, no reflections, casts no shadow. Shader `Resources/Shaders/LowPolyWater.shader` (material
  `Data/Materials/VN_Agua.mat`): small vertex waves, flat facet normals from ddx/ddy exaggerated x6, sun glints on some
  facets, foam band + a lapping line along every quay (vertex colour R = distance to the quay / 3 m), and over a street
  (G) the flood is light turquoise and see-through (alpha 0.5), so in the acqua alta you see where the canal is. Isla del
  Faro keeps its old water. Also fixed: the acqua alta Nevera could land on the edge of a closed barrio's quay (now rings
  round the player, open zone, 0.6 m from water). Verified: `VeneciaPlayTest` 56 OK / 0 FAIL, `SmokePlayTest.RunVenecia`
  15 OK; captures `Renders/Agua_*.png`.
- **Arched bridges, one canal network, online + split-screen tests (2026-11-16 night, Opus; user: "finish this map"):**
  - B4: the top cells (level >= 1, flat, over a canal) of the two big humped bridges (doors 3 and 7, col 30) are open
    arches (`VenArched` / `VenWet` / `VenBuildArches`): no ground block, a 0.3 m deck slab + a brick barrel vault with a white
    stone face (`VaultMesh`, no collider). The water mesh, the swim floor and the gondola routes treat them as water;
    `CanalRescue` ignores the zone check while swimming below y -0.1 (under a bridge of a closed barrio). Small level-0
    bridges stay solid (0.8 m over the water: no room).
  - Gondolas: with the two arches the 7 stops are ONE canal network (before: 3). Routes: Dijkstra over wet cells (a cell
    beside a quay costs more), pulled taut (straight to the farthest point the hull reaches with a 0.65 m margin), corners
    rounded only where the hull fits (`RoundCorners`), then a point every metre (`Densify`). Some routes go round half the
    map (up to ~250 m): `GondolaStop.maxRide` 10 s, long routes go faster. The passenger ducks into the hull when a bridge
    is low over him (`Room` raycasts, the acqua alta lifts the boat). Setup sweep: 4/6528 samples graze one tight T
    junction at (21, 31) (a 4.7 m hull turning out of a 4 m canal); the play test ride had 0/1536 hits. The sweep now skips
    the jump between two routes.
  - `OnlinePlayTest.RunDirectVenecia()` (Tools > Island > Play Test Online (La Serenisima), its own script `VeneciaScript`):
    gondolier spot, fences, bauta paint on both machines, client hits break the mask, client opens bridge 1 (fence gone on
    both), shovel / Giuseppe / gondolier / campanile + bell by the client, gondola ride by the client, acqua alta (round 5:
    `ForceNext` is host-only, the client works the tide out from the round number), swim, tide down. **22 OK / 0 FAIL.**
    `NetTestBot` reports `tasks`, `fences`, `tide`, `gondolasBusy`, `m<id>` (mask paint).
  - `CoopPlayTest.RunVenecia()` (3 players from the lobby on this map + J2 rides a gondola while J1 moves): **42 OK / 0 FAIL**,
    193 FPS on the test machine, `Renders/Coop_venecia_*.png`. The spare quarter / pause map title says "MAPA DE LA
    SERENÍSIMA" here.
  - Regression after these changes: `VeneciaPlayTest` 56 OK, Smoke Venecia 15, Smoke Isla 15, Online RunDirect 69 / 0
    (client rebuilt), Coop (Isla) 39 OK. `MainMenu.unity` was not modified by any of these runs. Captures `Renders/Arco_*`.
- **Flicker fix (2026-11-16, user: "faces overlap and flicker when you move", z-fighting):** two faces in one plane.
  Roofs: touching houses of the same height had their 25 cm eaves (`Alero`) in one plane (16 pairs) -> each eave is
  raised 0.006 m x (house index % 25), 0 pairs now. Corners: the cornice / plinth / string course of two strips met in
  one plane -> east / west facing strips are 1 % taller. Models (`make_venecia_props.py`): window arch / gothic top 2 cm
  behind the frame face, sill underside below the frame's, door lunette 2.5 cm in front of the arch, balcony windows start
  inside the slab, barred-window and cross bars of different depths, Arsenale corner merlons not doubled, balustrade
  base / rail end inside the posts, gondola / water taxi decks, Giuseppe's ribs, parasol pole. A mesh check (same normal,
  same plane within 1.5 mm, overlapping triangles) went from 273 to 43 pairs, and the 43 left are faces resting on or
  inside another piece (never seen). Rule for new props: no two faces of different pieces in one plane, >= 1.5 cm apart.
  Verified: `VeneciaPlayTest` 56 OK, Smoke Venecia 15 OK.
- **Not done / not verified:** small bridges are still solid, no balance pass (mask chances, tide, gondola price are
  first guesses), what rewrote
  `MainMenu.unity` on 2026-11-15 is still unknown (it did not happen again in these runs).

## Fiestas de Villajubilada (fourth map, files: Villajubilada) — first playable version 2026-11-16 (Fable), NOT committed
Decided with the user (don't re-ask): an inland village during its fiestas, in **two acts**.
- **Act 1 (sunset):** La Parada (start) + two small zones you pay for (El Bar 750, Los Corrales 750). The way out, the
  **Portón del encierro** (`Q`), is shut. A mission item (the lighter) lies in whichever paid zone you open **second**.
- **Round 5, fixed: the first encierro.** Rocket, the Portón opens by itself (never paid), night falls for good, the
  Calle Mayor opens, you run up the Calle del Encierro ahead of the bulls while old ladies throw slow flowerpots from the
  windows. The Portón shuts behind the herd: no way back, the start zones close, whoever stayed is dragged through.
- **Act 2 (night):** a ring of 4 m streets round the Plaza Mayor (the wide one), Barrio de las Cuestas (the narrow one, two
  levels), Calle de la Iglesia, Recinto ferial, Las Eras (boss + tractor). **Every zone has its own fuse box**: it lights that
  zone (garlands of coloured bulbs coming on one after another, a few real lights, some ambient) and its machines only work
  with it. Encierro every 5 rounds: only through open zones (ring closed = laps, else out and back along each open arm).
- Bull kills pay nothing; **traca kills pay** like a normal kill. Size and first prices accepted until humans play it.
- **Files:** layout from `Tools/Maps/make_villajubilada_layout.py` (58 x 60 cells, origin cell (28, 30), 9 zones, 9 doors, a
  `[route]` line = the encierro in cell corners; `--check` writes nothing) -> `IslaZombieSetup.SetupVillajubilada()` (Tools >
  Island > Setup Villajubilada). Theme `"pueblo"` in `IslaZombieSetup.Pueblo.cs`; `Urb` = Venecia or pueblo (shared facade /
  doorway / low-cover code). Kit: 24 FBX in `Assets/Maps/Villajubilada/Models` from `Tools/Blender/make_villajubilada_props.py`
  (it reuses the Builder of the Venice script). New layout characters: `%` traca post, `Q` Portón, `&` lighter spot, `V` the
  cattle lorry. Zone letters must not be legend characters (`o`, `t`, `q`, `R`...): the Cuestas are `u`, Las Eras `l`.
- **Runtime:** `Rounds/Encierro` + `EncierroData` (`Data/Encierro.asset`) + `EncierroGate`, `Combat/MacetaProjectile`,
  `Zones/FiestaLights`, `Zones/TracaTrap` (`EconomyData.traca*`), `PowerSwitch.zone`, `BoatMission` flavor `"pueblo"`
  (`ZoneLit / PowerOnFor(zone)`; Shovel = lighter, Giuseppe = chupinazo from the town hall balcony, Gondolier = golden
  cabezudo), `MissionTask.SetFree`, `DayNightCycle.FallForGood / AddGlow`, `Zone.Close()`, variants `AbueloBombo`
  (`auraRadius`: old men near him walk x1.3) and `AbuelaPetardos` (`throwCloud*`: her throw leaves a burning patch), the box is
  "La Tómbola" here. The encierro is worked out by every machine from the round and the open zones (no message);
  **protocol bumped to `animal-island-online-5`** (lit zones in the quest state, `NetMsg.Trap`): rebuild the online client.
- **Second pass the same day** (user: "do everything that needs no feedback from me; sound later"): bulls with legs that
  gallop (`VJ_Toro_Patas`, two pairs per bull) and faint dust; lit zones also light about half their house windows
  (`FiestaLights.SetWindows`, 259 fronts) and get up to 12 small lights each (the PC renderer is Forward+); more stalls in the
  ferial; the boss comes as the pregonero (`Boss/PregoneroLook`: red neckerchief and sash, rough); later-evening sun for act 1;
  9 new lines in `QuickChecks`; body walks in the play test. **Online: the host now announces every encierro
  (`NetMsg.Encierro`)** because a forced one never reached the client (found by the online test); protocol is still 5.
- **Verified 2026-11-16 (final code, client rebuilt):** setup 0 errors (68 entrances, 998 cells, 134 m to the extraction;
  warnings: 12 safe cells in Cuestas / Eras, 3 single-exit zones). `VillajubiladaPlayTest` **52 OK / 0** (incl. 8 walks with the
  player's body over every ramp, the doorways and the alleys). `CoopPlayTest.RunVillajubilada()` **18 OK** (3 players: a standing
  and a downed player left in La Parada are both dragged through, the downed one alive and revivable).
  `OnlinePlayTest.RunDirectVillajubilada()` **16 OK** (client picks the lighter, switches a fuse box, sees the rocket, is dragged
  by its own herd, night on the client, client lights a traca). Regression: QuickChecks 0 failures, Smoke Isla del Faro 15,
  Smoke Venecia 15, `VeneciaPlayTest` 56, `Phase1PlayTest` 21, `BasePlayTest` 28, `FeedbackPlayTest` 26, `IslaZombiePlayTest` 13,
  `CoopPlayTest` 39, `OnlinePlayTest.RunDirect` 69 / 0 (a first run had 1 intermittent FAIL: the dawn cooler did not refill the
  client; fine on the rerun). Captures `Renders/Villajubilada_*.png`, `Renders/VillajubiladaTest_*.png`,
  `Renders/Coop_villajubilada_split3.png`, `Renders/Online_villajubilada_*.png`.
- **Not done / not verified:** audio (the user said later); humans have not played it, so the balance (bull speed 6.5 / 9,
  damage 60, traca 750, four rounds shut in act 1) is a first guess; `RunRelay`, `RunDirectVenecia`, `RunDirectAlturas`,
  `CoopPlayTest.RunVenecia`, Original / Alturas tests not re-run after these changes; no debug-menu entry to force an encierro;
  the pregonero's sash barely shows; the bull's body is still one rigid piece; the online village test covers the first
  encierro, light and traca, not the whole mission; 4 players not tried.
  The earlier plan `Docs/VILLAJUBILADA_TRASPASO.md` describes the first design (no acts): this section wins.

## Hotel Jubiles and Crucero "Reina del Imserso" (maps 5 and 6) — IN PROGRESS since 2026-11-17 (Opus): designed, layouts built without theme
The user asked for both maps "at the same time", designed first. **All decisions so far are in `Docs/mapas.md`
(sections 6 and 7): don't re-ask them.** Short version:
- **Hotel Jubiles** (was "Hotel Gran Imserso"): 70s Benidorm hotel, stormy night, **real stacked floors**. Starts in a
  blackout (torch, sleepwalker old man), the power is two doors from reception; turning it on is the act change (lifts,
  minibars). Event = **bingo night** (first one the round after opening the Salón Dorado, then every 5): animador, free
  card per player, numbered old men, line = power-up, bingo = pot, "reto de la noche" (10 approved) wins the **pulsera
  todo incluido** (free drinks, wearer only). **Pulsera de puertas** hidden in 1 of 3 rooms opens secret passages for its
  wearer; lost when the wearer goes down and hidden again in another room (decided). All The Shining parodies approved;
  Hotel California only as the idea (no lyrics ever). Helicopter on the roof, boss Don Anselmo in a bathrobe.
- **Crucero**: act 1 in the port (start + 2 paid zones, boarding pass in the second one opened); round 4 = horn + 2 min
  countdown, the ship sails; not aboard = game over solo, in co-op only that player is out (back aboard next round).
  Act 2 at sea at night, **real stacked decks**; boarding by launches in rounds 5, 10, 15 (cut the grappling ropes);
  slides; jet skis = transport round the hull; overboard = ladder back. No bingo, no bracelets. Lifeboat crank exit,
  boss the captain.
- Both: lighting interesting but cheap (Android is a future target, no Android work now). Plan: shared tooling first
  (stacked floors in the layout generator + cheap light kit), then each map in its own files (can be parallel sessions).
- **Design done 2026-11-17:**
  - **Specs:** `Docs/PLANTAS_FORMATO.md` (stacked-floor format: one grid per floor, `[floors]` bases in levels, `~` void,
    `=` lift cab, `$` service door, `:` grappling hook, `[lifts]` / `[links]` / `[points]`, slabs / roofs / railings,
    switchback stairs 3x3 alternating every two floors, checks, shared runtime pieces, work order with a `PruebaPlantas`
    test map first), `Docs/HOTEL_JUBILES_DISENO.md`, `Docs/CRUCERO_DISENO.md` (zones, doors, rules with numbers,
    light, Blender kits `HJ_*` / `CR_*`, online = protocol 6 once for both, play-test lists).
  - **Layouts:** `Tools/Maps/make_hotel_layout.py` (5 floors, 8 zones, 1141 cells) and `make_crucero_layout.py`
    (3 floors, 9 zones, 1301 cells), with the shared checks in `Tools/Maps/floors.py`. Both pass `--check`: doors,
    groups sealed across floors through the stairs, headroom, ramps, lifts, links.
- **Stacked floors built and verified 2026-11-17 (later, Opus); status list in `Docs/PLANTAS_FORMATO.md`:**
  - Code: `IslaZombieSetup.Floors.cs` (BuildFloors: per-floor ground / slabs, solids, roofs, railings, lifts, links,
    service doors, floor map PNGs, checks), reader in `IslaZombieSetup.cs` (`Layout.floors / above / below / BaseY`,
    `Multi`), runtime `Zones/Lift`, `LiftButton`, `ServiceDoor`, `FloorLink`. Menu: Tools > Island > Setup Prueba de
    plantas / Setup Hotel Jubiles / Setup Crucero, Play Test Plantas (`PlantasPlayTest.Run()`). NavMesh area 16 "Servicio".
  - **Rules learned:** on floors every tall collider (open-air walls 10 m, spawn cabins, door barriers 5 m + carving
    obstacle) must stop under the slab of the first higher floor with something at that cell (`CeilingY`, `CapDoor`):
    a basement cabin cut the bufé, a ground-floor wall blocked the 2nd-floor balcony. `CutNavIslands` cuts per 2 m cell
    on floors (one roof island spanned the building at a landing's height). No `BuildCoast` on floors (invisible walls
    upstairs). Run each setup in its own RunCommand (two in one gave false "no spawn point on the NavMesh").
    `IslaZombieSetup.LastSafeCells` lists the coverage safe cells (`floor:zone(c,r):n`) for placing spawns.
  - **Verified:** `PlantasPlayTest` 21 OK / 0; Hotel (generic look, no theme yet) 0 errors, 112 m to the extraction,
    coverage 37/1163; Crucero 0 errors, 84 m, coverage 29/1296 (was 252: crew doors added every ~8 cells, pool ladders);
    Isla del Faro rebuilt with the same report, Smoke 15 OK, QuickChecks 0 fails.
  - **Next:** themes (`IslaZombieSetup.Hotel.cs` / `.Crucero.cs`) with the Blender kits `HJ_*` / `CR_*` (two parallel
    sessions made them), light kit, map rules (blackout, bingo, bracelets, sailing, boardings...), play tests, online
    (protocol 6). Floors still missing: fall rescue, per-floor pause map / door signs, area 15, lift-stop spawns.
- Name clash resolved (user, 2026-11-17): the card `Data/Cards/PulseraTodoIncluido.asset` is now titled "Media pensión"
  (asset file and id unchanged, same effect).

## Phase 3 (heights) — DONE (2026-11-15, Opus): tooling + all zone layouts of "Isla del Faro (alturas)", not committed yet
Spec: `Docs/FASE3_FORMATO.md`. Level = 2 m; `[heights]` grid before `[map]` (digits + ramp arrows `^ v < >`, runs of 2-3
cells per level); ledges inside a group are open drops with one-way `NavMeshLink`s; `!` in `[map]` = one-way slide between
groups (only into a zone that is always open when the source is).
- **Code:** `IslaZombieSetup.MapConfig` + `Setup(config)`: `Setup()` = Isla del Faro, `SetupAlturas()` = **"Isla del Faro
  (alturas)"** (`Assets/Maps/IslaFaroAlturas`, scene `IslaFaroAlturas`, in the selector as map 3, still an identical copy:
  the user asked to keep the original untouched), `SetupPrueba()` = test map `Assets/Maps/PruebaAlturas` (scene
  `PruebaAlturas`, not in the build). `World(c, r)` now returns the cell's floor height; `TexBox / Box / Blocker /
  PlaceInRect` place relative to the centre's floor; `Rects` never merges cells of different heights; prop colliders /
  footprints measure from the cell floor. `IslaZombieSetup.Heights.cs`: grid checks, ramps (wedges), kerbs, links
  (areas 12 "Caida" / 13 "Tobogan", both agent types), slides, NavMesh checks (walkable without drops, slide rule,
  `circuito` zone flag, single-exit warning). Runtime: `EnemyAI.TraverseLink` (old men jump links in an arc,
  `autoTraverseOffMeshLink` off), `Zones/OneWaySlide` (local players only, no network message).
- **Tests:** `HeightsPlayTest` (Tools > Island > Play Test Alturas, 20 OK: broken layouts fail, ramp, jump vs super jump,
  drop without damage, old men drop / climb, sealed door, slide down only, bullets, camera), `SmokePlayTest.RunAlturas()`,
  `IslaZombiePlayTest.RunAlturas()`, `IslaZombieSetup.CheckLayoutLines(lines)` (grid checks on text).
- **Verified 2026-11-15:** Isla del Faro rebuilt with the same report as before (69 entrances, 86 points, routes 97 / 123 m,
  coverage 16/519), Smoke 15 OK on both maps, QuickChecks 31 OK, Base 27, Feedback 26, IslaZombie 13 (both maps), Phase1 21,
  Coop 39, Original 3, map selector Original + Alturas OK (0 errors), Online RunDirect 69 / 0 (client rebuilt).
- **Zone layouts (the user said "termina la fase 3"; the header comments of `[heights]` list them):** levels 0/1/2, 9 ramp
  runs, 2 slides, 50+ one-way links.
  - Faro: mirador around the lighthouse (level 1, light switch up there, hut (29,7) became floor); two ramps make a loop
    over two levels. The planned "jump into the lake" breaks the slide rule (Faro can be opened without the Lago), so
    you drop inside the Faro instead.
  - Camping: ring of lanes around a 2x3 tent block (no more checkerboard); south terrace + east strip up to the Templo
    door at level 1, ramp at col 14.
  - Templo = crypt: the corridor starts high at the Camping door and goes down a ramp into the Microondas hall (level 0,
    same as the Lago).
  - Volley: high walk over the sunken court, slide (8,24) to the Chiringuito.
  - Cueva: central room (cols 20-24, rows 3-6) with a ring around a pond under a skylight (roofs are only built over
    floor, so the water has none); labyrinth route unchanged (122 m).
  - Mercadillo: main street rows 25-26 + ring around a stall block; north balcony (level 1) with a slide (26,22) down to
    the Lago (its second way out); high south strip + level-2 lookout (SW corner). "Drop from the Muelle" breaks the
    slide rule, not done.
  - Zones flagged `circuito`: Camping 28 m, Parking 28, Volley 26, Cueva 28, Mercadillo 32 (Templo is a bottleneck by
    design; Lago / Muelle / Chiringuito / Faro have none by the block rule).
  - Single-exit zones: 2 (Chiringuito, Muelle).
- **Ramp sides (`BuildRamps`):** beside lower floor the lowest ramp cell gets a stone kerb ("MuretRampa"), beside higher
  floor the top cell gets a 0.6 m parapet ("PretilRampa"). Without them the NavMesh (climb 0.45) let old men step onto
  a ramp sideways where the player (step 0.3) is stopped. A NavMesh "Not Walkable" `NavMeshModifierVolume` does NOT cut
  the mesh here (tried), use real geometry.
- `Circuit` check now prefers a loop that meets the rule over a longer one that doesn't.
- **Verified 2026-11-15 (final layout):**
  - Setup: 0 errors, 536/536 cells walkable without drops, routes 105 / 122 m, 70 entrances.
  - Coverage: 31/536 safe cells (warning only; original map 16): Volley 8, Faro 7, Templo 4 (crypt hall), Muelle 4.
  - Tests: `HeightsPlayTest` 20 OK; `IslaZombiePlayTest.RunAlturas` 13 OK (whole walk 103 m); Smoke 15 OK on both maps;
    QuickChecks 0 fails; `OnlinePlayTest.RunDirectAlturas()` (new) 69 / 0 with the client rebuilt.
  - Captures in `Renders/Alturas_*.png` (top, persp, Camping, Templo, Templo_rampa, Cueva, Cueva_tragaluz, Mercadillo,
    Faro, Volley).
  - Render cost probe: no difference vs the original map.
  - Not done: real split-screen FPS with 4 players; Base / Feedback / Coop run on the original map only; the user decides
    whether the heights map replaces Isla del Faro.

## Status (2026-11-15, later) — PHASE 2 numbers applied and play-tested (committed `31c558c`)
Written by Fable, tested by Sonnet; work list in `Docs/FASE2_TRASPASO.md`. `IslaZombieSetup.Setup()` and `TestMapSetup.SetupAll()`
re-run, all checks OK (3 wall weapons). **Verified 2026-11-15 with the new numbers:** `BasePlayTest` 27 OK, `FeedbackPlayTest`
26 OK, `IslaZombiePlayTest` 13 OK (needed `GodMode` at step 0: the idle test player died in the round-1 chase with 35 damage),
`Phase1PlayTest` 21 OK, `CoopPlayTest` 39 OK, `OriginalMapPlayTest` OK, `MapSelectorPlayTest.RunOriginal` OK (0 errors),
`OnlinePlayTest.RunDirect` 69 OK / 0 FAIL (client rebuilt first). Not run: `RunRelay`, humans, a 10-round bot per weapon,
real games to tune income (steps 4-5 of the work list; **balance is only theoretical so far**).
- **Fast checks (new):** `Tools > Island > Quick Checks` (`QuickChecks.Run()`, 31 checks on the `.asset` numbers, no Play Mode,
  instant; give every new design-rule number a line there) and `Tools > Island > Play Test Smoke` (`SmokePlayTest`, one Isla del
  Faro session in ~11 s: round 1, hit / kill / headshot pay, door + card, wall buy, no light = no drink, mission skipped, free
  dock, boss health, boss killed, extraction, 0 console errors). **Run these between changes; the long tests before a commit.**
  `TestClock.Fast()/Normal()` (x3 clock for filler waits only, see its header for what never to speed up) is used in
  `IslaZombiePlayTest` (chase wait); `IslaZombiePlayTest.RunSlow()` repeats a failure at x1.
- `Data/Economy.asset`: kill 50, headshot 100, melee kill 100; new `fastHitPoints` 5 for guns faster than `fastFireRate`
  8 shots/s (`EconomyData.HitPointsFor`, used by `EnemyHits.HitPay`; online the host reads the proxy's weapon in hand);
  boss health moved here (`bossHealthInEnemies` 45, `bossMinHealth` 30000, was 25 / 8000 in `BossEncounter`);
  `packAPunchCost` 5000 (was a field of `PackAPunch`).
- Ukulele 150 dmg (one hit only in round 1), lunge 2.5 m. Old men hit for 35 (culturista 50, senderista 25) = 3 hits
  without Piña, 5 with. Chándal health ×0.6. Catalejo upgrade ×2.5.
- Wall weapons on Isla del Faro: layout markers `U` La Chancla 1500 (Parking west wall) and `M` La Thermomix 2500 (Lago
  east wall), ammo half price; `G` stays the Lentos-peta. Door prices unchanged.
- `RunLog.Note`: mission steps and power-ups taken go to the events list.
- `Tools/balance_sheet.py`: theoretical income / TTK per round and boss table straight from the `.asset` files.
- Tests adapted to the data: `FeedbackPlayTest` (Lentos-peta by id, headshot pay from Economy), `OnlinePlayTest`,
  `BasePlayTest` (lunge distance from data).

## Status (2026-11-15) — PHASE 1 DONE (boat mission, light, power-ups), built and play-tested
Written by Fable (2026-11-14), finished and verified by Opus. Background and risks: `Docs/FASE1_TRASPASO.md`.
**The decided design changed** (replaces "Muelle 10000 or the Llave"):
- **Dar la luz** (`Zones/PowerSwitch`): drinks and the Microondas are off until someone does (`BoatMission.PowerOn`).
- **Boat mission** (`Zones/BoatMission`, next to the GameManager): petrol + radio (`Zones/MissionItem`, team-wide
  pickups, never dropped) → call from the `Zones/CallStation` (the Llave Maestra replaces ONE missing piece and is
  spent) → survive until the end of round `current + EconomyData.missionDefenseRounds` (a call in the intermission
  after round N = after round N+1) → the dock door (`ZoneDoor.requiresMission`) opens for free → boss.
  Maps without a CallStation / PowerSwitch keep the old behaviour. `DebugOpen` still opens the dock (tests, debug menu).
- **Power-ups** (`PowerUps/PowerUpManager` + `Data/PowerUps.asset` = `PowerUpData`): Nevera de munición, Siesta
  fulminante (insta-kill, not the boss), 2x1 en conchas (gains only), Paella para todos (nuke, +400, its kills drop
  nothing); caps per round / active. Boss minions pay kills only (`EnemyHealth.PaysHits`, `EconomyData.bossMinionsPayHits`).
- HUD (`GameHUD`): objective line under the health bar (y -200) and active power-ups under the conchas; split-screen puts
  the objective in the middle strip. Debug menu: "Dar la luz y traer el barco". Online: `NetMsg.Quest / Buffs`,
  `PowerUpSpawn` carries the kind, `WorldInit` carries the mission + timers, protocol `animal-island-online-2`.
- Layout markers `E` cuadro eléctrico, `Y` gasolina, `T` radio, `C` emisora (Isla: E/C in the Faro, Y in the Parking,
  T in the temple's south chamber); door flag `llave` = `requiresMission`; the Cueva `L` spot was removed. The setup
  check now includes the mission objects. Original: placed by `TestMapSetup.ClearSpot` (dry, under 3.2 m, clear) in
  Jungla / Playa / Cementerio / Campamento and checked for reachability at build time.
- **Verified 2026-11-15:** new `Phase1PlayTest` (Tools > Island > Play Test Phase 1) all OK (no light = no purchase and
  no charge; dock closed then free; emisora blockers; boat after the round after the call; insta-kill / 2x1 / nuke /
  caps / Nevera / minion pay; boss survives insta-kill; HUD lines, `Renders/Phase1_hud.png`), `IslaZombiePlayTest`
  13 OK (mission with the key instead of the radio, free dock, boss, victory), `OnlinePlayTest.RunDirect` 69 OK / 0 FAIL
  (client gives the light / picks the radio, host the petrol, client calls, both see the boat, client opens the dock;
  a first run had 3 intermittent FAILs: night chanclas with a single senderista and the box roll, both fine on the rerun),
  `FeedbackPlayTest` 26 OK, `CoopPlayTest` 39 OK, `BasePlayTest` 28 OK, `OriginalMapPlayTest` OK, `MapSelectorPlayTest`
  Original OK. Not run: `RunRelay` (needs internet), real humans.
- **Not done (moved on):** "Bingo" power-up; the run log doesn't record mission steps / power-ups yet; boss health is
  Phase 2 (see the plan's target table). Fable is used for planning only; Sonnet / Opus build and test.

## Status (2026-11-14) — PHASE 0 (plan in `Docs/PLAN_PRODUCCION.md`, ideas in `Docs/POSIBLES_MEJORAS.md`)
Design review by Fable (read-only) → plan by phases for Sonnet/Opus. **Phase 0 done on the Original map + run log:**
- **RunLog** now writes map, players, per round: duration, intermission (`pausa`), old-man health, damage taken, falls;
  a per-weapon table (shots / hits / kills) and an events list (CAÍDO / MUERTO / CAÍDA with round, time, x/z).
- **Original map** (`TestMapSetup.BuildGameScene` + `OriginalMap*.cs`; the layout is code, re-run `SetupAll`):
  - Door flanks: `OriginalMapBorders.Flanks/DecorateFlanks` put a fence (the door's own posts + cloth) beside every
    door where the invisible zone border used to run into nothing (the "missing fence" next to the Escalera VIP).
  - `OriginalMapEntrances` (new): free-standing green WC cabins = `SpawnEntrance` Cabina at each spawn point (25 of 30).
    Spawn points / box spots that stood in the sea (y < 0.12) are moved to dry land first (17 spawns, both box spots).
    Cabins that would straddle a zone border (a player could stand on the roof across it) are turned or dropped
    (`PruneAcrossBorders`); `ToPlain` = spawn point stays without cabin. **Still without cabin (design them by hand,
    e.g. crypt doors): Cementerio/Spawn0-3, Muelle/Spawn0.**
  - NavMesh of Original now also bakes the **Prop layer** (trunks etc.), stays at radius 0.35 / climb 0.28, and
    cuts two slanted rock lips (`SinMalla_CliffBlock_A_11/_A_02`) at the stair tops (the player capsule was stopped there
    although the mesh crossed). `FixIslands` moves / un-cabins any spawn or box spot not reachable from the start,
    and the bake repeats (max 3). Door obstacles are added **last** (`obstacle: false` in the build, then `AddDoorObstacle`).
  - Pause-menu map for Original (`MapInfo` + `Assets/Maps/Original/Original_Map.png`, rendered by `RenderOriginalPreviewCore`).
- **Verified 2026-11-14:** `OriginalMapPlayTest` OK (70 border jumps 0 across; new check **36/36 spawn points and box spots
  reachable on the NavMesh from the start with every door open**; 114 walks, 0 blocked), `MapSelectorPlayTest.RunOriginal`
  OK (0 errors), `BasePlayTest` OK on Isla del Faro with the new run log.
- **Known / open:** `OriginalMapBorders.Compute` still reports 1 conflict (Jungla touches Campamento at (-15.8,-0.8) without a
  door); players cannot cross it (0 across in the test) but the cause is unexplained. Original still has the invisible zone
  borders (rocks / fences only where exposed). Cementerio keeps few cabins. Not touched: Isla del Faro, balance, online
  (RunLog changes are local only), `CoopPlayTest`, `IslaZombiePlayTest`, `FeedbackPlayTest`, `OnlinePlayTest` not re-run.

## Status (2026-10-12) — ONLINE CO-OP DONE (Phase 3), tested host + real client through Unity Relay
The user asked for "the easiest way to play with friends each at home without own servers" and then to build it.
Done: menu button **JUGAR ONLINE (2-4)** → create (Relay code, "Copiar código") / join (code field + "Pegar", or an
IP for LAN; "Crear partida por IP" = direct host) → online room (4 slots, everyone ◄ ► their animal, host ▲ ▼
dificultad / modo / isla, START / Enter / ¡EMPEZAR!) → match; pause menu shows the code, "Reiniciar (todos)", "Volver a
la sala (todos)" / client "Abandonar la partida"; end screen: host retries or goes back to the room, client waits.
LEEME.txt of the zip explains online. Final zip: `Builds/AnimalIsland_2026-10-12_1128.zip` (85 MB).
**Verified 2026-10-12:** `OnlinePlayTest.RunRelay()` **66 OK / 0 FAIL** and `RunDirect()` 66 OK / 0 FAIL (room join +
animal change, start when both loaded, snapshots both ways (~0.05 m), shared pot, old men count + positions (≤0.35 m),
client headshot / real pistol shot kill on the host and pay, damage reaches the client, night round + chanclas seen on
the client, dawn cooler for both + refill, client opens a door and picks the card for both, box appears in the same
spot and gives the client its weapon, down / revive both ways (4.4 s), bleed-out 30 s + back next round with the pistol,
Llave Maestra + dock for free, 3 records + karaoke + Golden Disc carried / dropped on hit / extracted (×2), boss for
both, victory with the host's numbers, back to the room, second match, restart, both down = game over for both,
Reintentar, client quitting mid-match). Regression after the online work: `BasePlayTest` 27 OK, `IslaZombiePlayTest`
all OK, `FeedbackPlayTest` 26 OK, `CoopPlayTest` 39 OK, `MapSelectorPlayTest` Original OK (0 errors).
**Not tested by humans yet:** real friends on different internet connections (latency: remote players / old men are
shown ~0.12 s in the past; hits favour the shooter; old men hit remote players by the host's view of them), 3–4
players online (the code is N-player, tests used 2), keyboard text entry of the code by a real user.
Main menu was compacted (row 82, buttons 72 px) to fit the new button.

## Status (2026-10-11) — MULTIPLAYER PHASES 1+2 DONE (local split-screen co-op)
Verified 2026-10-11 (after the user's 2 fixes: lobby difficulty/mode, paused full-screen cards + D-pad):
`CoopPlayTest` **39 OK / 0 FAIL** (lobby with 3 virtual pads, 3-player quadrants + map,
input isolation, shared pot, opener chooses card / 15 s timer, last stand, revive 4.0 s, Max Ammo team, bleed-out
30 s + back next round, extraction waits, all down = game over, Reintentar with 2 (top/bottom) and 4 (quarters)
players, same HUD scale per player); solo regression: `BasePlayTest` 28 OK / 0 FAIL, `IslaZombiePlayTest` 11 OK,
`FeedbackPlayTest` 26 OK, `MapSelectorPlayTest` Original OK (0 errors). Screenshots `Renders/Coop_*.png`
(menu, lobby, split2/3/4, cards, downed, gameover) look right. Selected card = scaled up + bright accent frame
(`Frame<i>` image behind the button); lobby settings box under the slots (`MainMenu.LobbyRow` for tests).
Split-screen layout details: J1's health is under the round only in solo; in split-screen the round + remaining
old men live in the **middle strip** with the toasts (GameHUD `LayoutToast`: shows the round when no toast);
`GameManager.ExpectedPlayerCount` is set before spawning so every PlayerHUD gets the same scale; the FPS counter
sits at the top centre (touches the J1/J2 border in quarters, cosmetic). Lobby hides the menu title.
Test helpers: `MainMenu.ScreenName`, `OpenLobby()`, `Join(devices)`, `LobbyDone()`; `CoopPlayTest.ShotAll` renders
every player camera + the overlay canvases (UI camera into a transparent RT, blended on the CPU: URP clears every
camera, `CameraClearFlags.Nothing` doesn't work).
What was built (2026-10-08):
- `Core/Party.cs` (static local party: members = animal + devices; empty = solo), `Core/CoopData.cs` +
  `Data/Coop.asset` (bleed-out 30 s, revive 4 s / 2 s with Tinto, radius 2, down penalty 5 %, revive health 50 %,
  last-stand mags 2, +50 % old men and +50 % boss HP per extra player, card choice 15 s, extraction gather 10 m),
  `Player/PlayerSlot.cs` (index J1–J4, own cloned `InputActionAsset` with `devices` = that player's pad/keyboard,
  camera rig, HUD, colour, per-player kills/downs/revives; `ActionsFor / CameraOf / RigOf` helpers).
- `GameManager` rewritten: `Players` list (`Player` = J1 for old code/tests), spawns under an inactive holder so
  `PlayerSlot` exists before Awake, clones the camera per player (`ViewRect`: 2 = top/bottom, 3–4 = quadrants),
  pause from any player, `Targetable / Standing / NearestTarget / NearestStanding`, down penalty, all down = game
  over, `RespawnFallen` at each RoundStarted, `RegisterKill(head, by)`, `coop` field wired in `BuildSystems`.
- Player components read input from their slot (`PlayerController`, `PlayerShooter`, `PlayerInteractor`,
  `PlayerAbility`, `PlayerMelee`; camera `ThirdPersonCamera.BindActions`), camera/shake from their slot.
- `PlayerHealth` rewritten: solo unchanged (Tinto self-revive, else Died → GameOver via GameManager); co-op =
  BO last stand (`GoDownCoop`: drinks lost, pistol via `PlayerShooter.BeginLastStand/EndLastStand/
  ResetToStartingWeapon`, `Downed` event, ignored by old men via `CanBeTargeted`), `Revive()`, `BleedOut()`
  (hidden, camera spectates a teammate), `RespawnAt()`.
- `Interaction/ReviveTarget.cs` (added to every player in co-op) + hold-□ revive in `PlayerInteractor`
  (`BeginRevive / UpdateRevive / CancelRevive`, `HoldOverride` for tests).
- `EnemyAI` targets the nearest reachable standing player (re-picked every 0.75 s); `BossAI` nearest + slam hits
  everyone; `RoundManager` `PlayerCountFactor` + spawn weights over all standing players; `DayNightCycle` torch per
  camera; Max Ammo refills the whole team; `MasterKey` drops when the holder goes down; `ExtractionPoint` waits for
  every standing player (`Missing()`); `BossEncounter` scales boss HP.
- `CardManager` rewritten: the door opener (`Offer(player)` from `ZoneDoor`) chooses with their own stick + X/□/R2
  (or mouse); solo pauses as before, co-op doesn't pause (chooser frozen + ignored, timer → random card).
- HUD split: `GameHUD` (shared: round, pot + popups, toasts, boss, cards with highlight/timer, pause map with all
  players, end screen with per-player stats, 3-player map in the empty quarter) + new `UI/PlayerHUD.cs` (per
  player, inside its camera rect: health, weapon, prompt, drinks, ability, carry/key, CAÍDO + bleed-out, revive
  bar, markers on downed teammates, Eagle Eye markers).
- `MainMenu`: new button "PANTALLA DIVIDIDA (2-4)" → lobby (X / Enter joins, ◄ ► animal, O / Backspace leaves,
  START / Enter → map selector); "EMPEZAR PARTIDA" clears the Party (solo). Rows now 88 px apart.
- `DebugMenu` god mode toggles the whole team.
**Not tested by a human yet:** real DualShock pads in split-screen (only virtual pads), keyboard+mouse as one
player of a split-screen party, performance with 4 views on the user's PC (test machine: ~100 FPS with 4 views).
**Phase 3 online:** done 2026-10-12 (see the newer status above).

## Status (2026-10-08)
**Feedback pass done** (user's 10 points): spawn entrances one colour (green); night senderistas max 5 alive; box
first in Parking / Volley; Lentos-peta wall buy in Camping (500); headshot kill 65; FPS counter option; tree trunk
colliders fixed (were ~1 m off the trunk); "Anselmo cagón" mode. `FeedbackPlayTest` 26 OK / 0 FAIL (tunnelers
reached a player 56–74 m away in 11–19 s vs walkers 40 s; 0 entrance overlaps with 14 tunnelers), `BasePlayTest`
27 OK / 0 FAIL (shotgun check now aims along the crosshair line: the over-the-shoulder camera missed with the Frog),
`IslaZombiePlayTest` all OK, `MapSelectorPlayTest` Original OK (0 errors).
**Android port** (asked 2026-10-08, not started): Android Build Support is NOT installed for 6000.6.4f1 (Unity Hub →
Installs → Add modules: Android Build Support + OpenJDK + SDK/NDK). Needs touch controls (or a Bluetooth pad: the DS4
works on Android), URP mobile quality tier, perf pass (shadows, 69 entrances, decoration). PC stays the main target.

## Status (2026-10-07)
**Base pass done** (the user's 9-point list), play-tested with `BasePlayTest` — all OK on the last run:
no invisible walls (0 blocked in 74 random walks; per-part prop colliders + footprints), sprint latch, wall camera,
floating points, ukulele (one-hit kill, +130, lunge 3.4 m), shotgun pays +10 once per shot, spawn entrances
(in sight: Cabina 1.0 s, Escalerilla 1.3 s, Grieta 0.9 s, Objeto 0.9 s; 14 spawns: front 1, sides 9, back 4),
night round (only senderistas on the Saltador NavMesh, hop up to 2 m onto props, chanclas thrown, dark + torch,
dawn + ammo), run log, debug menu. `IslaZombiePlayTest` still all OK (7 doors, llave, walks, boss, Victory) and
`MapSelectorPlayTest` OK for Original (0 errors).

Earlier (2026-10-05): Isla del Faro built from the ASCII layout with low-poly models, textures, decoration,
"ESTÁS AQUÍ" signs, pause-menu map, Llave Maestra; map selector (Isla del Faro / Original). Done before that: main
menu, full run loop to extraction, cards, mystery box, 4 drinks, Pack-a-Punch, 4 weapons, 5 animals + abilities,
enemy variants, boss, Julio Iglesias easter egg, HUD, animations, recoil, muzzle flash, hit reactions.

**Known gaps / placeholders:** no audio at all; economy not tuned (too generous); no climb / throw clip for the old
man (procedural lean + the Attack clip) and no ukulele swing clip (procedural arm); doors (cloth barrier), drink
machines, mystery box and Pack-a-Punch are still primitive cubes with labels; menu backdrop is still the old test
island; no reload animation; walking backwards plays the forward walk; no objective text / waypoint arrow (Isla del
Faro has the signs and the pause-menu map, Original has neither); not verified visually: the pause-menu map UI.

## Next steps (proposed, the user chooses)
1. Economy tuning with the run logs (they now say the mode: clásico / Anselmo cagón).
2. Animations in Blender: old man climb / throw, ukulele swing; audio (doors rattling, ladder, chanclas, everything).
3. Objective text + waypoint arrow.
4. ~~Windows build for friends~~ done (BuildGame / Build-Game.ps1).
5. Classic power-up drops (insta-kill, double points, nuke, carpenter), hit markers.
6. Content design session with the user (cards, drinks, more box weapons).
7. Isla del Faro: apply the playtest survey answers (https://claude.ai/artifact/TWz7XYSCX8671kgYKcipNv, read with
   ArtifactData `get` feedback/respuestas); models for machines / doors; new menu backdrop.
8. ~~Multiplayer online~~ done 2026-10-12. Next for online: feedback from a real session with friends (lag feel,
   3–4 players), maybe a Unity Lobby list instead of codes, split-screen + online mixed.
