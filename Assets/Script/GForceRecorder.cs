using UnityEngine;
using UnityEngine.UI; // 버튼 텍스트 바꾸기 위해 필요
using TMPro; 
using System.IO;
using System.Text;

public class GForceRecorder : MonoBehaviour
{
    [Header("1. 연결 설정")]
    public DualMotionMixer mixer;   // 믹서기 연결
    public TMP_Text buttonText;         // 버튼 안의 텍스트 (옵션)

    // 내부 변수
    private StringBuilder sb = new StringBuilder();
    private bool isRecording = false;
    private float recordStartTime;

    // 버튼을 누르면 이 함수가 실행됨
    public void OnToggleButton()
    {
        if (isRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    void StartRecording()
    {
        // 1. 메모리 초기화
        sb.Clear();

        // 2. CSV 헤더 작성
        sb.AppendLine("Time,Car_Surge,Ship_Surge,Total_Surge,Car_Sway,Ship_Sway,Total_Sway,Total_Heave");

        // 3. 상태 변경
        isRecording = true;
        recordStartTime = Time.time;

        // 4. 버튼 글자 바꾸기 (시각적 피드백)
        if (buttonText != null)
        {
            buttonText.text = "STOP & SAVE";
            buttonText.color = Color.red;
        }

        Debug.Log(" 녹화 시작!");
    }

    void StopRecording()
    {
        // 1. 상태 변경
        isRecording = false;

        // 2. 파일 이름 생성 (G_Log_2026-01-30_210530.csv 형식)
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName = $"G_Log_{timestamp}.csv";
        string filePath = Path.Combine(Application.dataPath, "../", fileName);

        // 3. 파일 저장
        try
        {
            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($" 저장 완료! 파일명: {fileName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($" 저장 실패: {e.Message}");
        }

        // 4. 버튼 글자 원상복구
        if (buttonText != null)
        {
            buttonText.text = "RECORD START";
            buttonText.color = Color.black;
        }
    }

    void Update()
    {
        // 녹화 중일 때만 데이터를 쌓음
        if (!isRecording || mixer == null) return;

        float t = Time.time - recordStartTime;

        // 데이터 수집 (Surge)
        float c_surge = (mixer.carScript != null) ? mixer.carScript.GetSurgeG() : 0;
        float s_surge = (mixer.shipScript != null) ? mixer.shipScript.GetSurgeG() : 0;
        float t_surge = mixer.GetSurgeG();

        // 데이터 수집 (Sway)
        float c_sway = (mixer.carScript != null) ? mixer.carScript.GetSwayG() : 0;
        float s_sway = (mixer.shipScript != null) ? mixer.shipScript.GetSwayG() : 0;
        float t_sway = mixer.GetSwayG();

        // 데이터 수집 (Heave)
        float t_heave = mixer.GetHeaveG();

        // CSV 포맷으로 한 줄 추가
        sb.AppendLine($"{t:F3},{c_surge:F4},{s_surge:F4},{t_surge:F4},{c_sway:F4},{s_sway:F4},{t_sway:F4},{t_heave:F4}");
    }
}