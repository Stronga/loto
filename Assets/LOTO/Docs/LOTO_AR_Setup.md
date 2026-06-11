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
- Controller Tracking

No external download is needed. The project already has:

- `com.meta.xr.sdk.all`
- `com.unity.xr.meta-openxr`
- Meta MR Utility Kit package cache assets

This first MR version intentionally does not use room scanning, spatial anchors, tap-to-place, or hand tracking. The generator is placed once from the user's headset pose at runtime.

## Interaction

`LOTOXRControllerRayInput` casts from the Quest controller anchors and sends trigger presses into the existing `LOTORaycastInput` flow. This means the AR scene uses the same `LOTOClickable` and `LOTOSnapObject` actions as the desktop scene.

Expected controller flow:

- Aim controller ray at the highlighted target.
- Press trigger or primary button.
- Existing LOTO action runs.
- Padlock and warning tag still snap through `LOTOSnapObject`.

## MR Generator Placement

`LOTO/Create AR Scene` places the generator content under `LOTO_MR_PlacementRoot` and adds `MR_Placement_Manager` with `LOTOMRPlacementController`.

At startup the controller places `LOTO_MR_PlacementRoot`:

- 1.8 meters in front of the headset
- 0.5 meters to the user's right
- on floor Y = 0
- rotated to face the user

The root does not follow the head after placement. For persistent real-world placement later, add Meta Spatial Anchor or MR Utility Kit anchoring to `LOTO_MR_PlacementRoot` after passthrough and controller interaction are verified.
