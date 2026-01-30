using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [Header("0. 제어할 시트")]
    public SeatController seatController;

    [Header("1. 자동차 세팅")]
    public GameObject carObject;
    public GameObject carCamera;
    public CarPhysicsController carScript;
    public GameObject trackObject;

    [Header("2. 선박 세팅")]
    public GameObject shipObject;
    public GameObject shipCamera;
    public ShipPhysicsController shipScript;

    [Header("3. 비행기 세팅 (신규 추가)")]
    public GameObject airplaneObject; // 비행기 오브젝트
    public GameObject airplaneCamera; // 비행기 카메라
    // public AirplaneController airplaneScript; // 나중에 비행기 스크립트 만들면 주석 해제

    void Start()
    {
        // 자동차는 아직 없으니까 주석 처리하거나 지우세요
        // OnCarButtonClicked(); 

        // 게임 시작하자마자 "배 모드"로 진입!
        OnShipButtonClicked();
    }

    // [버튼 1] 자동차 모드
    public void OnCarButtonClicked()
    {
        SetMode(true, false, false); // 차만 켜기
        if (seatController != null && carScript != null)
            seatController.ConnectVehicle(carScript);
    }

    // [버튼 2] 선박 모드
    public void OnShipButtonClicked()
    {
        SetMode(false, true, false); // 배만 켜기
        if (seatController != null && shipScript != null)
            seatController.ConnectVehicle(shipScript);
    }

    // [버튼 3] 비행기 모드 (추가됨)
    public void OnAirplaneButtonClicked()
    {
        Debug.Log("비행기 모드 진입!");
        SetMode(false, false, true); // 비행기만 켜기

        // 나중에 비행기 스크립트 연결
        // if (seatController != null && airplaneScript != null)
        //    seatController.ConnectVehicle(airplaneScript);
    }

    // 중복 코드를 줄여주는 도우미 함수
    void SetMode(bool isCar, bool isShip, bool isPlane)
    {
        if (carObject) carObject.SetActive(isCar);
        if (carCamera) carCamera.SetActive(isCar);
        if (trackObject) trackObject.SetActive(isCar); // 트랙은 차 탈 때만

        if (shipObject) shipObject.SetActive(isShip);
        if (shipCamera) shipCamera.SetActive(isShip);

        if (airplaneObject) airplaneObject.SetActive(isPlane);
        if (airplaneCamera) airplaneCamera.SetActive(isPlane);
    }
}