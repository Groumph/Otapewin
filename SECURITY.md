# Security Policy

## Supported Versions

We support security fixes for the latest minor version.

## Reporting a Vulnerability

If you discover a security vulnerability within SecondBrain, please do NOT create a public GitHub issue.

Instead, please report it responsibly by emailing:

- security@yourdomain.com (replace with your real email)

Please include:
- A detailed description of the vulnerability
- Steps to reproduce
- Potential impact
- Any relevant logs or screenshots

We will respond within48 hours and work on a timeline for a fix.

## Handling of Secrets

- API keys and secrets must NOT be committed to the repository.
- Use environment variables prefixed with `SECONDBRAIN_`.
- Example configuration is provided in `src/SecondBrain/appsettings.example.json`.
- Local `appsettings.json` files are git-ignored by default.

## Dependencies

- We use Dependabot or Renovate to keep dependencies updated.
- Security patches are prioritized for high-severity issues.

## Disclosure Policy

- If a vulnerability is confirmed, we will assign a CVE if appropriate.
- A public disclosure and fix will be released together.
- Credit will be given to the reporter if desired.
