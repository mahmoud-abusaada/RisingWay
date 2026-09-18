# The ball's movement — what was wrong, and what it does now

**Status: done and measured in the Editor** (2026-09-17). Not yet seen on a device.

Reported symptoms: *"sometimes the trail shows the ball was not 100% straight, or it jumped a bit
when it is very fast; and when superspeed is on and it turns automatically, the ball doesn't turn
exactly on the centre — sometimes late, sometimes early."*

Both were real, and both are now measured at zero.

---

## How this was measured

`Assets/Scripts/Diagnostics/MovementProbe.cs` (Editor and development builds only) plays the game
with auto-pilot on and records, per physics step: the ball's distance from the centre line of the
lane it is travelling, how far it is above the track surface, how much of the asked speed it
actually achieves, and how evenly the rendered ball moves from frame to frame.

Run it headless, without opening the Editor:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe" -batchmode -projectPath C:\Work\RisingWay -executeMethod MovementProbeRunner.Run -movementProbe -probeFast -probeTurns 40 -logFile probe.log
```

`-probeFast` holds top speed (14) and the bolt time scale (3.5); `-probeFps` simulates a frame
rate; `-probeTurns` ends the run. Both the old and the new movement were measured the same way,
over 40 turns each, so the numbers below are directly comparable.

## What the numbers said

| | old, top speed | new, top speed | old, normal | new, normal |
|---|---|---|---|---|
| Turn error (max) | 0.41 | **0.00** | 0.08 | **0.00** |
| Speed actually achieved | 76% | **100%** | 75% | **100%** |
| Worst single step | 34% | **100%** | 36% | **100%** |
| Steps spent off the surface | 109 of 2036 | **0** | 41 of 3490 | **0** |
| Highest hop | 0.12 (half the ball's radius) | **0.00** | 0.06 | **0.00** |
| Frame-to-frame jitter | 0.20 | **0.04** | 0.07 | **0.02** |

At 30 fps, standing in for a weaker device, the same run gives turn error 0.00 and jitter 0.05
(was 0.13).

## What was actually happening

### 1. The ball was never given a vertical speed that made sense

```csharp
calculatedVelocity = new Vector3(dirX * speed, 0, dirZ * speed);
if (myRB.linearVelocity.magnitude < speed)
    calculatedVelocity.y += myRB.linearVelocity.y * 1.1f;
myRB.linearVelocity = calculatedVelocity;
```

On a ramp this alternates: on one step the vertical speed is thrown away, so the ball is driven
into the slope and the contact solver pushes it back out; on the next it is multiplied by 1.1.
The ramps are 37.8°, so this cost 27-36% of the intended speed on every ramp, and the pushes are
what the eye reads as a jump. The solver's impulses at each new ramp piece measured 20-32
(mass 5 — about 5 m/s of correction per push).

### 2. Ghost collisions at the seams

Every track part carries its own mesh collider. Where two parts meet, the solver can treat the
start edge of the next part as a bump, push the ball up and slow it down. This is a known
property of separate mesh colliders, not a fault in the track: the parts line up to within
0.0002 (measured with `TrackPieceSurvey`).

### 3. Friction was quietly eating speed

The ball had the default physics material, and Unity's maximum angular speed (7 rad/s) meant it
could never spin fast enough to roll without slipping above 3.5 units/s. Sliding friction then
took a constant bite out of every step: the flat parts only ever reached 89-94% of the asked
speed.

### 4. The auto-turn was a distance check running at an arbitrary moment

`PlayerTrigger.checkParts()` turned when the ball came within 0.1 of the part's centre, or when
that distance started growing again. At top speed the ball moves 0.28 per physics step, so the
turn landed up to 0.1 early or 0.28 late, and the script execution order decided whether the new
direction applied to that step at all. Measured: 0.15 off centre on average, 0.42 at worst.

## What it does now

`PlayerMovement.move()` decides where the ball is, every physics step:

- **Horizontal**: exactly `speed` in the current direction, as before.
- **Vertical**: a sphere cast of the ball's own radius finds the height at which the ball rests on
  the track at the position it will reach at the end of this step, and the vertical speed is set
  to land exactly there. Ramp starts, ramp crests and part seams are all just "the surface at the
  next position", so none of them can kick the ball. There must also be track under the ball's
  centre: without that check the ball rode along part edges in the air (fixed after the
  18 September playtest, see `playtest-2026-09-18.md`).
- **Contacts**: while the ball is on the track its contacts are ignored (`Physics.ContactModifyEvent`),
  so the solver cannot push it off the line the code has chosen. Triggers are unaffected, which is
  what pickups, turns and part spawning use.
- **Spin**: set directly, matching the old look (Unity clamped spin to 7 rad/s, and it still does).
  With a frictionless collider and the spin set by hand, friction can no longer slow the ball or
  pull it sideways after a turn.
- **Auto-turns**: `PlayerTrigger` hands the turn part to `PlayerMovement`, which turns inside the
  physics step that reaches the part's centre, ending that step exactly on the new centre line.
  The turn follows the part the ball is standing on (`LandLeft` / `LandRight`) rather than the
  shared `PathMaker.nextDirection`, which every turn consumes: a 150-turn run caught it already
  null at a turn part's centre, so the ball did not turn there and turned 0.16 late instead.
- **Off the track**: no ground under the ball means contacts count again and gravity takes over,
  which is how a missed turn still ends the run.

The rendered ball is also interpolated between physics steps while it is being driven, which is
what most of the frame-to-frame jitter improvement is. Interpolation is switched off again for the
menu and the respawn animation, which move the transform directly.

The camera's turn easing and zoom moved from `FixedUpdate` to `Update` with the same rates, so
they are smooth at any frame rate instead of advancing in physics-step increments, and the camera
now eases towards an absolute yaw so a long run cannot accumulate rounding error.

## The side effect: the game is now genuinely faster

The old ball only ever achieved about 76% of the speed the code asked for. Fixing that makes the
same `speed` value mean what it says, so at identical settings the game moves about 30% more
ground per second: 150 turns at top speed took 114s before and 88s after.

If that turns out to be too fast to enjoy, the honest knob is
`Utility.Constants.START_PLAYER_SPEED` and `TOP_PLAYER_SPEED`; multiplying both by 0.76 reproduces
the old speed over the ground exactly. The difficulty curve in `ScoreManager` is unchanged.

## A regression this introduced, and how it was found

The first version of this work made the ball fall through the track at top speed. Measured over
150 turns per run:

| | ball fell |
|---|---|
| old movement | 0 of 3 runs |
| new movement, first version | 2 of 3 runs |

**The cause was a double turn, not the speed.** `BoltPickUp.OnTriggerEnter` turns the ball when
the bolt sits on a turn part:

```csharp
if (transform.parent.CompareTag("LandLeft") || transform.parent.CompareTag("LandRight"))
    playerMovement.autoTurn();
```

With the old code that was harmless: `autoTurn()` consumed `PathMaker.nextDirection`, so the
auto-pilot turn that was due at the same part found nothing left to do. The new code turns by the
part's own tag, which cannot be consumed - so the ball turned twice, 180 degrees, and drove back
down the track it had just come from, which is destroyed behind it. It then fell, 30 to 100 turns
into a run, and the fall looked like "the track ran out".

Two changes:

- **Any turn cancels a queued automatic turn** (`turnLeft` / `turnRight`), which is the invariant
  the old code got for free from the shared queue.
- **The bolt queues its turn instead of turning on the spot**, so it happens on the part's centre
  like every other automatic turn. The ball reaches that pickup up to 0.7 units before the centre,
  so this also removes an off-centre turn that has always been there.

**Two other changes were made first, on a wrong reading of the evidence, and reverted**: making the
parts drop into place in 0.33s instead of 0.8s, and holding the ball level over a part that had not
landed yet. The falls looked like the ball outrunning the track being built, because after turning
back on itself the ball is over track that is being destroyed while the live path is elsewhere.
Neither change stopped the falls; the double turn did. Measured margin, at top speed: 8-10 placed
parts ahead of the ball, which is about twice the drop time.

Finding it needed a run to be reproduced with a stack trace on every call to `turnLeft` /
`turnRight`: the trail showed one turn logged by the new code and one from `BoltPickUp`.
`MovementProbe -probeVerbose` still logs every automatic turn, without the stack traces.

## Two things learned the hard way

**Switching a Rigidbody to kinematic mid-run fires OnTriggerExit on every trigger it is inside.**
An earlier version of this work held the ball kinematic while on the track. The start part's
`Destroyer` trigger saw that "exit", marked the part as passed, and dropped it from under the ball
0.08s later - the run ended 0.1s after it began, every time, at normal speed only (at top speed
the ball had already left that part). Contact modification avoids the switch entirely.

**The ball's radius is 0.25, not 0.5.** The collider is 0.5 on a prefab scaled by 0.5. The first
version of the probe assumed 0.5 and reported "no hops" for every run, old and new, because every
measured gap came out negative. Check the scale before trusting a measurement.

## Not changed

- Manual turns still turn wherever the player taps. Only the automatic turns (bolt and auto-pilot)
  are snapped to the centre; making manual turns snap would change how the game plays.
- Speeds, time scales, the track layout and the difficulty curve are untouched.
