using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class OptionManager : MonoBehaviour
{
    [Header("Option UI")]
    [SerializeField]
    private GameObject optionPanel;

    [SerializeField]
    private Slider musicSlider;

    [Header("Music")]
    [SerializeField]
    private AudioSource musicAudioSource;

    private bool isOptionOpen = false;

    private void Start()
    {
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
        SaveMusicVolume();

        optionPanel.SetActive(false);
        isOptionOpen = false;
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