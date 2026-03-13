#!/usr/bin/env bash
# installers/linux/apt/init-apt-repo.sh
#
# One-time setup: creates the 'apt-repo' orphan branch used as the APT
# repository backend. Run once from the repo root before the first release.
#
# Prerequisites:
#   1. Generate GPG key:
#        gpg --batch --gen-key <<EOF
#        Key-Type: RSA
#        Key-Length: 4096
#        Name-Real: GPX Analyzer
#        Name-Email: noreply@github.com
#        Expire-Date: 0
#        %no-passphrase
#        EOF
#
#   2. Get the key fingerprint:
#        gpg --list-keys --with-fingerprint "GPX Analyzer"
#
#   3. Export private key (add as GitHub secret APT_GPG_PRIVATE_KEY):
#        gpg --export-secret-keys --armor <fingerprint>
#
#   4. Add GitHub secrets:
#        APT_GPG_PRIVATE_KEY  — armored private key
#        APT_GPG_KEY_ID       — key fingerprint (e.g. ABCD1234...)
#
#   5. Export public key to docs/static/apt/gpg.key (committed to main):
#        gpg --export --armor <fingerprint> > docs/static/apt/gpg.key

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel)"

# Check we're not already on apt-repo
CURRENT_BRANCH=$(git -C "$REPO_ROOT" rev-parse --abbrev-ref HEAD)
if [ "$CURRENT_BRANCH" = "apt-repo" ]; then
  echo "ERROR: already on 'apt-repo' branch. Switch to main first." >&2
  exit 1
fi

# Check the branch doesn't already exist
if git -C "$REPO_ROOT" show-ref --verify --quiet refs/heads/apt-repo; then
  echo "ERROR: branch 'apt-repo' already exists." >&2
  exit 1
fi

cd "$REPO_ROOT"

echo "Creating orphan branch 'apt-repo'..."
git checkout --orphan apt-repo
git rm -rf . 2>/dev/null || true

# Seed the reprepro configuration
mkdir -p conf
cp "$SCRIPT_DIR/conf/distributions" conf/distributions

git add conf/
git commit -m "chore: initialize APT repository"
git push origin apt-repo

echo ""
echo "Done. Branch 'apt-repo' created and pushed."
echo ""
echo "APT repository URL (after docs are deployed):"
echo "  https://jchable.github.io/gpx-utility-analyzer/apt/"
echo ""
echo "User install instructions:"
echo "  curl -fsSL https://jchable.github.io/gpx-utility-analyzer/apt/gpg.key | sudo apt-key add -"
echo "  echo \"deb https://jchable.github.io/gpx-utility-analyzer/apt stable main\" \\"
echo "    | sudo tee /etc/apt/sources.list.d/gpx-analyzer.list"
echo "  sudo apt-get update && sudo apt-get install gpx-analyzer"

# Return to the original branch
git checkout "$CURRENT_BRANCH"
