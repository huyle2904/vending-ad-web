# Worklog

## Latest Important Commits
- `1affd71` `ci: add web build workflow`
- `6713eaa` `chore: split repository to web only`
- `a57b594` `refactor scheduling flow and clean legacy playlist model`
- `a79534e` `feat: improve portal dashboards and schedule editing`
- `078b71e` `feat: improve portal dashboards and schedule editing` (remote push)

## Latest Completed Work
- Repository split to web-only.
- `main` set as default branch, `dev` used for coding.
- CI workflow added for PRs to `main` and pushes to `dev`/`main`.
- Legacy playlist-device-time model replaced by playback schedule flow.
- DB schema repair added for old SQLite data.
- Device Wall web simulator added for multi-device testing.
- Portal dashboard upgraded with correct current/upcoming schedule logic.
- Schedule live editing added: add/remove/reorder items in modal.
- Login UI and global CMS styling standardized.
- `ui-ux-pro-max` skill installed for OpenCode in `.opencode/skills/ui-ux-pro-max/`.
- Quick-play immediate schedule flow added on portal `Devices` and `Dashboard` cards.
- Schedule badges updated with `Đã lên lịch` and distinct color styling.
- Major portal/admin UI text synchronized to Vietnamese.
- `find-skills`, `frontend-design`, and `web-design-guidelines` skills installed for OpenCode.

## Current Session Work
- CMS-wide Vietnamese localization and final UI polish.
- Portal dashboard / devices / schedules / admin consistency checks.
- Final QA on mobile + desktop.

## Resume Hint
- Start by reading `PROJECT_CONTEXT.md` and `OPEN_ISSUES.md`.
- Then inspect dirty files with `git status --short`.
