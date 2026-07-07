using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PatternFrame
{
    public float time;

    public Dictionary<string, Vector3> angles = new Dictionary<string, Vector3>();

    public Vector3 GetAngle(string angleName)
    {
        if (angles.TryGetValue(angleName, out Vector3 value))
        {
            return value;
        }

        Debug.LogWarning($"Angle not found: {angleName}");
        return Vector3.zero;
    }
}
