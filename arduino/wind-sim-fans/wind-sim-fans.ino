/*
 * Wind Sim Fan Controller — Arduino Nano (ATmega328)
 *
 * Drives PWM on D9 (left) and D10 (right) via Timer1 at ~31 kHz.
 * Serial protocol: "PWM <left> <right>\n" where duty values are 0-511.
 * D2/D3 reserved for future tach/RPM — do not use.
 */

static const unsigned long FAILSAFE_TIMEOUT_MS = 2500;
static const uint16_t PWM_MAX = 511;

static unsigned long lastValidCommandMs = 0;
static char lineBuffer[32];
static uint8_t lineLength = 0;

static void configureTimer1Pwm() {
  pinMode(9, OUTPUT);
  pinMode(10, OUTPUT);

  // Fast PWM mode 14: ICR1 is TOP, ~31.25 kHz at 16 MHz (9-bit resolution)
  TCCR1A = _BV(COM1A1) | _BV(COM1B1) | _BV(WGM11);
  TCCR1B = _BV(WGM13) | _BV(CS10);
  ICR1 = PWM_MAX;

  OCR1A = 0;
  OCR1B = 0;
}

static int clampDuty(int value) {
  if (value < 0) return 0;
  if (value > PWM_MAX) return PWM_MAX;
  return value;
}

static void applyPwm(int leftDuty, int rightDuty) {
  OCR1A = (uint16_t)clampDuty(leftDuty);
  OCR1B = (uint16_t)clampDuty(rightDuty);
}

static void processLine(const char* line) {
  while (*line == ' ' || *line == '\t' || *line == '\r') {
    line++;
  }

  int left = -1;
  int right = -1;

  if (sscanf(line, "PWM %d %d", &left, &right) == 2) {
    applyPwm(left, right);
    lastValidCommandMs = millis();
  }
}

static void checkFailsafe() {
  if (lastValidCommandMs == 0) {
    return;
  }

  if (millis() - lastValidCommandMs > FAILSAFE_TIMEOUT_MS) {
    applyPwm(0, 0);
    // Keep lastValidCommandMs set so fail-safe keeps enforcing 0 until a new command arrives.
  }
}

void setup() {
  configureTimer1Pwm();
  Serial.begin(115200);
}

void loop() {
  while (Serial.available() > 0) {
    char c = (char)Serial.read();

    if (c == '\n' || c == '\r') {
      if (lineLength > 0) {
        lineBuffer[lineLength] = '\0';
        processLine(lineBuffer);
        lineLength = 0;
      }
      continue;
    }

    if (lineLength < sizeof(lineBuffer) - 1) {
      lineBuffer[lineLength++] = c;
    }
  }

  checkFailsafe();
}
