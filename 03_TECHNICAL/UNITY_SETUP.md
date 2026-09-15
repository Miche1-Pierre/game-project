# UNITY SETUP

_État vérifié et procédure de création. Création différée : pas de projet tant que la spec n'est pas prête (ADR-002)._

## Vérifié sur cette machine
- Unity CLI : 1.0.0-beta.8 (`C:\Users\pierr\AppData\Local\Unity\bin`, à préfixer au PATH si le shell ne le trouve pas).
- Éditeur installé : 6000.6.0f1 (Unity 6.6).
- Licence : Unity Personal.
- Template retenu : `com.unity.template.urp-blank` (Universal 3D).
- Client MCP supporté : `claude-code`.

## À configurer au démarrage
- Version Unity : 6000.6.0f1
- Render pipeline : URP
- Input : Input System (package)
- Physics : 3D (PhysX)
- Modules build : Windows Build Support (IL2CPP) quand on voudra un .exe (pas requis pour jouer en éditeur)
- Networking : différé (Steam P2P)
- MCP : plugin Unity pour Claude Code, doc https://docs.unity.com/en-us/ai/unity-plugin/claude-code
- Conventions de scène et de prefabs : à définir avec le premier greybox

## Commandes (le jour J)
- `unity projects new <nom> --template com.unity.template.urp-blank --editor 6000.6.0f1 --path C:\GameProject\UnityProject`
- `unity mcp configure claude-code --project-path C:\GameProject\UnityProject`
- Puis ouvrir une session Claude Code DANS `C:\GameProject\UnityProject`.
