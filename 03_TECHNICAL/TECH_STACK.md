# TECH STACK

_What we use and why. Verified on Pierre's machine, Sept. 2026._

| Layer | Choice | Note |
|---|---|---|
| Engine | Unity 6 (6000.6.0f1) | The whole viral co-op wave is on Unity |
| Render pipeline | URP (com.unity.template.urp-blank) | Stylized low-poly + post-process, light |
| Language | C# | |
| Dev agent | Claude Code + official Unity plugin (MCP) | Loop prompt -> Unity -> test |
| CLI | Unity CLI 1.0.0-beta.8 | `C:\Users\pierr\AppData\Local\Unity\bin` |
| License | Unity Personal | Enough for an MVP for two |
| Versioning | git + GitHub (private) | Decision repo + `UnityProject` |
| Networking (later) | Steam Networking (P2P / SDR relay) | Deferred, 4 players to start |
| Target | PC / Steam | |

Unity detail: `UNITY_SETUP.md`. MCP workflow: `MCP_WORKFLOW.md`.
