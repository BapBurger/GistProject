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

    [Header("=== 모니터링: 전송 명령 ===")]
    public int monitorCmd1 = 0;
    public int monitorCmd2 = 0;
    public int monitorCmd3 = 0;
    public int monitorCmd4 = 0;

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

    [Header("4. 제어 설정")]
    [Tooltip("정지 판정 데드존 (스텝): 오차가 이 이하면 도착 판단하고 정지")]
    public float deadZone = 30f;

    [Header("5. Dead Reckoning (위치 추정)")]
    [Tooltip("풀파워(255)일 때 초당 추정 이동 스텝 수. 실제 모터 속도에 맞게 튜닝")]
    public float stepsPerSecondAtMax = 1000f;

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
    private int[] lastCmd = new int[4];
    private float[] targetSteps = new float[4];

    void Start()
    {
        OpenConnection();
        for (int i = 0; i < 4; i++)
        {
            estimatedPos[i] = 0f;
            lastCmd[i] = 0;
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

        // ── 1. Dead Reckoning: 지난 프레임의 명령으로 추정 위치 갱신 ──
        float dt = Time.deltaTime;
        for (int i = 0; i < 4; i++)
        {
            // lastCmd는 -1, 0, +1 이므로 방향 × 풀스피드 × 시간
            estimatedPos[i] += lastCmd[i] * stepsPerSecondAtMax * dt;
        }

        // ── 2. 각 모터 명령 계산: 풀파워(+1) / 정지(0) / 풀파워역방향(-1) ──
        int c1 = enableMotor1 ? CalcCommand(0, motor1_Index, limit1, distanceRatio, reverse1) : 0;
        int c2 = enableMotor2 ? CalcCommand(1, motor2_Index, limit2, angleRatio,    reverse2) : 0;
        int c3 = enableMotor3 ? CalcCommand(2, motor2_Index, limit3, angleRatio,    reverse3) : 0;
        int c4 = enableMotor4 ? CalcCommand(3, motor4_Index, limit4, distanceRatio, reverse4) : 0;

        lastCmd[0] = c1; lastCmd[1] = c2;
        lastCmd[2] = c3; lastCmd[3] = c4;

        // ── 3. 패킷 전송 ──
        string packet = $"{c1},{c2},{c3},{c4}";
        SendPacket(packet);

        // ── 4. Inspector 모니터링 ──
        monitorPos1 = estimatedPos[0]; monitorPos2 = estimatedPos[1];
        monitorPos3 = estimatedPos[2]; monitorPos4 = estimatedPos[3];
        monitorTarget1 = targetSteps[0]; monitorTarget2 = targetSteps[1];
        monitorTarget3 = targetSteps[2]; monitorTarget4 = targetSteps[3];
        monitorCmd1 = c1; monitorCmd2 = c2;
        monitorCmd3 = c3; monitorCmd4 = c4;
    }

    /// <summary>
    /// 단순 방향 제어: 오차가 있으면 풀파워로 그 방향 이동, 도착하면 정지.
    /// 반환: +1 (정방향 풀파워), -1 (역방향 풀파워), 0 (정지)
    /// 아두이노가 항상 maxSpeed(255)로 구동하므로 소음 구간 진입 없음.
    /// </summary>
    int CalcCommand(int motorIdx, int partIdx, int limit, float ratio, bool isReverse)
    {
        // 목표 스텝 계산
        float offset = GetSeatPartOffset(partIdx);
        float target = offset * ratio;
        if (isReverse) target *= -1f;
        target = Mathf.Clamp(target, -limit, limit);
        targetSteps[motorIdx] = target;

        // 추정 위치 클램프
        estimatedPos[motorIdx] = Mathf.Clamp(estimatedPos[motorIdx], -limit, limit);

        // 오차
        float error = target - estimatedPos[motorIdx];

        // 데드존 이내면 정지
        if (Mathf.Abs(error) < deadZone) return 0;

        // 리밋 보호
        if (error > 0 && estimatedPos[motorIdx] >= limit) return 0;
        if (error < 0 && estimatedPos[motorIdx] <= -limit) return 0;

        // 오차 방향으로 풀파워
        return error > 0 ? 1 : -1;
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
