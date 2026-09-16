# Security Policy

## What this project does not collect

Scheduler counts keyboard presses, mouse clicks, and wheel ticks for local daily statistics. It does **not** record key characters, clipboard contents, screenshots, or passwords. Input totals stay in a local `stats.txt` file unless you yourself enable a notification that sends the numeric summary to your webhook.

## Reporting a vulnerability

Please **do not** open a public GitHub issue for security problems.

Email the maintainer privately (see the GitHub profile of the repository owner) with:

- a description of the issue
- steps to reproduce
- affected version / commit if you know it

You should receive an acknowledgement within 7 days. After a fix is available, we will credit reporters who want to be named.

## Secrets

Never commit webhook URLs, API keys, or `appsettings.Local.json`. If a secret was committed, rotate it immediately — rewriting git history is not enough once the repo is public.
