using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // Wears things. Goes on a crew body, meaning the GameObject that carries the Animator whose
    // avatar is Humanoid.
    //
    // Two ways to wear a piece, and the piece decides which by what it is made of:
    //
    //   rigid   a mesh parented to a bone. Cheap, and it works because the character imports
    //           with optimizeGameObjects off, so the bone transforms exist in the scene.
    //   skinned a SkinnedMeshRenderer whose bones are re-pointed at the body's own bones. The
    //           piece was modelled over this body and exported with a copy of its armature, so
    //           re-binding is a rename lookup plus one change of basis, not a transfer of
    //           weights (BakeIntoBodySpace).
    //
    // Only a piece that crosses a joint that really bends has to be skinned, and in the first
    // lot that is the dressing gown alone (05_ART/CHARACTERS.md).
    //
    // Anchors resolve through the Humanoid avatar rather than by bone name, so this survives
    // changing character pack.
    public class CrewEquip : MonoBehaviour
    {
        public Animator animator;

        [Header("Skinned pieces")]
        // The renderer a skinned piece gets re-bound to. Captured once at Awake, on purpose:
        // after the first gown is on there are two skinned renderers under this body, and the
        // second piece must bind to the body rather than to the gown.
        public SkinnedMeshRenderer bodyRenderer;

        // What is worn, and what to put back when it comes off.
        class Worn
        {
            public EquipItem item;
            public Transform originalParent;
            public bool wasKinematic;
            public bool hadGravity;
            public Collider[] colliders;
            // An imported FBX can carry a compensation on its root scale, and assigning over it
            // is how the slippers first arrived one millimetre long. Kept, multiplied, restored.
            public Vector3 scale;
            public Vector3 secondScale;
            // Same story for rotation: an FBX exported without baking the axis conversion
            // carries it on the root, and assigning a world rotation lays the piece on its
            // back. Composed with, not replaced.
            public Quaternion rotation;
            public Quaternion secondRotation;

            // Skinned pieces only, all null or default for a rigid one.
            public SkinnedMeshRenderer skin;
            public Transform[] originalBones;
            public Transform originalRootBone;
            public bool originalUpdateWhenOffscreen;
            public Material[] originalMaterials;
            public Mesh originalMesh;
        }

        // A piece whose joints sit further than this from the body's, in metres once both are
        // brought into one space, was not modelled over this body.
        const float MaxJointError = 0.01f;

        readonly Dictionary<EquipSlot, Worn> worn = new Dictionary<EquipSlot, Worn>();

        // Body bone transforms by name, for the re-bind. Built on first use, because the
        // armature does not change once the character is in the scene.
        Dictionary<string, Transform> boneByName;

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning("[CrewEquip] no humanoid Animator under " + name + ", nothing can be worn.");
                enabled = false;
                return;
            }

            if (bodyRenderer == null) bodyRenderer = FindBodyRenderer();
        }

        // The body's own skinned renderer, meaning the one carrying the full armature. Picked by
        // bone count rather than by order, so a stray skinned prop already in the prefab cannot
        // win. Nothing is worn yet at Awake, so nothing worn can win either.
        SkinnedMeshRenderer FindBodyRenderer()
        {
            SkinnedMeshRenderer best = null;
            foreach (var r in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.bones == null || r.bones.Length == 0) continue;
                if (best == null || r.bones.Length > best.bones.Length) best = r;
            }
            return best;
        }

        public bool IsWearing(EquipSlot slot) => worn.ContainsKey(slot);

        public EquipItem WornIn(EquipSlot slot) => worn.TryGetValue(slot, out var w) ? w.item : null;

        // The bone a slot hangs from, or null. Chest walks down the spine because not every rig
        // maps UpperChest, and a missing bone should cost a warning rather than an exception.
        public Transform Anchor(EquipSlot slot, bool otherSide = false)
        {
            if (animator == null) return null;
            switch (slot)
            {
                case EquipSlot.Head:
                case EquipSlot.Face:  return Bone(HumanBodyBones.Head);
                // Not the ?? operator: it ignores the == overload Unity uses to report a
                // destroyed or unassigned object, so a fake null would pass straight through.
                case EquipSlot.Chest: return FirstMapped(HumanBodyBones.UpperChest, HumanBodyBones.Chest, HumanBodyBones.Spine);
                case EquipSlot.Waist: return Bone(HumanBodyBones.Hips);
                case EquipSlot.Hands: return Bone(otherSide ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                case EquipSlot.Feet:  return Bone(otherSide ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            }
            return null;
        }

        // Unity reports an unmapped bone as null, so this is the one place that check lives.
        Transform Bone(HumanBodyBones b)
        {
            var t = animator.GetBoneTransform(b);
            return t == null ? null : t;
        }

        Transform FirstMapped(params HumanBodyBones[] candidates)
        {
            foreach (var b in candidates)
            {
                var t = Bone(b);
                if (t != null) return t;
            }
            return null;
        }

        // Put it on. Returns false and says why rather than failing silently: the likeliest
        // causes are an unmapped bone and a piece exported with an armature that no longer
        // matches the body's, and neither is visible from the scene view.
        public bool Equip(EquipItem item)
        {
            if (!enabled || item == null) return false;

            var skin = SkinnedPieceOf(item);

            // Everything that can refuse runs before anything is touched, so a piece that
            // cannot be worn leaves the scene exactly as it found it.
            Transform anchor = null;
            Transform[] rebound = null;
            Mesh baked = null;

            if (skin != null)
            {
                rebound = MapBonesToBody(skin, item);
                if (rebound == null) return false;
                baked = BakeIntoBodySpace(skin, item);
                if (baked == null) return false;
            }
            else
            {
                anchor = Anchor(item.slot);
                if (anchor == null)
                {
                    Debug.LogWarning("[CrewEquip] the avatar maps no bone for slot " + item.slot + ", " + item.name + " stays on the floor.");
                    return false;
                }
            }

            if (worn.ContainsKey(item.slot)) Unequip(item.slot);

            var mo = item.GetComponent<MovableObject>();
            var rb = mo != null && mo.rb != null ? mo.rb : item.GetComponent<Rigidbody>();

            var record = new Worn
            {
                item = item,
                originalParent = item.transform.parent,
                colliders = item.GetComponentsInChildren<Collider>(),
                scale = item.transform.localScale,
                secondScale = item.secondPart != null ? item.secondPart.localScale : Vector3.one,
                rotation = item.transform.localRotation,
                secondRotation = item.secondPart != null ? item.secondPart.localRotation : Quaternion.identity,
            };

            // Physics off while worn. A gown with a live collider inside the player capsule
            // fights the CharacterController, and a worn object is not a physical object any
            // more, it is part of a body.
            if (rb != null)
            {
                record.wasKinematic = rb.isKinematic;
                record.hadGravity = rb.useGravity;
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            foreach (var c in record.colliders) c.enabled = false;

            if (skin != null)
            {
                BindSkin(skin, rebound, baked, record);
            }
            else
            {
                Fit(item.transform, anchor, item.localPosition, item.localEuler, record.scale, record.rotation, item.localScale);

                if (item.secondPart != null)
                {
                    var other = Anchor(item.slot, true);
                    if (other != null)
                        Fit(item.secondPart, other, item.secondLocalPosition, item.secondLocalEuler, record.secondScale, record.secondRotation, item.localScale);
                }
            }

            worn[item.slot] = record;
            return true;
        }

        // A skinned piece is one whose renderer actually has an armature. The test is the bone
        // array, not the component: the character pack's glasses are a SkinnedMeshRenderer with
        // zero bones, which is a static mesh wearing the wrong component, and it takes the
        // rigid path like any other prop.
        static SkinnedMeshRenderer SkinnedPieceOf(EquipItem item)
        {
            foreach (var r in item.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.sharedMesh != null && r.bones != null && r.bones.Length > 0) return r;
            return null;
        }

        // Re-points a piece's bones at the body's, by name.
        //
        // This is the whole of skinning support, and it is a lookup rather than a computation
        // because of how the piece is authored: modelled over this body in Blender and exported
        // with a copy of the body's own armature (05_ART/CHARACTERS.md). The piece arrives with
        // the same bone names and the same weights, on an armature that duplicates one already
        // in the scene. No weight has to be transferred, the duplicate just has to stop being
        // used; the mesh itself is moved into the body's bind space by BakeIntoBodySpace.
        //
        // Returns null and says which bone it could not place. A partial bind is worse than a
        // refusal: the vertices weighted to an unmatched bone collapse to the origin, and a gown
        // stretched to the world origin reads as an engine bug rather than as an export that
        // needs redoing.
        Transform[] MapBonesToBody(SkinnedMeshRenderer skin, EquipItem item)
        {
            if (bodyRenderer == null)
            {
                Debug.LogWarning("[CrewEquip] " + item.name + " is skinned, but no skinned body renderer was found under " + name + ", so there is no armature to bind it to.");
                return null;
            }
            if (bodyRenderer == skin)
            {
                Debug.LogWarning("[CrewEquip] " + item.name + " is the body renderer itself, refusing to bind it to itself.");
                return null;
            }

            var map = BoneMap();
            var src = skin.bones;
            var dst = new Transform[src.Length];
            List<string> missing = null;

            for (int i = 0; i < src.Length; i++)
            {
                var n = src[i] == null ? null : src[i].name;
                if (n != null && map.TryGetValue(n, out var t) && t != null) { dst[i] = t; continue; }
                if (missing == null) missing = new List<string>();
                missing.Add(n == null ? "<null>" : n);
            }

            if (missing != null)
            {
                Debug.LogWarning("[CrewEquip] " + item.name + " has " + missing.Count + " of " + src.Length
                    + " bones the body does not have, so it stays on the floor. Missing: "
                    + string.Join(", ", missing.ToArray(), 0, Mathf.Min(8, missing.Count))
                    + (missing.Count > 8 ? ", ..." : "")
                    + ". The piece has to be exported with the body's own armature, not a renamed copy.");
                return null;
            }

            return dst;
        }

        Dictionary<string, Transform> BoneMap()
        {
            if (boneByName != null) return boneByName;

            boneByName = new Dictionary<string, Transform>();
            foreach (var b in bodyRenderer.bones)
                if (b != null && !boneByName.ContainsKey(b.name)) boneByName.Add(b.name, b);

            var root = bodyRenderer.rootBone;
            if (root != null && !boneByName.ContainsKey(root.name)) boneByName.Add(root.name, root);

            // Leaf bones (hand.L_end, toe.R_end and the like) carry no weight, so the body's
            // renderer does not list them, but a piece exported with a copy of the armature does,
            // and a missing name refuses the whole bind. They are still transforms of the rig,
            // because the character imports with optimizeGameObjects off, so every name under the
            // root bone is added too. Measured 2026-09-25 on the robe: 36 bones, 27 in the body
            // renderer, the other 9 all leaves.
            if (root != null)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (!boneByName.ContainsKey(t.name)) boneByName.Add(t.name, t);

            return boneByName;
        }

        // Re-expresses a skinned piece in the body's bind space, so it skins with the body's own
        // bind poses. Returns a new mesh, or null and says why.
        //
        // Matching names is not the same as agreeing armatures. The piece's armature is a copy
        // of the body's as Blender re-imported it, and the re-import turns every bone's local
        // axes to Blender's convention: the joints stay exactly where they were, the frames do
        // not. Measured on the robe, 2026-09-25: all 19 weighted bones disagree with the body's
        // bind poses, the arms by 160 to 170 degrees, while the joint positions fit the body's
        // to 0.00 cm under a single uniform scale of 0.6956, the body's import factor. Skinned
        // with its own bind poses on the body's bones, the robe tore apart the moment the body
        // animated. An earlier check looked at the first shared bone only, the spine, and passed.
        //
        // So the frames are not trusted and the joints are. The map that sends the piece's joint
        // origins onto the body's, both read in bind pose, is fitted by least squares, the mesh
        // is moved through it, and every shared bone takes the body's bind pose. The piece then
        // deforms exactly as the body's own skin would at the same place with the same weights.
        // Joints that do not fit mean a genuinely different armature, and the piece is refused.
        //
        // The piece has to import with Read/Write enabled, since its vertices are read here.
        Mesh BakeIntoBodySpace(SkinnedMeshRenderer skin, EquipItem item)
        {
            var src = skin.sharedMesh;
            var body = bodyRenderer.sharedMesh;
            if (src == null || body == null) return null;
            if (!src.isReadable)
            {
                Debug.LogWarning("[CrewEquip] " + item.name + " imports without Read/Write, so its mesh cannot be moved onto the body. Tick Read/Write on its model importer.");
                return null;
            }

            var pieceBind = src.bindposes;
            var bodyBind = body.bindposes;
            var bodyBones = bodyRenderer.bones;
            var bodyIndex = new Dictionary<string, int>();
            for (int k = 0; k < bodyBones.Length && k < bodyBind.Length; k++)
                if (bodyBones[k] != null && !bodyIndex.ContainsKey(bodyBones[k].name)) bodyIndex.Add(bodyBones[k].name, k);

            var from = new List<Vector3>();
            var to = new List<Vector3>();
            var bind = (Matrix4x4[])pieceBind.Clone();
            var bones = skin.bones;
            for (int i = 0; i < bones.Length && i < pieceBind.Length; i++)
            {
                // Leaf bones carry no weight and have no bind pose on the body; they keep theirs.
                if (bones[i] == null || !bodyIndex.TryGetValue(bones[i].name, out int j)) continue;
                from.Add(pieceBind[i].inverse.GetColumn(3));
                to.Add(bodyBind[j].inverse.GetColumn(3));
                bind[i] = bodyBind[j];
            }

            float rms = float.PositiveInfinity;
            var map = from.Count >= 4 ? FitJoints(from, to, out rms) : Matrix4x4.identity;
            if (rms > MaxJointError)
            {
                Debug.LogWarning("[CrewEquip] the joints of " + item.name + " do not sit on the body's ("
                    + from.Count + " shared, rms " + (rms * 100f).ToString("F1") + " cm), so it stays on the floor. "
                    + "The piece has to be modelled over this body and exported with its armature.");
                return null;
            }

            var mesh = Instantiate(src);
            mesh.name = src.name + " (on body)";
            var normalMap = map.inverse.transpose;
            var v = src.vertices;
            for (int k = 0; k < v.Length; k++) v[k] = map.MultiplyPoint3x4(v[k]);
            mesh.vertices = v;
            var n = src.normals;
            for (int k = 0; k < n.Length; k++) n[k] = normalMap.MultiplyVector(n[k]).normalized;
            if (n.Length == v.Length) mesh.normals = n;
            var tan = src.tangents;
            for (int k = 0; k < tan.Length; k++)
            {
                var d = map.MultiplyVector(tan[k]).normalized;
                tan[k] = new Vector4(d.x, d.y, d.z, tan[k].w);
            }
            if (tan.Length == v.Length) mesh.tangents = tan;
            mesh.bindposes = bind;
            mesh.RecalculateBounds();
            return mesh;
        }

        // The least squares affine map A with A * from[k] as close as possible to to[k], and the
        // root mean square of what is left over. Public so the editor inspector reports the very
        // fit the game uses.
        public static Matrix4x4 FitJoints(List<Vector3> from, List<Vector3> to, out float rms)
        {
            // A = Y X^T (X X^T)^-1, with X the sources in homogeneous form and Y the targets.
            Matrix4x4 xxT = Matrix4x4.zero, yxT = Matrix4x4.zero;
            for (int k = 0; k < from.Count; k++)
            {
                var x = new Vector4(from[k].x, from[k].y, from[k].z, 1f);
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 4; c++)
                    {
                        xxT[r, c] += x[r] * x[c];
                        if (r < 3) yxT[r, c] += to[k][r] * x[c];
                    }
            }
            var a = yxT * xxT.inverse;
            a.SetRow(3, new Vector4(0f, 0f, 0f, 1f));

            float sum = 0f;
            for (int k = 0; k < from.Count; k++) sum += (a.MultiplyPoint3x4(from[k]) - to[k]).sqrMagnitude;
            rms = from.Count > 0 ? Mathf.Sqrt(sum / from.Count) : float.PositiveInfinity;
            return a;
        }

        // Hands the piece over to the body's armature.
        //
        // The transform is not what places a skinned piece: with bones assigned, Unity ignores
        // the renderer's own transform and builds every vertex from the bone matrices. So the
        // reparenting here is tidiness, and the piece lands wherever the body's bones are.
        //
        // The piece keeps its own localBounds, which were authored around its own armature, so
        // updateWhenOffscreen goes on: Unity then measures the real deformed mesh every frame
        // and the gown cannot vanish because its inherited bounds sit somewhere else. One small
        // mesh costs nothing to measure, and a piece that disappears at the wrong camera angle
        // is the kind of bug that gets blamed on the equip key.
        void BindSkin(SkinnedMeshRenderer skin, Transform[] rebound, Mesh baked, Worn record)
        {
            record.skin = skin;
            record.originalBones = skin.bones;
            record.originalRootBone = skin.rootBone;
            record.originalUpdateWhenOffscreen = skin.updateWhenOffscreen;
            record.originalMesh = skin.sharedMesh;

            skin.sharedMesh = baked;
            skin.bones = rebound;
            skin.rootBone = bodyRenderer.rootBone;
            skin.updateWhenOffscreen = true;

            // A garment that closes hides the torso panel, so its trim carries the player colour
            // instead (05_ART/CHARACTERS.md, amended 2026-09-20): slot 1 takes the crew material
            // off the body. The crew material is found by name, because the four crew prefabs
            // swap it into the body's first slot and nothing guarantees that stays slot 0.
            var mats = skin.sharedMaterials;
            var crew = CrewMaterial();
            if (mats.Length > 1 && crew != null)
            {
                record.originalMaterials = mats;
                var painted = (Material[])mats.Clone();
                painted[1] = crew;
                skin.sharedMaterials = painted;
            }

            var t = record.item.transform;
            t.SetParent(transform, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
        }

        // The material that says which player this is: MAT_Crew_0N on the body, else its first slot.
        Material CrewMaterial()
        {
            var mats = bodyRenderer != null ? bodyRenderer.sharedMaterials : null;
            if (mats == null || mats.Length == 0) return null;
            foreach (var m in mats)
                if (m != null && m.name.StartsWith("MAT_Crew")) return m;
            return mats[0];
        }

        // Places a piece on a bone using the BODY frame, not the bone frame.
        //
        // Measured on this pack, 2026-09-17: the head bone is spine.005 and its local +Z points
        // at the floor, because bone axes are whatever the rig author did in Blender. An offset
        // authored in bone space is therefore unreadable and unportable: "4 cm up" put the
        // glasses 10 cm below the skull. Against the body, x is right, y is up, z is forward,
        // which is what anyone tuning a slipper in the inspector expects.
        //
        // The world pose is set after parenting, so Unity works out the local transform and the
        // piece still follows the bone when the head turns.
        void Fit(Transform piece, Transform anchor, Vector3 offset, Vector3 euler, Vector3 baseScale, Quaternion baseRotation, float scale)
        {
            piece.SetParent(anchor, false);
            piece.position = anchor.position
                           + transform.right * offset.x
                           + transform.up * offset.y
                           + transform.forward * offset.z;
            piece.rotation = transform.rotation * Quaternion.Euler(euler) * baseRotation;
            // Multiplied into whatever the import left there, never assigned over it.
            piece.localScale = baseScale * scale;
        }

        // Take it off. It becomes an ordinary object of the house again, where the body is.
        public EquipItem Unequip(EquipSlot slot)
        {
            if (!worn.TryGetValue(slot, out var w)) return null;
            worn.Remove(slot);

            var item = w.item;
            if (item == null) return null;

            // Give the piece its own armature back before it leaves, so a gown on the floor is
            // the same object it was before it was worn, and can be picked up and worn again.
            if (w.skin != null)
            {
                // The baked copy only makes sense on this body; the piece's own mesh goes back.
                var baked = w.skin.sharedMesh;
                w.skin.sharedMesh = w.originalMesh;
                if (baked != null && baked != w.originalMesh) Destroy(baked);
                w.skin.bones = w.originalBones;
                w.skin.rootBone = w.originalRootBone;
                w.skin.updateWhenOffscreen = w.originalUpdateWhenOffscreen;
                if (w.originalMaterials != null) w.skin.sharedMaterials = w.originalMaterials;
            }

            if (item.secondPart != null)
            {
                item.secondPart.SetParent(item.transform, true);
                item.secondPart.localScale = w.secondScale;
                item.secondPart.localRotation = w.secondRotation;
            }

            item.transform.SetParent(w.originalParent, true);
            item.transform.localScale = w.scale;
            item.transform.localRotation = w.rotation;

            foreach (var c in w.colliders) if (c != null) c.enabled = true;

            var mo = item.GetComponent<MovableObject>();
            var rb = mo != null && mo.rb != null ? mo.rb : item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = w.wasKinematic;
                rb.useGravity = w.hadGravity;
            }

            return item;
        }
    }
}
