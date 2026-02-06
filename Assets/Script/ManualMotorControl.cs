using UnityEngine;
using System.IO.Ports; // 시리얼 통신 필수
using System;

public class ManualMotorControl : MonoBehaviour
{
    [Header("1. 포트 설정")]
    public string portName = "COM3"; // 아두이노 포트 번호 확인 후 입력!
    public int baudRate = 9600;

    private SerialPort sp;
    private char lastCommand = ' '; // 중복 전송 방지용

    void Start()
    {
        OpenConnection();
    }

    void Update()
    {
        if (sp == null || !sp.IsOpen) return;

        // 1. W키를 누르고 있을 때 (위로)
        if (Input.GetKey(KeyCode.W))
        {
            SendCommand('U');
        }
        // 2. S키를 누르고 있을 때 (아래로)
        else if (Input.GetKey(KeyCode.S))
        {
            SendCommand('D');
        }
        // 3. 아무것도 안 누르면 (정지)
        else
        {
            SendCommand('X');
        }
    }

    // 명령 전송 함수 (상태가 바뀔 때만 전송)
    void SendCommand(char cmd)
    {
        if (lastCommand != cmd) // 이전 명령과 다를 때만 쏜다!
        {
            try
            {
                sp.Write(cmd.ToString());
                lastCommand = cmd;
                Debug.Log($"Send to Arduino: {cmd}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Serial Error: {e.Message}");
            }
        }
    }

    // 포트 열기
    void OpenConnection()
    {
        try
        {
            sp = new SerialPort(portName, baudRate);
            sp.Open();
            sp.ReadTimeout = 100;
            Debug.Log($"Serial Port {portName} Opened!");
        }
        catch (Exception e)
        {
            Debug.LogError($"포트 열기 실패 ({portName}): {e.Message}");
        }
    }

    // 종료 시 포트 닫기 (중요!)
    void OnApplicationQuit()
    {
        if (sp != null && sp.IsOpen)
        {
            SendCommand('X'); // 끄기 전에 정지 신호
            sp.Close();
            Debug.Log("Serial Port Closed.");
        }
    }
}