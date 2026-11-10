# EthanRP Launcher 2.0

Windows launcher prototype for EthanRP.

## Build
Push the project to GitHub and open **Actions → Build EthanRP Launcher → Run workflow**. Download the `EthanRP-Launcher-Windows` artifact.

## File manifest
`manifest.json` contains `files`, each with `path`, `url` (HTTPS GitHub asset), and `sha256` (hexadecimal SHA-256). Only distribute files you are licensed to distribute. The launcher verifies each file before installing it to `%LOCALAPPDATA%\EthanRP\Game`.

The prototype requires an authorized GTA San Andreas installation and compatible SA-MP executable in its game directory before it can launch. It does not bundle or download proprietary GTA files. The current launch arguments and client compatibility must be tested with the actual game build.
