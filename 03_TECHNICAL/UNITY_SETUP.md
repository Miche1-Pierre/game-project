# UNITY SETUP

_Verified state and creation procedure. Creation deferred: no project until the spec is ready (ADR-002)._

## Verified on this machine
- Unity CLI: 1.0.0-beta.8 (`C:\Users\pierr\AppData\Local\Unity\bin`, prefix it to PATH if the shell cannot find it).
- Installed editor: 6000.6.0f1 (Unity 6.6).
- License: Unity Personal.
- Chosen template: `com.unity.template.urp-blank` (Universal 3D).
- Supported MCP client: `claude-code`.

## To configure at start
- Unity version: 6000.6.0f1
- Render pipeline: Built-in RP (verified 2026-09-17: no URP package, `Shader.Find("Universal Render Pipeline/Lit")` returns null)
- Input: Input System (package)
- Physics: 3D (PhysX)
- Build modules: Windows Build Support (IL2CPP) when we want an .exe (not required to play in the editor)
- Networking: deferred (Steam P2P)
- MCP: Unity plugin for Claude Code, docs https://docs.unity.com/en-us/ai/unity-plugin/claude-code
- Scene and prefab conventions: to define with the first greybox

## Commands (on the day)
- `unity projects new <name> --template com.unity.template.urp-blank --editor 6000.6.0f1 --path C:\GameProject\UnityProject`
- `unity mcp configure claude-code --project-path C:\GameProject\UnityProject`
- Then open a Claude Code session INSIDE `C:\GameProject\UnityProject`.
