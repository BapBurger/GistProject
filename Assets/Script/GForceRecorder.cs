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

        // 1. Raw Input (게인 적용 전, 순수 합계)
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

        // 캔슬링 모드 여부에 상관없이 Raw Input은 그냥 더해서 보여줌 (참고용)
        float input_surge = c_surge + s_surge;
        float input_sway = c_sway + s_sway;
        float input_heave = c_heave + s_heave;

        // 2. ★ Gain Adjusted (Master Gain + Cancellation이 모두 적용된 값)
        // SeatController가 계산해둔 모니터 값을 그대로 가져옵니다.
        float gain_surge = seatController.monitorSurgeG;
        float gain_sway = seatController.monitorSwayG;
        float gain_heave = seatController.monitorHeaveG;

        // 3. Output (실제 시트 움직임 역산)
        float out_surge = 0f, out_sway = 0f, out_heave = 0f;

        if (seatController.seatParts.Length > seatController.backSeatIndex)
        {
            float currentPitch = seatController.seatParts[seatController.backSeatIndex].currentValue;
            out_surge = Mathf.Sin(currentPitch * Mathf.Deg2Rad);
        }

        if (seatController.seatParts.Length > seatController.rightBolsterIndex &&
            seatController.seatParts.Length > seatController.leftBolsterIndex)
        {
            float r_val = seatController.seatParts[seatController.rightBolsterIndex].currentValue;
            float l_val = seatController.seatParts[seatController.leftBolsterIndex].currentValue;
            float combinedBolster = Mathf.Abs(r_val) - Mathf.Abs(l_val);
            if (Mathf.Abs(seatController.bolsterGain) > 0.001f)
                out_sway = combinedBolster / seatController.bolsterGain;
        }

        if (seatController.seatParts.Length > seatController.wholeLiftIndex)
        {
            float currentLift = seatController.seatParts[seatController.wholeLiftIndex].currentValue;
            if (Mathf.Abs(seatController.heaveGain) > 0.001f)
                out_heave = currentLift / seatController.heaveGain;
        }

        // CSV 저장
        sb.AppendLine($"{t:F3}," +
                      $"{input_surge:F4},{gain_surge:F4},{out_surge:F4}," +
                      $"{input_sway:F4},{gain_sway:F4},{out_sway:F4}," +
                      $"{input_heave:F4},{gain_heave:F4},{out_heave:F4}");
    }
}