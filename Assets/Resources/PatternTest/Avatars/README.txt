Dance avatar selection
======================
OptionManager connects the existing TMP dropdown to AvatarCatalog.
Closing the options window saves SelectedAvatarId (santa / boy / girl) in PlayerPrefs.
Default / invalid selection: santa.

AvatarSelectionController applies the selection to PatternPosePlayer targets in
Pattern Test before Start. It does not replace the webcam-driven avatar.
Song data no longer selects an avatar.

SantaClaus.prefab, BoyAvatar.prefab and GirlAvatar.prefab are runtime models.
BoyAvatar and GirlAvatar are FBX variants preserving the supplied materials and outline.
Keep their Humanoid rigs valid. Model height and floor offset are matched to the
existing dance avatar at scene initialization. Verify facing and limb poses in Play mode.

Manual verification:
- Select each avatar, close options, start a song and check the selected dancer.
- Switch songs and restart the application: selection should persist.
- Check arm/leg poses, stage height, floor contact, materials and camera visibility.
- Verify T-pose gate, pause/resume and the webcam avatar behave as before.
