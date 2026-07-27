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
