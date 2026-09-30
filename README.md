# KittyKam

Personal project: track what each of our three free-fed cats eats, for the vet (and to notice
fast if one stops eating).

An ESP32-CAM sits by the food bowl. A PIR sensor detects a cat, the camera takes a few photos,
and load cells + HX711 under the bowl weigh the food before and after the visit. Each visit is
posted to a small Azure Functions API (Blob for photos, Table for visits).

```
firmware/   ESP32-CAM (PlatformIO / Arduino). Copy src/secrets.h.example -> src/secrets.h
server/     Azure Functions (C# isolated worker, .NET 10, consumption plan)
```

## Wiring (AI-Thinker ESP32-CAM)
| Part | Pin |
|---|---|
| PIR (AM312) OUT | GPIO13 |
| HX711 DT | GPIO14 |
| HX711 SCK | GPIO15 |

Power the PIR and HX711 from 3.3V. Four half-bridge load cells combine into one full bridge
(platform scale); see the HX711 module docs for the E+/E-/A+/A- wiring.

## Calibration
Set `SCALE_FACTOR` in `main.cpp`: put a known weight on the bowl platform, read the raw value
printed with scale factor 1, and divide raw by grams.

## Roadmap
1. Firmware + API end to end (this scaffold)
2. Label photos, then auto-identify the cat (image embeddings + nearest neighbor)
3. Simple daily-intake page and a printable report for the vet
4. Alert if a cat (Ruby) hasn't eaten in X hours
