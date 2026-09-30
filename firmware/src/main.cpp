// KittyKam firmware: PIR wakes a "visit", camera snaps photos, HX711 weighs the bowl
// before and after. Each finished visit is POSTed to the Azure Function API.
#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <WiFiClientSecure.h>
#include <time.h>
#include <ArduinoOTA.h>
#include "esp_camera.h"
#include "HX711.h"
#include "secrets.h"

// Free GPIOs on the AI-Thinker ESP32-CAM (camera uses the rest).
// TEST_MODE 1: bench test with only the ESP32-CAM plugged in. Snaps a photo every 30s and
// uploads it; ignores the PIR and scale (the HX711 library blocks forever if none is attached).
#define TEST_MODE 1

constexpr int PIN_PIR = 13;
constexpr int PIN_HX_DT = 14;
constexpr int PIN_HX_SCK = 15;

constexpr float SCALE_FACTOR = 1.0f;      // calibrate: known grams / raw units (see README)
constexpr int MAX_PHOTOS = 5;
constexpr uint32_t PHOTO_INTERVAL_MS = 3000;
constexpr uint32_t QUIET_MS = 20000;      // no motion for this long = cat has left
constexpr float SETTLE_G = 1.0f;          // reading is "settled" when it moves less than this

HX711 scale;
float baselineG = 0;
String visitId;

enum State { IDLE, VISIT } state = IDLE;
uint32_t lastMotionMs = 0, lastPhotoMs = 0;
int photoCount = 0;
float weightBeforeG = 0;
time_t visitStart = 0;

bool initCamera() {
  camera_config_t c = {};
  c.ledc_channel = LEDC_CHANNEL_0; c.ledc_timer = LEDC_TIMER_0;
  c.pin_d0 = 5; c.pin_d1 = 18; c.pin_d2 = 19; c.pin_d3 = 21;
  c.pin_d4 = 36; c.pin_d5 = 39; c.pin_d6 = 34; c.pin_d7 = 35;
  c.pin_xclk = 0; c.pin_pclk = 22; c.pin_vsync = 25; c.pin_href = 23;
  c.pin_sccb_sda = 26; c.pin_sccb_scl = 27; c.pin_pwdn = 32; c.pin_reset = -1;
  c.xclk_freq_hz = 20000000; c.pixel_format = PIXFORMAT_JPEG;
  c.frame_size = FRAMESIZE_SVGA; c.jpeg_quality = 12; c.fb_count = 1;
  c.fb_location = CAMERA_FB_IN_PSRAM; c.grab_mode = CAMERA_GRAB_LATEST;
  return esp_camera_init(&c) == ESP_OK;
}

float readGrams() { return scale.get_units(5); }

// Wait until the scale stops moving, then return the average.
float settledGrams() {
  float prev = readGrams();
  for (int i = 0; i < 20; i++) {
    delay(500);
    float cur = readGrams();
    if (fabsf(cur - prev) < SETTLE_G) return (cur + prev) / 2;
    prev = cur;
  }
  return prev;
}

void connectWifi() {
  if (WiFi.status() == WL_CONNECTED) return;
  WiFi.setSleep(false);  // modem sleep drops packets during OTA and slows uploads
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  for (int i = 0; i < 40 && WiFi.status() != WL_CONNECTED; i++) delay(500);
}

int post(const String& path, const char* type, const uint8_t* body, size_t len) {
  connectWifi();
  WiFiClientSecure client; client.setInsecure();  // personal project; key travels in header over TLS
  HTTPClient http;
  http.begin(client, String(API_BASE) + path);
  http.addHeader("Content-Type", type);
  http.addHeader("x-functions-key", API_KEY);
  int code = http.POST((uint8_t*)body, len);
  http.end();
  return code;
}

void capturePhoto() {
  camera_fb_t* fb = esp_camera_fb_get();
  if (!fb) return;
  int code = post("/photo?visitId=" + visitId + "&seq=" + photoCount, "image/jpeg", fb->buf, fb->len);
  Serial.printf("photo %d -> %d\n", photoCount, code);
  esp_camera_fb_return(fb);
  photoCount++;
  lastPhotoMs = millis();
}

void finishVisit() {
  float after = settledGrams();
  char json[256];
  snprintf(json, sizeof json,
           "{\"visitId\":\"%s\",\"startedAt\":%lld,\"durationS\":%lld,\"weightBeforeG\":%.1f,\"weightAfterG\":%.1f,\"photos\":%d}",
           visitId.c_str(), (long long)visitStart, (long long)(time(nullptr) - visitStart),
           weightBeforeG, after, photoCount);
  Serial.printf("visit: %s -> %d\n", json, post("/visit", "application/json", (uint8_t*)json, strlen(json)));
  baselineG = after;
  state = IDLE;
}

// Over-the-air updates: `pio run -e esp32cam-ota -t upload` from the PC on the same network.
void startOta() {
  ArduinoOTA.setHostname("kittykam");
  ArduinoOTA.setPassword(OTA_PASS);
  ArduinoOTA.begin();
}

void setup() {
  Serial.begin(115200);
  pinMode(PIN_PIR, INPUT);
  if (!initCamera()) { Serial.println("camera init failed"); delay(3000); ESP.restart(); }
#if TEST_MODE
  connectWifi();
  startOta();
  Serial.printf("wifi: %s ip=%s\n", WiFi.status() == WL_CONNECTED ? "connected" : "FAILED", WiFi.localIP().toString().c_str());
  configTime(0, 0, "pool.ntp.org");
  while (time(nullptr) < 100000) delay(200);
  return;
#endif
  scale.begin(PIN_HX_DT, PIN_HX_SCK);
  scale.set_scale(SCALE_FACTOR);
  scale.tare();
  connectWifi();
  startOta();
  configTime(0, 0, "pool.ntp.org");
  baselineG = settledGrams();
}

void loop() {
  ArduinoOTA.handle();
#if TEST_MODE
  visitId = String((long long)time(nullptr));
  photoCount = 0;
  capturePhoto();
  for (int i = 0; i < 600; i++) { ArduinoOTA.handle(); delay(50); }  // 30s, staying responsive to OTA
  return;
#endif
  bool motion = digitalRead(PIN_PIR) == HIGH;
  uint32_t now = millis();
  if (state == IDLE && motion) {
    state = VISIT; photoCount = 0; lastMotionMs = now; lastPhotoMs = 0;
    visitStart = time(nullptr);
    weightBeforeG = baselineG;
    visitId = String((long long)visitStart);
  }
  if (state == VISIT) {
    if (motion) lastMotionMs = now;
    if (photoCount < MAX_PHOTOS && now - lastPhotoMs >= PHOTO_INTERVAL_MS) capturePhoto();
    if (now - lastMotionMs >= QUIET_MS) finishVisit();
  }
  delay(50);
}
