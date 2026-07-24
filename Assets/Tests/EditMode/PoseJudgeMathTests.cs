using NUnit.Framework;
using UnityEngine;

public class PoseJudgeMathTests
{
    [Test]
    public void MediaPipeDirection_IsConvertedToUnityPatternAxes()
    {
        Vector3 mediaPipe = new Vector3(0.25f, -0.75f, -0.5f);
        Vector3 expected = new Vector3(0.25f, 0.75f, 0.5f).normalized;

        Vector3 actual = PoseJudgeMath.ToPatternSpace(
            mediaPipe,
            new Vector3(1.0f, -1.0f, -1.0f));

        Assert.That(Vector3.Angle(actual, expected), Is.LessThan(0.001f));
    }

    [TestCase(0.0f, JudgeRank.Perfect)]
    [TestCase(15.0f, JudgeRank.Perfect)]
    [TestCase(15.1f, JudgeRank.Good)]
    [TestCase(30.1f, JudgeRank.Bad)]
    [TestCase(45.1f, JudgeRank.Miss)]
    public void Rank_UsesConfiguredAngleThresholds(float angle, JudgeRank expected)
    {
        Assert.That(PoseJudgeMath.Rank(angle, 15.0f, 30.0f, 45.0f), Is.EqualTo(expected));
    }

    [Test]
    public void MatchingBoneDirection_HasZeroAngle()
    {
        Vector3 direction = new Vector3(-0.5f, -0.8f, 0.2f).normalized;
        Assert.That(PoseJudgeMath.DirectionAngle(direction, direction), Is.LessThan(0.001f));
    }
}
