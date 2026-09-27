namespace Movers
{
    // The title screen's and the loading screen's words, French first, added to the game's
    // table (Loc) at start-up through Loc.Register, so UICORE's file stays untouched. Shared
    // words (Options, Retour, Valider, the options' rows) are UICORE's own keys.
    public static class MenuText
    {
        static bool registered;

        public struct Tip
        {
            public string key;
            public string icon;         // a UiSprites icon
            public bool hasButton;
            public CrewButton button;   // the key shown in front of the tip, in the player's device
            public bool twoPlayers;     // only worth saying with two players
        }

        public static readonly Tip[] Tips =
        {
            new Tip { key = "tip.loot", icon = UiSprites.IconMoney },
            new Tip { key = "tip.ears", icon = UiSprites.IconGrandmaAnnoyed },
            new Tip { key = "tip.pockets", icon = UiSprites.IconPocket, hasButton = true, button = CrewButton.Pocket1 },
            new Tip { key = "tip.heavy", icon = UiSprites.IconCrate, hasButton = true, button = CrewButton.Grab },
            new Tip { key = "tip.grenade", icon = UiSprites.IconGrenade },
            new Tip { key = "tip.truck", icon = UiSprites.IconTruck, hasButton = true, button = CrewButton.Interact },
            new Tip { key = "tip.smoke", icon = UiSprites.IconCigarette, hasButton = true, button = CrewButton.Throw },
            new Tip { key = "tip.beer", icon = UiSprites.IconBeer, hasButton = true, button = CrewButton.Alt },
            new Tip { key = "tip.keys", icon = UiSprites.IconKey },
            new Tip { key = "tip.deliver", icon = UiSprites.IconCheck },
            new Tip { key = "tip.police", icon = UiSprites.IconGrandmaAngry },
            new Tip { key = "tip.duo", icon = UiSprites.IconBox, twoPlayers = true },
        };

        public static void Ensure()
        {
            if (registered && Loc.Has("menu.play1")) return;
            registered = true;

            // Title screen
            Loc.Register("menu.play1", "Jouer seul", "Play solo");
            Loc.Register("menu.play2", "Jouer à deux", "Play with two");
            Loc.Register("menu.options", "Options", "Options");
            Loc.Register("menu.controls", "Contrôles", "Controls");
            Loc.Register("menu.quit", "Quitter", "Quit");
            Loc.Register("menu.tagline", "Déménagement express, discrétion en option", "Express moving, discretion optional");
            Loc.Register("menu.padReady", "Manette branchée : le joueur 2 est prêt", "Gamepad found: player 2 is ready");
            // F1 (hand the keyboard to the other player) is a debug key: SliceDebug ignores it
            // outside the editor and development builds, so a release build must not promise it.
            Loc.Register("menu.padNone", "Pas de manette : le joueur 2 restera planté là (F1 lui passe le clavier)",
                         "No gamepad: player 2 will just stand there (F1 hands him the keyboard)");
            Loc.Register("menu.padNoneRelease", "Pas de manette : le joueur 2 restera planté là",
                         "No gamepad: player 2 will just stand there");
            Loc.Register("menu.solo", "Clavier et souris, rien que toi et Mamie", "Keyboard and mouse, just you and Grandma");
            Loc.Register("menu.duo", "Écran partagé : clavier pour J1, manette pour J2", "Split screen: keyboard for P1, gamepad for P2");
            Loc.Register("menu.optionsHint", "Le son, la vue, la langue", "Sound, view, language");
            Loc.Register("menu.controlsHint", "Toutes les touches, clavier ou manette", "Every key, keyboard or gamepad");
            Loc.Register("menu.quitHint", "À bientôt !", "See you soon!");
            Loc.Register("menu.keyboard", "Clavier et souris", "Keyboard and mouse");
            Loc.Register("menu.gamepad", "Manette", "Gamepad");
            Loc.Register("menu.build", "Tranche verticale · greybox", "Vertical slice · greybox");
            Loc.Register("menu.tab", "Changer", "Switch");
            Loc.Register("key.arrows", "Flèches", "Arrows");

            // Loading screen
            Loc.Register("load.toHouse", "EN ROUTE !", "ON OUR WAY!");
            Loc.Register("load.toHouseSub", "Direction la maison de Mamie", "Heading for Grandma's house");
            Loc.Register("load.toMenu", "RETOUR AU DÉPÔT", "BACK TO THE DEPOT");
            Loc.Register("load.toMenuSub", "On range le camion", "Parking the truck");
            Loc.Register("load.tip", "Astuce", "Tip");
            Loc.Register("load.p1", "J1", "P1");
            Loc.Register("load.p2", "J2", "P2");
            Loc.Register("load.noPad", "pas de manette", "no gamepad");
            Loc.Register("load.ready", "C'est prêt !", "Ready!");

            // Tips
            Loc.Register("tip.loot", "Tout ce qui part dans le camion sans être sur la liste est à vous. Si Mamie ne voit rien.",
                         "Anything that leaves in the truck without being on the list is yours. If Grandma sees nothing.");
            Loc.Register("tip.ears", "Mamie entend tout. Une assiette cassée à l'étage, et elle monte voir.",
                         "Grandma hears everything. One broken plate upstairs and she comes to look.");
            Loc.Register("tip.pockets", "Quatre poches par déménageur. Parfait pour une montre en or.",
                         "Four pockets per mover. Perfect for a gold watch.");
            Loc.Register("tip.heavy", "Trop lourd pour le porter ? Attrape-le quand même : ça se traîne.",
                         "Too heavy to carry? Grab it anyway: it drags.");
            Loc.Register("tip.grenade", "Une grenade vide une pièce très vite. Mamie ne trouve pas ça drôle.",
                         "A grenade empties a room very fast. Grandma does not find it funny.");
            Loc.Register("tip.truck", "Le camion se conduit, à la portière du chauffeur. Gare aux clôtures.",
                         "The truck can be driven, from the driver's door. Mind the fences.");
            Loc.Register("tip.smoke", "Cigarette en main, maintiens pour fumer. Mamie a horreur de ça.",
                         "Cigarette in hand, hold to smoke. Grandma can't stand it.");
            Loc.Register("tip.beer", "Une bière rend tout plus drôle. Et marcher droit plus difficile.",
                         "A beer makes everything funnier. And walking straight harder.");
            Loc.Register("tip.keys", "Parle à Mamie pour avoir les clés. Casser une fenêtre avant, c'est une effraction.",
                         "Talk to Grandma to get the keys. Breaking a window first is a break-in.");
            Loc.Register("tip.deliver", "Tout est chargé ? Livre au panneau jaune, à l'arrière du camion.",
                         "Everything loaded? Deliver at the yellow board, at the back of the truck.");
            Loc.Register("tip.police", "À bout de patience, Mamie appelle la police. Et le contrat s'envole.",
                         "Out of patience, Grandma calls the police. And the contract is gone.");
            Loc.Register("tip.duo", "À deux : pendant que l'un porte le canapé sous ses yeux, l'autre remplit ses poches.",
                         "With two: while one carries the sofa right under her nose, the other fills his pockets.");
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            registered = false;
        }
    }
}
