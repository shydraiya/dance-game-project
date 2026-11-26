// 자세 판정에 사용하는 좌표축 변환·방향 간 각도·등급 분류를 제공하는 공통 계산 모듈이다.
// 각도는 도 단위이며 오차가 작을수록 높은 등급을 부여한다.

using UnityEngine;

/// <summary>
/// Coordinate conversion and angle scoring shared by live landmark and bone judges.
/// Pattern vectors are stored in the local coordinate system of the Animator root.
/// </summary>
public static class PoseJudgeMath
{
    public static Vector3 ToPatternSpace(Vector3 mediaPipeDirection, Vector3 landmarkScale)
    {
        Vector3 converted = Vector3.Scale(mediaPipeDirection, landmarkScale);
        return converted.sqrMagnitude > 0.000001f ? converted.normalized : Vector3.zero;
    }

    // 길이가 없는 벡터는 유효한 방향이 아니므로 NaN으로 비교 불가를 표시한다.
    public static float DirectionAngle(Vector3 currentDirection, Vector3 targetDirection)
    {
        if (currentDirection.sqrMagnitude < 0.000001f ||
            targetDirection.sqrMagnitude < 0.000001f)
        {
            return float.NaN;
        }

        return Vector3.Angle(currentDirection, targetDirection);
    }

    public static JudgeRank Rank(
        float averageAngle,
        float perfectAngle,
        float goodAngle,
        float badAngle)
    {
        if (averageAngle <= perfectAngle) return JudgeRank.Perfect;
        if (averageAngle <= goodAngle) return JudgeRank.Good;
        if (averageAngle <= badAngle) return JudgeRank.Bad;
        return JudgeRank.Miss;
    }
}
