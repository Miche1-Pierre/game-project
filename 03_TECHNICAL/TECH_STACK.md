# TECH STACK

_What we use and why. Verified on Pierre's machine, Sept. 2026._

| Layer | Choice | Note |
|---|---|---|
| Engine | Unity 6 (6000.6.0f1) | Most of the co-op corpus is Unity. The 2026 outlier is Meccha Chameleon on Unreal 5, driven by its UEFN background, not by a Unity limitation |
| Render pipeline | URP (com.unity.template.urp-blank) | Stylized low-poly + post-process, light |
| Language | C# | |
| Dev agent | Claude Code + official Unity plugin (MCP) | Loop prompt -> Unity -> test |
| CLI | Unity CLI 1.0.0-beta.8 | `C:\Users\pierr\AppData\Local\Unity\bin` |
| License | Unity Personal | Enough for an MVP for two |
| Versioning | git + GitHub (private) | Decision repo + `UnityProject` |
| Networking (later) | Epic Online Services (Unity plugin), listen server | Deferred, 4 players to start. Free at any scale. See ADR-003 (proposed) and `../07_MULTIPLAYER/NETWORK_ARCHITECTURE.md` |
| Target | PC / Steam | |

Unity detail: `UNITY_SETUP.md`. MCP workflow: `MCP_WORKFLOW.md`.

## Note on AI-assisted development
Valve rewrote its disclosure rules in January 2026. AI-powered development tools, including code assistants, are explicitly outside the scope of the content survey. Only generative content that **ships and is consumed by players** must be declared, in two categories, pre-generated and live-generated.

Consequence for us: using Claude Code needs no disclosure. Any AI-generated 3D asset, texture, audio or store image that ships **does**, and that declaration appears on our store page. This is a reason to decide the asset pipeline deliberately, not a reason to avoid it. See `05_ART/ASSET_STATUS.md` and `00_PROJECT/OPEN_QUESTIONS.md` Q23.
