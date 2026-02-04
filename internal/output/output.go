package output

import (
	"fmt"
	"io"
	"time"

	"github.com/jchable/gpx-utility-analyzer/internal/stats"
)

// Formatter defines the interface for outputting statistics.
type Formatter interface {
	Format(w io.Writer, filename string, summary stats.Summary, cfg stats.StopConfig) error
}

// NewFormatter returns the appropriate formatter for the given format name.
func NewFormatter(format string) (Formatter, error) {
	switch format {
	case "text":
		return &TextFormatter{}, nil
	case "json":
		return &JSONFormatter{}, nil
	default:
		return nil, fmt.Errorf("unknown format %q, expected 'text' or 'json'", format)
	}
}

// FormatDuration formats a duration in a human-readable way (e.g. "2d 5h 30m 15s").
func FormatDuration(d time.Duration) string {
	if d <= 0 {
		return "0s"
	}

	days := int(d.Hours()) / 24
	hours := int(d.Hours()) % 24
	minutes := int(d.Minutes()) % 60
	seconds := int(d.Seconds()) % 60

	if days > 0 {
		return fmt.Sprintf("%dd %dh %dm %ds", days, hours, minutes, seconds)
	}
	if hours > 0 {
		return fmt.Sprintf("%dh %dm %ds", hours, minutes, seconds)
	}
	if minutes > 0 {
		return fmt.Sprintf("%dm %ds", minutes, seconds)
	}
	return fmt.Sprintf("%ds", seconds)
}

// FormatPace formats a pace duration as min:sec per km.
func FormatPace(d time.Duration) string {
	if d <= 0 {
		return "-"
	}
	totalSeconds := int(d.Seconds())
	minutes := totalSeconds / 60
	seconds := totalSeconds % 60
	return fmt.Sprintf("%d:%02d min/km", minutes, seconds)
}

// FormatDistance formats a distance in meters as km with 1 decimal.
func FormatDistance(meters float64) string {
	if meters < 1000 {
		return fmt.Sprintf("%.0f m", meters)
	}
	return fmt.Sprintf("%.1f km", meters/1000)
}

// FormatSpeed formats a speed in m/s as km/h with 1 decimal.
func FormatSpeed(mps float64) string {
	return fmt.Sprintf("%.1f km/h", mps*3.6)
}

// FormatElevation formats an elevation value in meters.
func FormatElevation(meters float64) string {
	return fmt.Sprintf("%.0f m", meters)
}
