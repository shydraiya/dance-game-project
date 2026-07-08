using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SongSelectManager : MonoBehaviour
{
    [Header("Song Data")]
    [SerializeField] private TextAsset songDataCsv;

    [Tooltip("처음 선택될 곡의 List 인덱스. 0이면 CSV 첫 번째 곡.")]
    [SerializeField] private int initialSelectedIndex = 0;

    [Tooltip("마지막 곡에서 아래를 누르면 첫 곡으로 돌아갈지 여부")]
    [SerializeField] private bool loopSongList = true;

    [Tooltip("곡이 선택되면 이동할 씬의 이름")]
    [Header("Scene Flow")]
    [SerializeField] private string gameSceneName = "Game";

    [Header("UI References")]
    [SerializeField] private SongDescUI songDescUI;
    [SerializeField] private RectTransform songListContent;
    [SerializeField] private SongRowUI songRowPrefab;

    [Header("List Layout")]
    [Tooltip("화면에 실제로 보일 곡 개수. 반드시 홀수 권장.")]
    [SerializeField] private int visibleRowCount = 7;

    [Tooltip("위와 아래에 둘 숨겨진 버퍼 슬롯 수.")]
    [SerializeField] private int bufferRowCount = 1;

    [SerializeField] private Vector2 rowSize = new Vector2(950f, 105f);

    [Tooltip("SongRow 중심점 사이의 세로 간격")]
    [SerializeField] private float rowSpacing = 120f;

    [Header("Visual")]
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float selectedScale = 1f;

    [Header("Scroll Animation")]
    [SerializeField] private float scrollDuration = 0.16f;

    private List<SongData> songs = new List<SongData>();
    private List<SongRowUI> rows = new List<SongRowUI>();

    private int selectedIndex;
    private bool isScrolling;

    private int TotalRowCount => visibleRowCount + bufferRowCount * 2;

    // 생성된 SongRow 배열에서 중앙 선택 곡이 위치하는 인덱스
    private int CenterRowIndex => bufferRowCount + visibleRowCount / 2;

    private void Start()
    {
        LoadSongData();

        if (!ValidateSetup())
            return;

        selectedIndex = loopSongList
            ? Mod(initialSelectedIndex, songs.Count)
            : Mathf.Clamp(initialSelectedIndex, 0, songs.Count - 1);

        CreateRows();
        RefreshAllRows();
        RefreshSongDescription();
    }

    private void Update()
    {
        if (isScrolling || Keyboard.current == null)
            return;

        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            MoveUp();
        }
        else if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            MoveDown();
        }
        else if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            ConfirmSelection();
        }
    }

    public void MoveUp()
    {
        TryMove(-1);
    }

    public void MoveDown()
    {
        TryMove(1);
    }

    public void ConfirmSelection()
    {
        SongData selectedSong = GetSelectedSong();

        if (selectedSong == null)
        {
            Debug.LogError("Selected song is missing.", this);
            return;
        }

        SongSessionController.GetOrCreate().SetSelectedSong(selectedSong);
        SceneManager.LoadScene(gameSceneName);
    }

    public SongData GetSelectedSong()
    {
        return GetSongByIndex(selectedIndex);
    }

    private void LoadSongData()
    {
        /*
         * 아래 줄은 현재 사용하는 CSV 로더의 실제 클래스명/함수명에 맞추면 됨.
         *
         * 이전에 SongDataLoader.cs를 만들었다면:
         * songs = SongDataLoader.Load(songDataCsv);
         *
         * 이전 이름이 SongCsvLoader.cs라면:
         * songs = SongCsvLoader.Load(songDataCsv);
         */

        songs = SongDataLoader.Load(songDataCsv);

        if (songs == null)
            songs = new List<SongData>();
    }

    private bool ValidateSetup()
    {
        if (songDataCsv == null)
        {
            Debug.LogError("Song Data Csv가 SongSelectManager에 연결되지 않았습니다.");
            return false;
        }

        if (songs.Count == 0)
        {
            Debug.LogError("SongData.csv에서 읽어온 곡 데이터가 없습니다.");
            return false;
        }

        if (songListContent == null)
        {
            Debug.LogError("Song List Content가 연결되지 않았습니다.");
            return false;
        }

        if (songRowPrefab == null)
        {
            Debug.LogError("Song Row Prefab이 연결되지 않았습니다.");
            return false;
        }

        if (songDescUI == null)
        {
            Debug.LogError("Song Desc UI가 연결되지 않았습니다.");
            return false;
        }

        if (visibleRowCount < 1 || visibleRowCount % 2 == 0)
        {
            Debug.LogError("Visible Row Count는 1 이상의 홀수여야 합니다. 예: 5, 7, 9");
            return false;
        }

        if (bufferRowCount < 1)
        {
            Debug.LogError("Buffer Row Count는 최소 1이어야 합니다.");
            return false;
        }

        return true;
    }

    private void CreateRows()
    {
        if (songListContent.childCount > 0)
        {
            Debug.LogWarning(
                "SongListContent 안에 기존 오브젝트가 있습니다. " +
                "SongRow 프리팹 원본은 Scene에서 제거하고 Content를 비워두는 것을 권장합니다."
            );
        }

        for (int i = 0; i < TotalRowCount; i++)
        {
            SongRowUI row = Instantiate(songRowPrefab, songListContent);

            row.name = $"SongRow_{i}";
            rows.Add(row);
        }
    }

    private void TryMove(int direction)
    {
        if (isScrolling || songs.Count <= 1)
            return;

        int nextIndex = GetNextSelectedIndex(direction);

        // loopSongList가 꺼진 상태에서 첫 곡/마지막 곡을 넘어가려는 경우
        if (!loopSongList && nextIndex == selectedIndex)
            return;

        StartCoroutine(ScrollRoutine(direction, nextIndex));
    }

    private IEnumerator ScrollRoutine(int direction, int nextIndex)
    {
        isScrolling = true;

        // 이동할 곡에 선택 프레임을 미리 붙여서
        // 해당 행이 중앙으로 들어오는 느낌을 줌.
        SetIncomingRowSelected(direction);

        Vector2[] startPositions = new Vector2[rows.Count];
        Vector2[] targetPositions = new Vector2[rows.Count];

        float[] startScales = new float[rows.Count];
        float[] targetScales = new float[rows.Count];

        float[] startAlphas = new float[rows.Count];
        float[] targetAlphas = new float[rows.Count];

        for (int i = 0; i < rows.Count; i++)
        {
            int startOffset = i - CenterRowIndex;
            int targetOffset = startOffset - direction;

            startPositions[i] = GetPositionFromOffset(startOffset);
            targetPositions[i] = GetPositionFromOffset(targetOffset);

            startScales[i] = GetScaleFromOffset(startOffset);
            targetScales[i] = GetScaleFromOffset(targetOffset);

            startAlphas[i] = GetAlphaFromOffset(startOffset);
            targetAlphas[i] = GetAlphaFromOffset(targetOffset);
        }

        float elapsed = 0f;

        while (elapsed < scrollDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / scrollDuration);

            // Ease Out Cubic
            t = 1f - Mathf.Pow(1f - t, 3f);

            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].SetVisual(
                    Vector2.Lerp(startPositions[i], targetPositions[i], t),
                    rowSize,
                    Mathf.Lerp(startScales[i], targetScales[i], t),
                    Mathf.Lerp(startAlphas[i], targetAlphas[i], t)
                );
            }

            yield return null;
        }

        selectedIndex = nextIndex;

        RecycleRow(direction);

        // 재활용된 슬롯을 포함해 위치, 제목, 선택 프레임을 최종 상태로 갱신
        RefreshAllRows();
        RefreshSongDescription();

        isScrolling = false;
    }

    private void RecycleRow(int direction)
    {
        if (direction > 0)
        {
            // 아래 방향키:
            // 가장 위에 있던 슬롯을 맨 아래로 이동
            SongRowUI topRow = rows[0];

            rows.RemoveAt(0);
            rows.Add(topRow);
        }
        else
        {
            // 위 방향키:
            // 가장 아래에 있던 슬롯을 맨 위로 이동
            SongRowUI bottomRow = rows[rows.Count - 1];

            rows.RemoveAt(rows.Count - 1);
            rows.Insert(0, bottomRow);
        }
    }

    private void RefreshAllRows()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            SongRowUI row = rows[i];
            SongData song = GetSongForRowIndex(i);

            int offset = i - CenterRowIndex;

            if (song == null)
            {
                row.SetSelected(false);
                row.gameObject.SetActive(false);
                continue;
            }

            row.gameObject.SetActive(true);

            row.Bind(song);

            row.SetVisual(
                GetPositionFromOffset(offset),
                rowSize,
                GetScaleFromOffset(offset),
                GetAlphaFromOffset(offset)
            );

            row.SetSelected(i == CenterRowIndex);
        }
    }

    private void RefreshSongDescription()
    {
        songDescUI.Bind(songs[selectedIndex]);
    }

    private void SetIncomingRowSelected(int direction)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].SetSelected(false);
        }

        int incomingRowIndex = CenterRowIndex + direction;

        if (incomingRowIndex < 0 || incomingRowIndex >= rows.Count)
            return;

        if (rows[incomingRowIndex].gameObject.activeSelf)
        {
            rows[incomingRowIndex].SetSelected(true);
        }
    }

    private SongData GetSongForRowIndex(int rowIndex)
    {
        int offset = rowIndex - CenterRowIndex;
        int songIndex = selectedIndex + offset;

        return GetSongByIndex(songIndex);
    }

    private SongData GetSongByIndex(int index)
    {
        if (songs.Count == 0)
            return null;

        if (loopSongList)
        {
            return songs[Mod(index, songs.Count)];
        }

        if (index < 0 || index >= songs.Count)
        {
            return null;
        }

        return songs[index];
    }

    private int GetNextSelectedIndex(int direction)
    {
        if (loopSongList)
        {
            return Mod(selectedIndex + direction, songs.Count);
        }

        return Mathf.Clamp(selectedIndex + direction, 0, songs.Count - 1);
    }

    private Vector2 GetPositionFromOffset(int offset)
    {
        // offset = 0이면 중앙
        // 음수면 위쪽, 양수면 아래쪽
        return new Vector2(0f, -offset * rowSpacing);
    }

    private float GetScaleFromOffset(int offset)
    {
        return offset == 0 ? selectedScale : normalScale;
    }

    private float GetAlphaFromOffset(int offset)
    {
        int visibleHalf = visibleRowCount / 2;

        // 버퍼 슬롯은 기본적으로 투명.
        // 스크롤 도중 화면 안으로 들어오며 자연스럽게 나타남.
        return Mathf.Abs(offset) <= visibleHalf ? 1f : 0f;
    }

    private int Mod(int value, int modulo)
    {
        return (value % modulo + modulo) % modulo;
    }
}
