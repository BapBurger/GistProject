using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO.Ports; // 시리얼 통신 필수
using System;

public class RealSeatBridge : MonoBehaviour
{
    [Header("1. 연결 설정")]
    public string portName = "COM5"; // 아두이노 포트 번호 꼭 확인!
    public int baudRate = 9600;

    [Header("2. 데이터 연결")]
    public SeatController seatController; // SeatController 스크립트 연결

    [Header("3. 감도 조절 (Sensitivity)")]
    [Range(0.1f, 40.0f)]
    public float motionGain = 1.0f; // ★ 게인: 높을수록 민감하게 반응

    [Range(0.001f, 0.1f)]
    public float deadZone = 0.01f;  // 떨림 방지: 이 값보다 작게 움직이면 무시

    private SerialPort sp;
    private float lastVirtualPos = 0f; // 이전 프레임의 위치
    private char lastCommand = ' ';    // 중복 전송 방지용

    void Start()
    {
        OpenConnection();

        // 초기 위치 저장
        if (seatController != null && seatController.seatParts.Length > seatController.wholeLiftIndex)
        {
            lastVirtualPos = seatController.seatParts[seatController.wholeLiftIndex].currentValue;
        }
    }

    void Update()
    {
        if (sp != null && sp.IsOpen && seatController != null)
        {
            // 1. 가상 시트의 현재 높이 가져오기
            float currentVirtualPos = 0f;
            if (seatController.seatParts.Length > seatController.wholeLiftIndex)
            {
                currentVirtualPos = seatController.seatParts[seatController.wholeLiftIndex].currentValue;
            }

            // 2. 변화량(속도) 계산: (현재위치 - 과거위치) * 게인
            float velocity = (currentVirtualPos - lastVirtualPos) * motionGain;

            // 3. 방향 판단 및 명령 전송 (★ 여기서 방향을 반대로 뒤집었습니다!)
            // 변화량이 DeadZone보다 크면 위로 가야 하는데 -> 실제로는 'D'를 보냄
            if (velocity > deadZone)
            {
                SendCommand('D'); // ★ 원래 'U'였던 것을 'D'로 변경 (반전)
            }
            // 변화량이 작으면 아래로 가야 하는데 -> 실제로는 'U'를 보냄
            else if (velocity < -deadZone)
            {
                SendCommand('U'); // ★ 원래 'D'였던 것을 'U'로 변경 (반전)
            }
            else
            {
                SendCommand('X'); // 멈춰있거나 변화가 미미함
            }

            // 4. 현재 위치를 과거 위치로 업데이트 (다음 프레임 비교용)
            lastVirtualPos = currentVirtualPos;
        }
    }

    // 명령 전송 함수 (중복 전송 방지 포함)
    void SendCommand(char cmd)
    {
        if (lastCommand != cmd) // 명령이 바뀔 때만 보냄 (통신 부하 감소)
        {
            try
            {
                sp.Write(cmd.ToString());
                lastCommand = cmd;
                // Debug.Log($"Motor Command: {cmd}"); // 필요시 주석 해제하여 확인
            }
            catch (Exception) { }
        }
    }

    void OpenConnection()
    {
        try
        {
            sp = new SerialPort(portName, baudRate);
            sp.Open();
            sp.ReadTimeout = 50;
            Debug.Log($"Serial Port {portName} Connected.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Connection Failed: {e.Message}");
        }
    }

    void OnApplicationQuit()
    {
        if (sp != null && sp.IsOpen)
        {
            SendCommand('X'); // 종료 시 정지
            sp.Close();
        }
    }
}