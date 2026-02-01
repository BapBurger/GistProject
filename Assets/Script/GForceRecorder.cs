using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Text;
using System.Diagnostics; // Process.Start를 위해 필요 (Debug 충돌 원인)

public class GForceRecorder : MonoBehaviour
{
    [Header("1. 연결 설정")]
    public SeatController seatController;
    public TMP_Text buttonText;

    // 내부 변수
    private StringBuilder sb = new StringBuilder();
    private bool isRecording = false;
    private float recordStartTime;

    public void OnToggleButton()
    {
        if (isRecording) StopRecording();
        else StartRecording();
    }

    void StartRecording()
    {
        sb.Clear();
        // 헤더 작성: [입력값] -> [토글반영값] -> [★실제출력값(Output)]
        sb.AppendLine("Time," +
                      "Input_Surge,Applied_Surge,Output_Surge," +
                      "Input_Sway,Applied_Sway,Output_Sway," +
                      "Input_Heave,Applied_Heave,Output_Heave");

        isRecording = true;
        recordStartTime = Time.time;

        if (buttonText != null)
        {
            buttonText.text = "STOP & SAVE";
            buttonText.color = Color.red;
        }
        // [수정] UnityEngine.Debug 라고 명시
        UnityEngine.Debug.Log("Record Started.");
    }

    void StopRecording()
    {
        isRecording = false;

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName = $"G_Log_Full_{timestamp}.csv";
        string filePath = Path.Combine(Application.dataPath, "../", fileName);

        try
        {
            File.WriteAllText(filePath, sb.ToString());
            UnityEngine.Debug.Log($"Saved: {fileName}");

            // ▼▼▼ 파이썬 그래프 자동 실행 ▼▼▼
            string pythonScriptPath = Path.Combine(Application.dataPath, "../GForceVisualizer.py");

            if (File.Exists(pythonScriptPath))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "python"; // 혹은 "python3"
                startInfo.Arguments = $"\"{pythonScriptPath}\"";
                startInfo.UseShellExecute = true;
                startInfo.CreateNoWindow = false;

                Process.Start(startInfo);
                UnityEngine.Debug.Log("그래프 시각화 실행!");
            }
            else
            {
                UnityEngine.Debug.LogError($"파이썬 스크립트를 찾을 수 없습니다: {pythonScriptPath}");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Save/Run Failed: {e.Message}");
        }

        if (buttonText != null)
        {
            buttonText.text = "RECORD START";
            buttonText.color = Color.black;
        }
    }

    void Update()
    {
        if (!isRecording || seatController == null) return;

        float t = Time.time - recordStartTime;

        // ---------------------------------------------------
        // 1. Input Total (차 + 배 입력 총합)
        // ---------------------------------------------------
        float c_surge = 0, c_sway = 0, c_heave = 0;
        if (seatController.carSourceObj != null)
        {
            var carParams = seatController.carSourceObj.GetComponent<IMotionSource>();
            if (carParams != null) { c_surge = carParams.GetSurgeG(); c_sway = carParams.GetSwayG(); c_heave = carParams.GetHeaveG(); }
        }

        float s_surge = 0, s_sway = 0, s_heave = 0;
        if (seatController.shipSourceObj != null)
        {
            var shipParams = seatController.shipSourceObj.GetComponent<IMotionSource>();
            if (shipParams != null) { s_surge = shipParams.GetSurgeG(); s_sway = shipParams.GetSwayG(); s_heave = shipParams.GetHeaveG(); }
        }

        float input_surge = c_surge + s_surge;
        float input_sway = c_sway + s_sway;
        float input_heave = c_heave + s_heave;

        // ---------------------------------------------------
        // 2. Applied (토글 상태 반영)
        // ---------------------------------------------------
        float app_surge = 0, app_sway = 0, app_heave = 0;
        if (seatController.enableCar) { app_surge += c_surge; app_sway += c_sway; app_heave += c_heave; }
        if (seatController.enableShip) { app_surge += s_surge; app_sway += s_sway; app_heave += s_heave; }


        // ---------------------------------------------------
        // 3. ★ Output (실제 시트가 표현한 G값 역산)
        // ---------------------------------------------------
        float out_surge = 0f;
        float out_sway = 0f;
        float out_heave = 0f;

        // [Output Surge] 등받이 각도(Pitch)를 G값으로 환산
        if (seatController.seatParts.Length > seatController.backSeatIndex)
        {
            float currentPitch = seatController.seatParts[seatController.backSeatIndex].currentValue;
            out_surge = Mathf.Sin(currentPitch * Mathf.Deg2Rad);
        }

        // [Output Sway] 볼스터 움직임을 게인으로 나눠서 원본 G 추정
        if (seatController.seatParts.Length > seatController.rightBolsterIndex)
        {
            float currentBolster = seatController.seatParts[seatController.rightBolsterIndex].currentValue;
            if (Mathf.Abs(seatController.bolsterGain) > 0.001f)
                out_sway = currentBolster / seatController.bolsterGain;
        }

        // [Output Heave] 리프트 위치를 게인으로 나눠서 원본 G 추정
        if (seatController.seatParts.Length > seatController.wholeLiftIndex)
        {
            float currentLift = seatController.seatParts[seatController.wholeLiftIndex].currentValue;
            if (Mathf.Abs(seatController.heaveGain) > 0.001f)
                out_heave = currentLift / seatController.heaveGain;
        }

        // ---------------------------------------------------
        // 4. CSV 저장
        // ---------------------------------------------------
        sb.AppendLine($"{t:F3}," +
                      $"{input_surge:F4},{app_surge:F4},{out_surge:F4}," +
                      $"{input_sway:F4},{app_sway:F4},{out_sway:F4}," +
                      $"{input_heave:F4},{app_heave:F4},{out_heave:F4}");
    }
}