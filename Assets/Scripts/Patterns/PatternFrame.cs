using System;
using UnityEngine;

public enum PatternJoint
{
    Neck = 0,
    ShoulderL = 1,
    ShoulderR = 2,
    ElbowL = 3,
    ElbowR = 4,
    HipL = 5,
    HipR = 6,
    KneeL = 7,
    KneeR = 8
}

[Serializable]
public class PatternFrame
{
    public const int JointCount = 9;

    public float time;
    public Vector3 rootPosition;
    public Vector3[] angles = new Vector3[JointCount];

    public Vector3 GetAngle(PatternJoint joint)
    {
        return GetAngle((int)joint);
    }

    public Vector3 GetAngle(int jointId)
    {
        if (jointId >= 0 && jointId < angles.Length)
        {
            return angles[jointId];
        }

        Debug.LogWarning($"Pattern joint id out of range: {jointId}");
        return Vector3.zero;
    }
}
