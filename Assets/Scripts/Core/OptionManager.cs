// 곡 선택 화면의 옵션 패널을 관리한다. 볼륨·아바타·목표 FPS를 저장하고 UI에 반영한다.
// 해상도 항목은 현재 1920×1080 하나만 제공하므로 선택 기능은 비활성화된다.

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;

public class OptionManager : MonoBehaviour
{
    [Header("Option UI")]
    [SerializeField]
    private GameObject optionPanel;

    [SerializeField]
    private Slider musicSlider;

    [SerializeField] private TMP_Dropdown avatarDropdown;

    [Header("Music")]
    [SerializeField]
    private AudioSource musicAudioSource;

    private TMP_Text frameRateLabel;
    private TMP_Dropdown resolutionDropdown;

    private bool isOptionOpen = false;

    public bool IsOptionOpen => optionPanel != null && optionPanel.activeInHierarchy;

    private void Start()
    {
        if (avatarDropdown != null)
        {
            var names = new List<string>();
            for (int i = 0; i < AvatarCatalog.Count; i++) names.Add(AvatarCatalog.GetName(i));
            avatarDropdown.ClearOptions();
            avatarDropdown.AddOptions(names);
            avatarDropdown.SetValueWithoutNotify(AvatarCatalog.IndexOf(AvatarSelectionSettings.Load()));
            avatarDropdown.RefreshShownValue();
        }
        CreateFrameRateButton();
        CreateResolutionDropdown();
        float savedVolume = MusicVolumeSettings.LoadVolume();

        musicSlider.minValue = 0f;
        musicSlider.maxValue = 1f;
        musicSlider.value = savedVolume;

        // 현재 곡 선택 화면 음악에도 저장된 볼륨 적용
        if (musicAudioSource != null)
        {
            musicAudioSource.volume = savedVolume;
        }

        // 슬라이더 움직일 때 실시간으로 볼륨 변경
        musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

        optionPanel.SetActive(false);
        isOptionOpen = false;
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isOptionOpen)
            {
                CloseOption();
            }
            else
            {
                OpenOption();
            }
        }
    }

    private void OpenOption()
    {
        optionPanel.SetActive(true);
        isOptionOpen = true;
    }

    private void CloseOption()
    {
        if (avatarDropdown != null)
        {
            avatarDropdown.Hide();
            AvatarSelectionSettings.Save(AvatarCatalog.GetId(avatarDropdown.value));
        }
        if (resolutionDropdown != null) resolutionDropdown.Hide();
        SaveMusicVolume();

        optionPanel.SetActive(false);
        isOptionOpen = false;
    }

    // 기존 아바타 드롭다운의 스타일을 재사용한다. 현재 지원 해상도가 하나이므로 선택은 비활성화된다.
    private void CreateResolutionDropdown()
    {
        if (optionPanel == null || avatarDropdown == null) return;

        // Reuse the option panel's font, item template and keyboard navigation.
        resolutionDropdown = Instantiate(avatarDropdown, optionPanel.transform);
        resolutionDropdown.name = "Resolution Dropdown";
        resolutionDropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        RectTransform rect = (RectTransform)resolutionDropdown.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 190f);
        rect.sizeDelta = new Vector2(640f, 72f);
        rect.localScale = Vector3.one;

        // Open upward so the list fits above the bottom of the options panel.
        RectTransform template = resolutionDropdown.template;
        template.anchorMin = new Vector2(0f, 1f);
        template.anchorMax = new Vector2(1f, 1f);
        template.pivot = new Vector2(0.5f, 0f);
        template.anchoredPosition = new Vector2(0f, 2f);
        template.sizeDelta = new Vector2(0f, 320f);
        template.gameObject.SetActive(false);

        var options = new List<string>();
        for (int i = 0; i < DisplaySettings.Count; i++)
        {
            Vector2Int size = DisplaySettings.GetResolution(i);
            options.Add("Resolution: " + size.x + " x " + size.y);
        }
        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.SetValueWithoutNotify(DisplaySettings.LoadIndex());
        resolutionDropdown.RefreshShownValue();
        resolutionDropdown.interactable = DisplaySettings.Count > 1;
        resolutionDropdown.onValueChanged.AddListener(DisplaySettings.Save);
    }

    private void CreateFrameRateButton()
    {
        if (optionPanel == null) return;

        GameObject buttonObject = new GameObject("Frame Rate Button",
            typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.layer = optionPanel.layer;
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(optionPanel.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 100f);
        rect.sizeDelta = new Vector2(640f, 72f);

        Image background = buttonObject.GetComponent<Image>();
        background.color = new Color(0.16f, 0.18f, 0.24f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(ToggleFrameRate);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.layer = optionPanel.layer;
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 4f);
        labelRect.offsetMax = new Vector2(-12f, -4f);
        frameRateLabel = labelObject.AddComponent<TextMeshProUGUI>();
        if (avatarDropdown != null && avatarDropdown.captionText != null)
            frameRateLabel.font = avatarDropdown.captionText.font;
        frameRateLabel.fontSize = 30f;
        frameRateLabel.alignment = TextAlignmentOptions.Center;
        frameRateLabel.color = Color.white;
        frameRateLabel.raycastTarget = false;
        RefreshFrameRateLabel();
    }

    private void ToggleFrameRate()
    {
        FrameRateSettings.Save(FrameRateSettings.Load() == 60 ? 120 : 60);
        RefreshFrameRateLabel();
    }

    private void RefreshFrameRateLabel()
    {
        int current = FrameRateSettings.Load();
        frameRateLabel.text = "FPS: " + current + "   |   Switch to " + (current == 60 ? 120 : 60);
    }

    private void OnMusicVolumeChanged(float volume)
    {
        // Option 화면에서 즉시 볼륨 변화를 들을 수 있도록 함
        if (musicAudioSource != null)
        {
            musicAudioSource.volume = volume;
        }
    }

    private void SaveMusicVolume()
    {
        MusicVolumeSettings.SaveVolume(musicSlider.value);
    }
}