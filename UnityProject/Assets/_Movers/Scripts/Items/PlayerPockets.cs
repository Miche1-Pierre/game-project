using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Four pockets, on the number keys (the d-pad on a pad). Goes on the player, next to
    // PlayerGrab.
    //
    // ADR-007 made the cigarette and the beer real objects you carry in your hands, and that
    // stays true: a pocket holds the object itself, switched off and riding along with you,
    // and its button puts that same object back in your hands. Nothing is spawned or converted,
    // so what you pocket is exactly what comes out (a half-drunk beer is still half drunk, a
    // grenade with its pin out is still counting down).
    //
    // What fits: the crew's things with a use (cigarette, beer, grenade) and anything small
    // enough to be marked pocketable, which in the grandmother's house means her valuables
    // (ADR-009: pockets are one of the two ways out for what you steal). A chair does not fit,
    // and the button says so instead of doing something surprising.
    //
    // One button, decided by what is in your hands:
    //   holding a pocketable, pocket empty   -> it goes in
    //   hands empty, pocket filled           -> it comes out, into your hands
    //   holding a pocketable, pocket filled  -> swap
    //   holding anything else                -> nothing ("hands full")
    //
    // Runs just before PlayerGrab (default order 0), so something taken out this frame is
    // already in the hands when PlayerGrab reads the grab button, and PlayerInteract (-100)
    // still takes its presses first.
    [DefaultExecutionOrder(-10)]
    public class PlayerPockets : MonoBehaviour
    {
        public const int SlotCount = 4;

        static readonly CrewButton[] SlotButtons =
            { CrewButton.Pocket1, CrewButton.Pocket2, CrewButton.Pocket3, CrewButton.Pocket4 };

        [Header("Wiring (found automatically if left empty)")]
        public PlayerGrab grab;

        [Header("Start")]
        // The crew arrives with a smoke in pocket 1 and a beer in pocket 2, each player his own.
        // The van still lays out spares for whoever throws theirs away.
        public bool startWithCigaretteAndBeer = true;

        [Header("Feel")]
        // Where something taken out appears, in front of the eyes. The carry pulls it out to
        // your usual reach from there, so it is seen leaving the pocket rather than popping in
        // at arm's length.
        public float takeOutDistance = 0.55f;
        // Where pocketed things ride, relative to the player: roughly a trouser pocket. It is
        // also where a pocketed grenade goes off.
        public Vector3 pocketLocalPosition = new Vector3(0.22f, 0.85f, 0f);
        public float hintSeconds = 1.2f;

        // What is in each pocket right now. Empty while its item is out in your hands.
        readonly MovableObject[] slots = new MovableObject[SlotCount];
        // Particle systems that were running when each item went in. Switching an object off
        // stops its particles and switching it back on does not restart them, so the lit
        // cigarette would come out without its wisp.
        readonly List<ParticleSystem>[] playingWhenPocketed = new List<ParticleSystem>[SlotCount];
        // A swap can put the item in hand into the very pocket the other one leaves, so the
        // leaving item's record is copied here first, before Stow writes over that pocket's.
        readonly List<ParticleSystem> swapRecord = new List<ParticleSystem>();

        // The thing in your hands that came out of a pocket, and which one. The slot bar shows
        // it highlighted in its pocket, and a swap sends it back home rather than into the
        // pocket you just pressed, so the cigarette stays on 1 and the beer on 2.
        MovableObject outOfPocket;
        int outOfPocketSlot = -1;

        CrewInput input;
        Camera view;
        Transform pocketRoot;
        readonly RaycastHit[] rayHits = new RaycastHit[16];

        string hint;
        float hintUntil;

        // The slot bar is only rebuilt when what it shows changes: OnGUI runs several times a
        // frame and string building every time is steady garbage for no reason.
        readonly GUIContent barContent = new GUIContent("");
        int barKey = int.MinValue;
        bool barForPad;
        GUIStyle barStyle;
        GUIStyle hintStyle;

        // What is in a pocket, or null.
        public MovableObject GetItem(int index)
        {
            return index >= 0 && index < SlotCount && !Gone(slots[index]) ? slots[index] : null;
        }

        // The usable in a pocket (cigarette, beer, grenade), or null: the old question, kept.
        public HeldUsable GetSlot(int index)
        {
            var mo = GetItem(index);
            return mo != null && mo.TryGetComponent(out HeldUsable u) ? u : null;
        }

        public bool Contains(MovableObject mo)
        {
            if (mo == null) return false;
            for (int i = 0; i < SlotCount; i++)
                if (slots[i] == mo) return true;
            return false;
        }

        // Whether it would go in a pocket: the crew's usable things, or anything marked small.
        public static bool Fits(MovableObject mo)
        {
            return mo != null && (mo.pocketable || mo.TryGetComponent(out HeldUsable _));
        }

        // Nothing there any more: destroyed, shattered into debris, or a grenade that has gone
        // off this frame and is only waiting for the end of the frame to be destroyed.
        static bool Gone(MovableObject item)
        {
            if (item == null) return true;
            // TryGetComponent: this runs every frame per pocket, and a GetComponent that finds
            // nothing allocates in the editor.
            if (item.TryGetComponent(out GrenadeItem g) && g.HasExploded) return true;
            // Shattered by the destruction system: the object still exists, switched off, but
            // its debris is what is in the world now. Handing it back would bring it back whole.
            return item.destroyed;
        }

        // Out of the world is out of the truck. Switching an object off sends no OnTriggerExit,
        // so the cargo zone is told here, or its list would keep an entry for something that is
        // in a pocket or on a body (and later maybe blown up). Put back in the world inside the
        // truck, the zone picks it up again on its own. A button press, not a frame, so the
        // search costs nothing that matters.
        public static void LeaveTruck(MovableObject mo)
        {
            if (mo == null) return;
            mo.loaded = false;
            foreach (var cargo in FindObjectsByType<TruckCargo>())
                cargo.inside.Remove(mo);
        }

        void Awake()
        {
            input = CrewSetup.InputOf(gameObject);
            if (grab == null) grab = GetComponent<PlayerGrab>();
            if (grab == null)
            {
                Debug.LogWarning("[PlayerPockets] no PlayerGrab on " + name + ", pockets are disabled.");
                enabled = false;
                return;
            }

            for (int i = 0; i < SlotCount; i++) playingWhenPocketed[i] = new List<ParticleSystem>();

            // Switched off: everything under it is out of the world, no physics, no triggers,
            // no rendering, yet it still follows the player around.
            var go = new GameObject("Pockets");
            go.SetActive(false);
            pocketRoot = go.transform;
            pocketRoot.SetParent(transform, false);
            pocketRoot.localPosition = pocketLocalPosition;
        }

        void Start()
        {
            if (!startWithCigaretteAndBeer) return;
            // Built through the same factories the van uses, then put straight in. They exist
            // in the world for no physics step at all, and no event says they were pocketed:
            // the crew came with them.
            Vector3 at = pocketRoot.position;
            if (slots[0] == null) Stow(CigaretteItem.Create(at).GetComponent<MovableObject>(), 0, false);
            if (slots[1] == null) Stow(BeerItem.Create(at).GetComponent<MovableObject>(), 1, false);
        }

        void Update()
        {
            // Cheap, and it lets the pocket be moved from the Inspector during Play.
            pocketRoot.localPosition = pocketLocalPosition;

            // A grenade can go off in a pocket; what is left of it is nothing. Checked by what
            // the grenade says, not only by null: the fuse clock may have set it off earlier
            // this frame, and Destroy has not landed yet.
            for (int i = 0; i < SlotCount; i++)
            {
                if (ReferenceEquals(slots[i], null) || !Gone(slots[i])) continue;
                if (slots[i] != null) slots[i].inPocket = false;
                slots[i] = null;
                playingWhenPocketed[i].Clear();
            }

            // Out of a pocket counts only while it is still in your hands.
            if (outOfPocket != null && grab.Held != outOfPocket)
                ForgetOutOfPocket();
            else if (!ReferenceEquals(outOfPocket, null) && outOfPocket == null)
                ForgetOutOfPocket();

            for (int i = 0; i < SlotCount; i++)
            {
                if (!input.TryConsume(SlotButtons[i])) continue;
                Press(i);
                break;   // one pocket per frame, two buttons at once is a mash, not a plan
            }
        }

        void Press(int slot)
        {
            MovableObject held = grab.Held;
            // Shattered in your hands this very frame. The collision that broke it ran after
            // PlayerGrab last checked, so the hands still point at it. It is debris: not
            // something to pocket (it would come back out whole) and not something that fills
            // your hands. Counted as empty hands; Hold() below lets go of it properly, and so
            // does PlayerGrab's own next Update.
            if (held != null && (held.destroyed || !held.gameObject.activeInHierarchy)) held = null;
            MovableObject inPocket = slots[slot];

            if (held != null && (!Fits(held) || grab.IsDragging))
            {
                Hint("Hands full");
                return;
            }

            if (held == null)
            {
                if (inPocket == null) { Hint("Pocket " + ButtonLabels.For(input, SlotButtons[slot]) + " is empty"); return; }
                slots[slot] = null;
                TakeOut(inPocket, slot, playingWhenPocketed[slot]);
                return;
            }

            if (inPocket == null)
            {
                Stow(held, slot, true);
                return;
            }

            // Swap. What you are holding goes back to the pocket it came from when that one is
            // free, otherwise into this one; either way this pocket's item ends up in your hands.
            int home = held == outOfPocket && outOfPocketSlot >= 0 && slots[outOfPocketSlot] == null
                ? outOfPocketSlot
                : slot;
            slots[slot] = null;
            swapRecord.Clear();
            swapRecord.AddRange(playingWhenPocketed[slot]);
            playingWhenPocketed[slot].Clear();
            Stow(held, home, true);
            TakeOut(inPocket, slot, swapRecord);
        }

        // announce: an event says so (the grandmother may be watching). The starting smokes
        // go in silently.
        void Stow(MovableObject item, int slot, bool announce)
        {
            if (Gone(item)) return;   // debris or a spent grenade: nothing to put away

            // Let go properly first, so the carry gives the object its own damping back and a
            // cigarette in use stops smoking. Silent: it is not dropped, it is pocketed.
            if (grab.Held == item) grab.Release(false, false);
            if (item == outOfPocket) ForgetOutOfPocket();

            var playing = playingWhenPocketed[slot];
            playing.Clear();
            foreach (var ps in item.GetComponentsInChildren<ParticleSystem>())
                if (ps.isPlaying) playing.Add(ps);

            var rb = item.rb;
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            // Pocketed is not loaded, even if you did it standing in the truck.
            LeaveTruck(item);

            // Off before it moves, so the jump into the pocket is not a teleport the physics
            // engine gets to see.
            item.gameObject.SetActive(false);
            item.transform.SetParent(pocketRoot, true);
            item.transform.localPosition = Vector3.zero;
            item.inPocket = true;

            slots[slot] = item;
            if (announce)
                WorldEvents.Raise(WorldEventType.ItemPocketed, transform.position, grab.Actor, 0f, 0f, item.contractValue, item);
        }

        // record: the particle systems that were running when it went in; emptied here.
        void TakeOut(MovableObject item, int slot, List<ParticleSystem> record)
        {
            if (item == null) return;

            // World rotation is kept: it rode in the pocket in the pose it went in with,
            // turned with you, so it comes out the way you last held it.
            item.inPocket = false;
            item.transform.SetParent(null, true);
            item.transform.position = TakeOutPoint();
            item.gameObject.SetActive(true);

            for (int i = 0; i < record.Count; i++)
                if (record[i] != null && !record[i].isPlaying) record[i].Play();
            record.Clear();

            var rb = item.rb;
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            WorldEvents.Raise(WorldEventType.ItemUnpocketed, transform.position, grab.Actor, 0f, 0f, item.contractValue, item);

            // If the hands refuse it (they cannot, they were just emptied, but a refusal must not
            // lose the object) it simply drops where it appeared.
            if (grab.Hold(item))
            {
                outOfPocket = item;
                outOfPocketSlot = slot;
            }
        }

        // Just in front of the eyes, pulled in if a wall is closer than that.
        Vector3 TakeOutPoint()
        {
            Transform eye = grab.cam != null ? grab.cam : transform;
            Vector3 origin = grab.cam != null ? eye.position : transform.position + Vector3.up * 1.5f;
            Vector3 dir = eye.forward;

            float dist = takeOutDistance;
            int n = Physics.RaycastNonAlloc(origin, dir, rayHits, takeOutDistance + 0.1f,
                                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = rayHits[i].collider;
                if (c == null || c.transform.IsChildOf(transform)) continue;
                dist = Mathf.Min(dist, rayHits[i].distance - 0.1f);
            }
            return origin + dir * Mathf.Max(0.05f, dist);
        }

        void ForgetOutOfPocket()
        {
            outOfPocket = null;
            outOfPocketSlot = -1;
        }

        void Hint(string text)
        {
            hint = text;
            hintUntil = Time.time + hintSeconds;
        }

        // ---- the slot bar, bottom right of this player's view ----

        void OnGUI()
        {
            // Same layer as the HUD: the smoke may take the view, never the pockets.
            GUI.depth = 0;
            if (view == null) view = CrewView.Of(this);
            if (!CrewView.TryGetRect(view, out Rect r)) return;

            int size = ViewportGUI.FontSize(r, 15);
            if (barStyle == null)
            {
                barStyle = new GUIStyle(GUI.skin.box) { richText = true, alignment = TextAnchor.MiddleCenter };
                barStyle.padding = new RectOffset(10, 10, 4, 4);
                hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
            }
            bool sizeChanged = barStyle.fontSize != size;
            barStyle.fontSize = size;
            hintStyle.fontSize = size;

            bool forPad = input.Source is GamepadSource;
            int key = BarKey();
            if (key != barKey || forPad != barForPad || sizeChanged)
            {
                barKey = key;
                barForPad = forPad;
                barContent.text = BuildBar();
            }

            var bar = barStyle.CalcSize(barContent);
            var box = ViewportGUI.Region(r, HudRegion.BottomRight, bar.x, bar.y);
            GUI.Box(box, barContent, barStyle);

            // Left of the bar, kept inside the view when it is narrow.
            if (hint != null && Time.time < hintUntil)
                GUI.Label(new Rect(Mathf.Max(r.x + 12f, box.x - 260f), box.y, 250f, bar.y), hint, hintStyle);
        }

        // Everything the bar shows, packed in one int: which pockets are full, which one the
        // item in hand came from, which pocketed grenades are ticking.
        int BarKey()
        {
            int k = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] == null) continue;
                k |= 1 << i;
                if (slots[i].TryGetComponent(out GrenadeItem g) && g.IsArmed) k |= 1 << (8 + i);
            }
            k |= (outOfPocket != null ? outOfPocketSlot + 1 : 0) << 16;
            return k;
        }

        string BuildBar()
        {
            var sb = new System.Text.StringBuilder(96);
            for (int i = 0; i < SlotCount; i++)
            {
                if (i > 0) sb.Append("   ");
                string n = ButtonLabels.For(input, SlotButtons[i]);
                var item = slots[i];
                if (item != null)
                {
                    if (item.TryGetComponent(out GrenadeItem g) && g.IsArmed)
                        sb.Append(n).Append(" <color=#ff5040><b>").Append(Name(item)).Append("!</b></color>");
                    else
                        sb.Append(n).Append(' ').Append(Name(item));
                }
                else if (outOfPocket != null && outOfPocketSlot == i)
                    sb.Append("<color=#ffd24a><b>").Append(n).Append(' ').Append(Name(outOfPocket)).Append("</b></color>");
                else
                    sb.Append(n).Append(" -");
            }
            return sb.ToString();
        }

        static string Name(MovableObject item)
        {
            return !string.IsNullOrEmpty(item.displayName) ? item.displayName : item.name;
        }
    }
}
