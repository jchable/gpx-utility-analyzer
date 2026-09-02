---
name: release
description: Cut a new release of the gpx-analyzer CLI - choose the version, write the CHANGELOG entry, open the dev->main promotion PR, then tag main and watch the build through to a published GitHub Release. Use when asked to release, cut a version, ship a release, publish a new version, or tag a release.
---

# Release

Cuts a release of the `gpx-analyzer` CLI and publishes the GitHub Release.

## What is already automated — do not rebuild it

`.github/workflows/release.yml` fires on a pushed tag matching `v*.*.*` and owns
everything downstream:

| Job | Produces |
|---|---|
| `build-windows` | `gpx-analyzer-{v}-win-x64.zip`, `gpx-analyzer-setup-{v}-win-x64.exe` (NSIS) |
| `build-linux` | `gpx-analyzer-{v}-linux-x64.tar.gz`, `gpx-analyzer_{v}_amd64.deb` |
| `build-macos` | `gpx-analyzer-{v}-osx-arm64.tar.gz` |
| `release` | GitHub Release with all five assets |
| `submit-winget` | PR to `microsoft/winget-pkgs` (`Coderise.gpx-analyzer`, secret `WINGET_TOKEN`) |

**There is no version to bump.** No `.csproj` carries a `<Version>`, there is no
`Directory.Build.props`, and `ui/client` / `docs` `package.json` both sit at
`0.0.0` on purpose. The version exists only at publish time, injected from the
tag name (`v1.2.3` -> `-p:Version=1.2.3`). If you find yourself editing a version
string in a source file, stop — you are doing something the pipeline does not
expect.

This skill owns the manual half only: version choice, CHANGELOG, the dev->main
promotion, and the tag.

## Hard rules

These are the failure modes this repo has actually hit or is one slip away from.
Violating any of them is a broken release, not a style problem.

1. **Tag `main`, never `dev`.** The workflow builds the tagged commit. A tag on
   `dev` publishes unmerged code under a release number.
2. **The tag must match `v*.*.*`.** `v0.2` and `0.2.0` trigger nothing at all —
   no run, no error, no notification. You discover it by noticing the release
   never appeared.
3. **Refuse to tag if `CHANGELOG.md` on `main` has no section for the version.**
   This is the failure that already happened: `v0.1.1` shipped on 2026-07-09 and
   never got a CHANGELOG entry, so 138 commits of work stayed under
   `[Unreleased]`.
4. **`git commit -s`.** The DCO check walks the whole PR range, which on a
   promotion PR is all of `dev`. History through `392b1ec` predates the habit and
   is exempted in `dco.yml`; `.githooks/prepare-commit-msg` appends the trailer
   for everything after it (`git config core.hooksPath .githooks`, once per
   clone). The check is therefore green by default — a red one means a genuinely
   unsigned new commit. `dev` and `main` forbid force-pushes, so it cannot be
   signed after the push: sign it the first time. Never move `DCO_EXEMPT_THROUGH`
   forward to excuse a new commit. No `Co-Authored-By` trailer — repo policy.
5. **Stop at both gates.** Never push a tag in the same breath as showing the
   CHANGELOG diff.

## Steps

### 1. Preconditions

```bash
git fetch --all --tags --prune
git status --porcelain                       # must be empty
git rev-parse --abbrev-ref HEAD              # expect: dev
LAST=$(git tag --sort=-v:refname | head -1)  # last released tag
git rev-list --count "$LAST"..dev            # 0 means nothing to release
git rev-list --count dev..origin/main         # must be 0 (see below)
```

Abort with a plain explanation if the tree is dirty, if the branch is not `dev`,
or if the target tag already exists (`git rev-parse -q --verify "refs/tags/vX.Y.Z"`).

`dev` must also not be **behind** `main`: dependabot PRs are merged straight into
`main`, so `main` regularly holds commits `dev` does not. Releasing from a `dev`
that is behind silently drops them from the tagged build. Merge first
(`git merge origin/main` on `dev`) — the history already carries one such
`merge: bring dependabot updates from main into dev`.

### 2. Propose the version

An explicit argument (`/release 0.2.0`) wins outright. Otherwise derive a
suggestion from the conventional-commit types in `$LAST..dev`:

```bash
git log --merges --format='%s' "$LAST"..dev   # human-written summaries
git log --format='%s' "$LAST"..dev | grep -E '^(feat|fix)' | sort | uniq -c
git log --format='%s%n%b' "$LAST"..dev | grep -E '(^[a-z]+(\(.+\))?!:|BREAKING CHANGE)'
```

- breaking change -> minor **while the project is pre-1.0**, major after
- any `feat:` -> minor
- otherwise -> patch

Always show the derivation and the proposed number, and let the human override
before writing anything.

### 3. Write the CHANGELOG entry

Source of truth is **merge commit subjects plus the conventional commits under
them** — not `gh pr list`. On this repo feature work lands on `dev` through local
merge commits (`merge: segment boundary transfer ... (#142, #144)`) and never
opens a PR; the only PRs against `main` are dependabot bumps and the promotion PR
itself. Reading PRs produces a changelog of dependency bumps with the real work
missing.

Edit `CHANGELOG.md`:

- Insert `## [X.Y.Z] - YYYY-MM-DD` directly under `## [Unreleased]`, and fold
  anything that was sitting in `[Unreleased]` into it.
- Keep a Changelog sections (`### Added` / `### Changed` / `### Fixed`), each
  line prefixed by its component in bold — `**CLI**`, `**AI analyzer**`,
  `**API**`, `**Client**`, `**Docs**`, `**Build**` — matching the shape of the
  existing `0.1.0-alpha` entry.
- Carry the issue numbers the merge subjects already reference (`(#142, #144)`).
- Collapse dependabot bumps into a single line; never list them individually.
- Repair the link refs at the bottom of the file:

```text
[Unreleased]: https://github.com/jchable/gpx-utility-analyzer/compare/vX.Y.Z...HEAD
[X.Y.Z]: https://github.com/jchable/gpx-utility-analyzer/compare/vPREV...vX.Y.Z
```

Then commit on `dev`:

```bash
git add CHANGELOG.md
git commit -s -m "docs(changelog): release vX.Y.Z"
```

### 4. GATE 1 — show the diff, stop

Show `git show --stat HEAD` and the full CHANGELOG diff. Nothing has left the
machine yet. Wait for an explicit yes.

### 5. Promotion PR

```bash
BODY=$(mktemp)   # the CHANGELOG section just written, verbatim
git push origin dev
gh pr create --base main --head dev --title "release: vX.Y.Z" --body-file "$BODY"
```

### 6. GATE 2 — the human merges

Branch protection on `main` requires an approving review, so the merge is not
yours to make. Print the PR URL and stop. Resume only when told the PR is merged.

### 7. Tag and watch

```bash
git checkout main && git pull --ff-only
# refuse to tag when the section is missing — this is the v0.1.1 failure mode
grep -q '^## \[X\.Y\.Z\]' CHANGELOG.md   # no match: stop here, do not tag
git tag -a vX.Y.Z -m "vX.Y.Z"
git push origin vX.Y.Z
# the run takes a few seconds to appear; re-list if this returns the previous one
RUN=$(gh run list --workflow=release.yml --limit 1 --json databaseId --jq ".[0].databaseId")
gh run watch "$RUN" --exit-status
```

Then verify, and report honestly if anything is missing:

```bash
gh release view vX.Y.Z            # expect exactly 5 assets
gh run list --workflow=release.yml --limit 1
```

Finally realign `dev` (repo convention: `dev` fast-forwards from `main`):

```bash
git checkout dev && git merge --ff-only main && git push origin dev
```

## The winget submission fails silently-ish

`submit-winget` runs on a `WINGET_TOKEN` PAT that **expires**. On v0.2.0 it died
with `GitHub token is invalid` after the Release was already published: the five
assets were fine, nothing was submitted to `microsoft/winget-pkgs`, and the only
signal was a red job in an otherwise finished release.

So step 7 is not done when `gh release view` shows five assets. Check the job:

```bash
gh run view "$RUN" --json jobs --jq ".jobs[] | \"\(.name)\t\(.conclusion)\""
gh pr list --repo microsoft/winget-pkgs --author jchable --state all --limit 3
```

Two failures, two causes. The second one cost three needless PAT
regenerations on v0.2.0 — read the message before touching the token:

- `GitHub token is invalid` — the PAT expired or was revoked. Rotate it.
- `<user> does not have the correct permissions to execute ``CreateRef``` — **not
  a token problem.** It is GitHub's verbatim FORBIDDEN reply when the target repo
  is one that user cannot write to, and komac targets upstream
  `microsoft/winget-pkgs` when it fails to sync the fork first. The trigger is a
  **stale fork**: komac compares the fork against upstream before branching, and
  GitHub's compare API caps at 250 commits, so a fork thousands of commits behind
  breaks that step. On v0.2.0 the fork was 5220 behind after 13 days.

  The fix is one command, and it needs no new token and no new tag:

  ```bash
  gh api "repos/microsoft/winget-pkgs/compare/master...jchable:master" \
    --jq "\"behind: \(.behind_by)  ahead: \(.ahead_by)\""
  gh repo sync jchable/winget-pkgs      # safe: ahead_by is 0, pure fast-forward
  gh run rerun <run-id> --failed
  ```

  To prove which repo is being refused, run the same GraphQL mutation komac uses
  against the fork and against upstream — the fork succeeds, upstream reproduces
  the error word for word.

Recovering needs no new tag — regenerate a classic PAT with the `public_repo`
scope, then replay just that job:

```bash
gh secret set WINGET_TOKEN            # paste the new PAT
gh run rerun --job <winget job id>
```

## If it goes wrong

A tag pushed too early can be withdrawn, but **the winget PR cannot be unsent** —
`submit-winget` runs right after `release`, and pulling a version back from
`microsoft/winget-pkgs` means opening a removal PR by hand.

```bash
gh release delete vX.Y.Z --yes
git push origin :refs/tags/vX.Y.Z
git tag -d vX.Y.Z
```

Check whether the winget PR went out before assuming the release is cleanly
reverted.

## Known wart

`release.yml` sets `generate_release_notes: true`, so the notes GitHub attaches
to the Release are built from merged PRs — which on this repo means a list of
dependabot bumps that omits the actual work. The CHANGELOG is the real record.
Passing the CHANGELOG section as the release `body` would fix this; it is a
workflow change, out of this skill's scope.
