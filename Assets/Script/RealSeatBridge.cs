using UnityEngine;
using System.IO.Ports;
using System;
using System.Text;

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
    public float distanceRatio = 5000f; // Slide, Heave
    public float angleRatio = 200f;     // Tilt

    [Header("4. 피드포워드 + P제어 (핵심!)")]
    [Tooltip("속도 피드포워드 게인: 가상 시트 속도를 얼마나 반영할지 (0.5~2.0)")]
    public float kV = 1.0f; // ★ Feedforward Gain

    [Tooltip("위치 보정 게인 (P): 오차를 얼마나 빨리 수정할지 (0.5~2.0)")]
    public float kP = 1.0f; // ★ Position Correction Gain

    [Tooltip("소음 방지 최소 PWM: 이 값 이하는 0으로 처리")]
    public int minPWM = 130;

    [Header("5. Dead Reckoning (위치 추정)")]
    [Tooltip("PWM 255일 때 초당 이동 스텝 수 (튜닝 필요)")]
    public float stepsPerSecondAtMax = 1000f;

    [Header("6. 중력 보정 (Drift 방지)")]
    [Tooltip("등받이가 누울 때(중력 방향)는 값을 빼고, 일어날 때(반중력)는 값을 더해줍니다.")]
    public int backSeatBias = 30; // ★ 등받이 전용 보정값 (눕는 게 문제라면 양수 입력)

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
    private float[] targetSteps = new float[4];

    // ★ 피드포워드를 위한 이전 프레임 위치 저장
    private float[] prevTargetSteps = new float[4];

    void Start()
    {
        OpenConnection();
        for (int i = 0; i < 4; i++)
        {
            estimatedPos[i] = 0f;
            targetSteps[i] = 0f;
            prevTargetSteps[i] = 0f;
        }
    }

    void Update()
    {
        // 리셋
        if (Input.GetKeyDown(resetKey))
        {
            for (int i = 0; i < 4; i++)
            {
                estimatedPos[i] = 0f;
                prevTargetSteps[i] = targetSteps[i]; // 튀는거 방지
            }
            Debug.Log("<color=cyan>[RealSeatBridge] Position Reset to 0</color>");
        }

        if (sp == null || !sp.IsOpen || seatController == null) return;

        // ── 1. Dead Reckoning: 내 위치 추정 (지난 프레임 PWM 기준) ──
        // (주의: 여기서는 단순화를 위해 monitorSpeed 변수를 사용합니다)
        float dt = Time.deltaTime;
        if (dt <= 0) return;

        estimatedPos[0] += (monitorSpeed1 / 255f) * stepsPerSecondAtMax * dt;
        estimatedPos[1] += (monitorSpeed2 / 255f) * stepsPerSecondAtMax * dt;
        estimatedPos[2] += (monitorSpeed3 / 255f) * stepsPerSecondAtMax * dt;
        estimatedPos[3] += (monitorSpeed4 / 255f) * stepsPerSecondAtMax * dt;

        // ── 2. 속도 계산 (Feedforward + P) ──
        int s1 = enableMotor1 ? CalcSpeedFF(0, motor1_Index, limit1, distanceRatio, reverse1, dt) : 0;
        int s2 = enableMotor2 ? CalcSpeedFF(1, motor2_Index, limit2, angleRatio, reverse2, dt) : 0;
        int s3 = enableMotor3 ? CalcSpeedFF(2, motor2_Index, limit3, angleRatio, reverse3, dt) : 0; // Motor 3 -> Back Index
        int s4 = enableMotor4 ? CalcSpeedFF(3, motor4_Index, limit4, distanceRatio, reverse4, dt) : 0;

        // ── 3. 패킷 전송 ──
        string packet = $"{s1},{s2},{s3},{s4}";
        SendPacket(packet);

        // ── 4. 모니터링 업데이트 ──
        monitorPos1 = estimatedPos[0]; monitorPos2 = estimatedPos[1];
        monitorPos3 = estimatedPos[2]; monitorPos4 = estimatedPos[3];
        monitorTarget1 = targetSteps[0]; monitorTarget2 = targetSteps[1];
        monitorTarget3 = targetSteps[2]; monitorTarget4 = targetSteps[3];
        monitorSpeed1 = s1; monitorSpeed2 = s2;
        monitorSpeed3 = s3; monitorSpeed4 = s4;
    }



    int CalcSpeedFF(int motorIdx, int partIdx, int limit, float ratio, bool isReverse, float dt)
    {
        // 1. 목표 위치 계산
        float offset = GetSeatPartOffset(partIdx);
        float target = offset * ratio;
        if (isReverse) target *= -1f;
        target = Mathf.Clamp(target, -limit, limit);
        targetSteps[motorIdx] = target;

        // 2. 가상 시트의 속도 계산 (Feedforward)
        float targetVelocity = (target - prevTargetSteps[motorIdx]) / dt;
        prevTargetSteps[motorIdx] = target;

        // 3. 위치 오차 계산 (P Control)
        estimatedPos[motorIdx] = Mathf.Clamp(estimatedPos[motorIdx], -limit, limit);
        float positionError = target - estimatedPos[motorIdx];

        // 4. 최종 속도 명령 계산
        float pwmFromVel = targetVelocity * kV * (255f / stepsPerSecondAtMax);
        float pwmFromPos = positionError * kP;
        float finalPWM = pwmFromVel + pwmFromPos;

        int outputPWM = (int)finalPWM;

        // =================================================================
        // ★ [추가됨] 중력 보정 (Gravity Bias) - 등받이 눕는 현상 해결
        // =================================================================
        // 만약 이 모터가 '등받이(Back Seat)'라면? (index 1번)
        if (motorIdx == 1 || motorIdx == 2)
        {
            // 목표가 '위로(일어나는 방향)' 갈 때 힘을 더해줌
            // (참고: 방향은 배선에 따라 다를 수 있으니, 
            // 만약 더 빨리 누워버리면 backSeatBias를 음수(-30)로 바꾸세요)
            outputPWM += backSeatBias;
        }

        // 리밋 도달 시 차단
        if (outputPWM > 0 && estimatedPos[motorIdx] >= limit) return 0;
        if (outputPWM < 0 && estimatedPos[motorIdx] <= -limit) return 0;

        // 부스트 로직 (최소 기동 부하)
        int absPWM = Mathf.Abs(outputPWM);
        int deadZoneNoise = 5;

        if (absPWM > deadZoneNoise)
        {
            if (absPWM < minPWM)
            {
                if (outputPWM > 0) outputPWM = minPWM;
                else outputPWM = -minPWM;
            }
        }
        else
        {
            outputPWM = 0;
        }

        return Mathf.Clamp(outputPWM, -255, 255);
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
        try { sp = new SerialPort(portName, baudRate); sp.Open(); sp.ReadTimeout = 20; }
        catch (Exception e) { Debug.LogError($"[RealSeatBridge] Connection Error: {e.Message}"); }
    }

    void OnApplicationQuit()
    {
        if (sp != null && sp.IsOpen) { sp.WriteLine("0,0,0,0"); sp.Close(); }
    }
}