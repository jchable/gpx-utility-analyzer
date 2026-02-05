package cmd

import (
	"fmt"
	"os"

	"github.com/spf13/cobra"
)

var formatFlag string

var rootCmd = &cobra.Command{
	Use:   "gpx-analyzer",
	Short: "Analyze GPX files: distance, elevation, stops, and more",
	Long: `gpx-analyzer is a command-line tool for analyzing GPX files.

It computes distance, elevation gain/loss, speed, pace, stop detection,
and supports time-based splitting and multi-file merging.`,
}

func Execute() {
	if err := rootCmd.Execute(); err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
}

func init() {
	rootCmd.PersistentFlags().StringVar(&formatFlag, "format", "text",
		"Output format: text or json")
}
