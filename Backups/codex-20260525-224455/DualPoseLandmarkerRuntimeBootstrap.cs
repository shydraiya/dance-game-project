using System.Collections;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;

public sealed class DualPoseLandmarkerRuntimeBootstrap : MonoBehaviour
{
  private const string BootstrapName = "Dual Pose Landmarker Runtime Bootstrap";
  private const string DroidCamScreenName = "DroidCam Annotatable Screen";
  private const string DroidCamRunnerName = "DroidCam PoseLandmarker Runner";

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  private static void CreateIfMissing()
  {
    if (FindFirstObjectByType<DualPoseLandmarkerRuntimeBootstrap>() != null)
    {
      return;
    }

    var bootstrapObject = new GameObject(BootstrapName);
    bootstrapObject.AddComponent<DualPoseLandmarkerRuntimeBootstrap>();
  }

  private IEnumerator Start()
  {
    yield return new WaitUntil(() =>
      FindFirstObjectByType<PoseLandmarkerRunner>() != null &&
      FindFirstObjectByType<Mediapipe.Unity.Screen>() != null &&
      FindFirstObjectByType<PoseLandmarkerResultAnnotationController>() != null);

    if (FindFirstObjectByType<DroidCamPoseLandmarkerRunner>() != null)
    {
      yield break;
    }

    var originalScreen = FindFirstObjectByType<Mediapipe.Unity.Screen>();
    var originalScreenObject = originalScreen.gameObject;

    var droidCamScreenObject = Instantiate(originalScreenObject, originalScreenObject.transform.parent, false);
    droidCamScreenObject.name = DroidCamScreenName;

    var droidCamScreen = droidCamScreenObject.GetComponent<Mediapipe.Unity.Screen>();
    var droidCamAnnotationController = droidCamScreenObject.GetComponentInChildren<PoseLandmarkerResultAnnotationController>(true);

    ApplySideBySideLayout(originalScreenObject, false);
    ApplySideBySideLayout(droidCamScreenObject, true);

    var runnerObject = new GameObject(DroidCamRunnerName);
    var droidCamRunner = runnerObject.AddComponent<DroidCamPoseLandmarkerRunner>();
    droidCamRunner.Configure(droidCamScreen, droidCamAnnotationController);

    Debug.Log("[DualPose] Existing pose runner kept on the left; DroidCam pose runner added on the right.");
  }

  private static void ApplySideBySideLayout(GameObject screenObject, bool rightSide)
  {
    if (!screenObject.TryGetComponent<RectTransform>(out var rectTransform))
    {
      return;
    }

    rectTransform.anchorMin = new Vector2(rightSide ? 0.5f : 0.0f, 0.0f);
    rectTransform.anchorMax = new Vector2(rightSide ? 1.0f : 0.5f, 1.0f);
    rectTransform.pivot = new Vector2(0.5f, 0.5f);
    rectTransform.offsetMin = new Vector2(rightSide ? 12f : 24f, 64f);
    rectTransform.offsetMax = new Vector2(rightSide ? -24f : -12f, -64f);
    rectTransform.localScale = Vector3.one;
    rectTransform.localRotation = Quaternion.identity;
  }
}
