using UnityEngine;

namespace Movers
{
    // The four layers destruction sorts the world into, looked up by name (SLICE_ARCHITECTURE
    // section 10: 8 Structure, 9 Props, 10 Glass, 11 Debris).
    //
    // Why layers at all: a blast after a blast used to overlap every shard of the first one and
    // pay a component lookup per shard. With Debris on its own layer the blast and its cover rays
    // simply do not see it. Why by name: the integrator adds the layers to the TagManager, and a
    // scene opened before that (or a new map) must still work. A missing layer reads as -1, the
    // object stays on its own layer, and the queries fall back to "everything", which is exactly
    // how the game behaved before layers existed.
    public static class DestructionLayers
    {
        const int Unknown = -2;
        static int structure = Unknown, props = Unknown, glass = Unknown, debris = Unknown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            structure = props = glass = debris = Unknown;
        }

        public static int Structure => Get(ref structure, "Structure");
        public static int Props => Get(ref props, "Props");
        public static int Glass => Get(ref glass, "Glass");
        public static int Debris => Get(ref debris, "Debris");

        // What a blast, a cover ray or the debug crosshair looks at: everything but debris.
        public static int QueryMask
        {
            get
            {
                int d = Debris;
                return d >= 0 ? ~(1 << d) : ~0;
            }
        }

        // Moves one object (not its children) to a layer, when that layer exists. Objects on a
        // layer somebody else chose on purpose (not Default) keep it.
        public static void Assign(GameObject go, int layer)
        {
            if (go == null || layer < 0) return;
            if (go.layer != 0 && go.layer != layer) return;
            go.layer = layer;
        }

        // Debris always moves, whatever it was: a chunk of wall stops being structure the moment
        // it falls.
        public static void AssignDebris(GameObject go)
        {
            int d = Debris;
            if (go != null && d >= 0) go.layer = d;
        }

        static int Get(ref int cache, string name)
        {
            if (cache == Unknown) cache = LayerMask.NameToLayer(name);
            return cache;
        }
    }
}
