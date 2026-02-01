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

    [Header("4. 모션 믹서")]
    public DualMotionMixer motionMixer;

    void Start()
    {
        // 1. UI 버튼 숨기기
        GameObject btnCar = GameObject.Find("Btn_Car");
        if (btnCar) btnCar.SetActive(false);

        GameObject btnShip = GameObject.Find("Btn_Ship");
        if (btnShip) btnShip.SetActive(false);

        GameObject btnAir = GameObject.Find("Btn_Air");
        if (btnAir) btnAir.SetActive(false);

        // 2. 혼합 모드 진입 (Car + Ship)
        EnterMixedMode();
    }

    void EnterMixedMode()
    {
        // A. 자동차와 배 둘 다 활성화
        if (carObject) carObject.SetActive(true);
        if (carCamera) carCamera.SetActive(false); // 카메라는 배 카메라 쓸거임
        if (trackObject) trackObject.SetActive(true);

        if (shipObject) shipObject.SetActive(true);
        if (shipCamera) 
        {
            shipCamera.SetActive(true);
            // 카메라는 배를 따라가야 함 (원복)
            ShipCameraFollow camScript = shipCamera.GetComponent<ShipCameraFollow>();
            if (camScript != null && shipObject != null)
            {
                camScript.target = shipObject.transform;
            }
        }

        // B. 믹서 설정
        if (motionMixer == null) motionMixer = GetComponent<DualMotionMixer>();
        if (motionMixer == null) motionMixer = gameObject.AddComponent<DualMotionMixer>();

        // 믹서에 스크립트 연결
        if (motionMixer != null)
        {
            motionMixer.carScript = carScript;
            motionMixer.shipScript = shipScript;
            // 혹시 0으로 되어있을 경우를 대비해 1로 강제 설정
            motionMixer.carMixRatio = 1.0f;
            motionMixer.shipMixRatio = 1.0f;
            
            // 시트에 믹서 연결
            if (seatController != null)
            {
                seatController.ConnectVehicle(motionMixer);
            }
        }
    }
    
    // (기존 메서드 유지/무시)
    public void OnCarButtonClicked() { /* ... */ }


    // [버튼 2] 선박 모드 (필요시 사용, 현재는 혼합 모드가 기본)
    public void OnShipButtonClicked()
    {
        // SetMode(false, true, false); 
        // if (seatController != null && shipScript != null)
        //    seatController.ConnectVehicle(shipScript);
    }

    // 중복 코드를 줄여주는 도우미 함수
    void SetMode(bool isCar, bool isShip)
    {
        if (carObject) carObject.SetActive(isCar);
        if (carCamera) carCamera.SetActive(isCar);
        if (trackObject) trackObject.SetActive(isCar); 

        if (shipObject) shipObject.SetActive(isShip);
        if (shipCamera) shipCamera.SetActive(isShip);
    }
}