// ============================================================
// [4-Axis Motion Controller - Full Power Mode]
//
// Unity에서 방향 명령(-1, 0, +1)을 수신하여 DC 모터를 구동.
// +1 = 정방향 풀파워, -1 = 역방향 풀파워, 0 = 즉시 정지.
//
// ★ 소음 방지 원칙 ★
// 중간 PWM 없음. 항상 maxSpeed(255) 또는 0만 사용.
// → 소음 구간(0~minPWM) 진입 자체가 불가능.
//
// 하드웨어: DC 모터 × 4, MD20A 드라이버, Arduino
// 통신: Serial 115200 baud, "cmd1,cmd2,cmd3,cmd4\n"
// ============================================================

const int PWM_PINS[4] = {3, 5, 6, 9};
const int DIR_PINS[4] = {2, 4, 7, 8};

int maxSpeed = 255;

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

    int m1 = parseValue(data, 0);
    int m2 = parseValue(data, 1);
    int m3 = parseValue(data, 2);
    int m4 = parseValue(data, 3);

    applyMotor(0, m1);
    applyMotor(1, m2);
    applyMotor(2, m3);
    applyMotor(3, m4);
  }
}

// ──────────────────────────────────────────
// 모터 구동: 풀파워(255) 또는 완전정지(0)만 사용
// cmd: +1(정방향), -1(역방향), 0(정지)
// ──────────────────────────────────────────
void applyMotor(int i, int cmd) {
  // 정지
  if (cmd == 0) {
    analogWrite(PWM_PINS[i], 0);
    return;
  }

  // 방향 설정
  if (cmd > 0) {
    digitalWrite(DIR_PINS[i], HIGH);
  } else {
    digitalWrite(DIR_PINS[i], LOW);
  }

  // 항상 풀파워
  analogWrite(PWM_PINS[i], maxSpeed);
}

// ──────────────────────────────────────────
// 시리얼 파싱: 쉼표 구분 N번째 정수 추출
// "1,-1,0,1" → index 1 → -1
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
