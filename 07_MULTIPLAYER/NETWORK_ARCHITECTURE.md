# NETWORK ARCHITECTURE

_Décision différée : on ne choisit pas le netcode avant d'avoir figé le gameplay et le nombre de joueurs (section 24)._

## Piste par défaut
Steam Networking (P2P relayé via Steam Datagram Relay), host/client, 4 joueurs pour commencer. Évite une infra serveur au départ.

## À trancher après le concept (créer un ADR)
- P2P vs serveur autoritaire
- Nombre de joueurs cible
- Autorité (host) et déconnexions
- Voix (proximity) intégrée ou tierce

## Greybox
Testable en hot-seat ou 2 PC, sans netcode définitif.

_Statut : différé, piste notée._