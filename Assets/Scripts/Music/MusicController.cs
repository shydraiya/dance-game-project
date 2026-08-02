using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;

[RequireComponent(typeof(AudioSource))]
public class MusicController : MonoBehaviour
{
    private const string DefaultMusicFolder = "Musics/wav";

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool loop;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        audioSource.loop = loop;
    }

    private void Start()
    {
        if (playOnStart)
        {
            PlayFromSongSession();
        }
    }

    public void PlayFromSongSession()
    {
        SongSessionController session = SongSessionController.Instance;

        if (session == null || !session.HasSelectedSong)
        {
            Debug.LogWarning("MusicController: No selected song session found.", this);
            return;
        }

        string musicPath = session.SelectedSong.musicPath;

        if (string.IsNullOrWhiteSpace(musicPath))
        {
            Debug.LogWarning($"MusicController: Music path is empty. song={session.SelectedSong.title}", this);
            return;
        }

        StartCoroutine(LoadAndPlayRoutine(musicPath));
    }

    private IEnumerator LoadAndPlayRoutine(string musicPath)
    {
        string fullPath = ResolveMusicPath(musicPath);

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"MusicController: Music file not found. path={fullPath}", this);
            yield break;
        }

        AudioType audioType = GetAudioType(fullPath);
        string uri = "file:///" + fullPath.Replace("\\", "/");

        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uri, audioType))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"MusicController: Failed to load music. path={fullPath}\n{request.error}", this);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            clip.name = Path.GetFileNameWithoutExtension(fullPath);

            audioSource.clip = clip;
            audioSource.Play();

            Debug.Log($"MusicController: Playing music. path={musicPath}", this);
        }
    }

    private static string ResolveMusicPath(string musicPath)
    {
        if (Path.IsPathRooted(musicPath))
        {
            return musicPath;
        }

        string normalizedPath = musicPath.Replace("\\", "/").TrimStart('/');

        if (normalizedPath.StartsWith("Assets/"))
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, normalizedPath);
        }

        if (normalizedPath.Contains("/"))
        {
            return Path.Combine(Application.dataPath, normalizedPath);
        }

        return Path.Combine(Application.dataPath, DefaultMusicFolder, normalizedPath);
    }

    private static AudioType GetAudioType(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();

        switch (extension)
        {
            case ".wav":
                return AudioType.WAV;
            case ".ogg":
                return AudioType.OGGVORBIS;
            case ".mp3":
            case ".m4a":
                return AudioType.MPEG;
            default:
                return AudioType.UNKNOWN;
        }
    }
}
