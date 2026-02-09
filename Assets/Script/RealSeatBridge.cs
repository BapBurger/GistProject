using UnityEngine;
using System.IO.Ports;
using System;
using System.Text;

public class RealSeatBridge : MonoBehaviour
{
    [Header("0. 테스트 및 리셋")]
    [Tooltip("R키: 현재 위치를 0점으로 강제 초기화")]
    public KeyCode resetKey = KeyCode.R;
    public bool enableMotor1 = true;
    public bool enableMotor2 = true;
    public bool enableMotor3 = true;
    public bool enableMotor4 = true;

    [Header("=== 모니터링 ===")]
    public int currentStep1 = 0;
    public int currentStep2 = 0;
    public int currentStep3 = 0;
    public int currentStep4 = 0;

    [Header("1. 연결 설정")]
    public string portName = "COM6";
    public int baudRate = 115200;

    [Header("2. 데이터 연결")]
    public SeatController seatController;

    [Header("3. 변환 비율 설정 (핵심!)")]
    [Tooltip("거리(Slide/Heave) 비율: 유니티 값이 작으므로(0.1) 큰 수를 곱해야 함")]
    public float distanceRatio = 5000f; // ★ Slide, Heave용 (보통 1000~5000)

    [Tooltip("각도(Back/Bottom) 비율: 유니티 값이 크므로(10도) 작은 수를 곱해야 함")]
    public float angleRatio = 200f;     // ★ Tilt용 (보통 100~500)

    [Header("4. 드드득 방지 (DeadZone)")]
    public int startThreshold = 80;
    public int stopThreshold = 10;

    [Header("Motor 1 : Slide (거리)")]
    public int motor1_Index = 0;
    public int limit1 = 3000;
    public bool reverse1 = false;

    [Header("Motor 2 : Back Seat (각도)")]
    public int motor2_Index = 1;
    public int limit2 = 3000;
    public bool reverse2 = false;

    [Header("Motor 3 : Bottom Seat (각도 - Motor 2 연동)")]
    public int limit3 = 3000;
    public bool reverse3 = false;

    [Header("Motor 4 : Heave (거리)")]
    public int motor4_Index = 6;
    public int limit4 = 3000;
    public bool reverse4 = false;

    private SerialPort sp;
    private string lastPacket = "";
    private int[] currentVirtualSteps = new int[4];
    private bool[] isMoving = new bool[4];

    void Start()
    {
        OpenConnection();
        for (int i = 0; i < 4; i++)
        {
            currentVirtualSteps[i] = 0;
            isMoving[i] = false;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(resetKey))
        {
            for (int i = 0; i < 4; i++) currentVirtualSteps[i] = 0;
            Debug.Log("<color=cyan>Position Reset to 0</color>");
        }

        if (sp != null && sp.IsOpen && seatController != null)
        {
            StringBuilder sb = new StringBuilder();

            // ★ 각 모터 성격에 맞는 Ratio 적용
            // Motor 1 (Slide) -> distanceRatio
            sb.Append(enableMotor1 ? FollowTarget(0, motor1_Index, limit1, distanceRatio, reverse1) : "0").Append(",");

            // Motor 2 (Back/Angle) -> angleRatio
            sb.Append(enableMotor2 ? FollowTarget(1, motor2_Index, limit2, angleRatio, reverse2) : "0").Append(",");

            // Motor 3 (Back/Angle) -> angleRatio
            sb.Append(enableMotor3 ? FollowTarget(2, motor2_Index, limit3, angleRatio, reverse3) : "0").Append(",");

            // Motor 4 (Heave) -> distanceRatio
            sb.Append(enableMotor4 ? FollowTarget(3, motor4_Index, limit4, distanceRatio, reverse4) : "0");

            string currentPacket = sb.ToString();
            SendPacket(currentPacket);

            currentStep1 = currentVirtualSteps[0];
            currentStep2 = currentVirtualSteps[1];
            currentStep3 = currentVirtualSteps[2];
            currentStep4 = currentVirtualSteps[3];
        }
    }

    // ★ Ratio를 매개변수로 받도록 수정됨
    string FollowTarget(int motorArrIdx, int partIdx, int limit, float ratio, bool isReverse)
    {
        float targetUnityValue = GetSeatPartOffset(partIdx);

        // 해당 모터에 맞는 비율(Ratio) 적용
        int targetStep = (int)(targetUnityValue * ratio);

        if (isReverse) targetStep *= -1;
        targetStep = Mathf.Clamp(targetStep, -limit, limit);

        int error = targetStep - currentVirtualSteps[motorArrIdx];
        int cmd = 0;
        int absError = Mathf.Abs(error);

        // 히스테리시스 로직
        if (isMoving[motorArrIdx])
        {
            if (absError > stopThreshold)
            {
                if (error > 0) cmd = 1; else cmd = -1;
            }
            else
            {
                cmd = 0;
                isMoving[motorArrIdx] = false;
            }
        }
        else
        {
            if (absError > startThreshold)
            {
                if (error > 0) cmd = 1; else cmd = -1;
                isMoving[motorArrIdx] = true;
            }
            else
            {
                cmd = 0;
            }
        }

        if (cmd == 1 && currentVirtualSteps[motorArrIdx] >= limit) cmd = 0;
        if (cmd == -1 && currentVirtualSteps[motorArrIdx] <= -limit) cmd = 0;

        if (cmd != 0) currentVirtualSteps[motorArrIdx] += cmd;

        return cmd.ToString();
    }

    float GetSeatPartOffset(int index)
    {
        if (seatController.seatParts.Length > index && index >= 0)
        {
            SeatPart part = seatController.seatParts[index];
            return part.currentValue - part.initialValue;
        }
        return 0f;
    }

    void SendPacket(string packet)
    {
        if (lastPacket != packet)
        {
            try { sp.WriteLine(packet); lastPacket = packet; } catch { }
        }
    }

    void OpenConnection()
    {
        try { sp = new SerialPort(portName, baudRate); sp.Open(); sp.ReadTimeout = 20; }
        catch (Exception e) { Debug.LogError($"Connection Error: {e.Message}"); }
    }

    void OnApplicationQuit()
    {
        if (sp != null && sp.IsOpen) { sp.WriteLine("0,0,0,0"); sp.Close(); }
    }
}