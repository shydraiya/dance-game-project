using System.Collections;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;
using UnityEngine.UI;

public sealed class DualPoseLandmarkerRuntimeBootstrap : MonoBehaviour
{
  private const string BootstrapName = "Dual Pose Landmarker Runtime Bootstrap";
  private const string DroidCamScreenName = "DroidCam Annotatable Screen";
  private const string DroidCamRunnerName = "DroidCam PoseLandmarker Runner";
  private const string MergedScreenName = "Merged Skeleton Screen";

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

    var originalScreen = FindFirstObjectByType<Mediapipe.Unity.Screen>();
    var originalScreenObject = originalScreen.gameObject;

    var droidCamScreenObject = GameObject.Find(DroidCamScreenName);
    if (droidCamScreenObject == null)
    {
      droidCamScreenObject = Instantiate(originalScreenObject, originalScreenObject.transform.parent, false);
      droidCamScreenObject.name = DroidCamScreenName;
    }

    var droidCamScreen = droidCamScreenObject.GetComponent<Mediapipe.Unity.Screen>();
    var droidCamAnnotationController = droidCamScreenObject.GetComponentInChildren<PoseLandmarkerResultAnnotationController>(true);

    ApplyThreeColumnLayout(originalScreenObject, 0);
    ApplyThreeColumnLayout(droidCamScreenObject, 1);

    var droidCamRunner = FindFirstObjectByType<DroidCamPoseLandmarkerRunner>();
    if (droidCamRunner == null)
    {
      var runnerObject = new GameObject(DroidCamRunnerName);
      droidCamRunner = runnerObject.AddComponent<DroidCamPoseLandmarkerRunner>();
    }
    droidCamRunner.Configure(droidCamScreen, droidCamAnnotationController);

    var mergedScreenObject = GameObject.Find(MergedScreenName);
    if (mergedScreenObject == null)
    {
      mergedScreenObject = new GameObject(MergedScreenName, typeof(RectTransform), typeof(CanvasRenderer), typeof(MergedPoseSkeletonGraphic));
      mergedScreenObject.transform.SetParent(originalScreenObject.transform.parent, false);
    }
    else if (mergedScreenObject.GetComponent<CanvasRenderer>() == null)
    {
      mergedScreenObject.AddComponent<CanvasRenderer>();
    }
    else if (mergedScreenObject.GetComponent<MergedPoseSkeletonGraphic>() == null)
    {
      mergedScreenObject.AddComponent<MergedPoseSkeletonGraphic>();
    }
    ApplyThreeColumnLayout(mergedScreenObject, 2);

    Debug.Log("[DualPose] Webcam, DroidCam, and merged skeleton screens are ready.");
  }

  private static void ApplyThreeColumnLayout(GameObject screenObject, int columnIndex)
  {
    if (!screenObject.TryGetComponent<RectTransform>(out var rectTransform))
    {
      return;
    }

    var xMin = columnIndex / 3.0f;
    var xMax = (columnIndex + 1) / 3.0f;
    rectTransform.anchorMin = new Vector2(xMin, 0.0f);
    rectTransform.anchorMax = new Vector2(xMax, 1.0f);
    rectTransform.pivot = new Vector2(0.5f, 0.5f);
    rectTransform.offsetMin = new Vector2(columnIndex == 0 ? 24f : 12f, 64f);
    rectTransform.offsetMax = new Vector2(columnIndex == 2 ? -24f : -12f, -64f);
    rectTransform.localScale = Vector3.one;
    rectTransform.localRotation = Quaternion.identity;
  }
}

public sealed class MergedPoseSkeletonGraphic : Graphic
{
  private static readonly (int, int)[] Connections =
  {
    (11, 13), (13, 15), (15, 17), (15, 19), (15, 21), (17, 19),
    (12, 14), (14, 16), (16, 18), (16, 20), (16, 22), (18, 20),
    (11, 12), (12, 24), (24, 23), (23, 11),
    (23, 25), (25, 27), (27, 29), (27, 31), (29, 31),
    (24, 26), (26, 28), (28, 30), (28, 32), (30, 32),
  };

  [SerializeField, Range(0.0f, 1.0f)] private float skeletonScale = 0.72f;
  [SerializeField, Range(0.0f, 1.0f)] private float sideDepthWeight = 0.40f;
  [SerializeField] private bool autoEstimateSideCameraYaw = true;
  [SerializeField, Range(20.0f, 90.0f)] private float sideCameraYawDegrees = 65.0f;
  [SerializeField, Range(0.0f, 1.0f)] private float yawSmoothing = 0.18f;
  [SerializeField] private float depthVisualOffset = 0.18f;
  [SerializeField] private float lineWidth = 5.0f;
  [SerializeField] private float pointRadius = 7.0f;
  [SerializeField] private Color lineColor = Color.white;
  [SerializeField] private Color nearPointColor = new(0.2f, 1.0f, 0.75f, 1.0f);
  [SerializeField] private Color farPointColor = new(0.35f, 0.55f, 1.0f, 1.0f);

  private readonly Vector3[] merged = new Vector3[PoseLandmarkFusionStore.LandmarkCount];
  private readonly Vector2[] projected = new Vector2[PoseLandmarkFusionStore.LandmarkCount];
  private readonly float[] confidence = new float[PoseLandmarkFusionStore.LandmarkCount];
  private float smoothedSideCameraYawDegrees = 65.0f;
  private bool hasMergedPose;

  private void Update()
  {
    FuseLatestPose();
    SetVerticesDirty();
  }

  protected override void OnPopulateMesh(VertexHelper vh)
  {
    vh.Clear();
    var rect = rectTransform.rect;
    AddBackground(vh, rect);

    if (!hasMergedPose)
    {
      var center = rect.center;
      AddLine(vh, center + new Vector2(-24f, 0f), center + new Vector2(24f, 0f), 3.0f, new Color(0.35f, 0.42f, 0.5f, 1.0f));
      AddLine(vh, center + new Vector2(0f, -24f), center + new Vector2(0f, 24f), 3.0f, new Color(0.35f, 0.42f, 0.5f, 1.0f));
      return;
    }

    for (var i = 0; i < Connections.Length; i++)
    {
      var (a, b) = Connections[i];
      if (confidence[a] < 0.2f || confidence[b] < 0.2f)
      {
        continue;
      }

      AddLine(vh, ToCanvasPoint(projected[a], rect), ToCanvasPoint(projected[b], rect), lineWidth, lineColor);
    }

    for (var i = 11; i < PoseLandmarkFusionStore.LandmarkCount; i++)
    {
      if (confidence[i] < 0.2f)
      {
        continue;
      }

      var depth01 = Mathf.InverseLerp(-0.8f, 0.8f, merged[i].z);
      AddDisc(vh, ToCanvasPoint(projected[i], rect), pointRadius, Color.Lerp(nearPointColor, farPointColor, depth01));
    }
  }

  private void FuseLatestPose()
  {
    hasMergedPose = false;

    if (!PoseLandmarkFusionStore.TryGetFrames(out var front, out var side))
    {
      return;
    }

    var frontScale = EstimateBodyScale(front.landmarks);
    var sideScale = EstimateBodyScale(side.landmarks);
    if (frontScale <= 0.0001f || sideScale <= 0.0001f)
    {
      return;
    }

    var frontCenter = EstimateBodyCenter(front.landmarks);
    var sideCenter = EstimateBodyCenter(side.landmarks);
    var targetYawDegrees = autoEstimateSideCameraYaw
      ? EstimateSideCameraYawDegrees(front.landmarks, side.landmarks, frontScale, sideScale)
      : sideCameraYawDegrees;
    smoothedSideCameraYawDegrees = Mathf.Lerp(smoothedSideCameraYawDegrees, targetYawDegrees, yawSmoothing);
    var yaw = Mathf.Max(0.2f, Mathf.Sin(smoothedSideCameraYawDegrees * Mathf.Deg2Rad));

    for (var i = 0; i < PoseLandmarkFusionStore.LandmarkCount; i++)
    {
      var frontPoint = front.landmarks[i];
      var sidePoint = side.landmarks[i];
      var frontX = (frontPoint.x - frontCenter.x) / frontScale;
      var frontY = (frontPoint.y - frontCenter.y) / frontScale;
      var sideDepth = -((sidePoint.x - sideCenter.x) / sideScale) / yaw;
      var modelDepth = frontPoint.z / frontScale;
      var depthWeight = sideDepthWeight * Mathf.Clamp01(side.visibility[i]);
      var mergedDepth = Mathf.Lerp(modelDepth, sideDepth, depthWeight);

      merged[i] = new Vector3(frontX, frontY, mergedDepth);
      projected[i] = new Vector2(0.5f + (frontX + mergedDepth * depthVisualOffset) * skeletonScale, 0.5f - frontY * skeletonScale);
      confidence[i] = Mathf.Min(front.visibility[i], side.visibility[i]);
    }

    hasMergedPose = true;
  }

  private static Vector2 EstimateBodyCenter(Vector3[] points)
  {
    return (ToVector2(points[11]) + ToVector2(points[12]) + ToVector2(points[23]) + ToVector2(points[24])) * 0.25f;
  }

  private static Vector2 ToVector2(Vector3 point) => new(point.x, point.y);

  private static float EstimateBodyScale(Vector3[] points)
  {
    var shoulderWidth = Mathf.Abs(points[12].x - points[11].x);
    var hipWidth = Mathf.Abs(points[24].x - points[23].x);
    var torsoHeight = Mathf.Abs(((points[11].y + points[12].y) * 0.5f) - ((points[23].y + points[24].y) * 0.5f));
    return Mathf.Max(shoulderWidth, hipWidth, torsoHeight, 0.05f);
  }

  private static float EstimateSideCameraYawDegrees(Vector3[] frontPoints, Vector3[] sidePoints, float frontScale, float sideScale)
  {
    var frontWidth = EstimateLateralBodyWidth(frontPoints) / frontScale;
    var sideWidth = EstimateLateralBodyWidth(sidePoints) / sideScale;
    if (frontWidth <= 0.0001f)
    {
      return 65.0f;
    }

    var apparentWidthRatio = Mathf.Clamp(sideWidth / frontWidth, 0.05f, 0.95f);
    return Mathf.Clamp(Mathf.Acos(apparentWidthRatio) * Mathf.Rad2Deg, 20.0f, 90.0f);
  }

  private static float EstimateLateralBodyWidth(Vector3[] points)
  {
    var shoulderWidth = Mathf.Abs(points[12].x - points[11].x);
    var hipWidth = Mathf.Abs(points[24].x - points[23].x);
    return Mathf.Max(shoulderWidth, hipWidth, 0.01f);
  }

  private static Vector2 ToCanvasPoint(Vector2 normalized, Rect rect)
  {
    var x = Mathf.Lerp(rect.xMin, rect.xMax, normalized.x);
    var y = Mathf.Lerp(rect.yMin, rect.yMax, normalized.y);
    return new Vector2(x, y);
  }

  private void AddBackground(VertexHelper vh, Rect rect)
  {
    AddQuad(vh, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMin), new Color(0.035f, 0.04f, 0.045f, 1.0f));
  }

  private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
  {
    var direction = (b - a).normalized;
    var normal = new Vector2(-direction.y, direction.x) * (width * 0.5f);
    AddQuad(vh, a - normal, a + normal, b + normal, b - normal, color);
  }

  private static void AddDisc(VertexHelper vh, Vector2 center, float radius, Color color)
  {
    const int segments = 16;
    var startIndex = vh.currentVertCount;
    vh.AddVert(center, color, Vector2.zero);

    for (var i = 0; i <= segments; i++)
    {
      var angle = (i / (float)segments) * Mathf.PI * 2.0f;
      vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
    }

    for (var i = 1; i <= segments; i++)
    {
      vh.AddTriangle(startIndex, startIndex + i, startIndex + i + 1);
    }
  }

  private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
  {
    var index = vh.currentVertCount;
    vh.AddVert(a, color, Vector2.zero);
    vh.AddVert(b, color, Vector2.zero);
    vh.AddVert(c, color, Vector2.zero);
    vh.AddVert(d, color, Vector2.zero);
    vh.AddTriangle(index, index + 1, index + 2);
    vh.AddTriangle(index, index + 2, index + 3);
  }
}
