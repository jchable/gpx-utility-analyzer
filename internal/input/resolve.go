package input

import (
	"fmt"
	"os"
	"path/filepath"
	"strings"
)

// ResolveFiles takes a list of arguments (file paths, directories, glob patterns)
// and returns a flat list of GPX file paths.
func ResolveFiles(args []string) ([]string, error) {
	var files []string
	seen := make(map[string]bool)

	for _, arg := range args {
		resolved, err := resolveArg(arg)
		if err != nil {
			return nil, fmt.Errorf("resolving %q: %w", arg, err)
		}
		for _, f := range resolved {
			abs, err := filepath.Abs(f)
			if err != nil {
				abs = f
			}
			if !seen[abs] {
				seen[abs] = true
				files = append(files, f)
			}
		}
	}

	if len(files) == 0 {
		return nil, fmt.Errorf("no GPX files found in arguments: %v", args)
	}

	return files, nil
}

func resolveArg(arg string) ([]string, error) {
	// Check if it's a glob pattern
	if strings.ContainsAny(arg, "*?[") {
		matches, err := filepath.Glob(arg)
		if err != nil {
			return nil, err
		}
		return filterGPX(matches), nil
	}

	info, err := os.Stat(arg)
	if err != nil {
		return nil, err
	}

	// If it's a directory, find all .gpx files in it
	if info.IsDir() {
		return findGPXInDir(arg)
	}

	// Single file
	if !isGPX(arg) {
		return nil, fmt.Errorf("%s is not a .gpx file", arg)
	}
	return []string{arg}, nil
}

func findGPXInDir(dir string) ([]string, error) {
	var files []string
	err := filepath.Walk(dir, func(path string, info os.FileInfo, err error) error {
		if err != nil {
			return err
		}
		if !info.IsDir() && isGPX(path) {
			files = append(files, path)
		}
		return nil
	})
	return files, err
}

func isGPX(path string) bool {
	return strings.EqualFold(filepath.Ext(path), ".gpx")
}

func filterGPX(paths []string) []string {
	var result []string
	for _, p := range paths {
		if isGPX(p) {
			result = append(result, p)
		}
	}
	return result
}
