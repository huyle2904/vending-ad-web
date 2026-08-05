# Security Policy

## Reporting

Report vulnerabilities privately to the company security owner or engineering lead.
Do not open a public issue containing credentials, exploit details, customer data, or production URLs.

## Secret handling

Runtime secrets must be stored in the deployment platform, local environment variables, or .NET user secrets.
Do not commit `.env`, `appsettings.Development.json`, database exports, uploaded media, tokens, or private keys.
Development example files must contain placeholders only.

If a secret is committed, revoke it first, then rewrite Git history and rotate every dependent credential.
Deleting the value in a later commit is not sufficient.

## Supported branch

Security fixes are applied to the protected `main` branch.
CI build, tests, dependency audit, and deployment checks must pass before release.
