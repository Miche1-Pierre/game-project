# UnityProject/

_The Unity project will live here. EMPTY for now: decision to create nothing until the spec is ready._

## At start (see `03_TECHNICAL/MCP_WORKFLOW.md`)
1. `unity projects new <name> --template com.unity.template.urp-blank --editor 6000.6.0f1 --path C:\GameProject\UnityProject`
2. `unity mcp configure claude-code --project-path C:\GameProject\UnityProject`
3. Open a Claude Code session INSIDE this folder.

The root `.gitignore` already ignores `UnityProject/Library/`, `Temp/`, `Logs/`, etc.