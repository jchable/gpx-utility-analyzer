# Security Policy

## Supported Versions

This project is under active development. Security fixes are applied to the latest
released version and the `main` branch. Older pre-release/alpha versions are not
maintained — please upgrade to the latest release before reporting.

| Version | Supported          |
|---------|--------------------|
| latest release / `main` | :white_check_mark: |
| older pre-releases | :x: |

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues,
discussions, or pull requests.**

Instead, report them privately through GitHub's built-in
[Security Advisories](../../security/advisories/new) ("Report a vulnerability").

If you cannot use GitHub Security Advisories, email **julien.chable@gmail.com**
with the subject line `SECURITY: gpx-utility-analyzer`.

Please include as much of the following as you can:

- The affected component (`cli`, `ai-analyzer`, `api`, `client`, Docker/infra).
- A description of the vulnerability and its potential impact.
- Steps to reproduce, a proof of concept, or affected source locations.
- Any known mitigations or workarounds.

## What to Expect

- **Acknowledgement**: within **5 business days**.
- **Assessment & triage**: we will confirm the issue and determine severity and
  affected versions.
- **Fix & disclosure**: we will work on a fix and coordinate a disclosure timeline
  with you. We aim to resolve high-severity issues promptly and will keep you
  informed of progress.
- **Credit**: with your permission, we are happy to credit you in the advisory and
  release notes.

## Scope

Areas of particular interest for this project:

- Authentication / authorization and multi-user data isolation in the API
  (ASP.NET Identity + JWT).
- Handling of OAuth secrets and integration tokens (e.g. Strava).
- Storage backends (local filesystem / S3-compatible) and file upload handling.
- AI provider API key handling and prompt/data leakage.
- Deserialization and parsing of untrusted GPX input.

Thank you for helping keep GPX Utility Analyzer and its users safe.
