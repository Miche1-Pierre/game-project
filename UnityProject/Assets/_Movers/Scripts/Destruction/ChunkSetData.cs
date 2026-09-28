using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Movers
{
    // What the Blender fracture script says about one chunk set (its sidecar JSON), read once
    // per file and shared by every wall that uses it.
    //
    // Two versions of the format are accepted, because the fracture pipeline is being built in
    // parallel: the A7 prototype (anchors as a list of words: "ground", "top", "side_ux_pos",
    // "side_ux_neg"; no mass share) and the slice format (anchors as an object with bottom, top,
    // side_pos, side_neg; mass_share per chunk). Anything missing is derived from the chunk
    // meshes at fracture time, so a wall with a chunk FBX and no JSON still works.
    internal sealed class ChunkSetData
    {
        internal sealed class Chunk
        {
            public string name;
            public bool glass;             // shards of a pane: the runtime GlassPane owns glass, these are dropped
            public bool bottom, top, sideNeg, sidePos;
            public bool anchorsKnown;
            public float massShare = -1f;  // share of the set's mass; -1 = unknown
            public float volume = -1f;     // cubic metres, Unity space; -1 = unknown
        }

        public readonly List<Chunk> chunks = new List<Chunk>();
        public readonly List<Vector2Int> neighbours = new List<Vector2Int>();   // index pairs, a < b
        // Unity m2 of cut face each pair shares, parallel to neighbours. 0 for a pair that only
        // touches (a wood frame against the plaster); 1 when the file does not say.
        public readonly List<float> neighbourArea = new List<float>();
        readonly Dictionary<string, int> byName = new Dictionary<string, int>();

        public int IndexOf(string chunkName)
        {
            return chunkName != null && byName.TryGetValue(chunkName, out int i) ? i : -1;
        }

        static readonly Dictionary<TextAsset, ChunkSetData> cache = new Dictionary<TextAsset, ChunkSetData>();
        static readonly HashSet<TextAsset> broken = new HashSet<TextAsset>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
            broken.Clear();
        }

        // Null when there is no file or it cannot be read (said once per file).
        public static ChunkSetData Get(TextAsset json)
        {
            if (json == null) return null;
            if (cache.TryGetValue(json, out var data)) return data;
            if (broken.Contains(json)) return null;
            try
            {
                data = Parse(json.text);
                cache[json] = data;
                return data;
            }
            catch (System.Exception e)
            {
                broken.Add(json);
                Debug.LogWarning("[Destruction] chunk sidecar " + json.name + " could not be read (" + e.Message +
                                 "); its chunks are wired from their shapes instead.");
                return null;
            }
        }

        static ChunkSetData Parse(string text)
        {
            var root = MiniJson.Parse(text) as Dictionary<string, object>;
            if (root == null) throw new System.FormatException("not a JSON object");
            var data = new ChunkSetData();

            if (root.TryGetValue("chunks", out object chunksObj) && chunksObj is List<object> list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var o = list[i] as Dictionary<string, object>;
                    var c = new Chunk();
                    if (o != null)
                    {
                        c.name = Str(o, "name");
                        string kind = Str(o, "kind");
                        c.glass = kind == "glass";
                        c.massShare = Num(o, "mass_share", -1f);
                        c.volume = Num(o, "volume_unity", -1f);
                        ReadAnchors(o, c);
                    }
                    data.byName[c.name ?? ("#" + i)] = data.chunks.Count;
                    data.chunks.Add(c);
                }
            }

            // Pairs from the top-level list when there is one, else from each chunk's neighbours.
            if (root.TryGetValue("adjacency", out object adjObj) && adjObj is List<object> adj)
            {
                for (int i = 0; i < adj.Count; i++)
                    if (adj[i] is Dictionary<string, object> p)
                        AddPair(data, (int)Num(p, "a", -1f), (int)Num(p, "b", -1f), Num(p, "shared_cut_area_unity", 1f));
            }
            else if (chunksObj is List<object> chunkList)
            {
                for (int i = 0; i < chunkList.Count; i++)
                {
                    if (!(chunkList[i] is Dictionary<string, object> o)) continue;
                    if (!o.TryGetValue("neighbors", out object nObj) || !(nObj is List<object> n)) continue;
                    for (int k = 0; k < n.Count; k++)
                        if (n[k] is Dictionary<string, object> nb)
                            AddPair(data, i, (int)Num(nb, "chunk", -1f), Num(nb, "shared_cut_area_unity", 1f));
                }
            }
            return data;
        }

        static void ReadAnchors(Dictionary<string, object> o, Chunk c)
        {
            if (o.TryGetValue("anchors", out object a))
            {
                if (a is Dictionary<string, object> flags)
                {
                    c.bottom = Bool(flags, "bottom");
                    c.top = Bool(flags, "top");
                    c.sidePos = Bool(flags, "side_pos");
                    c.sideNeg = Bool(flags, "side_neg");
                    c.anchorsKnown = true;
                    return;
                }
                if (a is List<object> words)
                {
                    for (int i = 0; i < words.Count; i++)
                    {
                        string w = words[i] as string;
                        if (w == "ground" || w == "bottom") c.bottom = true;
                        else if (w == "top") c.top = true;
                        else if (w == "side_ux_pos" || w == "side_pos") c.sidePos = true;
                        else if (w == "side_ux_neg" || w == "side_neg") c.sideNeg = true;
                    }
                    c.anchorsKnown = true;
                    return;
                }
            }
            if (o.ContainsKey("touches_bottom"))
            {
                c.bottom = Bool(o, "touches_bottom");
                c.top = Bool(o, "touches_top");
                c.anchorsKnown = true;
            }
        }

        static void AddPair(ChunkSetData data, int a, int b, float area)
        {
            if (a < 0 || b < 0 || a == b) return;
            var p = a < b ? new Vector2Int(a, b) : new Vector2Int(b, a);
            if (data.neighbours.Contains(p)) return;
            data.neighbours.Add(p);
            data.neighbourArea.Add(Mathf.Max(0f, area));
        }

        static string Str(Dictionary<string, object> o, string key)
        {
            return o.TryGetValue(key, out object v) ? v as string : null;
        }

        static float Num(Dictionary<string, object> o, string key, float fallback)
        {
            return o.TryGetValue(key, out object v) && v is double d ? (float)d : fallback;
        }

        static bool Bool(Dictionary<string, object> o, string key)
        {
            return o.TryGetValue(key, out object v) && v is bool b && b;
        }
    }

    // The smallest JSON reader that covers the sidecar: objects, arrays, strings, numbers,
    // true, false, null. Objects become Dictionary<string, object>, arrays List<object>, numbers
    // double. Setup-time only (it allocates freely); JsonUtility cannot read a field that is a
    // list in one version of the file and an object in the other.
    internal static class MiniJson
    {
        public static object Parse(string text)
        {
            if (text == null) throw new System.FormatException("empty");
            int i = 0;
            object v = Value(text, ref i);
            Skip(text, ref i);
            if (i < text.Length) throw new System.FormatException("trailing text at " + i);
            return v;
        }

        static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new System.FormatException("unexpected end");
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (c == 't') { Expect(s, ref i, "true"); return true; }
            if (c == 'f') { Expect(s, ref i, "false"); return false; }
            if (c == 'n') { Expect(s, ref i, "null"); return null; }
            return Number(s, ref i);
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            Skip(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                Skip(s, ref i);
                string key = Str(s, ref i);
                Skip(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new System.FormatException("':' expected at " + i);
                i++;
                d[key] = Value(s, ref i);
                Skip(s, ref i);
                if (i >= s.Length) throw new System.FormatException("unexpected end in object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new System.FormatException("',' or '}' expected at " + i);
            }
        }

        static List<object> Arr(string s, ref int i)
        {
            var l = new List<object>();
            i++;
            Skip(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i));
                Skip(s, ref i);
                if (i >= s.Length) throw new System.FormatException("unexpected end in array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return l; }
                throw new System.FormatException("',' or ']' expected at " + i);
            }
        }

        static string Str(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new System.FormatException("string expected at " + i);
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new System.FormatException("bad escape");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            throw new System.FormatException("unterminated string");
        }

        static double Number(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) throw new System.FormatException("value expected at " + i);
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        static void Expect(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw new System.FormatException(word + " expected at " + i);
            i += word.Length;
        }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }
    }
}
