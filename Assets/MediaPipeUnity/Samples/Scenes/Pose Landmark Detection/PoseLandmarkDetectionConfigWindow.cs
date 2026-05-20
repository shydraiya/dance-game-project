// Copyright (c) 2023 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using UnityEngine;
using UnityEngine.UI;
using Mediapipe.Unity.Sample.UI;

namespace Mediapipe.Unity.Sample.PoseLandmarkDetection.UI
{
  public class PoseLandmarkDetectionConfigWindow : ModalContents
  {
    [SerializeField] private Dropdown _delegateInput;
    [SerializeField] private Dropdown _imageReadModeInput;
    [SerializeField] private Dropdown _modelSelectionInput;
    [SerializeField] private Dropdown _runningModeInput;
    [SerializeField] private InputField _numPosesInput;
    [SerializeField] private InputField _minPoseDetectionConfidenceInput;
    [SerializeField] private InputField _minPosePresenceConfidenceInput;
    [SerializeField] private InputField _minTrackingConfidenceInput;
    [SerializeField] private Toggle _outputSegmentationMasksInput;

    private PoseLandmarkDetectionConfig _config;
    private PoseLandmarkerRunner _runner;
    private bool _isChanged;

    private readonly (string label, PoseLandmarkListAnnotation.BodyParts part)[] _bodyPartOptions =
    {
      ("Head", PoseLandmarkListAnnotation.BodyParts.Face),
      ("Torso", PoseLandmarkListAnnotation.BodyParts.Torso),
      ("Left Arm", PoseLandmarkListAnnotation.BodyParts.LeftArm),
      ("Right Arm", PoseLandmarkListAnnotation.BodyParts.RightArm),
      ("Left Hand", PoseLandmarkListAnnotation.BodyParts.LeftHand),
      ("Right Hand", PoseLandmarkListAnnotation.BodyParts.RightHand),
      ("Lower Body", PoseLandmarkListAnnotation.BodyParts.LowerBody),
    };

    private void Start()
    {
      _runner = GameObject.Find("Solution").GetComponent<PoseLandmarkerRunner>();
      _config = _runner.config;
      InitializeContents();
    }

    public override void Exit() => GetModal().CloseAndResume(_isChanged);

    private void SwitchDelegate()
    {
      _config.Delegate = (Tasks.Core.BaseOptions.Delegate)_delegateInput.value;
      _isChanged = true;
    }

    private void SwitchImageReadMode()
    {
      _config.ImageReadMode = (ImageReadMode)_imageReadModeInput.value;
      _isChanged = true;
    }

    private void SwitchModelType()
    {
      _config.Model = (ModelType)_modelSelectionInput.value;
      _isChanged = true;
    }

    private void SwitchRunningMode()
    {
      _config.RunningMode = (Tasks.Vision.Core.RunningMode)_runningModeInput.value;
      _isChanged = true;
    }

    private void SetNumPoses()
    {
      if (int.TryParse(_numPosesInput.text, out var value))
      {
        _config.NumPoses = value;
        _isChanged = true;
      }
    }

    private void SetMinPoseDetectionConfidence()
    {
      if (float.TryParse(_minPoseDetectionConfidenceInput.text, out var value))
      {
        _config.MinPoseDetectionConfidence = value;
        _isChanged = true;
      }
    }

    private void SetMinPosePresenceConfidence()
    {
      if (float.TryParse(_minPosePresenceConfidenceInput.text, out var value))
      {
        _config.MinPosePresenceConfidence = value;
        _isChanged = true;
      }
    }

    private void SetMinTrackingConfidence()
    {
      if (float.TryParse(_minTrackingConfidenceInput.text, out var value))
      {
        _config.MinTrackingConfidence = value;
        _isChanged = true;

      }
    }

    private void ToggleOutputSegmentationMasks()
    {
      _config.OutputSegmentationMasks = _outputSegmentationMasksInput.isOn;
      _isChanged = true;
    }

    private void InitializeContents()
    {
      InitializeDelegate();
      InitializeImageReadMode();
      InitializeModelSelection();
      InitializeRunningMode();
      InitializeNumPoses();
      InitializeMinPoseDetectionConfidence();
      InitializeMinPosePresenceConfidence();
      InitializeMinTrackingConfidence();
      InitializeOutputSegmentationMasks();
      InitializeVisibleBodyParts();
    }

    private void InitializeDelegate()
    {
      InitializeDropdown<Tasks.Core.BaseOptions.Delegate>(_delegateInput, _config.Delegate.ToString());
      _delegateInput.onValueChanged.AddListener(delegate { SwitchDelegate(); });
    }

    private void InitializeImageReadMode()
    {
      InitializeDropdown<ImageReadMode>(_imageReadModeInput, _config.ImageReadMode.GetDescription());
      _imageReadModeInput.onValueChanged.AddListener(delegate { SwitchImageReadMode(); });
    }

    private void InitializeModelSelection()
    {
      InitializeDropdown<ModelType>(_modelSelectionInput, _config.ModelName);
      _modelSelectionInput.onValueChanged.AddListener(delegate { SwitchModelType(); });
    }

    private void InitializeRunningMode()
    {
      InitializeDropdown<Tasks.Vision.Core.RunningMode>(_runningModeInput, _config.RunningMode.ToString());
      _runningModeInput.onValueChanged.AddListener(delegate { SwitchRunningMode(); });
    }

    private void InitializeNumPoses()
    {
      _numPosesInput.text = _config.NumPoses.ToString();
      _numPosesInput.onValueChanged.AddListener(delegate { SetNumPoses(); });
    }

    private void InitializeMinPoseDetectionConfidence()
    {
      _minPoseDetectionConfidenceInput.text = _config.MinPoseDetectionConfidence.ToString();
      _minPoseDetectionConfidenceInput.onValueChanged.AddListener(delegate { SetMinPoseDetectionConfidence(); });
    }

    private void InitializeMinPosePresenceConfidence()
    {
      _minPosePresenceConfidenceInput.text = _config.MinPosePresenceConfidence.ToString();
      _minPosePresenceConfidenceInput.onValueChanged.AddListener(delegate { SetMinPosePresenceConfidence(); });
    }

    private void InitializeMinTrackingConfidence()
    {
      _minTrackingConfidenceInput.text = _config.MinTrackingConfidence.ToString();
      _minTrackingConfidenceInput.onValueChanged.AddListener(delegate { SetMinTrackingConfidence(); });
    }

    private void InitializeOutputSegmentationMasks()
    {
      _outputSegmentationMasksInput.isOn = _config.OutputSegmentationMasks;
      _outputSegmentationMasksInput.onValueChanged.AddListener(delegate { ToggleOutputSegmentationMasks(); });
    }

    private void InitializeVisibleBodyParts()
    {
      var template = _outputSegmentationMasksInput.transform.parent.gameObject;
      var parent = template.transform.parent;

      foreach (var option in _bodyPartOptions)
      {
        var row = Instantiate(template, parent);
        row.name = $"Visible {option.label}";

        var labels = row.GetComponentsInChildren<Text>(true);
        if (labels.Length > 0)
        {
          labels[0].text = $"Show {option.label}";
        }

        var toggle = row.GetComponentInChildren<Toggle>(true);
        if (toggle == null)
        {
          continue;
        }

        toggle.onValueChanged.RemoveAllListeners();
        toggle.isOn = _config.VisibleBodyParts.HasFlag(option.part);
        toggle.onValueChanged.AddListener((isOn) => SetBodyPartVisible(option.part, isOn));
      }
    }

    private void SetBodyPartVisible(PoseLandmarkListAnnotation.BodyParts bodyPart, bool isVisible)
    {
      if (isVisible)
      {
        _config.VisibleBodyParts |= bodyPart;
      }
      else
      {
        _config.VisibleBodyParts &= ~bodyPart;
      }
      _runner.VisibleBodyParts = _config.VisibleBodyParts;
    }
  }
}
