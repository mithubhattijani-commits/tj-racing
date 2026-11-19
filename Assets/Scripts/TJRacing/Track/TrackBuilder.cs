// TJ Racing 3D — builds the whole Night City track at runtime from TrackSpline data.
// Everything is generated (meshes, materials, textures) so the project has ZERO
// binary assets: road ribbon, barriers, ramp wedges, tunnel shell, bridge deck,
// procedural city skyline (emissive windows painted into a generated texture),
// street lights, start gantry, hazard signposts and checkpoint triggers.
// Adventure elements (plan §5): ramps/jumps, sharp turns, obstacles, elevation,
// tunnel + bridge sections. Hazards are signposted early (research §A.3).
using System.Collections.Generic;
using UnityEngine;

namespace TJRacing
{
    public class TrackBuilder : MonoBehaviour
    {
        [Header("Road")]
        public float roadWidth = 12f;
        public float barrierHeight = 1.1f;

        [Header("City")]
        public int buildingCount = 46;
        public float cityRadius = 300f;

        public TrackSpline Spline { get; private set; }
        public List<Transform> Checkpoints { get; private set; } = new List<Transform>();
        public Transform StartLine { get; private set; }
        public List<Transform> GridSlots { get; private set; } = new List<Transform>();

        private Material _roadMat;
        private Material _barrierMat;
        private Material _concreteMat;
        private Material _neonPinkMat;
        private Material _neonCyanMat;

        void Awake()
        {
            Spline = new TrackSpline(NightCityTrackData.Waypoints());
            BuildMaterials();
            BuildRoad();
            BuildBarriers();
            BuildRamp();
            BuildTunnel();
            BuildBridge();
            BuildObstacles();
            BuildCity();
            BuildStreetLights();
            BuildStartGantry();
            BuildCheckpointsAndGrid();
        }

        // ---- Materials (all generated) ---------------------------------------
        private void BuildMaterials()
        {
            _roadMat = new Material(Shader.Find("Standard"));
            _roadMat.color = new Color(0.09f, 0.09f, 0.11f);
            _roadMat.SetFloat("_Smoothness", 0.25f);
            // Centre dashes painted into the road texture (generated, tiled along length).
            _roadMat.mainTexture = MakeRoadTexture();
            _roadMat.mainTextureScale = new Vector2(1f, 220f);

            _barrierMat = new Material(Shader.Find("Standard"));
            _barrierMat.color = new Color(0.16f, 0.16f, 0.2f);
            _barrierMat.SetFloat("_Metallic", 0.4f);
            _barrierMat.SetFloat("_Smoothness", 0.5f);

            _concreteMat = new Material(Shader.Find("Standard"));
            _concreteMat.color = new Color(0.22f, 0.22f, 0.26f);

            _neonPinkMat = Emissive(new Color(1f, 0.15f, 0.6f));
            _neonCyanMat = Emissive(new Color(0.1f, 0.85f, 1f));
        }

        private static Material Emissive(Color c)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = Color.black;
            m.SetColor("_EmissionColor", c * 2.5f);
            m.EnableKeyword("_EMISSION");
            return m;
        }

        private static Texture2D MakeRoadTexture()
        {
            // 64x64: dark asphalt with a white centre dash + edge lines.
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    Color c = new Color(0.09f, 0.09f, 0.11f);
                    if (x < 2 || x > 61) c = new Color(0.75f, 0.75f, 0.75f); // edge lines
                    if (x >= 30 && x <= 33 && y < 28) c = Color.white;        // centre dash
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeWindowsTexture(int seed)
        {
            // Building windows: random lit windows in pink / cyan / warm yellow,
            // like the mockup skyline. Generated per building from a seed.
            var rnd = new System.Random(seed);
            var tex = new Texture2D(32, 64, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color[] palette = {
                new Color(1f, 0.75f, 0.25f), new Color(0.2f, 0.9f, 1f),
                new Color(1f, 0.4f, 0.7f), new Color(0.05f, 0.05f, 0.08f),
            };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 32; x++)
                {
                    Color c = new Color(0.03f, 0.03f, 0.06f);
                    if (x % 4 < 2 && y % 4 < 2 && rnd.NextDouble() < 0.55)
                        c = palette[rnd.Next(palette.Length)];
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return tex;
        }

        // ---- Road ribbon ------------------------------------------------------
        private void BuildRoad()
        {
            var pts = Spline.Points;
            int n = pts.Count;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= n; i++)
            {
                int idx = i % n;
                Vector3 p = pts[idx];
                Vector3 dir = Spline.DirectionAt(idx);
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized * (roadWidth / 2f);
                verts.Add(p - side);
                verts.Add(p + side);
                uvs.Add(new Vector2(0f, i));
                uvs.Add(new Vector2(1f, i));
                if (i < n)
                {
                    int a = i * 2;
                    tris.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
                }
            }
            var mesh = new Mesh { name = "NightCityRoad" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Road");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().mesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.material = _roadMat;
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            // Ground plane far below everything, so a car that leaves the road lands.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            ground.transform.position = new Vector3(0f, -2.6f, 0f);
            ground.GetComponent<Renderer>().material = _concreteMat;
        }

        // ---- Barriers (both edges, following the spline) ----------------------
        private void BuildBarriers()
        {
            var pts = Spline.Points;
            for (int side = -1; side <= 1; side += 2)
            {
                var verts = new List<Vector3>();
                var tris = new List<int>();
                for (int i = 0; i <= pts.Count; i++)
                {
                    int idx = i % pts.Count;
                    Vector3 p = pts[idx];
                    Vector3 dir = Spline.DirectionAt(idx);
                    Vector3 off = Vector3.Cross(Vector3.up, dir).normalized * (roadWidth / 2f + 0.4f) * side;
                    Vector3 basePt = p + off;
                    verts.Add(basePt);
                    verts.Add(basePt + Vector3.up * barrierHeight);
                    if (i < pts.Count)
                    {
                        int a = i * 2;
                        tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                    }
                }
                var mesh = new Mesh { name = "Barrier" + side };
                mesh.SetVertices(verts);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
                var go = new GameObject("Barrier" + (side < 0 ? "L" : "R"));
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().mesh = mesh;
                go.AddComponent<MeshRenderer>().material = _barrierMat;
                var col = go.AddComponent<MeshCollider>();
                col.sharedMesh = mesh;
                // Neon strip on top of the barrier (pink left, cyan right — mockup colours).
                var strip = new GameObject("NeonStrip" + (side < 0 ? "L" : "R"));
                strip.transform.SetParent(transform, false);
                var stripMesh = BuildStripMesh(pts, side);
                strip.AddComponent<MeshFilter>().mesh = stripMesh;
                strip.AddComponent<MeshRenderer>().material = side < 0 ? _neonPinkMat : _neonCyanMat;
            }
        }

        private Mesh BuildStripMesh(List<Vector3> pts, int side)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= pts.Count; i++)
            {
                int idx = i % pts.Count;
                Vector3 p = pts[idx];
                Vector3 dir = Spline.DirectionAt(idx);
                Vector3 off = Vector3.Cross(Vector3.up, dir).normalized * (roadWidth / 2f + 0.4f) * side;
                Vector3 basePt = p + off + Vector3.up * (barrierHeight - 0.12f);
                verts.Add(basePt);
                verts.Add(basePt + Vector3.up * 0.12f);
                if (i < pts.Count)
                {
                    int a = i * 2;
                    tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        // ---- Ramp (jump) -------------------------------------------------------
        private void BuildRamp()
        {
            // Wedge at waypoint 5: a raised lip the car launches off. Landing zone
            // is the flat road after it; a chevron signpost stands 40 m before.
            int idx = NightCityTrackData.RampCheckpoint - 1;
            Vector3 p = Spline.Points[idx];
            Vector3 dir = Spline.DirectionAt(idx);
            var ramp = new GameObject("Ramp");
            ramp.transform.SetParent(transform, false);
            ramp.transform.position = p;
            ramp.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            var wedge = ramp.AddComponent<MeshFilter>();
            wedge.mesh = BuildWedgeMesh(roadWidth, 10f, 1.6f);
            ramp.AddComponent<MeshRenderer>().material = _concreteMat;
            var col = ramp.AddComponent<MeshCollider>();
            col.sharedMesh = wedge.mesh;
            BuildChevronSign(p - dir * 40f, dir, "JUMP AHEAD");
        }

        private static Mesh BuildWedgeMesh(float width, float length, float height)
        {
            // Simple wedge: flat base, sloped top rising toward +Z.
            var m = new Mesh();
            float hw = width / 2f;
            Vector3[] v =
            {
                new Vector3(-hw, 0, -length / 2), new Vector3(hw, 0, -length / 2),
                new Vector3(-hw, 0, length / 2),  new Vector3(hw, 0, length / 2),
                new Vector3(-hw, height, length / 2), new Vector3(hw, height, length / 2),
            };
            m.vertices = v;
            m.triangles = new[]
            {
                0, 2, 1, 1, 2, 3,       // base
                2, 4, 3, 3, 4, 5,       // slope
                0, 1, 4, 1, 5, 4,       // back face (low end)
                0, 4, 2, 1, 3, 5,       // sides
            };
            m.RecalculateNormals();
            return m;
        }

        private void BuildChevronSign(Vector3 pos, Vector3 dir, string text)
        {
            var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sign.name = "Sign_" + text;
            sign.transform.SetParent(transform, false);
            sign.transform.position = pos + Vector3.up * 2.2f;
            sign.transform.localScale = new Vector3(5f, 1.6f, 0.2f);
            sign.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            sign.GetComponent<Renderer>().material = _neonPinkMat;
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.transform.SetParent(transform, false);
            pole.transform.position = pos + Vector3.up * 1f;
            pole.transform.localScale = new Vector3(0.15f, 1.2f, 0.15f);
            pole.GetComponent<Renderer>().material = _barrierMat;
        }

        // ---- Tunnel ------------------------------------------------------------
        private void BuildTunnel()
        {
            // Enclosed shell over waypoints 14..19 + light strip inside.
            var pts = Spline.Points;
            var shell = new GameObject("Tunnel");
            shell.transform.SetParent(transform, false);
            for (int i = NightCityTrackData.TunnelStart; i < NightCityTrackData.TunnelEnd; i++)
            {
                Vector3 p = pts[i];
                Vector3 dir = Spline.DirectionAt(i);
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = "TunnelSeg" + i;
                seg.transform.SetParent(shell.transform, false);
                seg.transform.position = p + Vector3.up * 3.4f;
                seg.transform.localScale = new Vector3(roadWidth + 3f, 0.5f, Vector3.Distance(p, pts[(i + 1) % pts.Count]) + 0.6f);
                seg.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                seg.GetComponent<Renderer>().material = _concreteMat;
                // Light strip on the ceiling.
                var light = GameObject.CreatePrimitive(PrimitiveType.Cube);
                light.name = "TunnelLight" + i;
                light.transform.SetParent(shell.transform, false);
                light.transform.position = p + Vector3.up * 3.1f;
                light.transform.localScale = new Vector3(0.4f, 0.1f, 3f);
                light.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                light.GetComponent<Renderer>().material = _neonCyanMat;
            }
            BuildChevronSign(pts[NightCityTrackData.TunnelStart - 1], Spline.DirectionAt(NightCityTrackData.TunnelStart - 1), "TUNNEL");
        }

        // ---- Bridge (elevated deck; lap-2 traffic drives here) -----------------
        private void BuildBridge()
        {
            var pts = Spline.Points;
            var bridge = new GameObject("Bridge");
            bridge.transform.SetParent(transform, false);
            for (int i = NightCityTrackData.BridgeStart; i <= NightCityTrackData.BridgeEnd; i++)
            {
                Vector3 p = pts[i];
                Vector3 dir = Spline.DirectionAt(i);
                // Deck edge glow so the raised section reads clearly at night.
                var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                edge.name = "BridgeEdge" + i;
                edge.transform.SetParent(bridge.transform, false);
                edge.transform.position = p + Vector3.up * 0.15f;
                edge.transform.localScale = new Vector3(roadWidth + 1f, 0.3f, Vector3.Distance(p, pts[(i + 1) % pts.Count]) + 0.4f);
                edge.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                edge.GetComponent<Renderer>().material = _neonCyanMat;
                // Support pillar down to the ground.
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "BridgePillar" + i;
                pillar.transform.SetParent(bridge.transform, false);
                pillar.transform.position = new Vector3(p.x, p.y / 2f, p.z);
                pillar.transform.localScale = new Vector3(1.2f, Mathf.Max(0.5f, p.y), 1.2f);
                pillar.GetComponent<Renderer>().material = _concreteMat;
            }
        }

        // ---- Obstacles (chicane barrels + tyre stacks) -------------------------
        private void BuildObstacles()
        {
            var pts = Spline.Points;
            for (int i = NightCityTrackData.ChicaneStart; i < NightCityTrackData.ChicaneStart + 3; i++)
            {
                Vector3 p = pts[i];
                Vector3 dir = Spline.DirectionAt(i);
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                // Barrels force an S-line through the chicane; hitting one costs speed.
                foreach (float off in new[] { -3.2f, 3.2f })
                {
                    var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    barrel.name = "ObstacleBarrel" + i + "_" + (int)off;
                    barrel.transform.SetParent(transform, false);
                    barrel.transform.position = p + side * off + Vector3.up * 0.6f;
                    barrel.transform.localScale = new Vector3(0.9f, 1.2f, 0.9f);
                    barrel.GetComponent<Renderer>().material = _neonPinkMat;
                }
            }
        }

        // ---- City skyline -------------------------------------------------------
        private void BuildCity()
        {
            var rnd = new System.Random(1337);
            var city = new GameObject("City");
            city.transform.SetParent(transform, false);
            for (int i = 0; i < buildingCount; i++)
            {
                float angle = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float radius = 90f + (float)rnd.NextDouble() * (cityRadius - 90f);
                // Keep buildings OFF the track: skip if within 25 m of any waypoint.
                Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                bool nearTrack = false;
                foreach (var wp in Spline.Points)
                    if (Vector3.Distance(new Vector3(wp.x, 0f, wp.z), pos) < 26f) { nearTrack = true; break; }
                if (nearTrack) continue;

                float w = 10f + (float)rnd.NextDouble() * 14f;
                float h = 18f + (float)rnd.NextDouble() * 55f;
                float d = 10f + (float)rnd.NextDouble() * 14f;
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Building" + i;
                b.transform.SetParent(city.transform, false);
                b.transform.position = new Vector3(pos.x, h / 2f - 2.6f, pos.z);
                b.transform.localScale = new Vector3(w, h, d);
                var mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.04f, 0.04f, 0.08f);
                mat.mainTexture = MakeWindowsTexture(1000 + i);
                mat.SetColor("_EmissionColor", Color.white * 0.9f);
                mat.EnableKeyword("_EMISSION");
                // Emission map = the window texture itself.
                mat.SetTexture("_EmissionMap", mat.mainTexture);
                b.GetComponent<Renderer>().material = mat;
                // Rooftop beacon on the tallest towers.
                if (h > 55f)
                {
                    var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    beacon.name = "Beacon" + i;
                    beacon.transform.SetParent(city.transform, false);
                    beacon.transform.position = new Vector3(pos.x, h - 2.2f, pos.z);
                    beacon.transform.localScale = Vector3.one * 0.9f;
                    beacon.GetComponent<Renderer>().material = Emissive(Color.red);
                }
            }
        }

        // ---- Street lights -------------------------------------------------------
        private void BuildStreetLights()
        {
            var pts = Spline.Points;
            for (int i = 0; i < pts.Count; i += 2)
            {
                Vector3 p = pts[i];
                Vector3 dir = Spline.DirectionAt(i);
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "StreetLight" + i;
                pole.transform.SetParent(transform, false);
                pole.transform.position = p + side * (roadWidth / 2f + 1.2f) + Vector3.up * 3f;
                pole.transform.localScale = new Vector3(0.18f, 3f, 0.18f);
                pole.GetComponent<Renderer>().material = _barrierMat;
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lamp.name = "Lamp" + i;
                lamp.transform.SetParent(transform, false);
                lamp.transform.position = p + side * (roadWidth / 2f + 0.6f) + Vector3.up * 6f;
                lamp.transform.localScale = Vector3.one * 0.7f;
                lamp.GetComponent<Renderer>().material = Emissive(new Color(1f, 0.9f, 0.6f));
                var light = lamp.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.85f, 0.55f);
                light.intensity = 1.6f;
                light.range = 22f;
            }
        }

        // ---- Start gantry + checkpoints + grid ----------------------------------
        private void BuildStartGantry()
        {
            Vector3 p = Spline.Points[0];
            Vector3 dir = Spline.DirectionAt(0);
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            var gantry = new GameObject("StartGantry");
            gantry.transform.SetParent(transform, false);
            gantry.transform.position = p;
            gantry.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            foreach (float x in new[] { -6.5f, 6.5f })
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.transform.SetParent(gantry.transform, false);
                post.transform.localPosition = new Vector3(x, 3f, 0f);
                post.transform.localScale = new Vector3(0.5f, 6f, 0.5f);
                post.GetComponent<Renderer>().material = _barrierMat;
            }
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.transform.SetParent(gantry.transform, false);
            beam.transform.localPosition = new Vector3(0f, 5.8f, 0f);
            beam.transform.localScale = new Vector3(13.5f, 1f, 0.6f);
            beam.GetComponent<Renderer>().material = _neonPinkMat;
            StartLine = gantry.transform;
        }

        private void BuildCheckpointsAndGrid()
        {
            // One checkpoint trigger every ~4 spline points (10 total incl. start).
            for (int i = 0; i < Spline.Count; i += 4)
            {
                var cp = new GameObject("Checkpoint_" + (i / 4));
                cp.transform.SetParent(transform, false);
                cp.transform.position = Spline.Points[i];
                cp.transform.rotation = Quaternion.LookRotation(Spline.DirectionAt(i), Vector3.up);
                var trigger = cp.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(roadWidth + 2f, 6f, 2.5f);
                var comp = cp.AddComponent<Checkpoint>();
                comp.checkpointIndex = i / 4;
                Checkpoints.Add(cp.transform);
            }
            // Grid: 6 slots, 2 columns x 3 rows behind the start line. Player last
            // (row 3) so race 1 starts from the back and must be fought — but AI is
            // slowed 25% on race 1 (config firstRaceAiSlowdownPct) so it stays winnable.
            Vector3 start = Spline.Points[0];
            Vector3 dir = Spline.DirectionAt(0);
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 2; col++)
                {
                    var slot = new GameObject("GridSlot_" + (row * 2 + col));
                    slot.transform.SetParent(transform, false);
                    slot.transform.position = start - dir * (6f + row * 9f) + side * (col == 0 ? -3f : 3f);
                    slot.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                    GridSlots.Add(slot.transform);
                }
        }
    }
}
