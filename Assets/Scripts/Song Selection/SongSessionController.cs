using UnityEngine;

public class SongSessionController : MonoBehaviour
{

    // main menu에서 선택한 곡의 정보를 게임 씬으로 넘겨줌
    public static SongSessionController Instance { get; private set; }

    public SongData SelectedSong { get; private set; }

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
        SelectedSong = song;
        LoadSelectedPattern();
    }

    public void Clear()
    {
        SelectedSong = null;
        SelectedPatternFrames = new PatternFrame[0];
    }


    // patternPath를 바탕으로 패턴을 불러옴
    public void LoadSelectedPattern()
    {
        if (SelectedSong == null)
        {
            SelectedPatternFrames = new PatternFrame[0];
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedSong.patternPath))
        {
            Debug.LogWarning($"Pattern path is empty: {SelectedSong.title}", this);
            SelectedPatternFrames = new PatternFrame[0];
            return;
        }

        SelectedPatternFrames = PatternLoader.LoadPattern(SelectedSong.patternPath).ToArray();
    }
}
