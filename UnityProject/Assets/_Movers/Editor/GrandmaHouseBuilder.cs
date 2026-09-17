#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Movers
{
    // Grandma house Map 1: steep gable-FRONT roof (+round window), 2 storeys, brick base, porch,
    // glass conservatory-veranda (left), back terrace, basement, fenced garden, cat + grandma.
    public static class GrandmaHouseBuilder
    {
        const string ScenePath = "Assets/_Movers/Scenes/Map01_GrandmaHouse.unity";
        const string MatDir = "Assets/_Movers/Materials";
        static Transform root;
        static readonly Color CRoof = new Color(0.46f, 0.31f, 0.25f);   // warm dark brown
        static readonly Color CCream = new Color(0.92f, 0.89f, 0.80f);
        static readonly Color CGrass = new Color(0.55f, 0.62f, 0.45f);
        static readonly Color CPath = new Color(0.70f, 0.66f, 0.58f);
        static readonly Color CStone = new Color(0.55f, 0.55f, 0.55f);
        static readonly Color CGlass = new Color(0.60f, 0.78f, 0.85f);

        static GameObject Prefab(string name)
        {
            foreach (var g in AssetDatabase.FindAssets("PF_" + name + " t:Prefab", new[] { "Assets/_Project/Prefabs" }))
            { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p) == "PF_" + name) return AssetDatabase.LoadAssetAtPath<GameObject>(p); }
            return null;
        }
        static GameObject P(string name, Vector3 pos, float yRot, Transform parent, float scale = 1f)
        {
            var src = Prefab(name); if (src == null) { Debug.LogWarning("missing PF_" + name); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            go.transform.SetParent(parent); go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yRot, 0);
            if (scale != 1f) go.transform.localScale = Vector3.one * scale; return go;
        }
        static GameObject Raw(string exactName, Vector3 pos, float yRot, Transform parent, float scale = 1f)
        {
            foreach (var g in AssetDatabase.FindAssets(exactName + " t:Prefab"))
            { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p) == exactName) {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(p); var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
                go.transform.SetParent(parent); go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yRot, 0);
                if (scale != 1f) go.transform.localScale = Vector3.one * scale; return go; } }
            return null;
        }
        static Material Mat(Color c)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/mat_{Mathf.RoundToInt(c.r*255)}_{Mathf.RoundToInt(c.g*255)}_{Mathf.RoundToInt(c.b*255)}.mat";
            var ex = AssetDatabase.LoadAssetAtPath<Material>(path); if (ex != null) return ex;
            var m = new Material(Shader.Find("Standard")) { color = c }; AssetDatabase.CreateAsset(m, path); return m;
        }
        static void Prim(string name, Vector3 pos, Vector3 scale, Vector3 euler, Color col, Transform parent)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(parent);
            g.transform.position = pos; g.transform.rotation = Quaternion.Euler(euler); g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = Mat(col);
        }
        static void GableXY(float cz, float x0, float x1, float yBase, float xApex, float yApex, float thick, Color col, Transform parent)
        {
            var mesh = new Mesh(); var v = new Vector3[6]; float hz = thick / 2f;
            v[0]=new Vector3(x0,yBase,-hz); v[1]=new Vector3(x1,yBase,-hz); v[2]=new Vector3(xApex,yApex,-hz);
            v[3]=new Vector3(x0,yBase,hz); v[4]=new Vector3(x1,yBase,hz); v[5]=new Vector3(xApex,yApex,hz);
            mesh.vertices=v; mesh.triangles=new int[]{0,1,2,5,4,3,0,4,1,0,3,4,1,4,5,1,5,2,0,2,5,0,5,3}; mesh.RecalculateNormals();
            var go=new GameObject("Gable"); go.transform.SetParent(parent); go.transform.position=new Vector3(0,0,cz);
            go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=Mat(col);
        }
        static void FixShader(GameObject go, string texPath)
        {
            if (go == null) return;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var m = new Material(Shader.Find("Standard")); if (tex) m.mainTexture = tex;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            { var arr = r.sharedMaterials; for (int i = 0; i < arr.Length; i++) arr[i] = m; r.sharedMaterials = arr; }
        }

        [MenuItem("The Movers/Build Grandma House")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.15f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            root = new GameObject("GrandmaHouse").transform;
            const float B = 0.6f, U = 3.6f, T = 6.6f;

            Prim("Ground", new Vector3(0,-0.05f,4), new Vector3(70,0.1f,70), Vector3.zero, CGrass, root);

            var bs = new GameObject("Basement").transform; bs.SetParent(root);
            Prim("Cel_Floor", new Vector3(0,-3.1f,4), new Vector3(12,0.2f,8), Vector3.zero, CStone, bs);
            Prim("Cel_WallW", new Vector3(-6,-1.5f,4), new Vector3(0.3f,3.2f,8), Vector3.zero, CStone, bs);
            Prim("Cel_WallE", new Vector3(6,-1.5f,4), new Vector3(0.3f,3.2f,8), Vector3.zero, CStone, bs);
            Prim("Cel_WallN", new Vector3(0,-1.5f,8), new Vector3(12,3.2f,0.3f), Vector3.zero, CStone, bs);
            Prim("Cel_WallS", new Vector3(0,-1.5f,0), new Vector3(12,3.2f,0.3f), Vector3.zero, CStone, bs);
            P("Stairs_Interior", new Vector3(4.4f,-3.0f,1.5f), 180, bs);
            P("Shelving_Unit_Cave", new Vector3(-5,-3.0f,2), 90, bs); P("Shelving_Unit_Cave", new Vector3(-5,-3.0f,5), 90, bs);
            P("Barrel", new Vector3(-4,-3.0f,7), 0, bs); P("Barrel", new Vector3(-3,-3.0f,7), 0, bs);
            P("Crate", new Vector3(4,-3.0f,7), 0, bs); P("Workbench", new Vector3(0,-3.0f,7.2f), 0, bs);

            var fnd = new GameObject("Foundation").transform; fnd.SetParent(root);
            foreach (int fx in new[]{-4,0,4}){ P("Foundation_Brick",new Vector3(fx,0,0),0,fnd); P("Foundation_Brick",new Vector3(fx,0,8),0,fnd);}
            foreach (int fz in new[]{2,6}){ P("Foundation_Brick",new Vector3(-6,0,fz),90,fnd); P("Foundation_Brick",new Vector3(6,0,fz),90,fnd);}

            var gf = new GameObject("GroundFloor").transform; gf.SetParent(root);
            P("Wall_Window",new Vector3(-4,B,0),180,gf); P("Wall_Door",new Vector3(0,B,0),180,gf); P("Wall_Window",new Vector3(4,B,0),180,gf);
            P("Wall_Window",new Vector3(-4,B,8),0,gf); P("Wall_Solid",new Vector3(0,B,8),0,gf); P("Wall_Window",new Vector3(4,B,8),0,gf);
            P("Wall_Solid",new Vector3(-6,B,2),270,gf); P("Wall_Window",new Vector3(-6,B,6),270,gf);
            P("Wall_Window",new Vector3(6,B,2),90,gf); P("Wall_Solid",new Vector3(6,B,6),90,gf);
            var flo = new GameObject("Floors").transform; flo.SetParent(root);
            foreach (int fx in new[]{-4,0,4}) foreach (int fz in new[]{2,6}){ if(!(fx==4&&fz==2)) P("Floor_4x4",new Vector3(fx,B,fz),0,flo); P("Floor_4x4",new Vector3(fx,U,fz),0,flo);}

            var uf = new GameObject("UpperFloor").transform; uf.SetParent(root);
            P("Wall_Window",new Vector3(-4,U,0),180,uf); P("Wall_Solid",new Vector3(0,U,0),180,uf); P("Wall_Window",new Vector3(4,U,0),180,uf);
            P("Wall_Window",new Vector3(-4,U,8),0,uf); P("Wall_Solid",new Vector3(0,U,8),0,uf); P("Wall_Window",new Vector3(4,U,8),0,uf);
            P("Wall_Solid",new Vector3(-6,U,2),270,uf); P("Wall_Solid",new Vector3(-6,U,6),270,uf);
            P("Wall_Solid",new Vector3(6,U,2),90,uf); P("Wall_Solid",new Vector3(6,U,6),90,uf);

            // ---- STEEP gable roof, gable faces front, round window ----
            var rf = new GameObject("Roof").transform; rf.SetParent(root);
            float apex=T+6.5f, ctrY=(T+apex)/2f, slopeLen=Mathf.Sqrt(6.5f*6.5f+6.5f*6.5f);
            Prim("Roof_E",new Vector3(3.25f,ctrY,4f),new Vector3(slopeLen,0.16f,9.4f),new Vector3(0,0,-45f),CRoof,rf);
            Prim("Roof_W",new Vector3(-3.25f,ctrY,4f),new Vector3(slopeLen,0.16f,9.4f),new Vector3(0,0,45f),CRoof,rf);
            Prim("Ridge",new Vector3(0,apex+0.05f,4f),new Vector3(0.45f,0.25f,9.6f),Vector3.zero,CRoof,rf);
            GableXY(-0.55f,-6.5f,6.5f,T,0f,apex,0.35f,CCream,rf);
            GableXY(8.55f,-6.5f,6.5f,T,0f,apex,0.35f,CCream,rf);
            P("Window_Round",new Vector3(0,T+3.4f,-0.62f),0,rf);
            P("Chimney",new Vector3(3.0f,apex-1.6f,5.5f),0,rf);
            // dormers poking through the west slope (facing left)
            P("Dormer",new Vector3(-4.2f,8.5f,2.3f),270,rf); P("Dormer",new Vector3(-4.2f,8.5f,5.7f),270,rf);

            // ---- Porch ----
            var pr = new GameObject("Porch").transform; pr.SetParent(root);
            P("Porch_Column",new Vector3(-1.4f,B,-1.7f),0,pr); P("Porch_Column",new Vector3(1.4f,B,-1.7f),0,pr);
            P("Porch_Beam",new Vector3(0,B+2.9f,-1.7f),0,pr);
            Prim("PorchRoof",new Vector3(0,B+3.15f,-0.85f),new Vector3(3.6f,0.14f,2.4f),new Vector3(-14f,0,0),CRoof,pr);
            P("Steps_Stoop",new Vector3(0,B-0.2f,-2.4f),0,pr); P("Door_Front",new Vector3(0,B,0),0,pr);

            // ---- Veranda = glass conservatory with PEAKED glass roof (left/west) ----
            var vr = new GameObject("Veranda").transform; vr.SetParent(root);
            float vx=-8.4f;
            Prim("VerandaFloor",new Vector3(vx,0.05f,4f),new Vector3(4.6f,0.1f,4.6f),Vector3.zero,CPath,vr);
            P("Veranda_Panel",new Vector3(vx-2.1f,0,2.6f),90,vr); P("Veranda_Panel",new Vector3(vx-2.1f,0,5.4f),90,vr);   // west wall
            P("Veranda_Door",new Vector3(vx-2.1f,0,4.0f),90,vr);
            P("Veranda_Panel",new Vector3(vx-1.1f,0,1.9f),0,vr); P("Veranda_Panel",new Vector3(vx+0.2f,0,1.9f),0,vr);     // south wall
            P("Veranda_Panel",new Vector3(vx-1.1f,0,6.1f),180,vr); P("Veranda_Panel",new Vector3(vx+0.2f,0,6.1f),180,vr); // north wall
            Prim("VRoofW",new Vector3(vx-1.05f,2.85f,4f),new Vector3(2.4f,0.07f,4.7f),new Vector3(0,0,30f),CGlass,vr);
            Prim("VRoofE",new Vector3(vx+1.05f,2.85f,4f),new Vector3(2.4f,0.07f,4.7f),new Vector3(0,0,-30f),CGlass,vr);
            Prim("VRidge",new Vector3(vx,3.5f,4f),new Vector3(0.12f,0.12f,4.7f),Vector3.zero,CCream,vr);

            // ---- Back terrace (north) + furniture ----
            var tr = new GameObject("Terrace").transform; tr.SetParent(root);
            P("Terrace_Deck",new Vector3(0,0.0f,11.5f),0,tr);
            P("Garden_Bench",new Vector3(-2.5f,0.45f,12.5f),180,tr);
            if (P("Garden_Table", new Vector3(1.5f,0.45f,11.5f),0,tr) == null) P("Dining_Table_01", new Vector3(1.5f,0.45f,11.5f),0,tr);
            P("Garden_Bench",new Vector3(1.5f,0.45f,10.2f),0,tr);

            // ---- Garden ----
            var gd = new GameObject("Garden").transform; gd.SetParent(root);
            Prim("Path",new Vector3(0,0.02f,-6f),new Vector3(2.2f,0.06f,8f),Vector3.zero,CPath,gd);
            for(float x=-15; x<=15; x+=2f){ if(Mathf.Abs(x)<1.6f) continue; P("Fence_Section",new Vector3(x,0,-10),0,gd); P("Fence_Section",new Vector3(x,0,18),0,gd);}
            for(float z=-9; z<=17; z+=2f){ P("Fence_Section",new Vector3(-15,0,z),90,gd); P("Fence_Section",new Vector3(15,0,z),90,gd);}
            P("Gate",new Vector3(0,0,-10),0,gd);
            foreach(var t in new (float x,float z,float s,string n)[]{(-13,-7,1.3f,"Tree Type1 01"),(13,-7,1.4f,"Tree Type3 02"),(-14,13,1.5f,"Tree Type1 03"),(14,14,1.3f,"Tree Type3 04"),(13,3,1.2f,"Tree Type1 04"),(-13,4,1.3f,"Tree Type3 05")})
                Raw(t.n,new Vector3(t.x,0,t.z),Random.Range(0,360),gd,t.s);
            foreach(var b in new Vector3[]{new Vector3(-3,0,-2.6f),new Vector3(3,0,-2.6f),new Vector3(6.6f,0,-2.5f),new Vector3(7,0,7)}) P("Bush_01",b,Random.Range(0,360),gd);
            foreach(var f in new Vector3[]{new Vector3(-1.6f,0,-4),new Vector3(1.6f,0,-4),new Vector3(-1.6f,0,-7),new Vector3(1.6f,0,-7),new Vector3(8,0,-3)}) P(Random.value>0.5f?"Flower_01":"Flower_02",f,Random.Range(0,360),gd);
            P("Well",new Vector3(11,0,9),0,gd); P("Mailbox",new Vector3(2.2f,0,-9),0,gd);

            var an = new GameObject("Animals").transform; an.SetParent(root);
            FixShader(Raw("Kitty_001",new Vector3(2.5f,0,-3.5f),200,an), "Assets/ithappy/Animals_FREE/Textures/Texture.png");
            var ch = new GameObject("Characters").transform; ch.SetParent(root);
            var grandma = Raw("male03_1",new Vector3(-1.5f,B,-3.2f),20,ch) ?? Raw("male01_1",new Vector3(-1.5f,B,-3.2f),20,ch);
            if (grandma != null) grandma.name = "Grandma_Placeholder";

            var camGO=new GameObject("ReviewCam"); var cam=camGO.AddComponent<Camera>();
            camGO.transform.position=new Vector3(0,8,-22); camGO.transform.LookAt(new Vector3(0,6,4)); cam.tag="MainCamera";

            Directory.CreateDirectory("Assets/_Movers/Scenes");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("GrandmaHouse: refined (steep roof, conservatory veranda, dormers).");
        }
    }
}
#endif
