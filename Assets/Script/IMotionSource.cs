using UnityEngine;

// "나 시트랑 연결될 수 있어!" 라고 인증하는 명함 같은 것
public interface IMotionSource 
{
    float GetSurgeG(); // 앞뒤 가속도 줘!
    float GetSwayG();  // 좌우 가속도 줘!
    float GetHeaveG(); // 상하 가속도 줘! (배, 비행기용)
}