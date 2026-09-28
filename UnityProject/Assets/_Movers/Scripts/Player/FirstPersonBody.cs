using System.Collections.Generic;
using UnityEngine;

namespace Movers
{
    // A player's own camera does not draw the inside of that player's head, but does draw the
    // legs and feet under it. Goes on the player root, next to CrewAnimator.
    //
    // The eyes sit inside the body's skull, so without this the first-person view shows teeth,
    // shoulders and a pair of arms held out for a box. The body must still exist for everyone
    // else: the other player reads you by it, and the grandmother's mirror is there so you can
    // see what you are wearing.
    //
    // Chosen over the two usual answers:
    //   - a layer per player, culled by its own camera: needs a layer for each player on top
    //     of the slice's six, and every worn piece moved to it and back, since worn things are
    //     parented to the bones and keep their own layer;
    //   - pushing the camera forward out of the face: the arms and the chest still come into
    //     view as soon as you look down, and the eyes stop matching the body.
    // Here, just before each camera culls, the body is switched to "shadows only" if that
    // camera is this player's own, and back otherwise. Everything under the body counts, worn
    // pieces included, and you keep your own shadow on the floor. A camera rendered from inside
    // another one (the mirror renders during the view that sees it) is handled with a small
    // stack, so the mirror shows you and your own view, which resumes after it, still does not.
    //
    // Looking down, you see your legs and feet (and the slippers on them):
    //   - a rigid piece on a leg bone (the slippers) is simply left drawn;
    //   - a skinned mesh (the body, the dressing gown) gets a companion that shares its bones
    //     but keeps only what hangs from the hips and legs below the top of the thighs
    //     (LowerHalf): skinned there, and under cutBelowHips in the bind pose, so a gown's skirt,
    //     skinned to the hips, stays in. Drawn by this player's camera alone, without a shadow
    //     (the whole mesh still casts it). Not the hips themselves: the eyes are right above
    //     them, and the belt, 18 cm deep in front, hid the whole of the legs (measured). On the
    //     body every hole the cut opens is closed with a flat cap in the cloth along it (the
    //     tops of the thighs); a worn piece stays open and two-sided, and you look into the
    //     gown at your legs. Holes the mesh always had (the gown's hem) stay open. The skull, the
    //     chest and the shoulders, which would fill the view or cut the near plane, are never in
    //     it; the arms are FirstPersonHands'. Built the first time the own camera needs it, and
    //     again when the mesh under it changes (a gown put on, taken off).
    //   - the camera sits in the middle of the skull, 0.15 m behind the body's own eyes (measured),
    //     and straight above the hips, so the thighs would hide the shins. As you look down the
    //     picture moves forward, to the face and on as a lean over the feet would (ViewOffset,
    //     so the picture only: the carry, the grab and every cast still go from the camera),
    //     and turns so that what was in the middle of the view, the floor the look lands on,
    //     stays there. Your hands being full (the carry keeps things 10 cm from the eyes), the
    //     wheel, a fall and a view that is off take it back. A fall also hides the whole body
    //     again: it lies on the floor on its own clock while the view stands back up.
    //
    // Nothing is carried from one render to the next: the body is whole again as soon as the
    // outermost render ends, and every render of the own camera looks at the body afresh. With
    // only one camera on (solo layouts, a one-player scene) nothing else would ever put it back,
    // and a dressing gown taken off after the first frame would lie on the floor as a shadow.
    //
    // At the wheel the same camera is the truck's chase camera, looking at the driver from
    // outside: the body is left whole then.
    [DisallowMultipleComponent]
    public sealed class FirstPersonBody : MonoBehaviour
    {
        // Left empty: the player's camera and the Animator under this player.
        public Camera eyes;
        public Transform body;

        [Tooltip("How much of a vertex's skin weight must sit on the hips and legs for it to be drawn in your own view (keeps a hand hanging at the thigh out).")]
        [Range(0.1f, 1f)] public float lowerWeight = 0.5f;
        [Tooltip("Metres under the hip joints, in the bind pose, where your own view's legs start: the belt and the hips above it would hide them.")]
        public float cutBelowHips = 0.1f;

        [Header("Looking down")]
        [Tooltip("Metres the picture moves forward at most as you look down, the face (0.15 m ahead of the camera) and a lean over the feet: from 0.22 m the whole shoe shows past the thighs, measured. Stays under the capsule's radius (0.35) minus the near plane, so it never goes through a wall.")]
        public float lookDownForward = 0.25f;
        [Tooltip("Degrees below the horizon where the picture starts to move forward, and where it has moved all the way.")]
        public Vector2 lookDownPitch = new Vector2(25f, 75f);
        [Tooltip("Seconds to move forward or back when it becomes allowed or not (the hands filling, a fall).")]
        public float lookDownFadeSeconds = 0.25f;

        // One skinned mesh under the body and its legs companion (null when the mesh has
        // no such triangles, or cannot be read).
        sealed class Legs
        {
            public SkinnedMeshRenderer source;
            public Mesh sourceMesh;
            public SkinnedMeshRenderer companion;
            public Mesh mesh;
            public bool seen;
        }

        readonly List<Renderer> scratch = new List<Renderer>(16);
        readonly List<Renderer> madeShadowOnly = new List<Renderer>(16);
        readonly List<Renderer> madeInvisible = new List<Renderer>(4);
        readonly List<Legs> legs = new List<Legs>(4);
        readonly List<Material> matsA = new List<Material>(12), matsB = new List<Material>(12);
        readonly Camera[] stack = new Camera[4];
        int depth;
        bool hidden;
        CrewMember member;
        PlayerController controller;
        CrewAnimator crewAnimator;
        KnockdownTumble tumble;
        float lookDownWeight;
        Animator animator;
        Transform hips, spine, leftLeg, rightLeg, leftFoot;
        Transform holder;

        void OnEnable()
        {
            Resolve();
            Camera.onPreCull += OnCameraPreCull;
            Camera.onPostRender += OnCameraPostRender;
        }

        void OnDisable()
        {
            Camera.onPreCull -= OnCameraPreCull;
            Camera.onPostRender -= OnCameraPostRender;
            Show();
            depth = 0;
        }

        void OnDestroy()
        {
            for (int i = 0; i < legs.Count; i++) Drop(legs[i]);
            legs.Clear();
        }

        void Resolve()
        {
            if (member == null) member = GetComponent<CrewMember>();
            if (controller == null) controller = GetComponent<PlayerController>();
            if (crewAnimator == null) crewAnimator = GetComponent<CrewAnimator>();
            if (tumble == null) tumble = GetComponent<KnockdownTumble>();
            if (eyes == null && controller != null && controller.cam != null) eyes = controller.cam.GetComponent<Camera>();
            if (body == null)
            {
                var anim = GetComponentInChildren<Animator>(true);
                if (anim != null) body = anim.transform;
            }
            if (animator == null && body != null) animator = body.GetComponent<Animator>();
            if (leftLeg == null && animator != null && animator.isHuman)
            {
                leftLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                rightLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            }
        }

        // Every frame starts from an empty stack and a whole body: a camera that culled but
        // never rendered (it was switched off in between) must not leave either off for good.
        void LateUpdate()
        {
            depth = 0;
            Show();
            LookDown(Time.deltaTime);
        }

        // Runs before ViewOffset's LateUpdate (1010), which puts this frame's sum on the camera.
        void LookDown(float dt)
        {
            if (eyes == null || controller == null || body == null) return;
            bool allowed = eyes.isActiveAndEnabled
                           && !(member != null && (member.IsDriving || member.Held != null))
                           && !(crewAnimator != null && crewAnimator.IsDown)
                           && !(tumble != null && tumble.IsTumbling);
            float step = lookDownFadeSeconds > 0.001f ? dt / lookDownFadeSeconds : 1f;
            lookDownWeight = Mathf.MoveTowards(lookDownWeight, allowed ? 1f : 0f, step);
            if (lookDownWeight <= 0f) return;

            // How far below the horizon the camera looks, in the player's frame (its parent).
            var cam = eyes.transform;
            Vector3 look = transform.InverseTransformDirection(cam.forward);
            float pitch = Mathf.Asin(Mathf.Clamp(-look.y, -1f, 1f)) * Mathf.Rad2Deg;
            float s = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lookDownPitch.x, lookDownPitch.y, pitch)) * lookDownWeight;
            Vector3 ahead = new Vector3(look.x, 0f, look.z);
            if (s <= 0f || ahead.sqrMagnitude < 1e-6f) return;
            ahead.Normalize();
            float forward = lookDownForward * s;

            // Tip the view down by as much as it takes to keep the floor the look lands on in
            // the middle of the picture, so the hands go where the eyes are aiming.
            float h = cam.localPosition.y - controller.FeetHeight;
            float along = h / Mathf.Tan(pitch * Mathf.Deg2Rad);
            float tipped = Mathf.Min(Mathf.Atan2(h, along - forward) * Mathf.Rad2Deg, 89.5f);
            ViewOffset.For(eyes).Add(ahead * forward, Quaternion.identity, Quaternion.Euler(Mathf.Max(0f, tipped - pitch), 0f, 0f));
        }

        void OnCameraPreCull(Camera cam)
        {
            if (depth < stack.Length) stack[depth] = cam;
            depth++;
            Apply(cam);
        }

        void OnCameraPostRender(Camera cam)
        {
            if (depth > 0) depth--;
            // The outermost render is over: whole again until the next one decides.
            if (depth == 0) Show();
            // Back to whichever camera was rendering around the one that just finished.
            else if (depth <= stack.Length) Apply(stack[depth - 1]);
        }

        void Apply(Camera cam)
        {
            if (eyes == null || body == null || member == null) Resolve();
            bool driving = member != null && member.IsDriving;
            bool own = cam != null && cam == eyes && !driving;
            if (own) Hide();
            else Show();
        }

        void Hide()
        {
            if (hidden || body == null) return;
            hidden = true;
            // Knocked down, the body lies on the floor on its own clock while the view tumbles
            // and stands back up (KnockdownTumble moves the picture only): the legs would be
            // drawn somewhere else than under the eyes, or through them. The whole body goes,
            // as it did before the legs were shown.
            bool down = (crewAnimator != null && crewAnimator.IsDown) || (tumble != null && tumble.IsTumbling);
            body.GetComponentsInChildren(true, scratch);
            SyncLegs();
            for (int i = 0; i < scratch.Count; i++)
            {
                var r = scratch[i];
                if (r == null || !r.enabled) continue;
                // Worn on a leg (the slippers): seen as it is, like the feet it is on.
                if (!down && OnLeg(r.transform)) continue;
                if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    madeShadowOnly.Add(r);
                }
                else if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off && !r.forceRenderingOff)
                {
                    r.forceRenderingOff = true;
                    madeInvisible.Add(r);
                }
            }
            scratch.Clear();
            for (int i = 0; i < legs.Count; i++)
            {
                var l = legs[i];
                if (l.companion == null) continue;
                l.companion.forceRenderingOff = down || !(l.source.enabled && l.source.gameObject.activeInHierarchy);
            }
        }

        void Show()
        {
            if (!hidden) return;
            hidden = false;
            for (int i = 0; i < madeShadowOnly.Count; i++)
                if (madeShadowOnly[i] != null)
                    madeShadowOnly[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            for (int i = 0; i < madeInvisible.Count; i++)
                if (madeInvisible[i] != null) madeInvisible[i].forceRenderingOff = false;
            madeShadowOnly.Clear();
            madeInvisible.Clear();
            for (int i = 0; i < legs.Count; i++)
                if (legs[i].companion != null) legs[i].companion.forceRenderingOff = true;
        }

        bool OnLeg(Transform t)
        {
            return (leftLeg != null && t.IsChildOf(leftLeg)) || (rightLeg != null && t.IsChildOf(rightLeg));
        }

        // Hips and legs: the hips bone, everything under the thighs, and the hips' other children
        // that do not lead up the spine (this rig's pelvis.L and pelvis.R).
        bool LowerBone(Transform b)
        {
            if (b == null || hips == null) return false;
            if (b == hips || OnLeg(b)) return true;
            return b.parent == hips && (spine == null || !spine.IsChildOf(b));
        }

        // One companion per skinned mesh now under the body; rebuilt when its mesh changed,
        // dropped when its source left the body.
        void SyncLegs()
        {
            for (int i = 0; i < legs.Count; i++) legs[i].seen = false;
            for (int i = 0; i < scratch.Count; i++)
            {
                // Not s.bones here: it copies the array on every read, and this runs every frame.
                // A skin with no bones keeps no triangle in LowerHalf, once, and is skipped after.
                var s = scratch[i] as SkinnedMeshRenderer;
                if (s == null || s.sharedMesh == null) continue;
                Legs l = null;
                for (int k = 0; k < legs.Count; k++)
                    if (legs[k].source == s) { l = legs[k]; break; }
                if (l == null) { l = new Legs { source = s }; legs.Add(l); }
                else if (l.sourceMesh != s.sharedMesh) Drop(l);
                if (l.sourceMesh != s.sharedMesh) Build(l);
                l.seen = true;
                if (l.companion != null) SyncMaterials(l);
            }
            for (int i = legs.Count - 1; i >= 0; i--)
                if (!legs[i].seen) { Drop(legs[i]); legs.RemoveAt(i); }
        }

        void Build(Legs l)
        {
            var s = l.source;
            l.sourceMesh = s.sharedMesh;
            l.mesh = LowerHalf(s);
            if (l.mesh == null) return;
            if (holder == null)
            {
                holder = new GameObject("FirstPersonLegs").transform;
                holder.SetParent(transform, false);
            }
            var go = new GameObject(s.name + " (own view)");
            go.layer = s.gameObject.layer;
            go.transform.SetParent(holder, false);
            var c = go.AddComponent<SkinnedMeshRenderer>();
            c.sharedMesh = l.mesh;
            c.bones = s.bones;
            c.rootBone = s.rootBone;
            c.quality = s.quality;
            c.updateWhenOffscreen = true;
            c.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            c.receiveShadows = s.receiveShadows;
            c.sharedMaterials = s.sharedMaterials;
            c.forceRenderingOff = true;
            l.companion = c;
        }

        // The skin's own mesh with only the triangles whose three corners are mostly weighted to
        // the hips and legs and sit below the cut. Null when there are none (glasses, a hat), the
        // skin has no thigh and foot to measure the cut by, or the mesh cannot be read.
        Mesh LowerHalf(SkinnedMeshRenderer s)
        {
            var src = s.sharedMesh;
            if (hips == null || leftLeg == null || leftFoot == null) return null;
            if (!src.isReadable)
            {
                Debug.LogWarning("[FirstPersonBody] " + src.name + " imports without Read/Write, so its legs cannot be shown in its wearer's own view. Tick Read/Write on its model importer.");
                return null;
            }
            var bones = s.bones;
            var bind = src.bindposes;
            var lower = new bool[bones.Length];
            int thigh = -1, foot = -1;
            for (int i = 0; i < bones.Length; i++)
            {
                lower[i] = LowerBone(bones[i]);
                if (bones[i] == leftLeg) thigh = i;
                if (bones[i] == leftFoot) foot = i;
            }
            if (thigh < 0 || foot < 0 || thigh >= bind.Length || foot >= bind.Length) return null;

            // The cut, in the mesh's own space: up the leg from the ankle, whatever axes the
            // model was exported with. The gown is baked into the body's bind space, so both agree.
            Vector3 ankle = bind[foot].inverse.GetColumn(3);
            Vector3 hip = bind[thigh].inverse.GetColumn(3);
            Vector3 up = (hip - ankle).normalized;
            float top = Vector3.Dot(hip - ankle, up) - cutBelowHips;

            var pos = src.vertices;
            var w = src.boneWeights;
            if (w.Length != pos.Length) return null;
            var keep = new bool[w.Length];
            for (int v = 0; v < w.Length; v++)
            {
                var b = w[v];
                float sum = 0f;
                if (Lower(lower, b.boneIndex0)) sum += b.weight0;
                if (Lower(lower, b.boneIndex1)) sum += b.weight1;
                if (Lower(lower, b.boneIndex2)) sum += b.weight2;
                if (Lower(lower, b.boneIndex3)) sum += b.weight3;
                keep[v] = sum >= lowerWeight && Vector3.Dot(pos[v] - ankle, up) <= top;
            }

            // Corners welded by position: a flat-shaded mesh splits them per face, and the cut's
            // edges must be found across those splits.
            var weld = new int[pos.Length];
            var byPos = new Dictionary<Vector3Int, int>(pos.Length);
            for (int v = 0; v < pos.Length; v++)
            {
                var k = Vector3Int.RoundToInt(pos[v] * 10000f);
                if (!byPos.TryGetValue(k, out int id)) byPos.Add(k, id = v);
                weld[v] = id;
            }

            // The kept triangles per submesh, and how many triangles share each edge, in the
            // whole mesh and among the kept ones.
            int subs = src.subMeshCount;
            var kept = new List<int>[subs];
            var all = new Dictionary<long, int>();
            var inKept = new Dictionary<long, int>();
            var tris = new List<int>();
            int total = 0;
            for (int sub = 0; sub < subs; sub++)
            {
                kept[sub] = new List<int>();
                if (src.GetTopology(sub) != MeshTopology.Triangles) continue;
                src.GetTriangles(tris, sub);
                for (int t = 0; t + 2 < tris.Count; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    Count(all, weld[a], weld[b]); Count(all, weld[b], weld[c]); Count(all, weld[c], weld[a]);
                    if (!(keep[a] && keep[b] && keep[c])) continue;
                    kept[sub].Add(a); kept[sub].Add(b); kept[sub].Add(c);
                    Count(inKept, weld[a], weld[b]); Count(inKept, weld[b], weld[c]); Count(inKept, weld[c], weld[a]);
                }
                total += kept[sub].Count;
            }
            if (total == 0) return null;

            // The cut: an edge of a kept triangle whose neighbour was dropped. An edge with no
            // neighbour at all is a hole the mesh always had, and stays open.
            var cut = new List<Cut>();
            for (int sub = 0; sub < subs; sub++)
            {
                var k = kept[sub];
                for (int t = 0; t + 2 < k.Count; t += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        int a = k[t + e], b = k[t + (e + 1) % 3];
                        long key = EdgeKey(weld[a], weld[b]);
                        if (all[key] >= 2 && inKept[key] == 1) cut.Add(new Cut { a = a, b = b, sub = sub });
                    }
            }

            var verts = new List<Vector3>(pos);
            var normals = new List<Vector3>(src.normals);
            var tangents = new List<Vector4>(src.tangents);
            var uvs = new List<Vector2>();
            src.GetUVs(0, uvs);
            var weights = new List<BoneWeight>(w);
            // Only the body is closed. A worn piece is open cloth (the gown crosses over in front,
            // so its cut is no loop a flat cap could close) and you look into it at your legs:
            // it is drawn from both sides, since its inside is all you see of it from above
            // (Standard has no culling switch, so the second side is geometry).
            if (s.GetComponentInParent<EquipItem>() == null) CapCut(cut, weld, verts, normals, tangents, uvs, weights, kept);
            else
                for (int sub = 0; sub < subs; sub++)
                {
                    var k = kept[sub];
                    for (int t = 0, n = k.Count; t + 2 < n; t += 3) { k.Add(k[t]); k.Add(k[t + 2]); k.Add(k[t + 1]); }
                }

            var mesh = new Mesh { name = src.name + " (legs)" };
            if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            if (normals.Count == verts.Count) mesh.SetNormals(normals);
            if (tangents.Count == verts.Count) mesh.SetTangents(tangents);
            if (uvs.Count == verts.Count) mesh.SetUVs(0, uvs);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = src.bindposes;
            mesh.subMeshCount = subs;
            for (int sub = 0; sub < subs; sub++) mesh.SetTriangles(kept[sub], sub, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        struct Cut { public int a, b, sub; }

        // Closes each loop of the cut with a fan from its middle, in the material of the
        // triangles along it, lit as the flat face it is. The fan winds against the kept
        // triangle on each edge, so it faces out of the mesh.
        static void CapCut(List<Cut> cut, int[] weld, List<Vector3> verts, List<Vector3> normals,
                           List<Vector4> tangents, List<Vector2> uvs, List<BoneWeight> weights, List<int>[] kept)
        {
            var from = new Dictionary<int, List<int>>();
            for (int i = 0; i < cut.Count; i++)
            {
                int s = weld[cut[i].a];
                if (!from.TryGetValue(s, out var list)) from.Add(s, list = new List<int>(1));
                list.Add(i);
            }
            var used = new bool[cut.Count];
            var loop = new List<int>();
            var mix = new Dictionary<int, float>();
            for (int start = 0; start < cut.Count; start++)
            {
                if (used[start]) continue;
                loop.Clear();
                for (int i = start; i >= 0; )
                {
                    used[i] = true;
                    loop.Add(i);
                    int next = -1;
                    if (from.TryGetValue(weld[cut[i].b], out var list))
                        foreach (int j in list) if (!used[j]) { next = j; break; }
                    i = next;
                }
                if (loop.Count < 3) continue;

                Vector3 mid = Vector3.zero;
                foreach (int i in loop) mid += verts[cut[i].a];
                mid /= loop.Count;
                Vector3 n = Vector3.zero;
                foreach (int i in loop) n += Vector3.Cross(verts[cut[i].a] - verts[cut[i].b], mid - verts[cut[i].b]);
                if (n.sqrMagnitude < 1e-12f) continue;
                n.Normalize();

                // The middle is skinned to the loop's bones, averaged.
                mix.Clear();
                foreach (int i in loop)
                {
                    var bw = weights[cut[i].a];
                    Mix(mix, bw.boneIndex0, bw.weight0); Mix(mix, bw.boneIndex1, bw.weight1);
                    Mix(mix, bw.boneIndex2, bw.weight2); Mix(mix, bw.boneIndex3, bw.weight3);
                }
                int c = Add(verts, normals, tangents, uvs, weights, cut[loop[0]].a, n);
                verts[c] = mid;
                weights[c] = Strongest(mix);

                // A fresh corner per loop vertex, with the cap's normal.
                var corner = new Dictionary<int, int>(loop.Count);
                foreach (int i in loop)
                {
                    int a = Corner(corner, cut[i].a, weld, verts, normals, tangents, uvs, weights, n);
                    int b = Corner(corner, cut[i].b, weld, verts, normals, tangents, uvs, weights, n);
                    var k = kept[cut[i].sub];
                    k.Add(b); k.Add(a); k.Add(c);
                }
            }
        }

        static int Corner(Dictionary<int, int> corner, int v, int[] weld, List<Vector3> verts, List<Vector3> normals,
                          List<Vector4> tangents, List<Vector2> uvs, List<BoneWeight> weights, Vector3 n)
        {
            if (!corner.TryGetValue(weld[v], out int c)) corner.Add(weld[v], c = Add(verts, normals, tangents, uvs, weights, v, n));
            return c;
        }

        static int Add(List<Vector3> verts, List<Vector3> normals, List<Vector4> tangents, List<Vector2> uvs,
                       List<BoneWeight> weights, int copy, Vector3 n)
        {
            int count = verts.Count;
            verts.Add(verts[copy]);
            if (normals.Count == count) normals.Add(n);
            if (tangents.Count == count) tangents.Add(tangents[copy]);
            if (uvs.Count == count) uvs.Add(uvs[copy]);
            weights.Add(weights[copy]);
            return count;
        }

        static void Mix(Dictionary<int, float> mix, int bone, float weight)
        {
            if (weight <= 0f) return;
            mix.TryGetValue(bone, out float had);
            mix[bone] = had + weight;
        }

        // The four heaviest bones of a mix, normalised.
        static BoneWeight Strongest(Dictionary<int, float> mix)
        {
            var top = new List<KeyValuePair<int, float>>(mix);
            top.Sort((x, y) => y.Value.CompareTo(x.Value));
            float sum = 0f;
            for (int i = 0; i < top.Count && i < 4; i++) sum += top[i].Value;
            var bw = new BoneWeight();
            if (sum <= 0f) return bw;
            if (top.Count > 0) { bw.boneIndex0 = top[0].Key; bw.weight0 = top[0].Value / sum; }
            if (top.Count > 1) { bw.boneIndex1 = top[1].Key; bw.weight1 = top[1].Value / sum; }
            if (top.Count > 2) { bw.boneIndex2 = top[2].Key; bw.weight2 = top[2].Value / sum; }
            if (top.Count > 3) { bw.boneIndex3 = top[3].Key; bw.weight3 = top[3].Value / sum; }
            return bw;
        }

        static long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        static void Count(Dictionary<long, int> edges, int a, int b)
        {
            long key = EdgeKey(a, b);
            edges.TryGetValue(key, out int n);
            edges[key] = n + 1;
        }

        static bool Lower(bool[] lower, int i) => i >= 0 && i < lower.Length && lower[i];

        // The gown's trim takes the crew colour after it is bound: follow the source's slots.
        void SyncMaterials(Legs l)
        {
            l.source.GetSharedMaterials(matsA);
            l.companion.GetSharedMaterials(matsB);
            bool same = matsA.Count == matsB.Count;
            for (int i = 0; same && i < matsA.Count; i++) same = matsA[i] == matsB[i];
            if (!same) l.companion.SetSharedMaterials(matsA);
        }

        void Drop(Legs l)
        {
            if (l.companion != null) Destroy(l.companion.gameObject);
            if (l.mesh != null) Destroy(l.mesh);
            l.companion = null;
            l.mesh = null;
            l.sourceMesh = null;
        }
    }
}
