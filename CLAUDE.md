# CLAUDE.md, constitution du projet

> Agent : lis ce fichier en entier, puis [`00_PROJECT/PROJECT_STATE.md`](00_PROJECT/PROJECT_STATE.md), avant toute action.

## 1. PROJECT IDENTITY
Jeu indé coop/social, PC/Steam, équipe de 2 (Miche1-Pierre + Spykernv). Concept : TBD (voir `02_GAME_DESIGN/GAME_CONCEPT.md`). Ce dépôt est le repository de décision ; `UnityProject/` n'en est qu'une partie.

## 2. CURRENT PROJECT STATUS
Phase : sélection du concept. Rien n'est construit dans Unity, par décision. Source de vérité : `00_PROJECT/PROJECT_STATE.md`, à lire et à tenir à jour.

## 3. DEVELOPMENT PHILOSOPHY
- On optimise la vitesse d'apprentissage avant la vitesse de production.
- Greybox avant art. Si c'est nul en cubes gris, l'art ne le sauvera pas.
- Systèmes réutilisables avant features spécialisées.
- Le concept émerge de la recherche, pas l'inverse.

## 4. GAME DESIGN PRINCIPLES
- Ne jamais ajouter une feature parce qu'elle semble intéressante. Vérifier son objectif et son impact sur le core loop.
- Toute nouvelle mécanique doit expliquer sa contribution au core loop.
- Échec amusant plutôt que punition frustrante.
- Viser la friction sociale lisible par un spectateur en moins de 10 secondes (thèse du benchmark, `01_RESEARCH/`).

## 5. TECHNICAL PRINCIPLES
- Simple et systémique plutôt que complexe et spécialisé.
- Pas de backend ni d'infra sans nécessité explicitement documentée.
- Pas d'optimisation prématurée. Mesurer avant.

## 6. UNITY RULES
- Unity 6 (6000.6.0f1), URP. Voir `03_TECHNICAL/UNITY_SETUP.md`.
- Piloter l'éditeur via le MCP Unity (`03_TECHNICAL/MCP_WORKFLOW.md`).
- Après tout changement de gameplay : inspecter la scène et lire la console (zéro erreur).

## 7. C# RULES
- Conventions C#/Unity standard. Noms clairs, petites classes, composition avant héritage.
- Définir les frontières d'architecture dans les docs avant de créer des classes. Ne pas générer 150 classes d'avance : le code naît quand le prototype en a besoin.

## 8. MCP WORKFLOW
Boucle : prompt → code → Unity → test → observation → correction. Détail : `03_TECHNICAL/MCP_WORKFLOW.md`.

## 9. GIT WORKFLOW
- Branches courtes, commits atomiques, messages clairs.
- Commit et push seulement sur demande explicite.
- Ne jamais committer `UnityProject/Library`, `Temp`, `Logs` (voir `.gitignore`).

## 10. TESTING RULES
- Après un changement de gameplay : playtest greybox et vérification des erreurs Unity.
- Bugs amusants vs destructeurs : voir `11_TESTING/BUG_POLICY.md`.

## 11. PERFORMANCE RULES
Cible 60 fps en greybox sur nos machines. Profiler avant d'optimiser.

## 12. MULTIPLAYER RULES
Pas de netcode définitif avant d'avoir figé le gameplay et le nombre de joueurs. Greybox en hot-seat ou local. Voir `07_MULTIPLAYER/`.

## 13. ASSET RULES
Pas d'asset définitif tant que le système concerné n'est pas validé. Pipeline et statuts : `05_ART/ASSET_STATUS.md`.

## 14. AI USAGE RULES
IA 3D (génération, Blender) pour accélérer un contenu déjà décidé, jamais pour décider du contenu.

## 15. SCOPE RULES
- Toute feature qui augmente significativement le scope : la signaler, ne pas l'implémenter en douce.
- Idées écartées : les écrire dans `04_PRODUCTION/REJECTED.md` pour ne pas les réinventer.

## 16. DECISION RULES
- Avant une modification importante, lire le(s) document(s) de design concerné(s).
- Toute décision structurante devient un ADR dans `decisions/`.
- Si une demande implique une décision de game design non résolue, la signaler au lieu de trancher seul.

## 17. CURRENT PRIORITIES
1. Choisir le concept. 2. Écrire la spec du greybox. 3. Rien d'autre.

## 18. DO NOT DO
- Ne pas créer le projet Unity tant que la spec n'est pas prête.
- Ne pas produire d'assets définitifs.
- Ne pas transformer une hypothèse en exigence.
- Ne pas créer d'infrastructure sans besoin documenté.

## Style d'écriture
Français. Pas de tirets cadratins (« — » ou « – ») : utiliser une virgule, une parenthèse ou deux-points. Concision.
