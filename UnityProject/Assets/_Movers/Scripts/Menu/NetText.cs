namespace Movers
{
    // The online words (NETCODE_SLICE 3.3, 3.4, 10), French first, added to the game's table
    // (Loc) through Loc.Register, like MenuText. Registered by the title screen, the loading
    // screen and the HUD when online; registering is all it does, so offline nothing changes.
    public static class NetText
    {
        static bool registered;

        public static void Ensure()
        {
            if (registered && Loc.Has("net.title")) return;
            registered = true;

            // Title screen and the online pages
            Loc.Register("menu.online", "Jouer en ligne", "Play online");
            Loc.Register("menu.onlineHint", "Deux PC, un code à partager", "Two PCs, one code to share");
            Loc.Register("net.title", "En ligne", "Online");
            Loc.Register("net.host", "Héberger", "Host");
            Loc.Register("net.hostHint", "Crée la partie et donne le code à ton partenaire", "Create the game and give the code to your partner");
            Loc.Register("net.hostDirect", "Héberger en IP directe", "Host by direct IP");
            Loc.Register("net.hostDirectHint", "Réseau local, VPN ou test : l'adresse remplace le code", "LAN, VPN or test: the address replaces the code");
            Loc.Register("net.join", "Rejoindre", "Join");
            Loc.Register("net.joinHint", "Entre le code que ton partenaire t'a donné", "Enter the code your partner gave you");
            Loc.Register("net.back", "Retour", "Back");

            // Host page
            Loc.Register("net.code", "Code de la partie", "Game code");
            Loc.Register("net.copy", "Copier le code", "Copy the code");
            Loc.Register("net.copied", "Code copié !", "Code copied!");
            Loc.Register("net.start", "Lancer la partie", "Start the game");
            Loc.Register("net.cancel", "Annuler", "Cancel");
            Loc.Register("net.creating", "Création de la partie...", "Creating the game...");
            Loc.Register("net.waiting", "En attente du joueur 2...", "Waiting for player 2...");
            Loc.Register("net.peerJoined", "Le joueur 2 est là !", "Player 2 is here!");
            Loc.Register("net.loading", "Chargement de la maison...", "Loading the house...");

            // Join page
            Loc.Register("net.field", "Code ou adresse IP", "Code or IP address");
            Loc.Register("net.paste", "Coller", "Paste");
            Loc.Register("net.connect", "Se connecter", "Connect");
            Loc.Register("net.connecting", "Connexion...", "Connecting...");
            Loc.Register("net.connected", "Connecté, l'hôte va lancer", "Connected, the host will start");

            // Errors (NetSession.LocKey)
            Loc.Register("net.services", "Services en ligne injoignables : vérifie ta connexion", "Online services unreachable: check your connection");
            Loc.Register("net.notLinked", "Relay n'est pas activé pour ce projet", "Relay is not enabled for this project");
            Loc.Register("net.payment", "Relay demande un paiement : jouez en IP directe", "Relay asks for a payment: play by direct IP");
            Loc.Register("net.badCode", "Code inconnu : vérifie-le", "Unknown code: check it");
            Loc.Register("net.failed", "Connexion impossible", "Could not connect");
            Loc.Register("net.version", "Versions différentes : recompilez les deux", "Different versions: rebuild both");
            Loc.Register("net.full", "La partie est déjà complète", "The game is already full");
            Loc.Register("net.hostLeft", "L'hôte a quitté la partie", "The host left the game");

            // In game
            Loc.Register("net.partnerLeft", "Ton partenaire a quitté la partie", "Your partner left the game");
            Loc.Register("pause.leave", "Quitter la partie", "Leave the game");

            // Loading screen crew row
            Loc.Register("load.you", "Toi", "You");
            Loc.Register("load.remote1", "J1 en ligne", "P1 online");
            Loc.Register("load.remote2", "J2 en ligne", "P2 online");
            Loc.Register("load.waitHost", "En attente de l'hôte...", "Waiting for the host...");
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            registered = false;
        }
    }
}
