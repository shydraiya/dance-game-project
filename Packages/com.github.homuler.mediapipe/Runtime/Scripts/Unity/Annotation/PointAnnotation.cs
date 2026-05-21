// Copyright (c) 2021 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using Mediapipe.Unity.CoordinateSystem;
using UnityEngine;

using mplt = Mediapipe.LocationData.Types;
using mptcc = Mediapipe.Tasks.Components.Containers;

namespace Mediapipe.Unity
{
#pragma warning disable IDE0065
  using Color = UnityEngine.Color;
#pragma warning restore IDE0065

  public class PointAnnotation : HierarchicalAnnotation
  {
    
    [SerializeField] private Color _color = Color.green;
    [SerializeField] private float _radius = 15.0f;

    [SerializeField] private float _depthScale = 1000.0f; // <<Fixed Here>>
    [SerializeField] private bool _invertDepth = false;    // <<Fixed Here>>
    [SerializeField] private int _depthAxis = 2;          // <<Fixed Here>>

    [SerializeField] private bool _smoothDepth = true;    // <<Fixed Here>>
    [SerializeField] private float _depthSmoothSpeed = 35.0f; // <<Fixed Here>>

    private bool _hasSmoothedDepth = false;               // <<Fixed Here>>
    private float _smoothedDepth = 25.0f;                  // <<Fixed Here>>

    private void OnEnable()
    {
      ApplyColor(_color);
      ApplyRadius(_radius);
    }

    private void OnDisable()
    {
      ApplyRadius(0.0f);
    }

    public void SetColor(Color color)
    {
      _color = color;
      ApplyColor(_color);
    }

    public void SetRadius(float radius)
    {
      _radius = radius;
      ApplyRadius(_radius);
    }

    public void Draw(Vector3 position)
    {
      SetActive(true); // Vector3 is not nullable
      transform.localPosition = position;
    }

    public void Draw(Landmark target, Vector3 scale, bool visualizeZ = true)
    {
      if (ActivateFor(target))
      {
        var position = GetScreenRect().GetPoint(target, scale, rotationAngle, isMirrored);
        if (!visualizeZ)
        {
          position.z = 0.0f;
        }
        transform.localPosition = position;
      }
    }

    public void Draw(NormalizedLandmark target, bool visualizeZ = true)
    {
      if (ActivateFor(target))
      {
        var position = GetScreenRect().GetPoint(target, rotationAngle, isMirrored);

        // <<Fixed Here>>
        if (visualizeZ)
        {
          float z = target.Z * _depthScale;

          if (_invertDepth)
          {
            //z = -z;
          }

          position.z = z;
        }
        else
        {
          position.z = 0.0f;
        }

        transform.localPosition = position;
      }
    }
    public void Draw(in mptcc.NormalizedLandmark target, bool visualizeZ = true)
    {
      if (ActivateFor(target))
      {
        var position = GetScreenRect().GetPoint(in target, rotationAngle, isMirrored);

        // <<Fixed Here>>
        // GetPoint()가 이미 raw z를 position.z에 넣기 때문에,
        // 우리가 직접 smoothing한 depth만 쓰기 위해 기존 z를 제거한다.
        position.z = 0.0f;

        if (visualizeZ)
        {
          float depth = target.z * _depthScale;

          if (_invertDepth)
          {
            //depth = -depth;
          }

          // <<Fixed Here>>
          // depth만 smoothing
          if (_smoothDepth)
          {
            if (!_hasSmoothedDepth)
            {
              _smoothedDepth = depth;
              _hasSmoothedDepth = true;
            }
            else
            {
              float t = 1.0f - Mathf.Exp(-_depthSmoothSpeed * Time.deltaTime);
              _smoothedDepth = Mathf.Lerp(_smoothedDepth, depth, t);
            }

            depth = _smoothedDepth;
          }

          // <<Fixed Here>>
          // 선택한 축에 smoothed depth만 적용
          if (_depthAxis == 0)
          {
            position.x += depth;
          }
          else if (_depthAxis == 1)
          {
            position.y += depth;
          }
          else
          {
            position.z += depth;
          }
        }

        transform.localPosition = position;
      }
    }

    public void Draw(mplt.RelativeKeypoint target, float threshold = 0.0f)
    {
      if (ActivateFor(target))
      {
        Draw(GetScreenRect().GetPoint(target, rotationAngle, isMirrored));
        SetColor(GetColor(target.Score, threshold));
      }
    }

    public void Draw(mptcc.NormalizedKeypoint target, float threshold = 0.0f)
    {
      if (ActivateFor(target))
      {
        Draw(GetScreenRect().GetPoint(target, rotationAngle, isMirrored));
        SetColor(GetColor(target.score ?? 1.0f, threshold));
      }
    }

    private void ApplyColor(Color color)
    {
      GetComponent<Renderer>().material.color = color;
    }

    private void ApplyRadius(float radius)
    {
      transform.localScale = radius * Vector3.one;
    }

    private Color GetColor(float score, float threshold)
    {
      var t = (score - threshold) / (1 - threshold);
      var h = Mathf.Lerp(90, 0, t) / 360; // from yellow-green to red
      return Color.HSVToRGB(h, 1, 1);
    }
  }
}
