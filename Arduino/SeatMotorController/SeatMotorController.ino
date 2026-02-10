// ============================================================
// [4-Axis Motion Controller - Smooth Speed Mode]
//
// Unity에서 부호 있는 속도값(-255 ~ +255)을 수신하여 DC 모터를 구동.
// 양수 = 정방향, 음수 = 역방향, 0 = 정지.
//
// ★ 소음 방지 원칙 ★
// 0 < |PWM| < minPWM 구간은 절대 사용하지 않는다.
// (토크 부족 → 코일 진동 → 고주파 소음 발생)
//
// 하드웨어: DC 모터 × 4, MD20A 드라이버, Arduino
// 통신: Serial 115200 baud, "speed1,speed2,speed3,speed4\n"
// ============================================================

const int PWM_PINS[4] = {3, 5, 6, 9};
const int DIR_PINS[4] = {2, 4, 7, 8};

// ★ 소음 방지 최소 PWM ★
// Unity 측에서 이미 minPWM 미만은 0으로 잘라내지만,
// 안전장치로 아두이노에서도 이중 체크한다.
// 소리가 나면 130 → 140 → 150 으로 올려보세요.
int minPWM = 130;

void setup() {
  for (int i = 0; i < 4; i++) {
    pinMode(PWM_PINS[i], OUTPUT);
    pinMode(DIR_PINS[i], OUTPUT);
    analogWrite(PWM_PINS[i], 0);
  }
  Serial.begin(115200);
  Serial.setTimeout(5);
}

void loop() {
  if (Serial.available() > 0) {
    String data = Serial.readStringUntil('\n');
    data.trim();

    int s1 = parseValue(data, 0);
    int s2 = parseValue(data, 1);
    int s3 = parseValue(data, 2);
    int s4 = parseValue(data, 3);

    applyMotor(0, s1);
    applyMotor(1, s2);
    applyMotor(2, s3);
    applyMotor(3, s4);
  }
}

// ──────────────────────────────────────────
// 모터 구동 (소음 방지 이중 체크 포함)
// speed: -255 ~ +255 (부호 = 방향, 절대값 = PWM)
// ──────────────────────────────────────────
void applyMotor(int i, int speed) {
  // 1. 정지 명령 → 즉시 전원 차단
  if (speed == 0) {
    analogWrite(PWM_PINS[i], 0);
    return;
  }

  // 2. 방향 결정
  if (speed > 0) {
    digitalWrite(DIR_PINS[i], HIGH);
  } else {
    digitalWrite(DIR_PINS[i], LOW);
  }

  // 3. PWM 적용 (소음 구간 이중 차단)
  int pwm = abs(speed);
  if (pwm < minPWM) {
    // Unity에서 이미 잘라냈어야 하지만 혹시라도 들어오면 차단
    analogWrite(PWM_PINS[i], 0);
  } else {
    pwm = constrain(pwm, minPWM, 255);
    analogWrite(PWM_PINS[i], pwm);
  }
}

// ──────────────────────────────────────────
// 시리얼 파싱: 쉼표 구분 N번째 정수 추출
// "200,-150,0,180" → index 1 → -150
// ──────────────────────────────────────────
int parseValue(String data, int index) {
  int found = 0;
  int strIndex[] = {0, -1};
  int maxIndex = data.length() - 1;
  for (int i = 0; i <= maxIndex && found <= index; i++) {
    if (data.charAt(i) == ',' || i == maxIndex) {
      found++;
      strIndex[0] = strIndex[1] + 1;
      strIndex[1] = (i == maxIndex) ? i + 1 : i;
    }
  }
  return found > index ? data.substring(strIndex[0], strIndex[1]).toInt() : 0;
}
