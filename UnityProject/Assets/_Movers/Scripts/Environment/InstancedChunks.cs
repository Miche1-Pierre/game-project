using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Movers
{
    // Thousands of copies of a few meshes (trees, bushes, grass, flowers, stones) drawn with GPU
    // instancing, in spatial chunks: no GameObject per copy, one draw call per mesh per chunk.
    //
    // Each batch keeps its copies' matrices in a GPU buffer, uploaded once when it is made, and
    // the Movers/Environment shader reads them (procedural instancing): a frame costs a few
    // hundred draw calls and no matrix upload, whatever the number of copies. Each camera
    // draws the chunks within its own distance, frustum culled; the chunks close to it cast
    // shadows, the far ones do not; and a chunk far away can draw only a share of its copies
    // (they are stored shuffled, so any prefix is an even thinning), which is how grass fades
    // out instead of ending on a line.
    public sealed class InstancedChunks
    {
        // One mesh (and submesh) with one material, and where its copies stand.
        public sealed class Batch
        {
            public Mesh mesh;
            public int submesh;
            public Material material;
            public GraphicsBuffer buffer;
            public MaterialPropertyBlock block;
            public int count;
            public bool castShadows;
        }

        public sealed class Chunk
        {
            public Bounds bounds;
            public readonly List<Batch> batches = new List<Batch>();
        }

        static readonly int InstancesId = Shader.PropertyToID("_Instances");

        public float drawDistance = 800f;
        public float shadowDistance = 100f;
        // Full density up to this distance, then thinning to minDensity at drawDistance.
        public float fullDensityDistance = float.MaxValue;
        public float minDensity = 1f;
        public int layer;

        readonly List<Chunk> chunks = new List<Chunk>();
        readonly Plane[] planes = new Plane[6];

        public int ChunkCount => chunks.Count;
        public int InstanceCount { get; private set; }
        public int DrawnLastFrame { get; private set; }

        public void Add(Chunk chunk)
        {
            if (chunk == null || chunk.batches.Count == 0) return;
            chunks.Add(chunk);
            for (int i = 0; i < chunk.batches.Count; i++) InstanceCount += chunk.batches[i].count;
        }

        // Adds `list` to `chunk` as one batch, its matrices uploaded to the GPU now.
        public static void AddBatches(Chunk chunk, Mesh mesh, int submesh, Material material, List<Matrix4x4> list, bool castShadows)
        {
            if (mesh == null || material == null || list == null || list.Count == 0) return;
            var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, list.Count, 64);
            buffer.SetData(list);
            var block = new MaterialPropertyBlock();
            block.SetBuffer(InstancesId, buffer);
            chunk.batches.Add(new Batch { mesh = mesh, submesh = submesh, material = material, buffer = buffer, block = block, count = list.Count, castShadows = castShadows });
        }

        // Issues this frame's draws for one camera.
        public void Draw(Camera cam)
        {
            if (cam == null || chunks.Count == 0) return;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            Vector3 eye = cam.transform.position;
            float far = Mathf.Min(drawDistance, cam.farClipPlane);
            float far2 = far * far;
            int drawn = 0;
            var rp = new RenderParams
            {
                camera = cam,
                layer = layer,
                receiveShadows = true,
                // The map bakes no probes: blending them gives every copy the scene's ambient.
                lightProbeUsage = LightProbeUsage.BlendProbes,
            };
            for (int c = 0; c < chunks.Count; c++)
            {
                Chunk chunk = chunks[c];
                float d2 = chunk.bounds.SqrDistance(eye);
                if (d2 > far2) continue;
                float d = Mathf.Sqrt(d2);
                bool shadows = d < shadowDistance;
                // Near chunks may cast a shadow into the view from behind the camera: Unity culls
                // those itself. Beyond the shadows, anything outside the frustum is skipped here.
                if (!shadows && !GeometryUtility.TestPlanesAABB(planes, chunk.bounds)) continue;
                float density = 1f;
                if (d > fullDensityDistance)
                    density = Mathf.Lerp(1f, minDensity, Mathf.InverseLerp(fullDensityDistance, far, d));
                rp.worldBounds = chunk.bounds;
                for (int b = 0; b < chunk.batches.Count; b++)
                {
                    Batch batch = chunk.batches[b];
                    int n = batch.count;
                    if (density < 1f) n = Mathf.Max(1, Mathf.CeilToInt(n * density));
                    rp.material = batch.material;
                    rp.matProps = batch.block;
                    rp.shadowCastingMode = batch.castShadows && shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    Graphics.RenderMeshPrimitives(rp, batch.mesh, batch.submesh, n);
                    drawn += n;
                }
            }
            DrawnLastFrame = drawn;
        }

        // Frees the GPU buffers.
        public void Clear()
        {
            for (int c = 0; c < chunks.Count; c++)
                for (int b = 0; b < chunks[c].batches.Count; b++)
                    chunks[c].batches[b].buffer?.Release();
            chunks.Clear();
            InstanceCount = 0;
        }
    }
}
