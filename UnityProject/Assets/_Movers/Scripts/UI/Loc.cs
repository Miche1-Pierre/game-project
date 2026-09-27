using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Movers
{
    // The game's words, French first, English second. A key names a line of text; T(key) gives
    // it in the language of GameSettings. No package: the whole table is below, one line per
    // string, so a translation fix is one edit and a missing key shows as the key itself on
    // screen (loud in a playtest, never an exception).
    //
    // Formatting (F) allocates the string it returns: callers build text when what it says
    // changes, never per frame. Money and clock strings are cached for the same reason.
    public static class Loc
    {
        public static Language Current => GameSettings.Language;
        public static bool French => GameSettings.Language == Language.French;

        static Dictionary<string, string[]> table;
        static readonly List<string> missing = new List<string>();

        // Keys asked for that the table does not have, for the test that checks the table.
        public static IReadOnlyList<string> Missing => missing;

        public static string T(string key)
        {
            if (table == null) Build();
            if (key != null && table.TryGetValue(key, out var pair)) return pair[(int)Current];
            if (key != null && !missing.Contains(key)) missing.Add(key);
            return key ?? "";
        }

        // Another screen's own lines (the main menu, the loading screen), added at start-up
        // without editing this file. A key already in the table is replaced.
        public static void Register(string key, string french, string english)
        {
            if (table == null) Build();
            if (string.IsNullOrEmpty(key)) return;
            table[key] = new[] { french ?? key, english ?? french ?? key };
            missing.Remove(key);
        }

        public static bool Has(string key)
        {
            if (table == null) Build();
            return key != null && table.ContainsKey(key);
        }

        public static string F(string key, object a) => string.Format(T(key), a);
        public static string F(string key, object a, object b) => string.Format(T(key), a, b);
        public static string F(string key, object a, object b, object c) => string.Format(T(key), a, b, c);

        // ---- numbers ----

        static readonly StringBuilder sb = new StringBuilder(24);

        // "1 250 $" in French, "$1,250" in English. The game's money is dollars in both.
        public static string Money(int v)
        {
            sb.Length = 0;
            int a = Mathf.Abs(v);
            if (French)
            {
                if (v < 0) sb.Append('-');
                sb.Append(a.ToString("N0", FrenchNumbers)).Append("\u00A0$");
            }
            else
            {
                if (v < 0) sb.Append('-');
                sb.Append('$').Append(a.ToString("N0", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        // "+1 250 $" for a line that pays, the plain amount otherwise.
        public static string SignedMoney(int v) => v > 0 ? "+" + Money(v) : Money(v);

        static NumberFormatInfo frenchNumbers;
        static NumberFormatInfo FrenchNumbers
        {
            get
            {
                if (frenchNumbers == null)
                {
                    frenchNumbers = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                    frenchNumbers.NumberGroupSeparator = "\u00A0";   // no-break space: Fredoka has no narrow one
                    frenchNumbers.NumberDecimalSeparator = ",";
                }
                return frenchNumbers;
            }
        }

        // "09:41" for a count of seconds. Cached per second: the clock ticks once a second and
        // must not allocate every frame it is read.
        static readonly string[] clockCache = new string[100 * 60];
        public static string Clock(float seconds)
        {
            int s = Mathf.Clamp(Mathf.CeilToInt(seconds), 0, clockCache.Length - 1);
            return clockCache[s] ?? (clockCache[s] = (s / 60).ToString("00") + ":" + (s % 60).ToString("00"));
        }

        // Small integers as text, cached (percentages, counts, km/h).
        static readonly string[] intCache = new string[1000];
        public static string Int(int v)
        {
            if (v < 0 || v >= intCache.Length) return v.ToString(CultureInfo.InvariantCulture);
            return intCache[v] ?? (intCache[v] = v.ToString(CultureInfo.InvariantCulture));
        }

        // ---- names from the scene ----

        // The objects of the house are named in English in the scene (MovableObject.displayName).
        // In French they are shown through this table; a trailing variant letter is kept
        // ("Chair B" is "Chaise B"). A name the table does not know is shown as it is.
        static readonly Dictionary<string, string> itemFr = new Dictionary<string, string>
        {
            { "Alarm Clock", "Réveil" }, { "Armchair", "Fauteuil" }, { "Armchair Bedroom", "Fauteuil de la chambre" },
            { "Axe", "Hache" }, { "Barrel Garage", "Tonneau du garage" }, { "Barrel Old", "Vieux tonneau" },
            { "Bed", "Lit" }, { "Bedside Books", "Livres de chevet" }, { "Bird Cage", "Cage à oiseaux" },
            { "Books", "Livres" }, { "Bookshelf", "Bibliothèque" }, { "Box", "Carton" }, { "Box Attic", "Carton du grenier" },
            { "Box Workshop", "Carton de l'atelier" }, { "Brass Oil Lamp", "Lampe à huile en laiton" }, { "Broom", "Balai" },
            { "Bucket", "Seau" }, { "Cake", "Gâteau" }, { "Candlestick", "Bougeoir" }, { "Chair", "Chaise" },
            { "Coat Rack", "Portemanteau" }, { "Coffee Pot", "Cafetière" }, { "Coffee Table", "Table basse" },
            { "Crate", "Caisse" }, { "Cup", "Tasse" }, { "Dining Table", "Table à manger" }, { "DiningTable", "Table à manger" },
            { "Display Cabinet", "Vitrine" }, { "Dress Form", "Mannequin de couture" }, { "Dresser", "Commode" },
            { "Flask Set", "Flacons" }, { "Floor Lamp", "Lampadaire" }, { "Flower Pot", "Pot de fleurs" },
            { "Fridge", "Frigo" }, { "Fruit Bowl", "Coupe de fruits" }, { "Garage Shelves", "Étagères du garage" },
            { "Garden Bench Terrace", "Banc de la terrasse" }, { "Garden Table", "Table de jardin" },
            { "Gilt Travel Clock", "Pendulette dorée" }, { "Glasses", "Lunettes" }, { "Globe", "Globe" },
            { "Gold Crucifix", "Crucifix en or" }, { "Guest Bed", "Lit d'amis" }, { "Hall Table", "Console" },
            { "Hammer", "Marteau" }, { "Jar", "Bocal" }, { "Jewel Chest", "Coffret à bijoux" }, { "Kettle", "Bouilloire" },
            { "Keys", "Clés" }, { "Knitting Basket", "Panier à tricot" }, { "Lab Table", "Paillasse" }, { "Lamp", "Lampe" },
            { "Mantel Clock", "Pendule" }, { "Medicine Box", "Boîte à pharmacie" }, { "Microscope", "Microscope" },
            { "Model Ship", "Maquette de bateau" }, { "Nightstand", "Table de chevet" }, { "Old Cognac", "Vieux cognac" },
            { "Painting", "Tableau" }, { "Perfume", "Parfum" }, { "Photo Frame", "Cadre photo" }, { "Piano", "Piano" },
            { "Plant", "Plante" }, { "Plate", "Assiette" }, { "Porcelain Sugar Bowl", "Sucrier en porcelaine" },
            { "Porcelain Vase", "Vase en porcelaine" }, { "Potted Plant", "Plante en pot" }, { "Rocking Chair", "Fauteuil à bascule" },
            { "Saw", "Scie" }, { "Sewing Desk", "Table de couture" }, { "Sewing Machine", "Machine à coudre" },
            { "Shovel", "Pelle" }, { "Silver Candlestick", "Bougeoir en argent" }, { "Silver Christening Cup", "Timbale en argent" },
            { "Silver Photo Frame", "Cadre photo en argent" }, { "Slippers", "Pantoufles" }, { "Soap Perfume", "Savon parfumé" },
            { "Sofa", "Canapé" }, { "Stove", "Cuisinière" }, { "Suitcase", "Valise" }, { "Table", "Table" },
            { "Table Lamp", "Lampe de chevet" }, { "Telescope", "Télescope" }, { "Television", "Télévision" },
            { "Trunk", "Malle" }, { "Utensils", "Ustensiles" }, { "Vanity", "Coiffeuse" }, { "Vase", "Vase" },
            { "Veranda Table", "Table de véranda" }, { "Wardrobe", "Armoire" }, { "Wheelbarrow", "Brouette" },
            { "Wine", "Vin" }, { "Wine Barrel", "Tonneau de vin" }, { "Wine Bottle", "Bouteille de vin" },
            { "Workbench", "Établi" }, { "Yarn Ball", "Pelote de laine" }, { "Beer", "Bière" }, { "Cigarette", "Cigarette" },
            { "Grenade", "Grenade" }, { "Object", "Objet" },
        };

        static readonly Dictionary<string, string>[] itemCache = { new Dictionary<string, string>(), new Dictionary<string, string>() };

        public static string Item(string name)
        {
            if (string.IsNullOrEmpty(name)) return T("toast.something");
            var cache = itemCache[(int)Current];
            if (cache.TryGetValue(name, out var done)) return done;
            string clean = name.Replace('_', ' ').Trim();
            string result = clean;
            if (French)
            {
                if (itemFr.TryGetValue(clean, out var fr)) result = fr;
                else
                {
                    // "Chair B", "Crate G2": the variant stays, the noun is translated.
                    int sp = clean.LastIndexOf(' ');
                    if (sp > 0 && clean.Length - sp - 1 <= 2 && char.IsUpper(clean[sp + 1]) &&
                        itemFr.TryGetValue(clean.Substring(0, sp), out var baseFr))
                        result = baseFr + clean.Substring(sp);
                }
            }
            cache[name] = result;
            return result;
        }

        // The words an Interactable was authored with (English, in the scene or the code), in
        // the current language. Unknown words come back unchanged.
        static readonly Dictionary<string, string> promptKeys = new Dictionary<string, string>
        {
            { "Open", "verb.open" }, { "Close", "verb.close" }, { "Unlock", "verb.unlock" }, { "Lock", "verb.lock" },
            { "Locked", "verb.locked" }, { "Talk", "verb.talk" }, { "Drive", "verb.drive" }, { "Get out", "verb.getOut" },
            { "Drag", "verb.drag" }, { "Open the window", "verb.openWindow" }, { "Close the window", "verb.closeWindow" },
            { "Open the door", "prompt.openDoor" }, { "Close the door", "prompt.closeDoor" },
            { "Open the garage", "prompt.openGarage" }, { "Close the garage", "prompt.closeGarage" },
            { "Open the veranda door", "prompt.openVeranda" }, { "Close the veranda door", "prompt.closeVeranda" },
            { "No room to get out here", "drive.noRoom" },   // VehicleSeat.ActiveNotice
        };

        public static string Prompt(string english)
        {
            if (string.IsNullOrEmpty(english)) return "";
            return promptKeys.TryGetValue(english, out var key) ? T(key) : english;
        }

        // ---- the table ----

        static void Add(string key, string fr, string en) => table[key] = new[] { fr, en };

        static void Build()
        {
            table = new Dictionary<string, string[]>(256);

            // Contract board
            Add("contract.title", "Contrat", "Contract");
            Add("contract.keysFirst", "Clés d'abord", "Keys first");
            Add("contract.loaded", "Chargés {0}/{1}", "Loaded {0}/{1}");
            Add("contract.destroyed", "{0} détruit(s)", "{0} destroyed");
            Add("contract.payout", "Gain prévu", "Payout so far");
            Add("contract.payoutFinal", "Gain", "Payout");
            Add("contract.stolen", "Volé", "Stolen");
            Add("contract.unseen", "{0} en douce", "{0} unseen");
            Add("contract.seen", "{0} vu", "{0} seen");
            Add("state.missing", "À charger", "To load");
            Add("state.loaded", "Chargé", "Loaded");
            Add("state.delivered", "Livré", "Delivered");
            Add("state.pocketed", "En poche", "Pocketed");
            Add("state.worn", "Porté", "Worn");
            Add("state.destroyed", "Détruit", "Destroyed");
            Add("state.damaged", "Abîmé", "Damaged");

            // Banners
            Add("banner.intro", "Parle à Mamie pour avoir la liste et les clés", "Talk to Grandma to get the list and the keys");
            Add("banner.police", "LA POLICE ARRIVE  {0}", "THE POLICE ARE COMING  {0}");
            Add("banner.ready", "Tout est chargé : livre au panneau jaune du camion", "All loaded: deliver at the truck's yellow board");
            Add("police.short", "POLICE {0}", "POLICE {0}");

            // The grandmother
            Add("grandma.name", "Mamie", "Grandma");
            Add("grandma.patience", "Patience", "Patience");
            Add("mood.sweet", "Adorable", "Sweet");
            Add("mood.annoyed", "Agacée", "Annoyed");
            Add("mood.angry", "Fâchée", "Angry");
            Add("mood.furious", "Furieuse", "Furious");
            Add("mood.police", "Appelle la police !", "Calling the police!");

            // Toasts
            Add("toast.keys", "{0} a les clés. Le chrono tourne !", "{0} has the keys. The clock is running!");
            Add("toast.keysList", "Mamie vous confie la liste et les clés. Le chrono tourne !", "Grandma hands you the list and the keys. The clock is running!");
            Add("toast.breakIn", "Effraction ! {0} a cassé {1}. Le chrono tourne.", "Break-in! {0} broke {1}. The clock is running.");
            Add("toast.startedHow", "{0} {1} avant les clés. Le chrono tourne.", "{0} {1} before the keys. The clock is running.");
            Add("toast.pocketed", "{0} a empoché : {1} ({2})", "{0} pocketed the {1} ({2})");
            Add("toast.loot", "{0} est dans le camion. Pas sur la liste ({1})", "The {0} is in the truck. Not on the list ({1})");
            Add("toast.witnessed", "Mamie a vu {0} prendre : {1} !", "Grandma saw {0} take the {1}!");
            Add("toast.something", "quelque chose", "something");
            Add("toast.contractDamaged", "{0} est abîmé : moitié prix.", "The {0} is damaged: half pay.");
            Add("toast.contractDestroyed", "{0} est détruit : facturé {1}.", "The {0} is destroyed: billed {1}.");
            Add("toast.noticed", "Mamie a remarqué quelque chose...", "Grandma noticed something...");
            Add("toast.police", "Mamie appelle la police !", "Grandma is calling the police!");
            Add("toast.piece", "morceau", "piece");
            Add("what.window", "une fenêtre", "a window");
            Add("what.door", "une porte", "a door");
            Add("what.wall", "un mur", "a wall");
            Add("how.openedWindow", "a ouvert une fenêtre", "opened a window");
            Add("how.brokeWindow", "a cassé une fenêtre", "broke a window");
            Add("how.brokeDoor", "a cassé une porte", "broke a door");
            Add("how.brokeWall", "a percé un mur", "broke through a wall");
            Add("how.brought", "a fait tomber un bout de la maison", "brought part of the house down");

            // Pockets
            Add("pocket.handsFull", "Mains pleines", "Hands full");
            Add("pocket.empty", "Poche {0} vide", "Pocket {0} is empty");
            Add("pocket.none", "vide", "empty");
            Add("pocket.ticking", "ÇA VA PÉTER !", "IT'S TICKING!");

            // Verbs (key hints)
            Add("verb.grab", "Prendre", "Grab");
            Add("verb.drop", "Poser", "Drop");
            Add("verb.letGo", "Lâcher", "Let go");
            Add("verb.drag", "Traîner", "Drag");
            Add("verb.throw", "Lancer", "Throw");
            Add("verb.throwAway", "Jeter", "Throw away");
            Add("verb.rotate", "Tourner", "Rotate");
            Add("verb.roll", "Pivoter", "Roll");
            Add("verb.reach", "Rapprocher / éloigner", "Reach");
            Add("verb.pocket", "Empocher", "Pocket");
            Add("verb.takeOut", "Sortir de la poche", "Take out");
            Add("verb.smoke", "Fumer", "Smoke");
            Add("verb.drink", "Boire", "Drink");
            Add("verb.empty", "Bouteille vide", "Empty bottle");
            Add("verb.wear", "Enfiler", "Wear");
            Add("verb.takeOff", "Enlever", "Take off");
            Add("verb.pullPin", "Dégoupiller", "Pull the pin");
            Add("verb.throwGrenade", "Lancer la grenade", "Throw the grenade");
            Add("verb.releaseThrow", "Relâcher pour lancer", "Release to throw");
            Add("verb.open", "Ouvrir", "Open");
            Add("verb.close", "Fermer", "Close");
            Add("verb.openWindow", "Ouvrir la fenêtre", "Open the window");
            Add("verb.closeWindow", "Fermer la fenêtre", "Close the window");
            Add("verb.lock", "Verrouiller", "Lock");
            Add("verb.unlock", "Déverrouiller", "Unlock");
            Add("verb.locked", "Fermé à clé", "Locked");
            Add("verb.talk", "Parler", "Talk");
            Add("verb.drive", "Conduire", "Drive");
            Add("verb.getOut", "Descendre", "Get out");
            Add("verb.deliver", "Livrer {0}/{1}", "Deliver {0}/{1}");
            Add("verb.stopToGetOut", "Arrête-toi pour descendre", "Stop to get out");
            Add("verb.rampMoving", "La rampe bouge...", "Ramp moving...");
            Add("verb.handbrake", "Frein à main", "Handbrake");
            Add("verb.steer", "Accélérer, tourner", "Drive, steer");
            Add("verb.move", "Se déplacer", "Move");
            Add("verb.look", "Regarder", "Look");
            Add("verb.jump", "Sauter", "Jump");
            Add("verb.sprint", "Courir", "Sprint");
            Add("verb.crouch", "S'accroupir", "Crouch");
            Add("verb.pause", "Pause", "Pause");
            Add("verb.use", "Utiliser", "Use");
            Add("verb.controls", "Contrôles", "Controls");
            Add("prompt.openDoor", "Ouvrir la porte", "Open the door");
            Add("prompt.closeDoor", "Fermer la porte", "Close the door");
            Add("prompt.openGarage", "Ouvrir le garage", "Open the garage");
            Add("prompt.closeGarage", "Fermer le garage", "Close the garage");
            Add("prompt.openVeranda", "Ouvrir la véranda", "Open the veranda door");
            Add("prompt.closeVeranda", "Fermer la véranda", "Close the veranda door");
            Add("gesture.hold", "maintenir", "hold");
            Add("gesture.tap", "appuyer", "tap");
            Add("gesture.release", "relâcher", "release");

            // What is under the crosshair or in the hands
            Add("card.onList", "Sur la liste", "On the list");
            Add("card.hers", "À elle", "Hers");
            Add("card.crew", "À l'équipe", "The crew's");
            Add("card.fragile", "Fragile", "Fragile");
            Add("card.heavy", "Trop lourd : à traîner", "Too heavy: drag it");
            Add("card.heldBy", "Tenu par {0}", "Held by {0}");
            Add("card.kg", "{0} kg", "{0} kg");

            // Driving
            Add("drive.kmh", "km/h", "km/h");
            Add("drive.cargo", "Chargement", "Cargo");
            Add("drive.kg", "{0} / {1} kg", "{0} / {1} kg");
            Add("drive.over", "SURCHARGE", "OVERLOADED");
            Add("drive.noRoom", "Pas la place de descendre ici", "No room to get out here");

            // Drunk
            Add("drunk.label", "Pompette", "Tipsy");

            // Intro card
            Add("intro.title", "THE MOVERS", "THE MOVERS");
            Add("intro.subtitle", "La maison de Mamie", "Grandma's house");
            Add("intro.job", "Le boulot : charger les {0} objets de la liste dans le camion, puis livrer au panneau jaune, à l'arrière du camion, côté droit.",
                "The job: load the {0} things on the list into the truck, then deliver them at the yellow board on the truck's right side, at the back.");
            Add("intro.steal", "Tout le reste de ses affaires qui part avec vous est à vous... tant qu'elle ne vous voit pas le prendre.",
                "Anything else of hers that leaves with you is yours to sell, as long as she never sees you take it.");
            Add("intro.patience", "Casse, bruit, lenteur : elle perd patience. À bout, elle appelle la police.",
                "Break things, make noise, dawdle: she loses patience. At the end of it, she calls the police.");
            Add("intro.cta", "Parle à Mamie : elle vous donnera la liste et les clés", "Talk to Grandma: she will give you the list and the keys");
            Add("intro.start", "C'est parti", "Let's go");
            Add("intro.press", "Appuie sur", "Press");
            Add("intro.continue", "pour continuer", "to continue");

            // End screen
            Add("end.complete", "CONTRAT TERMINÉ", "CONTRACT COMPLETE");
            Add("end.failed", "MISSION RATÉE", "RUN FAILED");
            Add("end.timeUp", "Temps écoulé.", "Time is up.");
            Add("end.police", "Mamie a appelé la police.", "Grandma called the police.");
            Add("end.stopped", "La partie a été arrêtée.", "The run was stopped.");
            Add("end.total", "TOTAL", "TOTAL");
            Add("end.replay", "Rejouer", "Play again");
            Add("end.menu", "Menu", "Menu");
            Add("settle.intact", "Paie du contrat : {0} livré(s) intact(s)", "Contract pay: {0} delivered intact");
            Add("settle.damaged", "Paie partielle : {0} livré(s) abîmé(s)", "Part pay: {0} delivered damaged");
            Add("settle.missing", "Non livré : {0} laissé(s) sur place", "Not delivered: {0} left behind");
            Add("settle.destroyed", "Facturé : {0} de la liste détruit(s)", "Billed: {0} on the list destroyed");
            Add("settle.unseen", "Ses affaires prises en douce : {0}", "Her things nobody saw you take: {0}");
            Add("settle.confiscated", "Confisqué : {0} qu'elle vous a vu prendre", "Confiscated: {0} she saw you take");
            Add("settle.fine", "Amende pour ce qu'elle a vu", "Fine for what she saw");
            Add("settle.breakIn", "Effraction ({0})", "Break-in ({0})");
            Add("settle.damage", "dégâts", "damage");
            Add("settle.voidTime", "Temps écoulé : contrat annulé", "Time is up: the contract is void");
            Add("settle.voidPolice", "La police est venue : contrat annulé", "The police came: the contract is void");
            Add("settle.void", "Contrat annulé", "The contract is void");
            Add("settle.left", "Ses affaires que vous avez dû laisser : {0}", "Her things you had to leave: {0}");
            Add("settle.more", "et {0} de plus", "and {0} more");

            // Pause menu and options
            Add("pause.title", "Pause", "Paused");
            Add("pause.player", "Joueur {0}", "Player {0}");
            Add("pause.resume", "Reprendre", "Resume");
            Add("pause.options", "Options", "Options");
            Add("pause.controls", "Contrôles", "Controls");
            Add("pause.menu", "Retour au menu", "Back to menu");
            Add("pause.back", "Retour", "Back");
            Add("pause.select", "Choisir", "Select");
            Add("pause.ok", "Valider", "OK");
            Add("opt.title", "Options", "Options");
            Add("opt.master", "Volume général", "Master volume");
            Add("opt.music", "Musique", "Music");
            Add("opt.sfx", "Effets", "Effects");
            Add("opt.voice", "Voix", "Voices");
            Add("opt.ambience", "Ambiance", "Ambience");
            Add("opt.ui", "Interface", "Interface");
            Add("opt.sensitivity", "Sensibilité", "Look sensitivity");
            Add("opt.invertY", "Inverser l'axe vertical", "Invert Y");
            Add("opt.language", "Langue", "Language");
            Add("opt.layout", "Écran partagé", "Split screen");
            Add("opt.hints", "Aide des touches", "Key hints");
            Add("opt.on", "Oui", "On");
            Add("opt.off", "Non", "Off");
            Add("lang.fr", "Français", "Français");
            Add("lang.en", "English", "English");
            Add("layout.sideBySide", "Côte à côte", "Side by side");
            Add("layout.stacked", "Haut / bas", "Top / bottom");

            // Controls sheet
            Add("ctrl.title", "Contrôles", "Controls");
            Add("ctrl.onFoot", "À pied", "On foot");
            Add("ctrl.hands", "Les mains", "Hands");
            Add("ctrl.truck", "Au volant", "At the wheel");
            Add("ctrl.grab", "Prendre / poser", "Grab / drop");
            Add("ctrl.throw", "Lancer · maintenir : utiliser", "Throw · hold: use");
            Add("ctrl.rotate", "Maintenir : tourner l'objet", "Hold: rotate the object");
            Add("ctrl.reach", "Rapprocher / éloigner l'objet", "Push the object out / in");
            Add("ctrl.interact", "Ouvrir, parler, conduire, livrer", "Open, talk, drive, deliver");
            Add("ctrl.alt", "Enfiler, boire, enlever, clé", "Wear, drink, take off, key");
            Add("ctrl.pockets", "Poches 1 à 4", "Pockets 1 to 4");
            Add("ctrl.getOut", "Descendre", "Get out");
            Add("ctrl.toggle", "Afficher / cacher", "Show / hide");

            // Key names
            Add("key.lmb", "Clic G", "LMB");
            Add("key.rmb", "Clic D", "RMB");
            Add("key.wheel", "Molette", "Wheel");
            Add("key.mouse", "Souris", "Mouse");
            Add("key.space", "Espace", "Space");
            Add("key.shift", "Maj", "Shift");
            Add("key.ctrl", "Ctrl", "Ctrl");
            Add("key.esc", "Échap", "Esc");
            Add("key.tab", "Tab", "Tab");
            Add("key.enter", "Entrée", "Enter");
            Add("key.back", "Select", "Select");

            // Always-there corner hints
            Add("hud.controls", "Contrôles", "Controls");
            Add("hud.pause", "Pause", "Pause");
            Add("deliver.here", "LIVRER ICI", "DELIVER HERE");
            Add("player.label", "J{0}", "P{0}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            missing.Clear();
            itemCache[0].Clear();
            itemCache[1].Clear();
        }
    }
}
