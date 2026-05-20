# PoseLandmarker Portable Files

새 Unity 프로젝트에 사람 포즈 인식 및 스켈레톤 표시 샘플을 옮기기 위한 파일 묶음입니다.

## 포함 항목

- Packages/com.github.homuler.mediapipe
- Assets/MediaPipeUnity/Samples
- Assets/StreamingAssets/pose_landmarker_full.bytes
- Assets/StreamingAssets/pose_landmarker_lite.bytes
- Assets/StreamingAssets/pose_landmarker_heavy.bytes
- Assets/Plugins/Android/mainTemplate.gradle

## 사용 방법

1. 이 압축 파일을 새 Unity 프로젝트 루트에 풉니다.
2. Unity Editor를 다시 열거나 Assets/Packages를 refresh 합니다.
3. `Assets/MediaPipeUnity/Samples/Scenes/Pose Landmark Detection/Pose Landmark Detection.unity` 씬을 엽니다.
4. 샘플 씬을 그대로 실행하거나, `PoseLandmarkerRunner.cs`를 참고해 대상 프로젝트의 입력/캐릭터 제어 코드와 연결합니다.

## 참고

- PoseLandmarker 기능 자체는 `Packages/com.github.homuler.mediapipe`가 제공합니다.
- 샘플 씬 실행에는 `Assets/MediaPipeUnity/Samples`의 Bootstrap, ImageSource, UI, annotation 구성이 필요합니다.
- 모델 파일은 `Assets/StreamingAssets/pose_landmarker_*.bytes`를 사용합니다.
