# MCP WORKFLOW (Unity and Claude Code)

_Goal: compress the loop prompt -> code -> Unity -> test -> observation -> correction._

## Official plugin
Unity shipped an official Claude Code plugin on 9 September 2026: 29 Unity skills, the Unity CLI, and the Unity MCP server (live editor control: create/modify GameObjects, edit scenes and assets, inspect the hierarchy, run C#, manage installs/licenses/builds).
Docs: https://docs.unity.com/en-us/ai/unity-plugin/claude-code

## Verified on this machine
- Unity CLI 1.0.0-beta.8 in C:\Users\pierr\AppData\Local\Unity\bin
- Installed editor: 6000.6.0f1 (Unity 6.6)
- Supported MCP client: claude-code

## To do when we create Unity (NOT now)
1. `unity projects new <name> --template com.unity.template.urp-blank --editor 6000.6.0f1 --path C:\GameProject\UnityProject`
2. `unity mcp configure claude-code --project-path C:\GameProject\UnityProject`
3. Open a Claude Code session INSIDE C:\GameProject\UnityProject

## Rule
After each gameplay change: let the MCP inspect the scene and read the console.

_Status: workflow defined, activation deferred._