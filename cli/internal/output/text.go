package output

import (
	"fmt"
	"io"
	"os"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/stats"
	"github.com/olekukonko/tablewriter"
)

// TextFormatter outputs statistics as human-readable ASCII tables.
type TextFormatter struct{}

func (f *TextFormatter) Format(w io.Writer, filename string, s stats.Summary, cfg stats.StopConfig) error {
	fmt.Fprintf(w, "\n=== GPX Analysis: %s ===\n", filename)

	f.printGeneralTable(w, s)
	f.printDistanceTable(w, s)
	f.printElevationTable(w, s)
	f.printStopTable(w, s, cfg)
	f.printBiometricsTable(w, s)

	return nil
}

func (f *TextFormatter) printGeneralTable(w io.Writer, s stats.Summary) {
	fmt.Fprintln(w, "\nGeneral")
	table := newTable(w)
	table.Bulk([][]string{
		{"Start Time", s.StartTime.Format("2006-01-02 15:04:05 UTC")},
		{"End Time", s.EndTime.Format("2006-01-02 15:04:05 UTC")},
		{"Total Time", FormatDuration(s.TotalTime)},
		{"Moving Time", FormatDuration(s.MovingTime)},
		{"Stopped Time", FormatDuration(s.StoppedTime)},
		{"Points", fmt.Sprintf("%d", s.PointCount)},
		{"Segments", fmt.Sprintf("%d", s.SegmentCount)},
		{"Points/km", fmt.Sprintf("%.1f", s.PointsPerKm)},
	})
	table.Render()
}

func (f *TextFormatter) printDistanceTable(w io.Writer, s stats.Summary) {
	fmt.Fprintln(w, "\nDistance & Speed")
	table := newTable(w)
	table.Bulk([][]string{
		{"Total Distance (2D)", FormatDistance(s.TotalDistance)},
		{"Total Distance (3D)", FormatDistance(s.TotalDistance3D)},
		{"Avg Speed", FormatSpeed(s.Speed.AvgSpeed)},
		{"Avg Moving Speed", FormatSpeed(s.Speed.AvgMovingSpeed)},
		{"Max Speed", FormatSpeed(s.Speed.MaxSpeed)},
		{"Avg Pace", FormatPace(s.Speed.AvgPace)},
		{"Avg Moving Pace", FormatPace(s.Speed.AvgMovingPace)},
	})
	table.Render()
}

func (f *TextFormatter) printElevationTable(w io.Writer, s stats.Summary) {
	fmt.Fprintln(w, "\nElevation")
	table := newTable(w)
	table.Bulk([][]string{
		{"Elevation Gain", fmt.Sprintf("+%s", FormatElevation(s.Elevation.Gain))},
		{"Elevation Loss", fmt.Sprintf("-%s", FormatElevation(s.Elevation.Loss))},
		{"Max Elevation", FormatElevation(s.Elevation.Max)},
		{"Min Elevation", FormatElevation(s.Elevation.Min)},
	})
	table.Render()
}

func (f *TextFormatter) printStopTable(w io.Writer, s stats.Summary, cfg stats.StopConfig) {
	fmt.Fprintf(w, "\nStops (speed < %.1f m/s, min duration: %s)\n",
		cfg.MaxSpeed, FormatDuration(cfg.MinDuration))

	if s.StopCount == 0 {
		fmt.Fprintln(w, "  No stops detected.")
		return
	}

	table := newTable(w)
	data := [][]string{
		{"Stop Count", fmt.Sprintf("%d", s.StopCount)},
		{"Total Stopped Time", FormatDuration(s.TotalStopTime)},
		{"Avg Stop Duration", FormatDuration(s.AvgStopDuration)},
	}
	if s.LongestStop != nil {
		data = append(data, []string{
			"Longest Stop",
			fmt.Sprintf("%s (%s)", FormatDuration(s.LongestStop.Duration),
				s.LongestStop.StartTime.Format("2006-01-02 15:04")),
		})
	}
	table.Bulk(data)
	table.Render()
}

func (f *TextFormatter) printBiometricsTable(w io.Writer, s stats.Summary) {
	bio := s.Biometrics
	hasAny := bio.HeartRate != nil || bio.Power != nil ||
		bio.Cadence != nil || bio.Temperature != nil
	if !hasAny {
		return
	}

	fmt.Fprintln(w, "\nBiometrics")
	table := newTable(w)
	var rows [][]string

	if hr := bio.HeartRate; hr != nil {
		rows = append(rows,
			[]string{"Avg Heart Rate", fmt.Sprintf("%.0f bpm", hr.Avg)},
			[]string{"Max Heart Rate", fmt.Sprintf("%d bpm", hr.Max)},
			[]string{"Min Heart Rate", fmt.Sprintf("%d bpm", hr.Min)},
		)
		for _, z := range hr.Zones {
			rows = append(rows, []string{
				fmt.Sprintf("  %s", z.Name),
				FormatDuration(z.Duration),
			})
		}
	}
	if pw := bio.Power; pw != nil {
		rows = append(rows,
			[]string{"Avg Power", fmt.Sprintf("%.0f W", pw.Avg)},
			[]string{"Max Power", fmt.Sprintf("%d W", pw.Max)},
			[]string{"Normalized Power", fmt.Sprintf("%.0f W", pw.NormalizedPower)},
		)
	}
	if cad := bio.Cadence; cad != nil {
		rows = append(rows,
			[]string{"Avg Cadence", fmt.Sprintf("%.0f rpm", cad.Avg)},
			[]string{"Max Cadence", fmt.Sprintf("%d rpm", cad.Max)},
		)
	}
	if temp := bio.Temperature; temp != nil {
		rows = append(rows,
			[]string{"Avg Temperature", fmt.Sprintf("%.1f °C", temp.Avg)},
			[]string{"Min Temperature", fmt.Sprintf("%.1f °C", temp.Min)},
			[]string{"Max Temperature", fmt.Sprintf("%.1f °C", temp.Max)},
		)
	}

	table.Bulk(rows)
	table.Render()
}

func newTable(w io.Writer) *tablewriter.Table {
	tw := tablewriter.NewTable(os.Stdout)
	if w != os.Stdout {
		tw = tablewriter.NewTable(w)
	}
	return tw
}
