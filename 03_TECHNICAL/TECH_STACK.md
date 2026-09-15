# TECH STACK

_Ce qu'on utilise et pourquoi. Vérifié sur la machine de Pierre, sept. 2026._

| Couche | Choix | Note |
|---|---|---|
| Moteur | Unity 6 (6000.6.0f1) | Toute la vague coop virale est sous Unity |
| Render pipeline | URP (com.unity.template.urp-blank) | Stylisé low-poly + post-process, léger |
| Langage | C# | |
| Agent de dev | Claude Code + plugin Unity officiel (MCP) | Boucle prompt → Unity → test |
| CLI | Unity CLI 1.0.0-beta.8 | `C:\Users\pierr\AppData\Local\Unity\bin` |
| Licence | Unity Personal | Suffisant pour un MVP à 2 |
| Versioning | git + GitHub (privé) | Repo de décision + `UnityProject` |
| Réseau (plus tard) | Steam Networking (P2P / SDR relay) | Différé, 4 joueurs pour commencer |
| Cible | PC / Steam | |

Détail Unity : `UNITY_SETUP.md`. Workflow MCP : `MCP_WORKFLOW.md`.
