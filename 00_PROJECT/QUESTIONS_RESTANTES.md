# QUESTIONS RESTANTES

_Mis à jour le 17 septembre 2026, après la deuxième réunion de concept. Le journal complet avec toutes les réponses est dans `OPEN_QUESTIONS.md`._

**Il reste 2 questions ouvertes sur 29.** Vingt-deux sont tranchées, cinq ont été écartées volontairement.

---

## 1. Ouvert et bloquant, mais pas maintenant

### Q2. Quelle est la phrase unique qui justifie l'achat ?
Compléter sans citer un autre jeu: « Il faut acheter ce jeu parce que ______. »

**Reporté par décision.** L'équipe veut ressentir le concept en jeu avant d'écrire la phrase. La réponse vient juste après la greybox, pas avant.

Amorce retenue en réunion: l'impulsion du « et pourquoi pas ». Et pourquoi pas piquer ça. Et pourquoi pas faire cette connerie. Et qu'est-ce qui se passe si. C'est une direction, pas encore une phrase.

Le risque R6 reste actif tant que la phrase ne tient pas sans nommer un autre jeu.

**Réponse:**

### Q21. Quelle preuve nous ferait changer de direction commercialement ?
La courbe de wishlists Steam est désignée comme le signal qui tranche le prix et la qualité perçue. Il manque les seuils chiffrés, et rien n'est défini pour la rétention en playtest ni pour l'intérêt des créateurs.

Peut attendre l'ouverture de la page Steam.

**Réponse:**

---

## 2. Écartées volontairement, à rouvrir seulement si le projet devient sérieux commercialement

Décision du 16 septembre. Raison donnée: le projet est fait aussi pour le plaisir, et ces formalisations relèvent d'une logique de start-up qui n'est pas la vôtre.

- **Q12.** Disponibilité de Jonathan pendant le sprint.
- **Q13.** Répartition des responsabilités.
- **Q14.** Processus de décision en cas de désaccord.
- **Q15.** Niveau d'engagement commercial après le prototype.
- **Q22.** Capital réutilisable à construire en premier.

Ces sujets restent écrits dans `../08_BUSINESS/TEAM_AGREEMENT.md`, qui ne contient que les positions de Pierre. Rien n'est signé, et ça n'a pas d'importance tant que le jeu ne rapporte rien.

---

## 3. Points techniques encore flous à l'intérieur d'une question tranchée

Pas de nouvelles questions, mais trois détails à régler pendant le développement plutôt qu'en réunion.

- **Synchronisation physique en réseau.** C'est le plus gros risque technique du projet. Les deux équipes étudiées en benchmark l'ont désigné comme leur problème le plus dur, et la physique des objets est notre mécanique centrale.
- **Migration d'hôte.** Non traitée. Si l'hôte quitte, la partie meurt. Acceptable en greybox, à décider avant la bêta.
- **Voix.** Aucune décision. À trancher quand le multijoueur passera sur Steam.

---

## Ce qui a changé dans le dépôt
- `02_GAME_DESIGN/GREYBOX_SPEC.md` existe. La porte posée par ADR-002 est franchie, le projet Unity peut être créé.
- ADR-004 acte le concept, ADR-003 acte le réseau gratuit.
- `GAME_CONCEPT`, `CORE_LOOP`, `MILESTONES`, `TARGET_AUDIENCE`, `PRICING`, `PLAYTESTS`, `ASSET_STATUS` sont remplis.
- L'hypothèse H3 est retirée.
