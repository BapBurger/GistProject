using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Text;
using System.Diagnostics;

public class GForceRecorder : MonoBehaviour
{
    [Header("1. 연결 설정")]
    public SeatController seatController;
    public TMP_Text buttonText;

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
        // 헤더 수정: Applied -> Gain (명확하게 변경)
        sb.AppendLine("Time," +
                      "Input_Surge,Gain_Surge,Output_Surge," +
                      "Input_Sway,Gain_Sway,Output_Sway," +
                      "Input_Heave,Gain_Heave,Output_Heave");

        isRecording = true;
        recordStartTime = Time.time;

        if (buttonText != null)
        {
            buttonText.text = "STOP & SAVE";
            buttonText.color = Color.red;
        }
        UnityEngine.Debug.Log("Record Started.");
    }

    void StopRecording()
    {
        isRecording = false;
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName = $"G_Log_Gain_{timestamp}.csv"; // 파일명 변경
        string filePath = Path.Combine(Application.dataPath, "../", fileName);

        try
        {
            File.WriteAllText(filePath, sb.ToString());
            UnityEngine.Debug.Log($"Saved: {fileName}");

            // 파이썬 그래프 자동 실행
            string pythonScriptPath = Path.Combine(Application.dataPath, "../GForceVisualizer.py");
            if (File.Exists(pythonScriptPath))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "python";
                startInfo.Arguments = $"\"{pythonScriptPath}\"";
                startInfo.UseShellExecute = true;
                startInfo.CreateNoWindow = false;
                Process.Start(startInfo);
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

        // 1. Raw Input (게인 적용 전)
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

        // 2. Gain Adjusted (모니터 값 가져오기)
        float gain_surge = seatController.monitorSurgeG;
        float gain_sway = seatController.monitorSwayG;
        float gain_heave = seatController.monitorHeaveG;

        // ---------------------------------------------------
        // 3. ★ Output (여기를 수정했습니다!)
        // [수정] 절대 위치(currentValue)가 아니라 변화량(current - initial)을 사용
        // ---------------------------------------------------
        float out_surge = 0f, out_sway = 0f, out_heave = 0f;

        // [Output Surge] Pitch
        if (seatController.seatParts.Length > seatController.backSeatIndex)
        {
            // 각도는 보통 0도에서 시작하므로 그대로 둬도 되지만, 혹시 몰라 초기값 뺌
            var part = seatController.seatParts[seatController.backSeatIndex];
            float deltaAngle = part.currentValue - part.initialValue;
            out_surge = Mathf.Sin(deltaAngle * Mathf.Deg2Rad);
        }

        // [Output Sway] Bolster
        if (seatController.seatParts.Length > seatController.rightBolsterIndex &&
            seatController.seatParts.Length > seatController.leftBolsterIndex)
        {
            var r_part = seatController.seatParts[seatController.rightBolsterIndex];
            var l_part = seatController.seatParts[seatController.leftBolsterIndex];

            // 초기 위치 보정
            float r_delta = r_part.currentValue - r_part.initialValue;
            float l_delta = l_part.currentValue - l_part.initialValue;

            float combinedBolster = Mathf.Abs(r_delta) - Mathf.Abs(l_delta);
            if (Mathf.Abs(seatController.bolsterGain) > 0.001f)
                out_sway = combinedBolster / seatController.bolsterGain;
        }

        // [Output Heave] Lift (여기가 문제였음!)
        if (seatController.seatParts.Length > seatController.wholeLiftIndex)
        {
            var part = seatController.seatParts[seatController.wholeLiftIndex];

            // ★ 핵심: (현재 높이 - 초기 높이) / 게인
            float deltaLift = part.currentValue - part.initialValue;

            if (Mathf.Abs(seatController.heaveGain) > 0.001f)
                out_heave = deltaLift / seatController.heaveGain;
        }

        // CSV 저장
        sb.AppendLine($"{t:F3}," +
                      $"{input_surge:F4},{gain_surge:F4},{out_surge:F4}," +
                      $"{input_sway:F4},{gain_sway:F4},{out_sway:F4}," +
                      $"{input_heave:F4},{gain_heave:F4},{out_heave:F4}");
    }


}