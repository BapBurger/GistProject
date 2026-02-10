using UnityEngine;
using System.IO.Ports;
using System;

public class RealSeatBridge : MonoBehaviour
{
    [Header("0. 테스트 및 제어")]
    [Tooltip("R키: 추정 위치를 0으로 리셋")]
    public KeyCode resetKey = KeyCode.R;
    public bool enableMotor1 = true;
    public bool enableMotor2 = true;
    public bool enableMotor3 = true;
    public bool enableMotor4 = true;

    [Header("=== 모니터링: 추정 위치 (스텝) ===")]
    public float monitorPos1 = 0;
    public float monitorPos2 = 0;
    public float monitorPos3 = 0;
    public float monitorPos4 = 0;

    [Header("=== 모니터링: 목표 위치 (스텝) ===")]
    public float monitorTarget1 = 0;
    public float monitorTarget2 = 0;
    public float monitorTarget3 = 0;
    public float monitorTarget4 = 0;

    [Header("=== 모니터링: 전송 속도 (PWM) ===")]
    public int monitorSpeed1 = 0;
    public int monitorSpeed2 = 0;
    public int monitorSpeed3 = 0;
    public int monitorSpeed4 = 0;

    [Header("1. 시리얼 설정")]
    public string portName = "COM6";
    public int baudRate = 115200;

    [Header("2. 컨트롤러 연결")]
    public SeatController seatController;

    [Header("3. 변환 비율 (Unity값 → 가상 스텝)")]
    [Tooltip("거리(Slide/Heave)용: Unity 오프셋이 작으므로(~0.1) 큰 값 필요")]
    public float distanceRatio = 5000f;
    [Tooltip("각도(Back/Bottom)용: Unity 오프셋이 크므로(~10°) 작은 값 필요")]
    public float angleRatio = 200f;

    [Header("4. P제어 설정")]
    [Tooltip("비례 게인: 오차(스텝) × kP = PWM값. 높으면 빠른 반응, 낮으면 부드러운 반응")]
    public float kP = 0.5f;

    [Tooltip("소음 방지 최소 PWM: 이 값 미만의 속도는 즉시 0 차단")]
    public int minPWM = 130;

    [Header("5. Dead Reckoning (위치 추정)")]
    [Tooltip("PWM 255일 때 초당 추정 이동 스텝 수. 실제 모터 속도에 맞게 튜닝")]
    public float stepsPerSecondAtMax = 500f;

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

    // ── private ──
    private SerialPort sp;
    private string lastPacket = "";
    private float[] estimatedPos = new float[4];
    private int[] lastSpeed = new int[4];
    private float[] targetSteps = new float[4];

    void Start()
    {
        OpenConnection();
        for (int i = 0; i < 4; i++)
        {
            estimatedPos[i] = 0f;
            lastSpeed[i] = 0;
            targetSteps[i] = 0f;
        }
    }

    void Update()
    {
        // 리셋
        if (Input.GetKeyDown(resetKey))
        {
            for (int i = 0; i < 4; i++) estimatedPos[i] = 0f;
            Debug.Log("<color=cyan>[RealSeatBridge] Position Reset to 0</color>");
        }

        if (sp == null || !sp.IsOpen || seatController == null) return;

        // ── 1. Dead Reckoning: 지난 프레임의 속도로 추정 위치 갱신 ──
        float dt = Time.deltaTime;
        for (int i = 0; i < 4; i++)
        {
            estimatedPos[i] += (lastSpeed[i] / 255f) * stepsPerSecondAtMax * dt;
        }

        // ── 2. 각 모터 속도 계산 ──
        int s1 = enableMotor1 ? CalcSpeed(0, motor1_Index, limit1, distanceRatio, reverse1) : 0;
        int s2 = enableMotor2 ? CalcSpeed(1, motor2_Index, limit2, angleRatio,    reverse2) : 0;
        int s3 = enableMotor3 ? CalcSpeed(2, motor2_Index, limit3, angleRatio,    reverse3) : 0;
        int s4 = enableMotor4 ? CalcSpeed(3, motor4_Index, limit4, distanceRatio, reverse4) : 0;

        lastSpeed[0] = s1; lastSpeed[1] = s2;
        lastSpeed[2] = s3; lastSpeed[3] = s4;

        // ── 3. 패킷 전송 ──
        string packet = $"{s1},{s2},{s3},{s4}";
        SendPacket(packet);

        // ── 4. Inspector 모니터링 ──
        monitorPos1 = estimatedPos[0]; monitorPos2 = estimatedPos[1];
        monitorPos3 = estimatedPos[2]; monitorPos4 = estimatedPos[3];
        monitorTarget1 = targetSteps[0]; monitorTarget2 = targetSteps[1];
        monitorTarget3 = targetSteps[2]; monitorTarget4 = targetSteps[3];
        monitorSpeed1 = s1; monitorSpeed2 = s2;
        monitorSpeed3 = s3; monitorSpeed4 = s4;
    }

    /// <summary>
    /// P제어: 오차에 비례하는 속도(-255~+255)를 계산한다.
    /// |속도| < minPWM 이면 소음 방지를 위해 즉시 0 차단.
    /// </summary>
    int CalcSpeed(int motorIdx, int partIdx, int limit, float ratio, bool isReverse)
    {
        // 목표 스텝 계산
        float offset = GetSeatPartOffset(partIdx);
        float target = offset * ratio;
        if (isReverse) target *= -1f;
        target = Mathf.Clamp(target, -limit, limit);
        targetSteps[motorIdx] = target;

        // 추정 위치 클램프
        estimatedPos[motorIdx] = Mathf.Clamp(estimatedPos[motorIdx], -limit, limit);

        // P제어
        float error = target - estimatedPos[motorIdx];
        int speed = (int)Mathf.Clamp(error * kP, -255f, 255f);

        // ★ 소음 방지: |speed| < minPWM → 즉시 0 차단 ★
        if (speed > 0 && speed < minPWM) speed = 0;
        if (speed < 0 && speed > -minPWM) speed = 0;

        // 리밋 끝에서 더 이상 나가지 않도록
        if (speed > 0 && estimatedPos[motorIdx] >= limit) speed = 0;
        if (speed < 0 && estimatedPos[motorIdx] <= -limit) speed = 0;

        return speed;
    }

    float GetSeatPartOffset(int index)
    {
        if (index >= 0 && index < seatController.seatParts.Length)
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
        try
        {
            sp = new SerialPort(portName, baudRate);
            sp.Open();
            sp.ReadTimeout = 20;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RealSeatBridge] Connection Error: {e.Message}");
        }
    }

    void OnApplicationQuit()
    {
        if (sp != null && sp.IsOpen)
        {
            sp.WriteLine("0,0,0,0");
            sp.Close();
        }
    }
}
