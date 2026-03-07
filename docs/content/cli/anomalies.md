---
title: "Anomaly Detection"
sidebar_label: "Anomaly Detection"
sidebar_position: 8
slug: "/cli/anomalies"
---
Automatic detection and optional correction of GPS and sensor data quality issues. Detection is always enabled with negligible performance overhead (O(n) on track points).

## Quality Score

Each trace receives a quality score from 0 to 100, deducting per anomaly:

| Severity | Deduction |
|----------|-----------|
| Critical | -15 |
| Warning | -5 |
| Info | -1 |

## Detected Anomalies (14 types in 6 categories)

| Category | Type | Severity | Description |
|----------|------|----------|-------------|
| Position | GPS Frozen | Critical | Consecutive points at identical coordinates while biometrics indicate movement |
| Position | Signal Loss | Warning | Time gaps between consecutive points (> 30s) |
| Position | GPS Drift | Warning | Position oscillation during stops |
| Speed | Speed Spike | Warning | Points exceeding max speed threshold (already clamped) |
| Speed | Speed/Biometric Mismatch | Warning | Active cadence with zero movement |
| Elevation | Elevation Spike | Warning | Sudden elevation changes (pre-smoothing) |
| Elevation | Impossible Grade | Warning | Grade exceeding 80% |
| Temporal | Backward Time | Critical | Timestamps going backwards |
| Temporal | Duplicate Timestamp | Info | Consecutive identical timestamps |
| Biometric | HR Spike | Warning | Heart rate changes > 30 bpm between points |
| Biometric | HR Out of Range | Warning | Heart rate outside 30–230 bpm |
| Data Quality | Low Point Density | Warning | Less than 5 points per km |
| Data Quality | Constant Elevation | Warning/Critical | No elevation variation (barometer failure) |

## Correction (opt-in)

Use `--fix-anomalies` to apply automatic corrections for correctable anomalies. Corrections recalculate affected stats automatically.

| Type | Correctable | Strategy |
|------|-------------|----------|
| GPS Frozen | Yes | Interpolate lat/lon linearly between last good point and first good point after |
| GPS Teleportation | No | Already removed by GPS filter |
| GPS Drift | Yes | Collapse all points to centroid during stops |
| Signal Loss | No | Cannot reconstruct missing data |
| Speed Spike | No | Already clamped by speed filter |
| Speed/Biometric Mismatch | No | Informational |
| Elevation Spike | Yes | Interpolate elevation linearly between healthy neighbors |
| Impossible Grade | No | Often legitimate terrain |
| Backward Time | Yes | Set timestamp = previous + 1s |
| Duplicate Timestamp | Yes | Interpolate timestamps between surrounding unique timestamps |
| HR Spike | No | May be legitimate |
| HR Out of Range | Yes | Exclude from HR stats (set to null) |
| Low Point Density | No | Cannot add points |
| Constant Elevation | No | Would require DEM (separate feature) |

## Examples

**Default analysis (detection always enabled):**

```bash
gpx-analyzer analyze my-hike.gpx --preset trail
```

**Apply automatic corrections:**

```bash
gpx-analyzer analyze my-hike.gpx --preset trail --fix-anomalies
```

**Check data quality in JSON format:**

```bash
gpx-analyzer analyze my-hike.gpx --format json | jq '.anomalies'
```

## Text Output

When anomalies are detected, a "Data Quality" section appears after biometrics:

```
Data Quality (Score: 78/100)
+---------------------------+----------+
| Total Anomalies           | 5        |
| Critical                  | 1        |
| Warnings                  | 3        |
| Info                      | 1        |
| Distance Impact           | -2134 m  |
| Time Impact               | 16m 6s   |
| Corrections Applied       | No       |
+---------------------------+----------+

  [CRITICAL] GPS Frozen (07:05:01 - 07:21:07, points 0-966)
    GPS position frozen for 967 points (16m06s) while biometrics
    indicate movement (cadence=88rpm, HR 61→139bpm)
    Estimated distance lost: -2134 m

  [WARNING] GPS Drift (08:02:30 - 08:04:15, points 3421-3480)
    Position oscillation during stop: max drift 25m from centroid
    Inflated distance: +12 m
```

## JSON Output

In JSON format, the `anomalies` object contains all details:

```json
{
  "anomalies": {
    "quality_score": 78,
    "total_count": 5,
    "info_count": 1,
    "warning_count": 3,
    "critical_count": 1,
    "distance_impact_m": -2134,
    "time_impact_s": 966,
    "correction_applied": false,
    "anomalies": [
      {
        "type": "gps_frozen",
        "category": "position",
        "severity": "critical",
        "start_index": 0,
        "end_index": 966,
        "start_time": "2026-02-28T07:05:01Z",
        "end_time": "2026-02-28T07:21:07Z",
        "distance_impact_m": -2134,
        "time_impact_s": 966,
        "description": "GPS position frozen for 967 points...",
        "was_corrected": false
      }
    ]
  }
}
```

## Configuration Thresholds

All detection thresholds have sensible defaults and are not exposed as CLI flags. They can be adjusted programmatically via `AnomalyConfig`:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `GpsFrozenMinPoints` | 5 | Min consecutive identical points |
| `GpsFrozenEpsilon` | 0.000001 | Coordinate match tolerance (~0.1m) |
| `SignalLossThresholdS` | 30 | Gap threshold (seconds) |
| `GpsDriftThresholdM` | 20 | Max drift from centroid during stop |
| `ElevationSpikeThresholdM` | 50 | Sudden elevation change threshold |
| `ImpossibleGradePercent` | 80 | Grade threshold (%) |
| `HrSpikeThresholdBpm` | 30 | Max HR change between points |
| `HrMinBpm` / `HrMaxBpm` | 30 / 230 | Valid HR range |
| `MinPointsPerKm` | 5 | Point density threshold |
| `ConstantElevationRangeM` | 2 | Max elevation range for "constant" |
| `ActiveCadenceThreshold` | 30 | RPM threshold for "moving" |

## Web UI Integration

When viewing an activity in the web dashboard, a quality banner appears below the header when anomalies are detected. The banner shows the quality score, severity counts, and an expandable list of individual anomalies with their impact.

## AI Analysis Integration

The anomaly report is included in the AI analysis prompt context, allowing the AI to comment on data quality issues in its report. Only warning and critical anomalies are forwarded (top 5).
