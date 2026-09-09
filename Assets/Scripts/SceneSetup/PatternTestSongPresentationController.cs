using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public sealed class PatternTestSongPresentationController : MonoBehaviour
{
    private const string SceneName = "Pattern Test";
    private const string BackgroundFolder = "PatternTest/Backgrounds";

    private GameObject _background;
    private Material _material;
    private RenderTexture _videoTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneName &&
            FindFirstObjectByType<PatternTestSongPresentationController>() == null)
        {
            new GameObject(nameof(PatternTestSongPresentationController))
                .AddComponent<PatternTestSongPresentationController>();
        }
    }

    private IEnumerator Start()
    {
        yield return null;

        SongSessionController session = SongSessionController.Instance;
        if (session == null || !session.HasSelectedSong)
        {
            Debug.LogWarning("Pattern Test presentation: no selected song; scene defaults are kept.", this);
            yield break;
        }

        SongData song = session.SelectedSong;
        if (!string.IsNullOrWhiteSpace(song.backgroundPath))
        {
            yield return LoadBackground(song.backgroundPath);
        }
    }

    private IEnumerator LoadBackground(string configuredPath)
    {
        string fullPath = ResolveBackgroundPath(configuredPath);
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"Pattern Test background file not found: {fullPath}", this);
            yield break;
        }

        Camera camera = FindFinalOutputCamera();
        if (camera == null)
        {
            Debug.LogError("Pattern Test background: no camera found.", this);
            yield break;
        }

        CreateBackground(camera);
        string extension = Path.GetExtension(fullPath).ToLowerInvariant();
        if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
        {
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(ToFileUri(fullPath), false))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Pattern Test background image load failed: {request.error}", this);
                    yield break;
                }
                _material.mainTexture = DownloadHandlerTexture.GetContent(request);
                Debug.Log($"Pattern Test background image loaded: {fullPath} (camera={camera.name})", this);
            }
        }
        else if (extension == ".mp4" || extension == ".webm" || extension == ".mov")
        {
            _videoTexture = new RenderTexture(
                Mathf.Max(Screen.width, 1280), Mathf.Max(Screen.height, 720), 0,
                RenderTextureFormat.ARGB32);
            _material.mainTexture = _videoTexture;

            VideoPlayer player = _background.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = true;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = _videoTexture;
            player.url = fullPath;
            player.errorReceived += (_, error) =>
                Debug.LogError($"Pattern Test background video failed: {error}", this);
            player.prepareCompleted += preparedPlayer => preparedPlayer.Play();
            player.Prepare();
            Debug.Log($"Pattern Test background video preparing: {fullPath} (camera={camera.name})", this);
        }
        else
        {
            Debug.LogError($"Unsupported Pattern Test background format: {extension}", this);
        }
    }

    private void CreateBackground(Camera camera)
    {
        _background = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _background.name = "Song Background";
        Destroy(_background.GetComponent<Collider>());

        float distance = Mathf.Min(100f, camera.farClipPlane * 0.8f);
        float height = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Transform target = _background.transform;
        target.SetPositionAndRotation(
            camera.transform.position + camera.transform.forward * distance,
            camera.transform.rotation);
        target.localScale = new Vector3(height * camera.aspect, height, 1f);

        Shader shader = Shader.Find("Unlit/Texture");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        _material = new Material(shader) { name = "Song Background Material" };
        _background.GetComponent<MeshRenderer>().sharedMaterial = _material;
    }

    private static Camera FindFinalOutputCamera()
    {
        Camera[] cameras = FindObjectsByType<Camera>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Camera best = null;
        foreach (Camera candidate in cameras)
        {
            if (!candidate.enabled || candidate.targetTexture != null)
            {
                continue;
            }
            if (best == null || candidate.depth > best.depth)
            {
                best = candidate;
            }
        }
        return best != null ? best : Camera.main;
    }

    private static string ResolveBackgroundPath(string configuredPath)
    {
        string path = configuredPath.Trim().Replace("\\", "/");
        if (Path.IsPathRooted(path))
        {
            return path;
        }
        if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, path));
        }
        if (path.Contains("/"))
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, path));
        }
        return Path.Combine(Application.streamingAssetsPath, BackgroundFolder, path);
    }

    private static string ToFileUri(string path)
    {
        return "file:///" + path.Replace("\\", "/");
    }

    private void OnDestroy()
    {
        if (_videoTexture != null)
        {
            _videoTexture.Release();
            Destroy(_videoTexture);
        }
        if (_material != null)
        {
            Destroy(_material);
        }
    }
}
