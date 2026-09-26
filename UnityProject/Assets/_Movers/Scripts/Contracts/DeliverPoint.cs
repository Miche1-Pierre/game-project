using UnityEngine;

namespace Movers
{
    // Where the job is handed over: a yellow board on the truck's right side, at its back end.
    // Look at it and press the action key once every object on the list that still exists is
    // in the truck. Before that it offers nothing, so it can never settle the contract by
    // accident; the prompt then reads "Deliver 7/12" when five of the twelve were smashed on
    // the way.
    //
    // It replaces the old "E anywhere on the map delivers", which only the keyboard player
    // could use and which fought the doors for the same key (A5b S16).
    //
    // No Unity messages here on purpose: Interactable belongs to the interaction code, which
    // may give it its own Awake or OnEnable, and a subclass must not hide them. ContractManager
    // finds or builds the board and hands it the contract.
    public sealed class DeliverPoint : Interactable
    {
        [Tooltip("Set by ContractManager at Start; found in the scene when empty.")]
        public ContractManager contract;

        string prompt = "Deliver";
        int promptLoaded = -1, promptTotal = -1;
        bool searched;

        // ContractManager hands itself over at Start. Before that (or in a scene that has a
        // board but no contract) the scene is searched once, not every frame the board is seen.
        ContractManager Contract
        {
            get
            {
                if (contract == null && !searched)
                {
                    searched = true;
                    contract = SceneLookup.Find<ContractManager>(gameObject.scene);
                }
                return contract;
            }
        }

        public bool Ready
        {
            get
            {
                var c = Contract;
                return c != null && c.AllRequiredLoaded();
            }
        }

        public override bool CanInteract
        {
            get
            {
                if (!Session.IsRunning || !Ready) return false;
                var s = GameSession.Current;
                return s == null || !s.PoliceCalled;
            }
        }

        // Rebuilt only when the count changes: the prompt is read every frame it is looked at.
        public override string Prompt
        {
            get
            {
                var c = Contract;
                if (c == null) return prompt;
                var t = c.Tracker;
                int loaded = t.RemainingLoaded, total = t.Total;
                if (loaded != promptLoaded || total != promptTotal)
                {
                    promptLoaded = loaded;
                    promptTotal = total;
                    prompt = "Deliver " + loaded + "/" + total;
                }
                return prompt;
            }
        }

        public override void Interact(PlayerInteract by)
        {
            if (!CanInteract) return;
            var member = by != null ? by.GetComponent<CrewMember>() : null;
            int actor = member != null ? member.index : 0;
            var s = GameSession.Current;
            if (s != null) s.Deliver(actor, transform.position);
        }

        // ---- building one ----

        // The board: a yellow plate 0.5 m wide, 0.6 m tall and 3 cm thick, flat against the
        // outer face of the truck's right wall at its back end, facing out, at the eye height of
        // someone standing on the ground. Flat so it adds no paddle to the truck: once the truck
        // is one rigid body that drives, anything sticking out catches gate posts and her car.
        // On the side, not across the open back, so it is never in the way of a sofa going up
        // the ramp. Measured on Map01 (the right wall is 8 cm thick, its outer face at x 20.71):
        // the plate sticks out about 3 cm. The same rule fits the Tutorial_01 truck, whose open
        // side is also local +Z and whose right wall is 20 cm thick.
        static readonly Vector3 BoardSize = new Vector3(0.5f, 0.6f, 0.03f);
        const float AboveBed = 0.55f;   // plate centre above the bottom of the cargo zone (m)
        const float Gap = 0.002f;       // clearance kept from the wall
        const float Probe = 1f;         // how far outside the wall the search starts (m)
        const float Step = 0.005f;      // outward step when something else is in the way
        const int MaxSteps = 60;

        // Builds a board on the cargo zone of this truck: the scene recipe calls it in the
        // editor, and ContractManager calls it at Play in a scene that has none (Tutorial_01).
        // material: the board's look; null tints the default material yellow at runtime.
        // Everything in world space, so a scaled cargo zone does not stretch the plate.
        public static DeliverPoint CreateAt(TruckCargo cargo, ContractManager contract, Material material)
        {
            if (cargo == null) return null;
            Transform zt = cargo.transform;
            var zone = cargo.GetComponent<BoxCollider>();
            Vector3 s = zt.lossyScale;
            Vector3 half = zone != null
                ? new Vector3(Mathf.Abs(zone.size.x * s.x), Mathf.Abs(zone.size.y * s.y), Mathf.Abs(zone.size.z * s.z)) * 0.5f
                : Vector3.one;
            Vector3 centre = zone != null ? zt.TransformPoint(zone.center) : zt.position;
            Vector3 right = zt.right, up = zt.up, back = zt.forward;   // local +Z is the open back

            // On the zone's middle line, at plate height, its back edge at the end of the zone.
            Vector3 line = centre + up * (AboveBed - half.y) + back * (half.z - BoardSize.x * 0.5f);
            Transform truck = zt.parent != null ? zt.parent : zt;
            float side = OuterFace(line, right, half.x, truck, zone);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "DeliverPoint";
            go.transform.localScale = BoardSize;
            go.transform.SetPositionAndRotation(line + right * (side + BoardSize.z * 0.5f + Gap),
                                                Quaternion.LookRotation(right, up));
            // Something else of the truck's there (a mirror, a light): out until it is clear.
            var col = go.GetComponent<Collider>();
            for (int i = 0; i < MaxSteps && Blocked(go.transform, col); i++)
                go.transform.position += right * Step;
            go.transform.SetParent(zt, true);

            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                if (material != null) r.sharedMaterial = material;
                else if (Application.isPlaying) r.material.color = new Color(1f, 0.78f, 0.1f);
            }

            var dp = go.AddComponent<DeliverPoint>();
            dp.contract = contract;
            return dp;
        }

        static readonly Collider[] overlap = new Collider[8];
        static readonly RaycastHit[] rays = new RaycastHit[16];

        // How far from the middle line the truck's right side is (m): a ray from outside,
        // inward, and the nearest of the truck's own colliders it meets. No wall there (an open
        // side): the zone's own edge.
        static float OuterFace(Vector3 line, Vector3 right, float halfWidth, Transform truck, Collider zone)
        {
            Physics.SyncTransforms();
            float reach = halfWidth + Probe;
            int n = Physics.RaycastNonAlloc(line + right * reach, -right, rays, reach,
                                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = rays[i].collider;
                if (c == null || c == zone || !c.transform.IsChildOf(truck)) continue;
                nearest = Mathf.Min(nearest, rays[i].distance);
            }
            return nearest < float.MaxValue ? reach - nearest : halfWidth;
        }

        static bool Blocked(Transform board, Collider own)
        {
            Vector3 half = board.lossyScale * 0.5f + Vector3.one * Gap * 0.5f;
            int n = Physics.OverlapBoxNonAlloc(board.position, half, overlap, board.rotation,
                                               Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (overlap[i] != own) return true;
            return false;
        }
    }
}
