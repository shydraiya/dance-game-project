using UnityEngine;

/// <summary>
/// Reads the player's Humanoid bone directions in exactly the same coordinate
/// space used by DancePatternExtracterSceneController when writing pattern CSV.
/// This is useful both as a production judge source and as an Inspector diagnostic.
/// </summary>
public class BoneRotationPoseJudge : MonoBehaviour
{
    [SerializeField] private Animator _playerAnimator;
    [SerializeField] private bool _logTestResult = true;

    public bool IsReady => _playerAnimator != null && _playerAnimator.isHuman;

    private void Reset()
    {
        _playerAnimator = GetComponentInChildren<Animator>();
    }

    public bool TryGetDirections(Vector3[] destination)
    {
        if (!IsReady || destination == null || destination.Length < PatternFrame.JointCount)
        {
            return false;
        }

        destination[(int)PatternJoint.Neck] = GetNeckDirection();
        destination[(int)PatternJoint.ShoulderL] = GetDirection(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm);
        destination[(int)PatternJoint.ShoulderR] = GetDirection(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
        destination[(int)PatternJoint.ElbowL] = GetDirection(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
        destination[(int)PatternJoint.ElbowR] = GetDirection(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
        destination[(int)PatternJoint.HipL] = GetDirection(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg);
        destination[(int)PatternJoint.HipR] = GetDirection(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg);
        destination[(int)PatternJoint.KneeL] = GetDirection(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
        destination[(int)PatternJoint.KneeR] = GetDirection(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);

        for (int i = 0; i < PatternFrame.JointCount; i++)
        {
            if (destination[i].sqrMagnitude > 0.000001f)
            {
                return true;
            }
        }

        return false;
    }

    [ContextMenu("Test Against PoseNoteReader Last Pattern")]
    private void TestLastPattern()
    {
        PoseNoteReader reader = FindAnyObjectByType<PoseNoteReader>();
        PatternLoader loader = FindAnyObjectByType<PatternLoader>();
        if (reader == null || loader == null || loader.Patterns.Count == 0)
        {
            Debug.LogWarning("Bone judge test needs PoseNoteReader and a loaded pattern.", this);
            return;
        }

        PatternFrame closest = loader.Patterns[0];
        float time = GameManager.instance != null ? GameManager.instance.gameTime : closest.time;
        for (int i = 1; i < loader.Patterns.Count; i++)
        {
            if (Mathf.Abs(loader.Patterns[i].time - time) < Mathf.Abs(closest.time - time))
            {
                closest = loader.Patterns[i];
            }
        }

        PoseNoteReader.JudgeResult result = reader.EvaluatePattern(closest);
        if (_logTestResult)
        {
            Debug.Log($"Bone judge test: {result.rank}, angle={result.averageAngle:0.0}, parts={result.comparedParts}", this);
        }
    }

    private Vector3 GetNeckDirection()
    {
        HumanBodyBones start = _playerAnimator.GetBoneTransform(HumanBodyBones.Neck) != null
            ? HumanBodyBones.Neck
            : HumanBodyBones.UpperChest;
        if (_playerAnimator.GetBoneTransform(start) == null)
        {
            start = HumanBodyBones.Chest;
        }

        return GetDirection(start, HumanBodyBones.Head);
    }

    private Vector3 GetDirection(HumanBodyBones start, HumanBodyBones end)
    {
        Transform startBone = _playerAnimator.GetBoneTransform(start);
        Transform endBone = _playerAnimator.GetBoneTransform(end);
        if (startBone == null || endBone == null)
        {
            return Vector3.zero;
        }

        Vector3 localDirection = _playerAnimator.transform.InverseTransformDirection(
            endBone.position - startBone.position);
        return localDirection.sqrMagnitude > 0.000001f ? localDirection.normalized : Vector3.zero;
    }
}
