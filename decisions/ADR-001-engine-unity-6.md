# ADR-001 : Moteur, Unity 6

## Statut
Accepté (2026-09-15)

## Contexte
Petite équipe (2), cible PC/Steam, besoin d'une boucle d'itération rapide pilotée par un agent IA.

## Options
- Unity 6
- Unreal Engine 5
- Godot

## Décision
Unity 6 (éditeur 6000.6.0f1), URP.

## Pourquoi
- Toute la vague coop virale étudiée (Lethal, PEAK, R.E.P.O., Content Warning, Schedule I, Phasmophobia, Among Us) est sous Unity : chemin éprouvé.
- Plugin Unity officiel pour Claude Code (MCP), sept. 2026 : contrôle live de l'éditeur par l'agent.
- Licence Unity Personal suffisante pour notre échelle.
- C# connu de l'équipe.

## Conséquences
- Dépendance à l'écosystème Unity et à son MCP.
- URP : bon compromis stylisé/perf, pas de HDRP.

## Revisit if
Le MCP Unity se révèle inexploitable, ou un besoin technique majeur non couvert par Unity apparaît.
