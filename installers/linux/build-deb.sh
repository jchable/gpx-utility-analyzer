#!/usr/bin/env bash
# installers/linux/build-deb.sh
# Builds a .deb package for gpx-analyzer.
#
# Usage: build-deb.sh <version> <binary_path>
#   version     : e.g. 1.2.3
#   binary_path : path to the compiled gpx-analyzer binary

set -euo pipefail

VERSION="${1:?version argument is required}"
BINARY_PATH="${2:?binary_path argument is required}"

if [[ ! -f "$BINARY_PATH" ]]; then
  echo "ERROR: binary not found at '$BINARY_PATH'" >&2
  exit 1
fi

PKG_DIR="$(pwd)/pkg"
DEB_OUTPUT="gpx-analyzer_${VERSION}_amd64.deb"

# Clean any previous build
rm -rf "$PKG_DIR"

# ── Create package directory tree ──────────────────────────────────
mkdir -p "$PKG_DIR/DEBIAN"
mkdir -p "$PKG_DIR/usr/local/bin"

# ── Copy binary ────────────────────────────────────────────────────
cp "$BINARY_PATH" "$PKG_DIR/usr/local/bin/gpx-analyzer"
chmod 0755 "$PKG_DIR/usr/local/bin/gpx-analyzer"

# ── Generate control file ──────────────────────────────────────────
cat > "$PKG_DIR/DEBIAN/control" <<EOF
Package: gpx-analyzer
Version: ${VERSION}
Architecture: amd64
Maintainer: GPX Analyzer Project <noreply@github.com>
Section: utils
Priority: optional
Depends:
Description: GPX file analysis tool
 Analyzes GPX files for distance, elevation gain/loss, speed, stop detection,
 and biometrics (heart rate, power, cadence, temperature). Includes altitude
 correction via SRTM digital elevation model.
 Self-contained Native AOT binary with no runtime dependency.
EOF

chmod 0644 "$PKG_DIR/DEBIAN/control"

# ── Build the .deb ─────────────────────────────────────────────────
# --root-owner-group ensures files are owned by root:root in the package
# regardless of the UID running this script (important on CI runners)
dpkg-deb --build --root-owner-group "$PKG_DIR" "$DEB_OUTPUT"

echo "Built: $DEB_OUTPUT"
