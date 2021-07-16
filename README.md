# PlayspaceTracking
 
Provides extended tracking data inferred from VRChat's existing tracking data and Unity's built-in VR handling.

## Features
Provides 4 transforms that can be parented to or read from.

### PlayspaceRoot
The root of the player's playspace accounting for position, rotation, and scale.

### HeadRoot
The root position of the player's head accounting for positon, rotation, and scale.

### PlayerRoot
The root of the player accounting for position, rotation, and scale.

### CameraRoot
Depending on if the player is in VR or not, will either be the playspace root, or the head root.

## Setup
1. Install the latest VRCSDK and UdonSharp packages
2. Install the [latest release](https://github.com/MerlinVR/PlayspaceTracking/releases/latest)
3. Drag the **PlayspaceTracker** prefab into your scene
4. Parent things to the objects under *PlayspaceTracker/Roots/*

## Warnings
- Scale cannot be inferred when the player is in Desktop. Do not use this for things that need desktop to be scaled properly. The scale of all transforms in desktop will always be 1.

- Do not use this prefab in maps that are more than a few hundred meters off of origin on Y at any point in the map. This prefab is highly sensitive to numeric precision degradation.

- Use at your own risk, VRC may make some subtle change to the player controller or player tracking that breaks this.
