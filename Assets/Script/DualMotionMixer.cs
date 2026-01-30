using UnityEngine;

public class DualMotionMixer : MonoBehaviour, IMotionSource
{
    [Header("1. 가지고 있는 스크립트 연결")]
    public CarPhysicsController carScript; // 혁주님이 올리신 그 스크립트!
    public ShipPhysicsController shipScript; // 배 스크립트

    [Header("2. 강도 조절 (비율)")]
    [Range(0, 2)] public float carMixRatio = 1.0f;  // 자동차 느낌
    [Range(0, 2)] public float shipMixRatio = 1.0f; // 배 느낌

    // 시트가 "G값 내놔!" 하면 둘을 더해서 줌
    public float GetSurgeG()
    {
        float car = (carScript != null) ? carScript.GetSurgeG() : 0;
        float ship = (shipScript != null) ? shipScript.GetSurgeG() : 0;
        return (car * carMixRatio) + (ship * shipMixRatio);
    }

    public float GetSwayG()
    {
        float car = (carScript != null) ? carScript.GetSwayG() : 0;
        float ship = (shipScript != null) ? shipScript.GetSwayG() : 0;
        return (car * carMixRatio) + (ship * shipMixRatio);
    }

    public float GetHeaveG()
    {
        // 자동차는 Heave가 0이라고 되어있으니 배 값만 씀
        float car = (carScript != null) ? carScript.GetHeaveG() : 0;
        float ship = (shipScript != null) ? shipScript.GetHeaveG() : 0;
        return (car * carMixRatio) + (ship * shipMixRatio);
    }
}