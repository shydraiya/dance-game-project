using UnityEngine;

public class SongSessionController : MonoBehaviour
{

    // main menu에서 선택한 곡의 정보를 게임 씬으로 넘겨줌
    public static SongSessionController Instance { get; private set; }

    [Header("Current Selection (Runtime)")]
    [SerializeField] private SongData _selectedSong;
    [SerializeField] private int _loadedPatternFrameCount;
    [SerializeField] private string _patternTimeRange = "No pattern loaded";
    [SerializeField] private string _sessionStatus = "No song selected";

    public SongData SelectedSong => _selectedSong;

    // pattern data 담는 배열
    public PatternFrame[] SelectedPatternFrames { get; private set; } = new PatternFrame[0];
    public bool HasSelectedSong => SelectedSong != null;
    public bool HasSelectedPattern => SelectedPatternFrames.Length > 0;

    // singleton 패턴. game 씬으로 넘어가도 남아서 정보를 전달함
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // sessionController 만드는 함수
    public static SongSessionController GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject sessionObject = new GameObject(nameof(SongSessionController));
        return sessionObject.AddComponent<SongSessionController>();
    }


    public void SetSelectedSong(SongData song)
    {
        _selectedSong = song;
        LoadSelectedPattern();
    }

    public void Clear()
    {
        _selectedSong = null;
        SelectedPatternFrames = new PatternFrame[0];
        UpdateInspectorStatus("No song selected");
    }


    // patternPath를 바탕으로 패턴을 불러옴
    public void LoadSelectedPattern()
    {
        if (SelectedSong == null)
        {
            SelectedPatternFrames = new PatternFrame[0];
            UpdateInspectorStatus("No song selected");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedSong.patternPath))
        {
            Debug.LogWarning($"Pattern path is empty: {SelectedSong.title}", this);
            SelectedPatternFrames = new PatternFrame[0];
            UpdateInspectorStatus("Pattern path is empty");
            return;
        }

        SelectedPatternFrames = PatternLoader.LoadPattern(SelectedSong.patternPath).ToArray();
        UpdateInspectorStatus(SelectedPatternFrames.Length > 0
            ? "Song and pattern loaded"
            : "Pattern could not be loaded");
    }

    private void UpdateInspectorStatus(string status)
    {
        _loadedPatternFrameCount = SelectedPatternFrames != null ? SelectedPatternFrames.Length : 0;
        _sessionStatus = status;

        if (_loadedPatternFrameCount == 0)
        {
            _patternTimeRange = "No pattern loaded";
            return;
        }

        float firstTime = SelectedPatternFrames[0].time;
        float lastTime = SelectedPatternFrames[_loadedPatternFrameCount - 1].time;
        _patternTimeRange = $"{firstTime:0.###} - {lastTime:0.###} sec";
    }
}
