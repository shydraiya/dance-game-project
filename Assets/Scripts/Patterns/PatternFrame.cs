// 특정 시각의 기준 자세를 저장하는 공통 데이터 모델이다.
// PatternJoint 순서의 9개 방향 벡터와 루트 위치를 담아 재생·미리보기·판정에서 공유한다.

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
    // 이름은 angles이지만 Euler 각이 아니라 각 부위의 방향 벡터이다. 배열 순서는 PatternJoint와 일치한다.
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
