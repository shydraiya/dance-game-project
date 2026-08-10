Per-song rigged Humanoid prefabs go in this folder.

Requirements:
- Prefab contains an Animator.
- Model import Rig > Animation Type is Humanoid.
- Configure Avatar reports a valid mapping.
- Prefer root scale 1 and a height similar to the existing Dance Avatar.

Set avatarPath in Assets/Data/songData.csv without extension, for example:
PatternTest/Avatars/MyHumanoid

An empty avatarPath keeps the Pattern Test scene defaults.
