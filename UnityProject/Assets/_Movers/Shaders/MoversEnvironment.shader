// The countryside around the grandmother's house (Environment/*): the low-poly land, the road
// ribbons, trees, hedges, grass tufts, wheat, flowers and pebbles. One shader for all of it, so
// everything the landscape draws shares the same flat, warm look as the title screen.
//
// - Colour comes from the mesh's vertex colours (each triangle of a generated mesh carries its
//   own), times _MainTex (the tree pack's colour sheet; white for generated meshes), times
//   _Color, times a shade from the root (uv.y = 0) to the tip (uv.y = 1) of a blade, times a
//   soft patchy variation in world space so a lawn never looks printed.
// - Grass and flowers sway with a cheap wind (world-space sine waves, stronger at the tip) and
//   can take the ground's normal (_UpNormals = 1): a tuft is then lit exactly like the land
//   it stands on, which is what makes thousands of them read as a lawn, not as confetti.
// - GPU instancing, procedural: InstancedChunks keeps each batch's matrices in a GPU buffer
//   (_Instances) and draws it with Graphics.RenderMeshPrimitives; setup() reads the matrix of
//   the copy being drawn. Plain renderers (the land's chunks) use the shader as usual.
// - Built-in pipeline surface shader: fog, the sun's shadows and the ambient come for free.
Shader "Movers/Environment"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Colour sheet (trees)", 2D) = "white" {}
        _UseVertexColor ("Use vertex colours (0 for plain meshes)", Range(0, 1)) = 1
        _RootShade ("Root shade", Range(0, 2)) = 1
        _TipShade ("Tip shade", Range(0, 2)) = 1
        _Variation ("Patch variation", Range(0, 1)) = 0
        _PatchScale ("Patch scale (1/m)", Float) = 0.12
        _Wind ("Wind sway at the tip (m)", Float) = 0
        _WindSpeed ("Wind speed", Float) = 1.4
        _WindScale ("Wind wave scale (1/m)", Float) = 0.07
        _UpNormals ("Normals point up (0 mesh, 1 up)", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200
        Cull [_Cull]

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow fullforwardshadows
        #pragma multi_compile_instancing
        #pragma instancing_options procedural:setup
        #pragma target 4.5

        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
        StructuredBuffer<float4x4> _Instances;
        #endif

        // The copy's object-to-world matrix from the batch's buffer, and its inverse.
        void setup()
        {
        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            float4x4 m = _Instances[unity_InstanceID];
            unity_ObjectToWorld = m;
            float3x3 a = (float3x3)m;
            float3 c0 = cross(a[1], a[2]);
            float3 c1 = cross(a[2], a[0]);
            float3 c2 = cross(a[0], a[1]);
            float det = dot(a[0], c0);
            float3x3 inv = transpose(float3x3(c0, c1, c2)) / det;
            float3 t = float3(m[0][3], m[1][3], m[2][3]);
            unity_WorldToObject = float4x4(
                float4(inv[0], -dot(inv[0], t)),
                float4(inv[1], -dot(inv[1], t)),
                float4(inv[2], -dot(inv[2], t)),
                float4(0, 0, 0, 1));
        #endif
        }

        fixed4 _Color;
        sampler2D _MainTex;
        half _UseVertexColor;
        half _RootShade;
        half _TipShade;
        half _Variation;
        float _PatchScale;
        float _Wind;
        float _WindSpeed;
        float _WindScale;
        half _UpNormals;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
            float3 worldPos;
            float height;
        };

        // A smooth value noise in [0, 1], for the patches.
        float Hash(float2 p)
        {
            return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
        }

        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = Hash(i);
            float b = Hash(i + float2(1, 0));
            float c = Hash(i + float2(0, 1));
            float d = Hash(i + float2(1, 1));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.height = v.texcoord.y;

            // Object-space direction of world up, and of a world-space offset, without the
            // inverse matrix: the instances are rotated and uniformly scaled, so the transpose
            // of the object-to-world matrix divided by the squared scale is its inverse.
            float3x3 m = (float3x3)unity_ObjectToWorld;
            float scale2 = max(1e-6, dot(float3(m[0][0], m[1][0], m[2][0]), float3(m[0][0], m[1][0], m[2][0])));

            if (_Wind > 0)
            {
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y * _WindSpeed;
                float phase = dot(wp.xz, float2(_WindScale, _WindScale * 0.63));
                float sway = sin(t + phase) * 0.65 + sin(t * 2.31 + phase * 1.7) * 0.35;
                float gust = 0.6 + 0.4 * sin(t * 0.37 + phase * 0.21);
                float h = saturate(v.texcoord.y);
                float3 offset = float3(sway, 0, sway * 0.45) * (_Wind * gust * h * h);
                v.vertex.xyz += mul(offset, m) / scale2;
            }

            if (_UpNormals > 0)
            {
                float3 upObject = normalize(mul(float3(0, 1, 0), m));
                v.normal = normalize(lerp(v.normal, upObject, _UpNormals));
            }
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            half shade = lerp(_RootShade, _TipShade, saturate(IN.height));
            half patch = 1.0 + (ValueNoise(IN.worldPos.xz * _PatchScale) - 0.5) * _Variation;
            half3 vertexColour = lerp(half3(1, 1, 1), IN.color.rgb, _UseVertexColor);
            o.Albedo = vertexColour * tex2D(_MainTex, IN.uv_MainTex).rgb * _Color.rgb * shade * patch;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
