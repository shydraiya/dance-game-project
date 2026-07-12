using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//저기 화면 우측 상단 판정에 나오는 마네킹 관련 코드임
//쉽게 말하면 부리부리몬 그거 만든거
public class PosePreviewController : MonoBehaviour
{
    //inspector는 대부분 바이브 코딩임
    [Header("UI")]
    [SerializeField]
    private RawImage previewRawImage;

    [Header("Preview Objects")]
    [SerializeField]
    private Camera previewCamera;

    [SerializeField]
    private Transform mannequinRoot;

    [SerializeField]
    private GameObject mannequinPrefab;

    [SerializeField]
    private PatternLoader patternLoader;

    [Header("Layer")]
    [SerializeField]
    private string previewLayerName = "PosePreview";

    [Header("Render Texture")]
    [SerializeField]
    private int renderTextureWidth = 1024;

    [SerializeField]
    private int renderTextureHeight = 256;

    [Header("Mannequin Transform")]
    [SerializeField]
    private Vector3 mannequinLocalPosition = Vector3.zero;

    [SerializeField]
    private Vector3 mannequinLocalRotation =
        new Vector3(0f, 180f, 0f);

    [SerializeField]
    private Vector3 mannequinLocalScale = Vector3.one;

    [Header("Test")]
    [SerializeField]
    private bool spawnOnStart = true;

    [SerializeField]
    private bool spawnPatternFrames = true;

    [SerializeField, Min(0.01f)]
    private float patternSpawnInterval = 0.5f;

    private RenderTexture renderTexture;

    private int previewLayer;
    private int nextPatternFrameIndex;
    private float lastSpawnedPatternTime = float.NegativeInfinity;
    private float lastObservedGameTime;

    [Header("Movement")]
    [SerializeField, Min(0.01f)]
    private float timeToJudgeLine = 1f;

    [SerializeField, Range(0f, 1f)]
    private float judgeLineViewportX = 0.9f;

    [SerializeField, Min(0f)]
    private float offscreenPadding = 0.5f;

    [SerializeField, Min(0.01f)]
    private float exitDuration = 0.3f;

    //3d 오브젝트를 그대로 ui에 출력하면 원근감이 생겨버림
    //요걸 방지하기 위해 mask를 이용했음
    private void Awake()
    {
        ResolvePreviewRawImage();

        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        previewLayer = LayerMask.NameToLayer(previewLayerName);

        if (previewLayer == -1)
        {
            Debug.LogError(
                $"'{previewLayerName}' 레이어가 존재하지 않습니다. " +
                "Tags and Layers에서 레이어를 추가해주세요.",
                this
            );

            enabled = false;
            return;
        }

        CreateRenderTexture();
        ConfigurePreviewCamera();
    }

    //게임 시작할 때 마네킹 나오는 건 요거 때문임
    //나중에 지우면 됨!!!!!!
    private void Start()
    {
        ResolvePatternLoader();

        if (spawnOnStart && !ShouldSpawnFromPattern())
        {
            SpawnMannequin();
        }
    }

    private void Update()
    {
        if (!ShouldSpawnFromPattern())
        {
            return;
        }

        float gameTime = GameManager.instance != null
            ? GameManager.instance.gameTime
            : Time.timeSinceLevelLoad;

        if (gameTime < lastObservedGameTime)
        {
            nextPatternFrameIndex = 0;
            lastSpawnedPatternTime = float.NegativeInfinity;
        }

        lastObservedGameTime = gameTime;
        IReadOnlyList<PatternFrame> frames = patternLoader.Frames;
        float targetTime = gameTime + timeToJudgeLine;

        while (nextPatternFrameIndex < frames.Count && frames[nextPatternFrameIndex].time <= targetTime)
        {
            PatternFrame frame = frames[nextPatternFrameIndex];
            if (frame.time - lastSpawnedPatternTime >= patternSpawnInterval)
            {
                SpawnMannequin(frame);
                lastSpawnedPatternTime = frame.time;
            }

            nextPatternFrameIndex++;
        }
    }

    //그럴 일은 없겠지만 뭔가 Inspector에서 빠지면 알려주는 디버깅용 경고 문구
    private bool ValidateReferences()
    {
        if (previewRawImage == null)
        {
            Debug.LogError("Preview Raw Image가 연결되지 않았습니다.", this);
            return false;
        }

        if (previewCamera == null)
        {
            Debug.LogError("Preview Camera가 연결되지 않았습니다.", this);
            return false;
        }

        if (mannequinRoot == null)
        {
            Debug.LogError("Mannequin Root가 연결되지 않았습니다.", this);
            return false;
        }

        if (mannequinPrefab == null)
        {
            Debug.LogError("Mannequin Prefab이 연결되지 않았습니다.", this);
            return false;
        }

        return true;
    }

    private void ResolvePreviewRawImage()
    {
        if (previewRawImage != null)
        {
            return;
        }

        GameObject patternRawImageObject = GameObject.Find("HUD_PatternRawImage");
        if (patternRawImageObject != null && patternRawImageObject.TryGetComponent(out previewRawImage))
        {
            return;
        }

        RawImage[] rawImages = FindObjectsByType<RawImage>(FindObjectsInactive.Include);
        foreach (RawImage rawImage in rawImages)
        {
            if (rawImage.name.Contains("Pattern"))
            {
                previewRawImage = rawImage;
                return;
            }
        }
    }

    //요건 바이브 코딩 영역임
    //unity에서 mask texture 지정하는 코드인데 난 몰루
    private void CreateRenderTexture()
    {
        renderTexture = new RenderTexture(
            renderTextureWidth,
            renderTextureHeight,
            24,
            RenderTextureFormat.ARGB32
        );

        renderTexture.name = "PosePreviewRenderTexture";
        renderTexture.filterMode = FilterMode.Bilinear;
        renderTexture.wrapMode = TextureWrapMode.Clamp;

        renderTexture.Create();

        previewRawImage.texture = renderTexture;
    }

    //오건 바이브 코딩 영역임
    //mask texture에서 촬영하고, 사용되지 않는 영역은 투명화 하는 코드인데 난 몰루2
    private void ConfigurePreviewCamera()
    {
        previewCamera.targetTexture = renderTexture;

        // PosePreview 레이어만 촬영합니다.
        previewCamera.cullingMask = 1 << previewLayer;

        previewCamera.clearFlags = CameraClearFlags.SolidColor;

        // 투명 배경으로 설정합니다.
        // 뒤쪽 PatternBox의 회색 Image가 그대로 보입니다.
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);

        previewCamera.orthographic = true;
        previewCamera.enabled = true;
    }

    //마네킹 생성 코드임
    //나중에 관절 각도 반영해서 추가하도록 인자 넣는거 만들어야함
    public GameObject SpawnMannequin()
    {
        return SpawnMannequin(null);
    }

    public GameObject SpawnMannequin(PatternFrame patternFrame)
    {
        GameObject mannequin = Instantiate(
            mannequinPrefab,
            mannequinRoot
        );

        GetHorizontalMovePositions(
            out float startX,
            out float judgeX,
            out float endX
        );

        Transform mannequinTransform = mannequin.transform;

        mannequinTransform.localPosition = new Vector3(
            startX,
            mannequinLocalPosition.y,
            mannequinLocalPosition.z
        );

        mannequinTransform.localRotation =
            Quaternion.Euler(mannequinLocalRotation);

        mannequinTransform.localScale =
            mannequinLocalScale;

        SetLayerRecursively(mannequin, previewLayer);
        PrepareMannequin(mannequin);
        if (patternFrame != null)
        {
            ApplyPatternFrame(mannequin, patternFrame);
        }

        StartCoroutine(
            MoveMannequinRoutine(
                mannequinTransform,
                startX,
                judgeX,
                endX
            )
        );

        return mannequin;
    }

    private bool ShouldSpawnFromPattern()
    {
        ResolvePatternLoader();
        return spawnPatternFrames &&
            patternLoader != null &&
            patternLoader.Frames != null &&
            patternLoader.Frames.Count > 0;
    }

    private void ResolvePatternLoader()
    {
        if (patternLoader == null)
        {
            patternLoader = FindAnyObjectByType<PatternLoader>();
        }

        if (patternLoader != null && (patternLoader.Frames == null || patternLoader.Frames.Count == 0))
        {
            patternLoader.LoadSelectedOrDefaultPattern();
        }
    }

    //판정선까지 정해진 시간(t초)만에 이동하도록 좀 코드가 복잡해짐
    //근데 지금 parameter 조정 잘 해놔서 왠만하면 요걸 건들일은 없을듯 (없어야 함 제발)
    private IEnumerator MoveMannequinRoutine(
        Transform mannequinTransform,
        float startX,
        float judgeX,
        float endX
    )
    {
        float elapsedTime = 0f;

        // 생성 위치에서 판정선까지 정확히 timeToJudgeLine초 동안 이동
        while (
            mannequinTransform != null &&
            elapsedTime < timeToJudgeLine
        )
        {
            elapsedTime += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsedTime / timeToJudgeLine
            );

            Vector3 position =
                mannequinTransform.localPosition;

            position.x = Mathf.Lerp(
                startX,
                judgeX,
                progress
            );

            mannequinTransform.localPosition = position;

            yield return null;
        }

        if (mannequinTransform == null)
        {
            yield break;
        }

        // 프레임 오차가 남지 않도록 정확히 판정선 위치에 고정
        Vector3 judgePosition =
            mannequinTransform.localPosition;

        judgePosition.x = judgeX;
        mannequinTransform.localPosition = judgePosition;

        OnMannequinReachedJudgeLine(mannequinTransform.gameObject);

        // 판정선을 지난 후 오른쪽 화면 밖으로 이동
        elapsedTime = 0f;

        while (
            mannequinTransform != null &&
            elapsedTime < exitDuration
        )
        {
            elapsedTime += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsedTime / exitDuration
            );

            Vector3 position =
                mannequinTransform.localPosition;

            position.x = Mathf.Lerp(
                judgeX,
                endX,
                progress
            );

            mannequinTransform.localPosition = position;

            yield return null;
        }

        if (mannequinTransform != null)
        {
            Destroy(mannequinTransform.gameObject);
        }
    }

    //판정선 도달하면 메세지 뜨는거 (디버깅용)
    //지금은 안쓸건데, 뭐... 필요하면 쓰셈
    private void OnMannequinReachedJudgeLine(GameObject mannequin)
    {
        /*
        Debug.Log(
            $"{mannequin.name}이 판정선에 도착했습니다.",
            mannequin
        );
        //Debug.Break();
        */
    }

    //위에 t초 만에 이동하도록 하기 위해 좌표 설정하는거임
    //inspector에서 설정 가능함
    private void GetHorizontalMovePositions(
        out float startX,
        out float judgeX,
        out float endX
    )
    {
        float depth = Vector3.Dot(
            mannequinRoot.position - previewCamera.transform.position,
            previewCamera.transform.forward
        );

        Vector3 leftWorldPosition =
            previewCamera.ViewportToWorldPoint(
                new Vector3(0f, 0.5f, depth)
            );

        Vector3 judgeWorldPosition =
            previewCamera.ViewportToWorldPoint(
                new Vector3(judgeLineViewportX, 0.5f, depth)
            );

        Vector3 rightWorldPosition =
            previewCamera.ViewportToWorldPoint(
                new Vector3(1f, 0.5f, depth)
            );

        Vector3 leftLocalPosition =
            mannequinRoot.InverseTransformPoint(leftWorldPosition);

        Vector3 judgeLocalPosition =
            mannequinRoot.InverseTransformPoint(judgeWorldPosition);

        Vector3 rightLocalPosition =
            mannequinRoot.InverseTransformPoint(rightWorldPosition);

        startX = leftLocalPosition.x - offscreenPadding;
        judgeX = judgeLocalPosition.x;
        endX = rightLocalPosition.x + offscreenPadding;
    }

    //마네킹 property 설정한거
    //바이브 코딩 영역임
    private void PrepareMannequin(GameObject mannequin)
    {
        // 물리 동작 방지
        Rigidbody[] rigidbodies =
            mannequin.GetComponentsInChildren<Rigidbody>(true);

        foreach (Rigidbody rigidbodyComponent in rigidbodies)
        {
            rigidbodyComponent.useGravity = false;
            rigidbodyComponent.isKinematic = true;
        }

        Collider[] colliders =
            mannequin.GetComponentsInChildren<Collider>(true);

        foreach (Collider colliderComponent in colliders)
        {
            colliderComponent.enabled = false;
        }

        // 애니메이션의 Root Motion으로 위치가 움직이지 않도록 설정
        Animator animator =
            mannequin.GetComponentInChildren<Animator>(true);

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    private void ApplyPatternFrame(GameObject mannequin, PatternFrame frame)
    {
        Animator animator = mannequin.GetComponentInChildren<Animator>(true);
        if (animator == null || !animator.isHuman)
        {
            return;
        }

        animator.Rebind();
        animator.Update(0.0f);

        Dictionary<PatternJoint, PreviewBoneSegment> segments = CachePreviewSegments(animator);
        ApplyDirection(animator, segments, PatternJoint.Neck, frame.GetAngle(PatternJoint.Neck));
        ApplyDirection(animator, segments, PatternJoint.ShoulderL, frame.GetAngle(PatternJoint.ShoulderL));
        ApplyDirection(animator, segments, PatternJoint.ShoulderR, frame.GetAngle(PatternJoint.ShoulderR));
        ApplyDirection(animator, segments, PatternJoint.ElbowL, frame.GetAngle(PatternJoint.ElbowL));
        ApplyDirection(animator, segments, PatternJoint.ElbowR, frame.GetAngle(PatternJoint.ElbowR));
        ApplyDirection(animator, segments, PatternJoint.HipL, frame.GetAngle(PatternJoint.HipL));
        ApplyDirection(animator, segments, PatternJoint.HipR, frame.GetAngle(PatternJoint.HipR));
        ApplyDirection(animator, segments, PatternJoint.KneeL, frame.GetAngle(PatternJoint.KneeL));
        ApplyDirection(animator, segments, PatternJoint.KneeR, frame.GetAngle(PatternJoint.KneeR));
    }

    private Dictionary<PatternJoint, PreviewBoneSegment> CachePreviewSegments(Animator animator)
    {
        Dictionary<PatternJoint, PreviewBoneSegment> segments = new Dictionary<PatternJoint, PreviewBoneSegment>();
        AddSegment(segments, PatternJoint.Neck, animator, HumanBodyBones.Neck, HumanBodyBones.Head);
        if (!segments.ContainsKey(PatternJoint.Neck))
        {
            AddSegment(segments, PatternJoint.Neck, animator, HumanBodyBones.UpperChest, HumanBodyBones.Head);
        }
        if (!segments.ContainsKey(PatternJoint.Neck))
        {
            AddSegment(segments, PatternJoint.Neck, animator, HumanBodyBones.Chest, HumanBodyBones.Head);
        }

        AddSegment(segments, PatternJoint.ShoulderL, animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm);
        AddSegment(segments, PatternJoint.ShoulderR, animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
        AddSegment(segments, PatternJoint.ElbowL, animator, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
        AddSegment(segments, PatternJoint.ElbowR, animator, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
        AddSegment(segments, PatternJoint.HipL, animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg);
        AddSegment(segments, PatternJoint.HipR, animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg);
        AddSegment(segments, PatternJoint.KneeL, animator, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
        AddSegment(segments, PatternJoint.KneeR, animator, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
        return segments;
    }

    private static void AddSegment(Dictionary<PatternJoint, PreviewBoneSegment> segments, PatternJoint joint, Animator animator, HumanBodyBones start, HumanBodyBones end)
    {
        Transform startBone = animator.GetBoneTransform(start);
        Transform endBone = animator.GetBoneTransform(end);
        if (startBone == null || endBone == null)
        {
            return;
        }

        Vector3 restDirection = endBone.position - startBone.position;
        if (restDirection.sqrMagnitude < 0.000001f)
        {
            return;
        }

        PreviewBoneSegment segment = new PreviewBoneSegment();
        segment.Bone = startBone;
        segment.RestDirection = restDirection.normalized;
        segment.RestRotation = startBone.rotation;
        segments[joint] = segment;
    }

    private static void ApplyDirection(Animator animator, Dictionary<PatternJoint, PreviewBoneSegment> segments, PatternJoint joint, Vector3 localDirection)
    {
        PreviewBoneSegment segment;
        if (!segments.TryGetValue(joint, out segment) || segment.Bone == null || localDirection.sqrMagnitude < 0.000001f)
        {
            return;
        }

        Vector3 worldDirection = animator.transform.TransformDirection(localDirection.normalized).normalized;
        segment.Bone.rotation = Quaternion.FromToRotation(segment.RestDirection, worldDirection) * segment.RestRotation;
    }

    //바이브 코딩 영역
    private static void SetLayerRecursively(
        GameObject target,
        int layer
    )
    {
        target.layer = layer;

        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    //마네킹 파괴될 때 요거 호출됨
    private void OnDestroy()
    {
        if (previewCamera != null)
        {
            previewCamera.targetTexture = null;
        }

        if (previewRawImage != null)
        {
            previewRawImage.texture = null;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }

    private sealed class PreviewBoneSegment
    {
        public Transform Bone;
        public Vector3 RestDirection;
        public Quaternion RestRotation;
    }
}
