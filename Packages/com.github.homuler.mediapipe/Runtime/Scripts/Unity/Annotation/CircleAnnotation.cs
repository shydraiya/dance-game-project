// Copyright (c) 2021 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using UnityEngine;

namespace Mediapipe.Unity
{
#pragma warning disable IDE0065
  using Color = UnityEngine.Color;
#pragma warning restore IDE0065

  public class CircleAnnotation : HierarchicalAnnotation
  {
    [SerializeField] private LineRenderer _lineRenderer;
    [SerializeField] private Color _color = Color.green;
    [SerializeField, Range(0, 1)] private float _lineWidth = 1.0f;

    private const int _SortingOrder = 1;
#if UNITY_EDITOR
    private const string _LineMaterialPath = "Packages/com.github.homuler.mediapipe/PackageResources/Materials/Line.mat";
#endif

    private void Awake()
    {
      EnsureLineRenderer();
    }

    private void OnEnable()
    {
      EnsureLineRenderer();
      ApplyColor(_color);
      ApplyLineWidth(_lineWidth);
    }

    private void OnDisable()
    {
      ApplyLineWidth(0.0f);
      _lineRenderer.positionCount = 0;
      _lineRenderer.SetPositions(new Vector3[] { });
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
      if (!UnityEditor.PrefabUtility.IsPartOfAnyPrefab(this))
      {
        ApplyColor(_color);
        ApplyLineWidth(_lineWidth);
      }
    }
#endif

    public void SetColor(Color color)
    {
      _color = color;
      ApplyColor(color);
    }

    public void SetLineWidth(float lineWidth)
    {
      _lineWidth = lineWidth;
      ApplyLineWidth(lineWidth);
    }

    public void Draw(Vector3 center, float radius, int vertices = 128)
    {
      var start = new Vector3(radius, 0, 0);
      var positions = new Vector3[vertices];

      for (var i = 0; i < positions.Length; i++)
      {
        var q = Quaternion.Euler(0, 0, i * 360 / positions.Length);
        positions[i] = (q * start) + center;
      }

      _lineRenderer.positionCount = positions.Length;
      _lineRenderer.SetPositions(positions);
    }

    private void ApplyColor(Color color)
    {
      EnsureLineRenderer();
      if (_lineRenderer != null)
      {
        _lineRenderer.startColor = color;
        _lineRenderer.endColor = color;
      }
    }

    private void ApplyLineWidth(float lineWidth)
    {
      EnsureLineRenderer();
      if (_lineRenderer != null)
      {
        _lineRenderer.startWidth = lineWidth;
        _lineRenderer.endWidth = lineWidth;
      }
    }

    private void EnsureLineRenderer()
    {
      var shouldClearPositions = false;
      if (_lineRenderer == null)
      {
        _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer == null)
        {
          _lineRenderer = gameObject.AddComponent<LineRenderer>();
          shouldClearPositions = true;
        }
      }

      _lineRenderer.useWorldSpace = false;
      _lineRenderer.loop = true;
      if (shouldClearPositions)
      {
        _lineRenderer.positionCount = 0;
      }
      _lineRenderer.sortingOrder = _SortingOrder;

      var lineMaterial = GetLineMaterial();
      if (lineMaterial != null)
      {
        _lineRenderer.sharedMaterial = lineMaterial;
      }
      else if (_lineRenderer.sharedMaterial == null)
      {
        _lineRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
      }
    }

    private static Material GetLineMaterial()
    {
#if UNITY_EDITOR
      return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(_LineMaterialPath);
#else
      return null;
#endif
    }
  }
}
