package output

import (
	"fmt"
	"io"
	"os"

	"github.com/jchable/gpx-utility-analyzer/internal/stats"
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

func newTable(w io.Writer) *tablewriter.Table {
	tw := tablewriter.NewTable(os.Stdout)
	if w != os.Stdout {
		tw = tablewriter.NewTable(w)
	}
	return tw
}
