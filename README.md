# Naval Warfare — 2D Fleet Combat (Unity 6)

A top-down 2D naval combat game in the spirit of World of Warships: fleets of **1 to 30 ships a
side**, a **hybrid control scheme** that lets you either con a single warship yourself or command
the whole fleet as an RTS, **three battlefields** with configurable objectives, four combat ship
classes, and **time compression up to 8x**.

Everything is generated at runtime — no art, audio or prefab assets. Ship sprites, turrets, the
water/terrain shader, particle atlases and every sound effect are synthesised in code.

## Running it

Open `Assets/Scenes/SampleScene.unity` and press Play. The scene contains a single `GameBootstrap`
object that builds the entire game.

| Bootstrap field | Meaning |
|---|---|
| `startMode` | Domination (default), Skirmish, FleetBattle, CaptureAndControl, Escort |
| `seed` | 0 = new map every run, any other value = reproducible map |
| `startWeather` | Clear, Fog, Rain, Storm |
| `skipMenu` | Skip fleet selection and drop straight into deployment |
| `heightmapResolution` | Terrain sampling resolution (512 is a good default) |

**Flow:** fleet selection → deployment → battle → result.

1. **Battle setup.** Slide each side's fleet size (1–30, default 6), choose how the enemy is built
   (**Balanced** / **Custom slots** / **Mirror yours**), pick the battlefield, and choose whether you
   start as **fleet commander** (default) or as the **captain** of one ship.
2. **Deployment.** The sim is paused. The fleet deploys as **three squadrons — LEFT, CENTRE and
   RIGHT** — on your baseline, facing the enemy across the map, with caps A/B/C strung along the
   centre line between you. Drag ships to reposition them inside their squadron's area (drag one
   across to hand it to a neighbouring group), pick a formation, then **START BATTLE**.
3. **Battle.** You start in full RTS fleet command. `Tab` takes the helm of the selected ship and
   `Tab` again hands it back.

The three squadrons come **pre-bound to control groups 1, 2 and 3**, so `1`/`2`/`3` instantly
select your left, centre and right groups.

## Controls

**`Tab` — switch between DIRECT CONTROL and FLEET COMMAND.**

### Direct control (you are the captain)

| Input | Action |
|---|---|
| `W` / `S` | Engine order telegraph — throttle ahead / astern (momentum, not instant) |
| `A` / `D` | Rudder to port / starboard; releasing eases it amidships |
| `Space` | All stop |
| Mouse | Trains the guns; the reticle auto-leads a target it is resting on |
| Left click / hold | Fire the main battery |
| Right click | Torpedo spread along the reticle bearing (arcs are drawn on screen) |
| `1` – `4` | Class consumables (see below) |
| `X` | Submarine dive / surface |
| `E` | Damage control party |

### Fleet command (RTS)

Left click selects, drag box selects many, `Shift`+click adds, double click grabs the whole class.
**`1` / `2` / `3` select the left, centre and right squadrons**; `Ctrl`+`1..9` rebinds a group and
**`Ctrl`+`A` selects the whole fleet**. Right click moves or attacks; `Shift`+right click queues
waypoints. `C` attack-move, `V` patrol, `B` escort, `T` focus fire, `Space` stop, `H` hold,
`R` astern, `G` retreat, `Y` return to port, `Q` smoke, `F5`–`F9` formations, `F10` break.
Clicking a ship in the task force roster selects it (or takes its helm if you are in direct mode).

### Camera, time and system

`WASD`/edge scroll/middle-drag pan (fleet mode only — in direct control WASD is the helm), wheel
zooms out far enough to see the whole map, `F` follows, `` ` `` frames the fleet. **`+` / `-` cycle time compression 1x → 2x → 4x → 8x**,
`P` pauses, or use the buttons on the status bar. `F1` debug draw, `F2` reveal map, `F3` nav grid,
`F4` command reference.

## Ship classes and consumables

| Class | Character | `1` | `2` | `3` | `4` |
|---|---|---|---|---|---|
| **Destroyer** | Fastest, most agile, fragile, quick guns | HE shells | Torpedoes | Smoke screen | Engine boost |
| **Cruiser** | Balanced, strong utility | HE shells | AP shells | Hydroacoustic search | Surveillance radar |
| **Battleship** | Slow, sluggish, huge health and armour, devastating slow guns | HE shells | AP shells | Damage control | Repair party |
| **Submarine** | Stealthy, fragile, dives to hide | Homing torpedoes | Sonar ping | Hydrophone | Dive/surface (`X`) |

Every ship carries an **overhead class symbol** — DD two diamonds, CA diamond with a slash, BB
diamond with two slashes, SS chevron — held at a constant screen size and coloured
cyan for friendly, crimson for hostile, amber for neutral. It stays readable through smoke and
weather and at any zoom, which is how you read a 30-ship battle at a glance. The hull also carries an
elongated team aura tracing its waterline, so you can see which way a contact is pointing.

**HE vs AP** matters: HE trades penetration and raw damage for a much higher fire chance and can
never citadel; AP does full damage, can over-penetrate light hulls, and can land citadel hits on a
broadside target. Angle your armour and shells will shatter or ricochet.

**Submarine ping → homing torpedoes:** a sonar ping marks a target for ~24 seconds. Torpedoes fired
while the mark holds steer onto it; the marked ship also lights up on the plot until the lock decays.

## Battlefields and objectives

| Preset | Terrain | Default objective |
|---|---|---|
| **Ocean Archipelago** | Scattered islands giving cover and torpedo chokepoints | Three-point domination (A/B/C) |
| **Open Sea** | No cover at all — pure gunnery and angling | King of the Hill (one large central point) |
| **Strait Clash** | Two landmasses squeezing a narrow central channel | Two-flag assault (a flag in front of each base) |

Island density, weather and capture radius (500–2000 m) are all adjustable on the setup screen, and
each preset sets its own spawn-to-cap distance so first contact happens early rather than after a
five-minute sail.

Zones are `CircleCollider2D` triggers: ships inside fill the meter (about 40 seconds solo, faster
with more hulls, diminishing returns). **Both fleets inside freezes it — contested.**

**Capture is permanent.** Once the meter completes, the zone is yours and keeps scoring whether or
not anyone stays behind; partial progress the enemy made before breaking off decays away. The only
way to lose a point is for the other side to sail in and complete a capture of their own. That means
taking a cap frees your ships to move on instead of parking on it, and makes the objective a clean
discrete state — `Neutral → Capturing → Captured`, plus `Contested` — rather than a value that
quietly bleeds away.

A held zone pays 1.2 points/second and each kill is worth 12. **Win by** reaching **1000 points**,
sinking the enemy fleet, or leading on points when the **15:00** clock expires.

## Architecture

`Assets/Scripts/` — one namespace (`Naval`). Ships are `MonoBehaviour` hosts carrying a
`Rigidbody2D` and hull collider; every gameplay subsystem is a plain C# class ticked in a fixed
order, so the simulation never depends on Unity's component execution order. Frame work (AI,
navigation, gunnery, visuals) runs in `Update`; forces run in `FixedUpdate`.

```
Core/      NavalTypes, ShipStats, ShipDatabase, GameEvents, ShipRegistry,
           GameManager (fleet setup, domination, time compression), GameBootstrap
World/     WorldMap (height field, islands, ports, zones), MapConfig (presets/layouts),
           NavGrid (draft-aware A*), OceanRenderer, FogOfWarRenderer, WeatherSystem,
           CaptureZone, NavalPort
Ships/     Ship, ShipMovement (Rigidbody2D), ShipNavigation, ShipDamage, ShipDetection,
           ShipWeapons, ShipAbilities, SubmarineSystem, ShipResources, ShipVisual
Detection/ DetectionSystem (contact memory), SmokeScreen
Combat/    ProjectileSystem (shells, torpedoes, homing, depth charges)
AI/        BattleAssessment (shared situational picture), FleetCommander (strategy),
           ShipAI (per-ship FSM, tactics, consumables)
Player/    ControlModeManager, DirectShipController, RTSCamera, SelectionManager,
           CommandSystem, FormationManager
UI/        UIManager (fleet menu, HUD, action bar), Minimap, WorldOverlay, DebugOverlay
FX/        ParticleFX (batched CPU particles), LineDrawer (batched world lines)
Audio/     AudioManager (procedurally synthesised clips)
Util/      NavalMath, SpriteFactory, InputHub
Shaders/   NavalOcean, NavalFog, NavalParticle
```

### Physics

Ships are dynamic `Rigidbody2D` bodies (`gravityScale 0`, mass ≈ length × beam, continuous
collision, a `CapsuleCollider2D` hull). `ShipMovement` drives them with three forces:

* **Thrust** — a velocity servo along the bow clamped to the class's acceleration/deceleration, so
  ships build way and coast when the engines are cut.
* **Keel grip** — a lateral force that kills sideways slip, which is what makes a hull track its bow
  and drift through hard turns instead of sliding like an air-hockey puck.
* **Rudder torque** — derived from `dt`, so the turn response is identical at 1x and 8x. Turn rate
  scales with speed: a stopped ship cannot steer at all.

Hull damping is deliberately near zero — real drag there would cap a battleship below its rated
speed, because its acceleration budget is tiny. Ship-vs-ship collisions are resolved by Physics2D
and produce ramming damage and flooding; **land is not a collider**, it is the height field, and
grounding is resolved analytically against the depth gradient.

Time compression scales `Time.timeScale` and widens `Time.fixedDeltaTime` (clamped at 4x worth) so
8x does not turn into 400 solver ticks a second.

### How the systems feed each other

* **Detection → AI → gunnery.** `DetectionSystem` runs at 8 Hz over signature, weather, smoke, land
  line-of-sight and submarine depth, keeping per-team contacts as *confirmed*, *sonar/unknown* or
  *last known position*. The AI can only shoot at what its team can currently see.
* **Damage → capability → behaviour.** Hits resolve against armour and angle; damage lands on one of
  seven systems chosen by where the shell struck. Engine damage cuts speed, steering damage cuts turn
  rate, sensor damage widens dispersion. Fires and flooding tick damage and raise your signature.
* **Consumables → AI.** The same `ShipAbilities` code path serves the player's action bar and the AI,
  so enemy destroyers really do smoke up under fire, cruisers radar a knife-fighting destroyer,
  battleships heal once their fires are out, and submarines ping before shooting.
* **Fleet commander → squadrons.** See the AI section below.

## The enemy AI

The AI plays by **the same fog of war you do** — it only knows what its team has actually detected —
but it reasons hard about what it does know.

**It knows what game it is playing.** `BattleAssessment` (rebuilt about twice a second per team) reads
the mode, the score, the clock, and the points/second each side is earning, and projects who wins if
nothing changes. That projection drives a **posture**:

| Posture | When | Behaviour |
|---|---|---|
| `LandGrab` | early, points still neutral | take the cheap caps fast |
| `Press` | losing on projection | force fights and flip points |
| `Hold` | winning on projection | garrison what wins the match, trade only when safe |
| `CloseOut` | winning, clock running out | disengage and stall it out |
| `Desperate` | losing, clock running out | everything onto one point |

In Skirmish/Fleet Battle there are no points, so it drops the caps entirely, **masses into one force**
and goes after the weakest isolated enemy group.

**Allocation is a draft, not a fixed split.** Ships are dealt one at a time to whichever zone needs
help most, and a zone's need falls as it is fed — so a flank that is losing keeps pulling
reinforcements automatically, with no special-case rotation code.

**It actually captures points.** The first ships sent to a zone are *cap sitters*: they are required
to be inside the ring and orbit within it rather than drifting off to shoot at something.

**Fire concentration without overkill.** Shooters are committed to a target only until the assigned
firepower covers its remaining hit points; the rest move to the next target. Kill-securing overrides
everything — a target that dies to the next salvo gets shot first.

**Tactical habits** (both fleets, so your uncommanded ships fight well too):

- **Armour angling** — bow-on while reloading, broadside when the salvo is ready
- **Support discipline** — no pushing more than ~300 units past the nearest friendly heavy
- **Terrain cover** — when disengaging, prefers a spot that actually breaks line of sight, tested with
  the same function the detection system uses
- **Regroup, not retreat** — damaged ships fall back behind friends and heal instead of sailing home
- **Local strength gating** — pushes when winning its corner of the fight, opens the range when not
- **Inference** — a destroyer that goes dark inside torpedo range makes ships weave instead of
  steaming predictably, and cruisers will radar a point they believe a hidden destroyer is sitting on

**Difficulty** is set on `GameBootstrap.enemyDifficulty`: `Recruit` (slow reactions, no inference or
cover), `Veteran`, `Elite` (default — fastest reactions, full inference and cover).

Press **F1** in game to see it think: assignment-coloured lines to each ship's station, cap
commitments, local strength bars, and a readout of both commanders' postures and projections.

### Tuning

Balance lives in `Core/ShipDatabase.cs`, one block per class. Consumable cooldowns, durations and
charges are in `Ships/ShipAbilities.cs`. Objective pacing is `GameManager.ZonePointsPerSecond`,
`KillPoints`, `ScoreToWin` and `TimeLimit`. World scale is `GameConfig.WorldSize` (4000 units,
1 unit ≈ 10 m); `WorldMap.DraftToDepth` maps draft onto required depth — a destroyer clears a beach
about 17 units out, a battleship needs roughly 50.

## Deviations from the brief, and why

* **Gun shells are analytic, not colliders.** Shells arc *over* the water, so a collider-based shell
  would wrongly hit ships it should fly past. They fly with a real time of flight to a dispersion
  scattered aim point and resolve on landing — which is also what makes leading a target matter.
  Torpedoes and capture zones do use `Collider2D` triggers, since they run in the water.
* **Health bars are batched line-drawing, not a world-space Canvas per ship.** Same result on screen
  — a bar above every spotted hull, at constant screen size — but 36 ships plus contacts would
  otherwise mean dozens of extra canvases rebuilding every frame.
* **A\* grid pathfinding** rather than NavMesh: the water is a height field with per-draft
  passability, which a baked NavMesh cannot express (a destroyer and a battleship need different
  navigable areas over the same water).

## Verifying without the editor

`dotnet` can type-check the whole game against Unity's assemblies without opening Unity, which is
useful in CI or when the editor is closed:

```bash
dotnet build naval-check.csproj -v q --nologo
```

The project file references `Editor/Data/Managed/UnityEngine/*.dll` plus the package assemblies in
`Library/ScriptAssemblies` and compiles `Assets/Scripts/**/*.cs`.
