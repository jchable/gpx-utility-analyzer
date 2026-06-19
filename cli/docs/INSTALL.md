# Installing gpx-analyzer

## Package managers

### Windows — winget

```
winget install Coderise.gpx-analyzer
```

Once installed, open a new terminal and run:

```
gpx-analyzer --help
```

The installer adds `gpx-analyzer` to the system `PATH` automatically.

---

### Linux (Debian / Ubuntu) — apt

Add the GPX Analyzer APT repository, then install:

```bash
curl -fsSL https://jchable.github.io/gpx-utility-analyzer/apt/gpg.key | sudo apt-key add -
echo "deb https://jchable.github.io/gpx-utility-analyzer/apt stable main" \
  | sudo tee /etc/apt/sources.list.d/gpx-analyzer.list
sudo apt-get update
sudo apt-get install gpx-analyzer
```

Verify the installation:

```bash
gpx-analyzer --help
```

Upgrade later with:

```bash
sudo apt-get update && sudo apt-get upgrade gpx-analyzer
```

---

## Portable install (all platforms)

Download the archive for your platform from the [Releases page](https://github.com/jchable/gpx-utility-analyzer/releases/latest):

| Platform | File |
|---|---|
| Windows x64 | `gpx-analyzer-<version>-win-x64.zip` |
| Linux x64 | `gpx-analyzer-<version>-linux-x64.tar.gz` |
| macOS ARM64 (Apple Silicon) | `gpx-analyzer-<version>-osx-arm64.tar.gz` |

### Windows

```powershell
Expand-Archive gpx-analyzer-<version>-win-x64.zip -DestinationPath C:\tools\gpx-analyzer
# Add C:\tools\gpx-analyzer to your PATH, then:
gpx-analyzer --help
```

### Linux / macOS

```bash
tar -xzf gpx-analyzer-<version>-<platform>.tar.gz
sudo mv gpx-analyzer /usr/local/bin/
gpx-analyzer --help
```

---

## .deb package (direct install, no APT repository)

```bash
sudo dpkg -i gpx-analyzer_<version>_amd64.deb
gpx-analyzer --help
```

---

## Uninstall

### winget

```
winget uninstall Coderise.gpx-analyzer
```

### apt

```bash
sudo apt-get remove gpx-analyzer
sudo rm /etc/apt/sources.list.d/gpx-analyzer.list
```

### Portable

Delete the binary and remove its directory from `PATH`.

---

## Release workflow

Releases are built automatically by GitHub Actions on every `v*.*.*` tag push.
Each release includes:

- `gpx-analyzer-<version>-win-x64.zip` — Windows portable
- `gpx-analyzer-setup-<version>-win-x64.exe` — Windows NSIS installer
- `gpx-analyzer-<version>-linux-x64.tar.gz` — Linux portable
- `gpx-analyzer_<version>_amd64.deb` — Debian/Ubuntu package
- `gpx-analyzer-<version>-osx-arm64.tar.gz` — macOS Apple Silicon portable

### Triggering a release

```bash
git tag v1.0.0
git push --tags
```

The workflow at [`.github/workflows/release.yml`](../../.github/workflows/release.yml) runs three parallel build jobs (Windows, Linux, macOS), creates the GitHub Release with all artifacts, and submits a PR to `microsoft/winget-pkgs` automatically.

### APT repository update

The APT repository at `https://jchable.github.io/gpx-utility-analyzer/apt/` is updated automatically after each release by [`.github/workflows/update-apt-repo.yml`](../../.github/workflows/update-apt-repo.yml).

### One-time setup (maintainers)

Before the first release, the following GitHub secrets must be configured:

| Secret | Description |
|---|---|
| `WINGET_TOKEN` | GitHub PAT with `public_repo` scope, for submitting to `microsoft/winget-pkgs` |
| `APT_GPG_PRIVATE_KEY` | Armored GPG private key used to sign the APT repository |
| `APT_GPG_KEY_ID` | GPG key fingerprint (e.g. `ABCD1234...`) |

The `apt-repo` orphan branch must also be initialized once:

```bash
bash installers/linux/apt/init-apt-repo.sh
```

See [installers/linux/apt/init-apt-repo.sh](../../installers/linux/apt/init-apt-repo.sh) for the full setup procedure including GPG key generation.
