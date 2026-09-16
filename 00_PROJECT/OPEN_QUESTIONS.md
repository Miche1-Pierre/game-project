# QUESTIONS OUVERTES

_Journal des décisions par question. `[JONATHAN]` marque une question qui demandait une décision de Jonathan._

> **Note de langue.** Ce fichier et `QUESTIONS_RESTANTES.md` sont en français par demande explicite. Le reste du dépôt reste en anglais (`/CLAUDE.md`). Changer la langue de tout le dépôt serait une décision structurelle, non prise ici.

> **État au 17 septembre 2026.** Deux réunions de concept les 16 et 17 septembre ont tranché 22 questions sur 29. Cinq ont été écartées volontairement. **Deux restent ouvertes.** Le détail vivant est dans `QUESTIONS_RESTANTES.md`.

---

## Encore ouvertes (2)

### Q2. Quelle est la phrase unique qui justifie l'achat ?
**Reportée par décision, pas oubliée.** L'équipe veut ressentir le concept en jeu avant d'écrire la phrase, donc la réponse vient après la greybox.

Direction de travail issue de la réunion: l'impulsion du « et pourquoi pas ». Et pourquoi pas piquer ça, et pourquoi pas faire cette connerie, et qu'est-ce qui se passe si. C'est une amorce, pas encore une phrase de vente.

**C'est le point le plus important du projet.** Le risque R6 reste actif tant que la phrase ne tient pas debout sans citer un autre jeu.

_Statut: ouvert, à traiter juste après la greybox._

### Q21. Quelle preuve nous ferait changer de direction commercialement ?
Partiellement adressée. La courbe de wishlists Steam est désignée comme le signal qui tranche le prix et la qualité perçue. Aucun seuil chiffré n'est posé, et rien n'est défini pour la rétention en playtest ni pour l'intérêt des créateurs.

_Statut: ouvert. Peut attendre l'ouverture de la page Steam._

---

## Écartées volontairement (5)

Les questions d'équipe ont été écartées en réunion le 16 septembre. La raison donnée: le projet est fait aussi pour le plaisir, et formaliser ces points maintenant relève d'une logique de start-up qui n'est pas la leur.

- **Q12.** Disponibilité de Jonathan pendant le sprint. `[JONATHAN]`
- **Q13.** Répartition des responsabilités. `[JONATHAN]`
- **Q14.** Processus de décision en cas de désaccord. `[JONATHAN]`
- **Q15.** Niveau d'engagement commercial après le prototype. `[JONATHAN]`
- **Q22.** Capital réutilisable à construire en premier. `[JONATHAN]`

Ces sujets restent documentés dans `08_BUSINESS/TEAM_AGREEMENT.md`, qui ne contient à ce jour que les positions de Pierre. Rien n'est signé. Décision assumée, à rouvrir si le projet devient commercialement sérieux.

---

## Résolues (22)

### Direction du jeu

**Q1. Quel concept ?** Une équipe de déménageurs qui vide une maison pendant que le propriétaire est là, et vole ce qu'elle peut. Voir ADR-004.

**Q3. Quel verbe central ?** **Voler sous surveillance**, pas transporter. Le déménagement est la fiction qui donne une raison légitime de toucher à tout. Le vol sous observation est le jeu. Aucun camion conduisible en phase initiale.

**Q4. Garde-t-on H3 ?** Retirée. H3 comparait la détection par les joueurs à une IA de PNJ. Dans ce concept les joueurs sont dans le même camp, la comparaison ne s'applique pas.

**Q6. La cible de temps tient-elle ?** L'estimation produite par l'agent a été rejetée comme non fiable. Le plan de l'équipe fait foi: une semaine de greybox, une semaine de V1, un mois de communication en parallèle de la production, puis la bêta. Voir `04_PRODUCTION/MILESTONES.md`.

**Q16. Qu'est-ce qu'une greybox réussie ?** Le rire spontané est obligatoire. S'y ajoutent l'envie de tester quelque chose, l'envie de voir la réaction du propriétaire, et l'accrochage au système d'argent.

**Q17. Quel repli si le prototype échoue ?** Aucun, et c'est délibéré. Le projet est aussi fait pour le plaisir.

**Q26. La vigilance sans IA coûteuse ?** Oui. Pathfinding A* pour les déplacements, ce qui est un algorithme classique et non de l'IA. La détection se fait par conditions simples. Le vrai système de vigilance est reporté en V2, conditionné à la réussite de la V1.

**Q27. Périmètre du tutoriel ?** Dix pièces, environ 80 objets manipulables, deux PNJ vivants (le chat et le poisson), une seule interaction avec la grand-mère qui donne les clés. Ni boutique ni génération modulaire. Détail complet dans `02_GAME_DESIGN/GREYBOX_SPEC.md`.

**Q28. Où s'arrête l'absurde ?** Nulle part. Dérision assumée, aucune limite a priori. Seule conséquence pratique: les descripteurs de contenu Steam devront être remplis honnêtement au moment de la page boutique.

**Q29. Le camion ?** Une simple boîte creuse statique pour la greybox. Un vrai véhicule en V1.

### Technique

**Q5. Combien de joueurs ?** Quatre maximum, jouable en solo.

**Q7. Validation du multijoueur ?** Chacun en local d'abord, puis deux clients sur une machine via les VM de l'école, puis le réseau Steam en dernier.

**Q8. Réseau Steam dès le prototype ?** Non.

**Q9. Modèle d'autorité ?** Hôte joueur, pas de backend, pas de serveur dédié. Reste ouvert en interne: les objets synchronisés, la déconnexion, la migration d'hôte et la voix. La synchronisation physique demeure le plus gros risque technique.

**Q10. Assets achetés ou générés ?** Les deux. Packs gratuits en base, complétés par génération 3D par IA sur les abonnements existants. De l'argent seulement si la greybox valide.

**Q11. Quand construire l'outillage modulaire ?** Après la greybox, uniquement une fois le concept validé.

**Q23. Que sera le contenu généré par IA ?** Les assets. Conséquence acceptée: déclaration obligatoire sur la page Steam. Les assistants de code restent hors périmètre.

**Q24. Payer pour la vitesse réseau ?** Non. Tout ce qui peut être gratuit sera gratuit. Steam ou Unity, Photon exclu. Voir ADR-003.

### Validation et commercial

**Q18. Quels tests externes ?** Seize joueurs en quatre équipes de quatre. Une équipe avec les deux développeurs, une équipe d'amis laissée autonome, deux équipes d'inconnus. Les inconnus sont le signal.

**Q19. Quelle audience ?** Public casual qui rentre de l'école ou du travail et veut une bonne soirée entre amis. Groupe de quatre. Découverte par les créateurs et les lives, d'abord francophones puis internationaux. Achat et recommandation par effet de mode. Jeu non pérenne assumé.

**Q20. Quel prix ?** Fourchette acceptée de 4,99 à 14,99 dollars. Stratégie de volume plutôt que de marge. Le rendu final et la courbe de wishlists trancheront.

**Q25. Quand annoncer ?** Quand le tutoriel fonctionne de bout en bout en V1. Vidéo, page Steam et wishlists au même moment, puis un mois de contenu court pendant que les maps suivantes se construisent, puis la bêta.

---

## Priorité actuelle
1. Construire la greybox selon `02_GAME_DESIGN/GREYBOX_SPEC.md`.
2. Passer la porte du rire spontané.
3. Écrire la phrase de Q2, qui ne peut pas s'écrire avant.

## Décisions à documenter, faites
- ADR-004, choix de concept. Écrit.
- ADR-003, réseau gratuit uniquement. Mis à jour et accepté.
- `GAME_CONCEPT`, `CORE_LOOP`, `GREYBOX_SPEC`. Écrits.
- H3 retirée du tableau de bord. Fait.
