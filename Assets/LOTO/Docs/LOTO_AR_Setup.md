# LOTO AR Scene Setup

## Scene

- AR scene: `Assets/Scenes/LOTO_AR.unity`
- Source scene: `Assets/Scenes/loto.unity`
- Builder menu: `LOTO/Create AR Scene`

The AR scene is seeded from `loto.unity` so the current generator placement, snap targets, padlock/tag flow, checklist, and animation setup are preserved.

## Template Choice

Use the installed Meta XR Building Blocks already in this project:

- Camera Rig
- Passthrough
- OVR Controller Tracking
- `LOTOXRControllerRayInput` visible controller ray
- Meta Environment Raycast Manager when available

No external download is needed. The project already has:

- `com.meta.xr.sdk.all`
- `com.unity.xr.meta-openxr`
- MR Utility Kit package cache assets

This MR version intentionally does not use tap-to-place or spatial anchors. The generator is placed once from the user's headset pose at runtime.

The builder uses the controller anchors already inside `OVRCameraRig`. It does not add a separate controller rig. If older `Controllers` or `UnityXRComprehensiveInteractionRig` objects exist from previous setup attempts, `LOTO/Create AR Scene` removes them.

## Interaction

`LOTOXRControllerRayInput` is added to `OVRCameraRig` and uses:

- `RightControllerAnchor`
- `LeftControllerAnchor`
- `LOTORaycastInput` on `CenterEyeAnchor`

For normal click targets, trigger press calls the existing `LOTOClickable.TriggerAction()` through `LOTORaycastInput`.

For `Padlock` and `WarningTag`, trigger press grabs the object along the controller ray, and trigger release calls `LOTOSnapObject.TriggerSnap()`. The existing LOTO state checks still decide whether the lock or tag is allowed to snap.

Expected controller flow:

- Aim controller ray at the highlighted target.
- Press trigger or primary button.
- Existing LOTO action runs.
- Grab and release the padlock/tag when those steps are highlighted.
- Padlock and warning tag still complete through `LOTOSnapObject`.

## Audio

`LOTOAudioController` exposes optional clip slots for generator loop, generator shutdown, and each LOTO action. Missing clips are ignored.

The audio sources are created under the generator model and configured as spatial 3D sources with linear rolloff.

`LOTO/Create AR Scene` assigns the existing clips in `Assets/LOTO/audio`:

- `Generator loop Sound.wav`
- `Generator, Shutting Down .wav`

## MR Generator Placement

`LOTO/Create AR Scene` places the generator content under `LOTO_MR_PlacementRoot` and adds `MR_Placement_Manager` with `LOTOMRPlacementController`.

At startup the controller places `LOTO_MR_PlacementRoot`:

- 3 meters in front of the headset
- 0.5 meters to the user's right
- snapped to the detected floor with Meta Environment Raycast when available
- falling back to physics floor raycast, then `floorY = 0`
- rotated to face the user

The root does not follow the head after placement. For persistent real-world placement later, add Meta Spatial Anchor or MR Utility Kit anchoring to `LOTO_MR_PlacementRoot` after passthrough and controller interaction are verified.
