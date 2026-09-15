# DEVELOPMENT PLAN

_Comment on avance concrètement. L'ordre de prod est dans `00_PROJECT/ROADMAP.md`._

## Phase actuelle : concept vers greybox
1. Brainstorm concept avec Spykernv. Sortie : 1 (ou 2) concept(s) verrouillé(s) + un ADR.
2. Remplir le design minimal : GAME_CONCEPT, CORE_LOOP, GAME_RULES, MECHANICS, SYSTEMS, PLAYER_EXPERIENCE (et rien d'autre pour l'instant).
3. Écrire `02_GAME_DESIGN/GREYBOX_SPEC.md` : scène, joueurs, objets, interactions, systèmes, UI temporaire, conditions de victoire et de défaite, réseau, tests, et ce qui est explicitement hors scope.
4. Créer Unity (URP, git déjà en place, MCP branché), puis demander : "Create the greybox according to GREYBOX_SPEC.md".
5. Jouer. Nul → retour au design (pas de 3D). Amusant → verrouiller le core.
6. Seulement là : génération 3D / Blender / Asset Store / textures / audio.

## Règle de méthode
Prototyper plusieurs concepts concurrents si possible. Ne pas investir 2 semaines dans le premier concept juste parce qu'il était le premier.
