# MCP WORKFLOW (Unity et Claude Code)

_But : compresser la boucle prompt -> code -> Unity -> test -> observation -> correction._

## Plugin officiel
Unity a publié le 9 septembre 2026 un plugin officiel pour Claude Code : 29 skills Unity, le Unity CLI, et le serveur MCP Unity (contrôle live de l'éditeur : créer/modifier des GameObjects, éditer scènes et assets, inspecter la hiérarchie, exécuter du C#, gérer installs/licences/builds).
Doc : https://docs.unity.com/en-us/ai/unity-plugin/claude-code

## Vérifié sur cette machine
- Unity CLI 1.0.0-beta.8 dans C:\Users\pierr\AppData\Local\Unity\bin
- Éditeur installé : 6000.6.0f1 (Unity 6.6)
- Client MCP supporté : claude-code

## À faire au moment de créer Unity (PAS maintenant)
1. `unity projects new <nom> --template com.unity.template.urp-blank --editor 6000.6.0f1 --path C:\GameProject\UnityProject`
2. `unity mcp configure claude-code --project-path C:\GameProject\UnityProject`
3. Ouvrir une session Claude Code DANS C:\GameProject\UnityProject

## Règle
Après chaque changement de gameplay : laisser le MCP inspecter la scène et lire la console.

_Statut : workflow défini, activation différée._