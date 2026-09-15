# ADR-002 : Méthode, spec d'abord, greybox ensuite

## Statut
Accepté (2026-09-15)

## Contexte
Tentation de créer tout de suite le projet Unity. L'équipe veut d'abord figer la méthode et le concept.

## Décision
1. Tout spécifier (repository de décision + spec du greybox) AVANT de créer Unity.
2. Greybox en cubes gris, puis playtest, puis go/no-go, puis seulement la production visuelle.
3. Optimiser la vitesse d'apprentissage avant la vitesse de production.

## Pourquoi
- Si c'est nul en greybox, l'art ne le sauvera pas.
- Une équipe de 2 ne peut pas se permettre de contenu avant d'avoir prouvé le fun.
- Le concept doit émerger de la recherche, pas d'une hypothèse figée d'avance.

## Conséquences
- Pas de code Unity tant que `02_GAME_DESIGN/GREYBOX_SPEC.md` n'existe pas.
- Chaque décision structurante devient un ADR ; chaque hypothèse testée devient une entrée `experiments/`.

## Revisit if
La phase de spec devient un frein qui empêche d'apprendre (paralysie d'analyse) : basculer plus vite en proto.
