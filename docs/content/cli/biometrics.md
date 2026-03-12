---
title: "Biometrics"
sidebar_label: "Biometrics"
sidebar_position: 7
slug: "/cli/biometrics"
---
When a GPX file contains extension data (e.g., Garmin TrackPointExtension v1/v2), the CLI automatically extracts and computes biometric statistics.

## Supported extensions

| Source | Data |
|--------|------|
| Garmin TrackPointExtension v1/v2 (`<gpxtpx:hr>`) | Heart rate (bpm) |
| Garmin TrackPointExtension v1/v2 (`<gpxtpx:cad>`) | Cadence (rpm) |
| Garmin TrackPointExtension v1/v2 (`<gpxtpx:atemp>`) | Air temperature (°C) |
| Garmin TrackPointExtension v2 (`<gpxtpx:speed>`) | Device-reported speed (m/s) |
| Garmin TrackPointExtension v2 (`<gpxtpx:wtemp>`) | Water temperature (°C) |
| Standard `<power>` element | Power (watts) |

## Computed biometric metrics

| Metric | Statistics |
|--------|-----------|
| **Heart Rate** | Average, max, min (bpm). HR zones (Z1-Z5) when `--max-hr` is set |
| **Power** | Average, max (watts), normalized power (NP) |
| **Cadence** | Average, max (rpm) |
| **Air Temperature** | Average, min, max (°C) |
| **Water Temperature** | Average, min, max (°C) — present on aquatic activities recorded with a compatible Garmin device |

## GPS quality fields

Standard GPX 1.1 quality fields are parsed when present and stored per track point:

| Field | Description |
|-------|-------------|
| `<fix>` | Fix type: `none`, `2d`, `3d`, `dgps`, `pps` |
| `<sat>` | Number of satellites used |
| `<hdop>` | Horizontal dilution of precision |
| `<vdop>` | Vertical dilution of precision |
| `<pdop>` | Position dilution of precision |

These fields are used by the anomaly detector to identify unreliable GPS points (e.g., hdop > 5 or sat < 4 can correlate with position spikes). They are not directly surfaced in the JSON output summary but influence anomaly detection and speed clamping behaviour.

## Device speed vs. computed speed

When `<gpxtpx:speed>` is present, the device-reported speed (Doppler-derived, in m/s) is stored alongside the computed Haversine speed. The device speed is generally more reliable for instantaneous readings. Significant divergence between the two can indicate a GPS position anomaly.

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
