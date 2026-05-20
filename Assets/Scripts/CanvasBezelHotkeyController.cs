using System.Collections.Generic;
using Mediapipe.Unity.Sample.UI;
using UnityEngine;
using UnityEngine.UI;

public class CanvasBezelHotkeyController : MonoBehaviour
{
  [SerializeField] private string _canvasName = "Main Canvas";
  [SerializeField] private string _headerPath = "Container Panel/Header";
  [SerializeField] private string _footerPath = "Container Panel/Footer";
  [SerializeField] private string _modalPath = "Modal Panel";
  [SerializeField] private bool _hideButtonsOnStart = true;
  [SerializeField] private bool _invokeButtonOnKeyPress = true;

  private readonly List<Button> _buttons = new();
  private Modal _modal;

  private void Start()
  {
    var canvas = GameObject.Find(_canvasName);
    if (canvas == null)
    {
      Debug.LogWarning($"{nameof(CanvasBezelHotkeyController)} could not find {_canvasName}");
      return;
    }

    var header = canvas.transform.Find(_headerPath);
    var footer = canvas.transform.Find(_footerPath);
    var modal = canvas.transform.Find(_modalPath);

    DisablePanelImage(header);
    DisablePanelImage(footer);
    _modal = modal == null ? null : modal.GetComponent<Modal>();

    AddFirstButton(header);
    AddButtons(footer);

    if (_hideButtonsOnStart)
    {
      foreach (var button in _buttons)
      {
        button.gameObject.SetActive(false);
      }
    }
  }

  private void Update()
  {
    if (Input.GetKeyDown(KeyCode.Alpha1))
    {
      InvokeButton(0);
    }
    if (Input.GetKeyDown(KeyCode.Alpha2))
    {
      InvokeButton(1);
    }
    if (Input.GetKeyDown(KeyCode.Alpha3))
    {
      InvokeButton(2);
    }
  }

  private static void DisablePanelImage(Transform panel)
  {
    if (panel == null)
    {
      return;
    }

    var image = panel.GetComponent<Image>();
    if (image != null)
    {
      image.enabled = false;
    }
  }

  private void AddFirstButton(Transform root)
  {
    if (root == null)
    {
      return;
    }

    var buttons = root.GetComponentsInChildren<Button>(true);
    if (buttons.Length > 0)
    {
      _buttons.Add(buttons[0]);
    }
  }

  private void AddButtons(Transform root)
  {
    if (root == null)
    {
      return;
    }

    foreach (var button in root.GetComponentsInChildren<Button>(true))
    {
      _buttons.Add(button);
    }
  }

  private void InvokeButton(int index)
  {
    if (!_invokeButtonOnKeyPress || index < 0 || index >= _buttons.Count)
    {
      return;
    }

    if (_modal != null && _modal.gameObject.activeSelf)
    {
      var contents = _modal.GetComponentInChildren<ModalContents>();
      if (contents != null)
      {
        contents.Exit();
      }
      else
      {
        _modal.CloseAndResume();
      }
      return;
    }

    _buttons[index].onClick.Invoke();
  }
}
