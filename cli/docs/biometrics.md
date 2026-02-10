# Biometrics

When a GPX file contains extension data (e.g., Garmin TrackPointExtension v1/v2), the CLI automatically extracts and computes biometric statistics.

## Supported extensions

| Source | Data |
|--------|------|
| Garmin TrackPointExtension v1/v2 (`<gpxtpx:hr>`, `<gpxtpx:cad>`, `<gpxtpx:atemp>`) | Heart rate, cadence, temperature |
| Standard `<power>` element | Power (watts) |

## Computed biometric metrics

| Metric | Statistics |
|--------|-----------|
| **Heart Rate** | Average, max, min (bpm). HR zones (Z1-Z5) when `--max-hr` is set |
| **Power** | Average, max (watts), normalized power (NP) |
| **Cadence** | Average, max (rpm) |
| **Temperature** | Average, min, max (°C) |

## HR zones

When `--max-hr` is provided, heart rate zones are computed based on percentage of max HR:

| Zone | Range | Description |
|------|-------|-------------|
| Z1 | 50-60% | Recovery |
| Z2 | 60-70% | Endurance |
| Z3 | 70-80% | Tempo |
| Z4 | 80-90% | Threshold |
| Z5 | 90%+ | VO2max |

## Normalized Power (NP)

Computed using a 30-second rolling average of power data, raised to the 4th power, averaged, then taking the 4th root. This metric reflects the physiological cost of variable-intensity efforts.

## Examples

**Analyze a cycling ride with HR zones:**

```bash
gpx-analyzer analyze ride.gpx --max-hr 185 --preset cycling
```

**Extract biometrics in JSON:**

```bash
gpx-analyzer analyze ride.gpx --max-hr 190 --format json | jq '.heart_rate'
```

**Split a ride and track biometrics per segment:**

```bash
gpx-analyzer split ride.gpx --interval 1h --max-hr 185 --preset cycling
```

If the GPX file does not contain extension data, biometric sections are simply omitted from the output (both text and JSON).
