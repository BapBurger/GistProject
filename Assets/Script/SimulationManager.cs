using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [Header("0. 제어할 시트")]
    public SeatController seatController;

    [Header("1. 자동차 세팅")]
    public GameObject carObject;
    public GameObject carCamera;
    public GameObject trackObject;

    [Header("2. 선박 세팅")]
    public GameObject shipObject;
    public GameObject shipCamera;

    [Header("3. 비행기 세팅")]
    public GameObject airplaneObject;
    public GameObject airplaneCamera;

    void Start()
    {
        // 게임 시작 시 선박 모드로 시작 (원하시는 대로 변경 가능)
        OnShipButtonClicked();
    }

    // [버튼 1] 자동차 모드
    public void OnCarButtonClicked()
    {
        SetMode(true, false, false); // 오브젝트 활성화/비활성화

        // [수정] 함수 호출 대신, 시트 컨트롤러의 토글을 직접 켭니다.
        if (seatController != null)
        {
            seatController.enableCar = true;
            seatController.enableShip = false;
        }
    }

    // [버튼 2] 선박 모드
    public void OnShipButtonClicked()
    {
        SetMode(false, true, false);

        // [수정] 배 토글 켜기
        if (seatController != null)
        {
            seatController.enableCar = false;
            seatController.enableShip = true;
        }
    }

    // [버튼 3] 비행기 모드
    public void OnAirplaneButtonClicked()
    {
        Debug.Log("비행기 모드 진입!");
        SetMode(false, false, true);

        // 비행기는 아직 시트 로직이 없으므로 둘 다 끕니다.
        if (seatController != null)
        {
            seatController.enableCar = false;
            seatController.enableShip = false;
        }
    }

    // 중복 코드를 줄여주는 도우미 함수 (오브젝트 끄고 켜기)
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