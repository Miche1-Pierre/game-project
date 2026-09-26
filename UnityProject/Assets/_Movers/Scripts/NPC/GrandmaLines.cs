namespace Movers
{
    // What she can say. Short lines, a spectator reads them in a glance (CLAUDE.md section 4).
    // English first; French variants for Pierre's sessions (GrandmaSpeech.french). "{0}" is
    // replaced by an object's name. Placeholder writing: the voice of the character is a design
    // decision still to come.
    public enum Line
    {
        Greeting, Intro1, Intro2, HouseRule, HereAreTheKeys, OffYouGo,
        Chat, ChatGrumpy,
        WhatWasThat, NothingThere, SmallNoise, Suspect,
        Smash, Window, Door, Explosion,
        Theft, Smoking, Drinking, Bumped, SeatTaken,
        Missing, MissingPacked, TooSlow,
        Confront, DropIt, GiveUp, Police, OnThePhone, LastWarning, LastChance,
        ExcuseMe, Blocked, BreakIn, Goodbye,
        Rock, Read, TV, Tea, Cook, Water, Fire, LookOutside,
        Count
    }

    public static class GrandmaLines
    {
        static readonly string[][] English = Build(false);
        static readonly string[][] French = Build(true);

        // What she says about something she perceived.
        public static Line For(in Stimulus s)
        {
            switch (s.kind)
            {
                case StimulusKind.SmallNoise: return Line.SmallNoise;
                case StimulusKind.WindowBroken: return Line.Window;
                case StimulusKind.DoorBroken: return Line.Door;
                case StimulusKind.Explosion: return Line.Explosion;
                case StimulusKind.Bumped: return Line.Bumped;
                case StimulusKind.SeatTaken: return Line.SeatTaken;
                case StimulusKind.Smoking: return Line.Smoking;
                case StimulusKind.Drinking: return Line.Drinking;
                case StimulusKind.TheftWitnessed:
                case StimulusKind.CarryingSeen: return Line.Theft;
                case StimulusKind.BehindSchedule: return Line.TooSlow;
                default: return s.seen ? Line.Smash : Line.WhatWasThat;
            }
        }

        // "my rocking chair": the object's display name, as she would say it.
        public static string NameOf(MovableObject item)
        {
            if (item == null) return null;
            return string.IsNullOrEmpty(item.displayName) ? item.name.ToLowerInvariant() : item.displayName.ToLowerInvariant();
        }

        public static string NameOf(UnityEngine.GameObject go)
        {
            if (go == null) return null;
            var mo = go.GetComponent<MovableObject>();
            return mo != null ? NameOf(mo) : go.name.Replace('_', ' ').ToLowerInvariant();
        }

        public static string[] Variants(Line line, bool french)
        {
            var table = french ? French : English;
            int i = (int)line;
            return i >= 0 && i < table.Length ? table[i] : null;
        }

        static string[][] Build(bool fr)
        {
            var t = new string[(int)Line.Count][];
            t[(int)Line.Greeting] = fr
                ? new[] { "Coucou ! Par ici, mes petits !", "C'est vous, les déménageurs ?" }
                : new[] { "Yoo-hoo! Over here, dears!", "Are you the movers?" };
            t[(int)Line.Intro1] = fr
                ? new[] { "Ah, les déménageurs ! Pile à l'heure." }
                : new[] { "Ah, the movers! Right on time." };
            t[(int)Line.Intro2] = fr
                ? new[] { "Ce qui est sur la liste part dans mon nouvel appartement. Le reste reste ici." }
                : new[] { "Everything on the list goes to my new flat. The rest stays here." };
            t[(int)Line.HouseRule] = fr
                ? new[] { "Et attention au vase du chat. Le chat vous surveille.", "Et on ne fume pas chez moi !" }
                : new[] { "And careful with the cat's vase. The cat is watching you.", "And no smoking in my house!" };
            t[(int)Line.HereAreTheKeys] = fr
                ? new[] { "Voilà les clés, mon grand." }
                : new[] { "Here are the keys, dear." };
            t[(int)Line.OffYouGo] = fr
                ? new[] { "Allez, au travail ! Je ne suis pas loin." }
                : new[] { "Off you go! I'll be right here." };
            t[(int)Line.Chat] = fr
                ? new[] { "Mon défunt mari a construit cette clôture.", "Un petit thé tout à l'heure ?", "Attention à l'escalier, il grince." }
                : new[] { "My late husband built that fence.", "Would you like some tea later?", "Mind the stairs, they creak." };
            t[(int)Line.ChatGrumpy] = fr
                ? new[] { "Pas de \"mamie\" avec moi. Au travail !", "Je vous ai à l'oeil." }
                : new[] { "Don't you \"dear\" me. Work!", "I'm watching you." };
            t[(int)Line.WhatWasThat] = fr
                ? new[] { "C'était quoi, ça ?!", "Il y a quelqu'un ?" }
                : new[] { "What was that?!", "Hello? Who's there?" };
            t[(int)Line.NothingThere] = fr
                ? new[] { "Hmm. Ça devait être le chat.", "Rien... mes oreilles, encore." }
                : new[] { "Hmm. Must have been the cat.", "Nothing... my ears again." };
            t[(int)Line.SmallNoise] = fr
                ? new[] { "Hm ?", "Doucement !" }
                : new[] { "Hm?", "Careful!" };
            t[(int)Line.Suspect] = fr
                ? new[] { "C'était vous ?!", "Vous avez cassé quelque chose ?" }
                : new[] { "Was that you?!", "Did you break something?" };
            t[(int)Line.Smash] = fr
                ? new[] { "Mon {0} !", "C'était à ma mère !" }
                : new[] { "My {0}!", "That was my mother's!" };
            t[(int)Line.Window] = fr
                ? new[] { "Ma fenêtre !", "Qui a cassé la fenêtre ?!" }
                : new[] { "My window!", "Who broke the window?!" };
            t[(int)Line.Door] = fr
                ? new[] { "Ma porte ! Vous êtes des sauvages ?" }
                : new[] { "My door! Are you animals?" };
            t[(int)Line.Explosion] = fr
                ? new[] { "C'ÉTAIT QUOI ÇA ?!", "La maison brûle ?!" }
                : new[] { "WHAT WAS THAT?!", "Is the house on fire?!" };
            t[(int)Line.Theft] = fr
                ? new[] { "Ce n'est pas sur la liste, jeune homme !", "Reposez mon {0} !" }
                : new[] { "That's not on the list, young man!", "Put my {0} back!" };
            t[(int)Line.Smoking] = fr
                ? new[] { "Pas dans ma maison !", "Éteignez-moi cette cigarette !" }
                : new[] { "Not in my house!", "Put that cigarette out!" };
            t[(int)Line.Drinking] = fr
                ? new[] { "On boit pendant le travail ?!" }
                : new[] { "Drinking on the job?!" };
            t[(int)Line.Bumped] = fr
                ? new[] { "Attention !", "Aïe, ma hanche !" }
                : new[] { "Watch it!", "Ooh, my hip!" };
            t[(int)Line.SeatTaken] = fr
                ? new[] { "J'étais assise dessus !" }
                : new[] { "I was sitting on that!" };
            t[(int)Line.Missing] = fr
                ? new[] { "Où est passé mon {0} ?", "Qui a pris mon {0} ?" }
                : new[] { "Where is my {0}?", "Who took my {0}?" };
            t[(int)Line.MissingPacked] = fr
                ? new[] { "Ah, mon {0} est déjà emballé." }
                : new[] { "Oh, my {0} is packed already." };
            t[(int)Line.TooSlow] = fr
                ? new[] { "Vous comptez y passer la journée ?", "Mon appartement ne va pas se meubler tout seul !" }
                : new[] { "Are you going to take all day?", "My new flat won't furnish itself!" };
            t[(int)Line.Confront] = fr
                ? new[] { "Vous ! Venez ici !", "J'ai vu ce que vous avez fait !" }
                : new[] { "You! Come here!", "I saw what you did!" };
            t[(int)Line.DropIt] = fr
                ? new[] { "Posez ça. Tout de suite.", "Donnez-moi ça !" }
                : new[] { "Put that down. Now.", "Give me that!" };
            t[(int)Line.GiveUp] = fr
                ? new[] { "Pff. Les jeunes." }
                : new[] { "Hmph. Young people." };
            t[(int)Line.Police] = fr
                ? new[] { "Ça suffit. J'appelle la police !" }
                : new[] { "That's it. I'm calling the police!" };
            t[(int)Line.LastWarning] = fr
                ? new[] { "Encore UNE bêtise et j'appelle la police !", "Dernier avertissement, jeunes gens !" }
                : new[] { "One more thing and I'm calling the police!", "Last warning, young people!" };
            t[(int)Line.LastChance] = fr
                ? new[] { "Bon... Je passe l'éponge. Pour cette fois.", "Hmph. Tenez-vous bien, maintenant." }
                : new[] { "Well... I'll let it go. This once.", "Hmph. Behave yourselves now." };
            t[(int)Line.OnThePhone] = fr
                ? new[] { "Allô, la police ? Oui, les déménageurs...", "Ils cassent tout, monsieur l'agent !" }
                : new[] { "Hello, police? Yes, the movers...", "They're wrecking the place, officer!" };
            t[(int)Line.ExcuseMe] = fr
                ? new[] { "Pardon, mon petit.", "Laissez passer une vieille dame !" }
                : new[] { "Excuse me, dear.", "Let an old lady through!" };
            t[(int)Line.Blocked] = fr
                ? new[] { "Qui a mis ça là ?!", "Je ne peux plus passer, maintenant !" }
                : new[] { "Who put that there?!", "Now I can't get through!" };
            t[(int)Line.BreakIn] = fr
                ? new[] { "Vous êtes entrés par effraction ?! J'avais les clés !" }
                : new[] { "You broke in?! I had the keys right here!" };
            t[(int)Line.Goodbye] = fr
                ? new[] { "Merci, mes petits. Roulez prudemment !" }
                : new[] { "Thank you, dears. Drive carefully!" };
            t[(int)Line.Rock] = fr ? new[] { "Ah, mon fauteuil." } : new[] { "Ah, my chair." };
            t[(int)Line.Read] = fr ? new[] { "Où en étais-je..." } : new[] { "Now where was I..." };
            t[(int)Line.TV] = fr ? new[] { "Oh, mon émission commence." } : new[] { "Oh, my show is on." };
            t[(int)Line.Tea] = fr ? new[] { "L'heure du thé." } : new[] { "Time for a cup of tea." };
            t[(int)Line.Cook] = fr ? new[] { "La soupe est presque prête." } : new[] { "Soup's almost ready." };
            t[(int)Line.Water] = fr ? new[] { "Voilà, mes chéries." } : new[] { "There you go, my darlings." };
            t[(int)Line.Fire] = fr ? new[] { "Une petite flambée, voilà qui est mieux." } : new[] { "A little fire, that's better." };
            t[(int)Line.LookOutside] = fr ? new[] { "Belle journée pour déménager." } : new[] { "Lovely day for moving." };
            return t;
        }
    }
}
