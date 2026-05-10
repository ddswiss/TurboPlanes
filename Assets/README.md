# SkyBrawl Starter — Setup Steps

## 1. Drop the scripts into your project
Place the four `.cs` files into these folders:

- `Assets/_Project/Scripts/Tuning/PlaneTuningSO.cs`
- `Assets/_Project/Scripts/Flight/FlightModel.cs`
- `Assets/_Project/Scripts/Player/PlaneController.cs`
- `Assets/_Project/Scripts/Camera/ChaseCameraRig.cs`

Wait for Unity to finish compiling. Check the Console for errors. There should be none.

## 2. Create the Input Actions asset

1. In Project window: `Assets/_Project/` → right-click → Create → Input Actions
2. Name it `FlightControls`
3. Double-click to open the editor
4. Click `+` next to "Action Maps" → name it exactly `Flight` (case-sensitive, the script looks for this)
5. Add four Actions inside the Flight map. For each, set Action Type = `Value`, Control Type = `Axis`:
   - **Pitch** → add binding → "1D Axis" composite → Negative: `S [Keyboard]`, Positive: `W [Keyboard]`
     Then add a second binding for gamepad: Path = `Left Stick/Y [Gamepad]`
   - **Yaw** → "1D Axis" → Negative: `Q [Keyboard]`, Positive: `E [Keyboard]`
     Gamepad: `Left Stick/X` *(or use shoulder buttons, your call)*
   - **Roll** → "1D Axis" → Negative: `A [Keyboard]`, Positive: `D [Keyboard]`
     Gamepad: `Right Stick/X`
   - **Throttle** → "1D Axis" → Negative: `Left Shift [Keyboard]`, Positive: `Space [Keyboard]`
     Gamepad: `Right Trigger` minus `Left Trigger` (or use a 1D axis composite)
6. Click **Save Asset** at the top of the editor

## 3. Create your first PlaneTuning asset

1. `Assets/_Project/ScriptableObjects/PlaneTuning/` → right-click → Create → SkyBrawl → Plane Tuning
2. Name it `PlaneTuning_Default`
3. Leave the values at their defaults for now — they're a sane arcade starting point

## 4. Build the test plane

1. In your `FlightSandbox` scene, create empty GameObject → name it `Plane`
2. Add child: 3D Object → Cube. Scale it like a plane: `(2, 0.3, 1.5)`. This is your placeholder.
3. Add another child cube for a wing: Scale `(5, 0.1, 1)`, position `(0, 0, 0)`. Visual reference only.
4. Select the `Plane` parent. Add components:
   - **Rigidbody** (the script will set it kinematic)
   - **PlaneController** (your script)
5. In the PlaneController inspector:
   - Drag `PlaneTuning_Default` into the **Tuning** slot
   - Drag `FlightControls` into the **Input Actions** slot
6. Position the Plane somewhere up in the air, like `(0, 50, 0)`

## 5. Set up the camera

1. Select Main Camera in the scene
2. Add component: **ChaseCameraRig**
3. Drag the `Plane` GameObject into the **Target** slot
4. Leave the offset/lerp values at their defaults

## 6. Build a quick island to fly around

Use ProBuilder (Tools → ProBuilder → ProBuilder Window):
- New Shape → Cube → make it big and flat, like `(200, 5, 200)` for ground
- Add a few cylinders/cubes as obstacles
- Or just use a Unity Terrain — Game Object → 3D Object → Terrain, paint some height with the brush

## 7. Hit Play

You should be flying. WASD to fly, QE to yaw, Shift/Space for throttle.

## When it doesn't feel right (it won't, the first time)

Open `PlaneTuning_Default` in the Inspector **while in Play Mode** and tweak. Changes apply live. Things you'll probably want to adjust first:

- **Plane feels sluggish** → raise `pitchResponsiveness` and `rollResponsiveness` (toward 1)
- **Plane feels twitchy** → lower them (toward 0.2)
- **Falls out of the sky too fast** → lower `gravity` or raise `liftFromSpeed`
- **Doesn't bank-turn enough** → raise `bankTurnFactor` (try 1.5)
- **Drifts sideways too much** → raise `lateralDrag`
- **Too fast / too slow** → adjust `cruiseSpeed`, `maxSpeed`, `minSpeed`

**Important:** Play Mode tuning changes are LOST when you exit Play Mode. Either copy the values back manually, or right-click the component header → Copy Component → exit Play Mode → Paste Component Values.

## What we deliberately skipped (and why)

- **Comic shader / outlines** — flight feel first. Pretty later.
- **Cinemachine** — `ChaseCameraRig` is enough for tuning. Cinemachine is a Day-Two upgrade.
- **Multiplayer** — Fish-Net comes after the singleplayer plane feels good.
- **Collisions** — the Rigidbody is kinematic right now. We'll add collision response when we add terrain that matters.
- **Stalls, boost, stunts** — once cruise/turn/dive feel right, additive features become easy.

When the flight feels good — and *only* then — tell me and we'll move to step two: Cinemachine, comic visuals, and the first multiplayer scaffold.
