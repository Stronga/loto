# LOTO Generator Safety Simulator Setup

## Verified Project State

- Unity project path: `C:\GitHub\loto_unity\loto`
- Unity version: `6000.3.10f1`
- Meta XR All-in-One SDK: `203.0.0`
- Unity Meta OpenXR package: `2.5.0`
- TextMeshPro package is present through package dependencies.
- Generator FBX path: `Assets/FBX_inports/generator_unity_ar_ready.fbx`

## FBX Import

The FBX already has animation import and blendshapes enabled, but its clip list was empty when checked.

In Unity, run:

`LOTO > Configure Generator FBX Import`

This editor menu sets:

- Rig type: Generic
- Import Animation: on
- Import BlendShapes: on
- Clip `Door_Open`: frames `1-90`
- Clip `Generator_Shutdown`: frames `100-165`
- Clip `Cable_Baked_Shutdown_Wiggle_BlendShapes`: frames `100-165`
- Clip `SwitchBox_Door_Unlock_And_Open`: frames `170-230`
- Clip `MainPower_Handle_Toggle`: frames `240-280`
- Loop Time: off
- Loop Pose: off

## Scene Objects

The active setup scene is:

`Assets/Scenes/loto.unity`

To set up that scene automatically, run:

`LOTO > Setup loto Scene`

To generate the first working scene from scratch, run:

`LOTO > Create Training Scene`

If you already have `LOTO_Generator_Training.unity` open with the generator placed, run:

`LOTO > Complete Open Scene Setup`

This creates:

- `Assets/Scenes/LOTO_Generator_Training.unity`
- `Assets/LOTO/Animation/Generator_LOTO.controller`
- Placeholder click targets, padlock, warning tag, UI Toolkit `UIDocument` HUD, warning UI, and basic lighting.

The auto-generated click target positions are only a first pass. After the scene opens, move the invisible target cubes so they sit directly over the real FBX parts.

If you build the scene manually instead, create or rename the main scene to:

`LOTO_Generator_Training.unity`

Recommended hierarchy:

- `LOTO_Manager`
  - `LOTOStateController`
  - `LOTOAnimationController`
  - `LOTOChecklistUI`
  - `LOTOWarningFeedback`
- `Generator_Model`
  - Imported generator FBX instance
- `InteractionTargets`
  - `SwitchBoxClickTarget`
  - `PowerHandleClickTarget`
  - `LockSnapTarget`
  - `TagSnapTarget`
  - `MainDoorClickTarget`
- `Props`
  - `Padlock`
  - `WarningTag`
- `UI`
  - Floating checklist panel
  - Warning panel

## Click Targets

Add invisible cube colliders over hard-to-click parts. Attach `LOTOClickable` and set:

- Switch box door: `OpenSwitchBox`
- Main power handle: `TogglePowerHandle`
- Main generator door: `TryOpenMainDoor`

For padlock and tag placement, attach `LOTOSnapObject` to the prop or click target:

- Padlock: `ApplyLock`
- Warning tag: `ApplyTag`

Assign `snapTarget` to the matching lock/tag snap transform.

The padlock and warning tag are click-to-snap props, not drag objects. Their yellow next-step indicators should be separate sibling objects named `PadlockIndicator` and `WarningTagIndicator`, not the movable prop renderers themselves. If the indicator moves with the padlock, rerun:

`LOTO > Complete Open Scene Setup`

Then confirm `LOTOStateController.lockHighlight` points to `PadlockIndicator` and `LOTOStateController.tagHighlight` points to `WarningTagIndicator`.

## Animation Controller

Assign the imported generator Animator to `LOTOAnimationController.generatorAnimator`.

For the cable wiggle:

- Best setup: assign a separate Animator for the cable to `cableAnimator`.
- If using one Animator, create a second Animator layer for the cable clip and set `cableLayer` to that layer.
- If both shutdown and cable clips are on the same Animator layer, Unity cannot play both separate clips at the same time with `Animator.Play`.

## Action Animation Mapping

- Open switch box: plays `SwitchBox_Door_Unlock_And_Open`.
- Turn power OFF: plays `MainPower_Handle_Toggle`.
- Shutdown wait: starts after the power handle animation finishes, then plays `Generator_Shutdown`; cable wiggle uses `Cable_Baked_Shutdown_Wiggle_BlendShapes` on `cableLayer`.
- Close switch box: plays `SwitchBox_Door_Unlock_And_Open` in reverse.
- Apply lock: animates the `Padlock` prop to `LockSnapTarget`, then completes `Apply lock`.
- Apply warning tag: animates the `WarningTag` prop to `TagSnapTarget`, then completes `Apply warning tag`.
- Open service door: plays `Door_Open`.

`LOTOAnimationController` auto-finds the `Animator` under `Generator_Model` at runtime if the scene reference is missing. Lock/tag snap timing is controlled by `LOTOSnapObject.snapDuration`.

The generator clips are sampled through `LOTOAnimationController` so completed steps hold their final pose. This prevents later clips from closing the switch box or service door, or returning the handle to its original position.

## First Acceptance Test

1. Click switch box target. The switch box opens.
2. Click power handle target. The handle toggles.
3. Generator shutdown starts after the handle animation, and cable wiggle starts if the cable Animator/layer is configured.
4. Click the switch box target again after shutdown. The switch box closes by reversing the open animation.
5. Apply lock after the switch box is closed.
6. Apply warning tag after lock.
7. Open the service door after lockout/tagout completes.
8. Checklist updates after every completed step.
