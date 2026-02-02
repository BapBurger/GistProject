using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class RealDataReader : MonoBehaviour, IMotionSource
{
    [Header("1. 데이터 파일 설정")]
    [Tooltip("CSV 파일명을 적으세요 (확장자 제외). 파일은 반드시 'Assets/Resources' 폴더에 있어야 합니다.")]
    public string csvFileName = "vehicle_6_dof_imu";

    [Header("2. 데이터 조정")]
    [Tooltip("재생 속도 (1.0 = 정상 속도, 2.0 = 2배속)")]
    public float playbackSpeed = 1.0f;
    [Tooltip("데이터의 G단위 변환 (m/s^2 데이터라면 체크)")]
    public bool convertToG = true;

    // 내부 변수
    private List<Vector3> recordedData = new List<Vector3>();
    private float currentTimeIndex = 0f;
    private int maxIndex = 0;

    // 현재 프레임의 데이터
    private float currentSurge = 0f;
    private float currentSway = 0f;
    private float currentHeave = 0f;

    void Start()
    {
        LoadCSV();
    }

    void LoadCSV()
    {
        TextAsset csvData = Resources.Load<TextAsset>(csvFileName);
        if (csvData == null)
        {
            Debug.LogError($"[RealDataReader] Resources 폴더에서 '{csvFileName}' 파일을 찾을 수 없습니다!");
            return;
        }

        string[] lines = csvData.text.Split('\n');
        // 첫 번째 줄(헤더)은 건너뛰고 1부터 시작
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] values = line.Split(',');
            if (values.Length < 5) continue;

            try
            {
                // CSV 순서: srlnum, tm, x, y, z, ...
                float x_acc = float.Parse(values[2]);
                float y_acc = float.Parse(values[3]);
                float z_acc = float.Parse(values[4]);

                // 데이터 저장 (Surge=X, Sway=Y, Heave=Z)
                // Z축은 중력(9.81)을 빼서 0점을 맞춤
                Vector3 dataPoint = new Vector3(x_acc, y_acc, z_acc - 9.81f);

                // m/s^2 -> G 변환
                if (convertToG) dataPoint /= 9.81f;

                recordedData.Add(dataPoint);
            }
            catch { continue; }
        }
        maxIndex = recordedData.Count;
        Debug.Log($"[RealDataReader] 데이터 로드 완료: {maxIndex}개 프레임");
    }

    void Update()
    {
        if (maxIndex == 0) return;

        // 1. 시간 흐름에 따라 인덱스 증가 (60Hz 데이터 기준)
        // 실제 데이터 간격(약 0.016초)에 맞춰 인덱스를 이동시킵니다.
        // 여기서는 간단히 60fps라고 가정하고 Time.deltaTime * 60 * speed로 계산합니다.
        currentTimeIndex += Time.deltaTime * 60.0f * playbackSpeed;

        // 2. 루프 (끝나면 처음으로)
        if (currentTimeIndex >= maxIndex) currentTimeIndex = 0;

        // 3. 현재 데이터 가져오기
        int index = Mathf.FloorToInt(currentTimeIndex);
        Vector3 data = recordedData[index];

        currentSurge = data.x;
        currentSway = data.y;
        currentHeave = data.z;
    }

    // 인터페이스 구현 (SeatController가 가져가는 값)
    public float GetSurgeG() { return currentSurge; }
    public float GetSwayG() { return currentSway; }
    public float GetHeaveG() { return currentHeave; }
}