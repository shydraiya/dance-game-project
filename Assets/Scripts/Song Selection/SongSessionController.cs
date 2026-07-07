using UnityEngine;

public class SongSessionController : MonoBehaviour
{
    public static SongSessionController Instance { get; private set; }

    public SongData SelectedSong { get; private set; }
    public PatternFrame[] SelectedPatternFrames { get; private set; } = new PatternFrame[0];
    public bool HasSelectedSong => SelectedSong != null;
    public bool HasSelectedPattern => SelectedPatternFrames.Length > 0;

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
