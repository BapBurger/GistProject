using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [Header("0. 제어할 시트")]
    public SeatController seatController;

    [Header("1. 자동차 세팅")]
    public GameObject carObject;           // 자동차 모델
    public GameObject carCamera;           // 자동차 카메라
    public CarPhysicsController carScript; // 자동차 물리 스크립트
    public ShipPhysicsController shipScript;

    public GameObject trackObject;

    [Header("2. 선박 세팅 (나중에 연결)")]
    public GameObject shipObject;
    public GameObject shipCamera;
    // public ShipController shipScript; 

    // 게임 시작하면 일단 자동차로 시작
    void Start()
    {
        OnCarButtonClicked();
    }

    // [버튼 1] 자동차 버튼을 누르면 실행될 함수
    public void OnCarButtonClicked()
    {
        Debug.Log("자동차 모드 진입!");

        // 1. 오브젝트 켜고 끄기
        carObject.SetActive(true);
        carCamera.SetActive(true);

        // ▼▼▼ [추가] 트랙 켜기 ▼▼▼
        if (trackObject != null) trackObject.SetActive(true);

        if (shipObject != null) shipObject.SetActive(false);
        if (shipCamera != null) shipCamera.SetActive(false);

        // 2. 시트에 자동차 연결
        if (seatController != null && carScript != null)
        {
            seatController.ConnectVehicle(carScript);
        }
    }

    public void OnShipButtonClicked()
    {
        Debug.Log("선박 모드 진입!");

        // 1. 오브젝트 켜고 끄기
        if (shipObject != null) shipObject.SetActive(true);
        if (shipCamera != null) shipCamera.SetActive(true);

        carObject.SetActive(false);
        carCamera.SetActive(false);

        // ▼▼▼ [추가] 트랙 끄기 (핵심!) ▼▼▼
        if (trackObject != null) trackObject.SetActive(false);

        if (seatController != null && shipScript != null)
        {
            seatController.ConnectVehicle(shipScript);
        }
    }
}