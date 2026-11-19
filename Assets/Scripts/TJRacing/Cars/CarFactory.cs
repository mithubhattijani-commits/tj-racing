// TJ Racing 3D — procedural low-poly cars (no binary models in v1).
// Cars are built from primitives so the whole project stays TEXT-ONLY and the
// unityLibrary export needs no Asset Store download. SWAP GUIDE (docs/ASSETS.md):
// drop an official Asset Store car model (ARCADE: Ultimate Vehicles Pack or
// 60 Fantastic Race Cars — both free, standard Asset Store EULA) onto the
// "BodyRoot" child and delete the primitive body; physics, stats and colours
// keep working unchanged because they live on CarController, not the mesh.
using System.Collections.Generic;
using UnityEngine;

namespace TJRacing
{
    public static class CarFactory
    {
        public struct BuiltCar
        {
            public GameObject root;
            public CarController controller;
            public Transform bodyRoot;
        }

        private static readonly Dictionary<string, Material> _bodyMaterials = new Dictionary<string, Material>();

        public static BuiltCar Build(CarDef def, bool isPlayer)
        {
            var root = new GameObject("Car_" + def.id);
            root.tag = isPlayer ? "Player" : "AICar";
            // Shield power-up: the player car lives on its own layer
            // so RaceManager can ignore PlayerCar-vs-Traffic
            // collisions while a shield is active (see RaceLayers).
            // AI cars stay on Default and still collide with traffic.
            root.layer = isPlayer
                ? RaceLayers.PlayerCar
                : LayerMask.NameToLayer("Default");

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1350f;
            rb.drag = 0.35f;
            rb.angularDrag = 2.2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var controller = root.AddComponent<CarController>();
            controller.IsPlayer = isPlayer;
            controller.topSpeedKmh = def.topSpeedKmh;
            controller.acceleration = def.acceleration;
            controller.handling = def.handling;
            controller.nitroCapacity = def.nitroCapacity;

            // --- Body (primitive low-poly sports-car shape; swap per docs/ASSETS.md) ---
            var bodyRoot = new GameObject("BodyRoot").transform;
            bodyRoot.SetParent(root.transform, false);

            Color bodyColor = ParseHex(def.bodyColorHex, new Color(0.9f, 0.28f, 0.3f));
            Material bodyMat = GetBodyMaterial(def.id, bodyColor);
            Material darkMat = GetDarkMaterial();
            Material glassMat = GetGlassMaterial();

            AddBox(bodyRoot, "BodyLower", new Vector3(0f, 0.55f, 0f), new Vector3(1.9f, 0.55f, 4.4f), bodyMat);
            AddBox(bodyRoot, "BodyNose", new Vector3(0f, 0.42f, 1.95f), new Vector3(1.7f, 0.34f, 0.9f), bodyMat);
            AddBox(bodyRoot, "Cabin", new Vector3(0f, 1.02f, -0.25f), new Vector3(1.55f, 0.5f, 2.0f), glassMat);
            AddBox(bodyRoot, "SpoilerWing", new Vector3(0f, 1.12f, -2.05f), new Vector3(1.9f, 0.08f, 0.45f), darkMat);
            AddBox(bodyRoot, "SpoilerL", new Vector3(-0.8f, 0.9f, -2.05f), new Vector3(0.08f, 0.4f, 0.3f), darkMat);
            AddBox(bodyRoot, "SpoilerR", new Vector3(0.8f, 0.9f, -2.05f), new Vector3(0.08f, 0.4f, 0.3f), darkMat);
            AddBox(bodyRoot, "BumperF", new Vector3(0f, 0.3f, 2.28f), new Vector3(1.9f, 0.22f, 0.18f), darkMat);
            // Headlights (emissive — night city readability) + taillights.
            AddBox(bodyRoot, "LightFL", new Vector3(-0.6f, 0.5f, 2.42f), new Vector3(0.42f, 0.12f, 0.05f), GetEmissiveMaterial(new Color(1f, 0.95f, 0.75f)));
            AddBox(bodyRoot, "LightFR", new Vector3(0.6f, 0.5f, 2.42f), new Vector3(0.42f, 0.12f, 0.05f), GetEmissiveMaterial(new Color(1f, 0.95f, 0.75f)));
            AddBox(bodyRoot, "LightRL", new Vector3(-0.6f, 0.55f, -2.22f), new Vector3(0.5f, 0.12f, 0.05f), GetEmissiveMaterial(new Color(1f, 0.08f, 0.1f)));
            AddBox(bodyRoot, "LightRR", new Vector3(0.6f, 0.55f, -2.22f), new Vector3(0.5f, 0.12f, 0.05f), GetEmissiveMaterial(new Color(1f, 0.08f, 0.1f)));
            // Nitro flame cones (enabled by RaceHUD / CarController while boosting).
            var flameL = AddCone(bodyRoot, "NitroFlameL", new Vector3(-0.35f, 0.35f, -2.45f));
            var flameR = AddCone(bodyRoot, "NitroFlameR", new Vector3(0.35f, 0.35f, -2.45f));
            flameL.SetActive(false);
            flameR.SetActive(false);
            root.AddComponent<NitroFlames>().Init(flameL, flameR);

            // --- Wheels: colliders + visible meshes ---
            BuildWheel(root, controller, "FL", new Vector3(-0.85f, 0.35f, 1.45f), true);
            BuildWheel(root, controller, "FR", new Vector3(0.85f, 0.35f, 1.45f), true);
            BuildWheel(root, controller, "RL", new Vector3(-0.85f, 0.35f, -1.45f), false);
            BuildWheel(root, controller, "RR", new Vector3(0.85f, 0.35f, -1.45f), false);

            // Box collider for crashes / traffic bumps (wheels are triggers for physics only).
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.7f, 0f);
            box.size = new Vector3(1.9f, 1.0f, 4.4f);

            return new BuiltCar { root = root, controller = controller, bodyRoot = bodyRoot };
        }

        private static void BuildWheel(GameObject root, CarController c, string name,
            Vector3 pos, bool front)
        {
            var colObj = new GameObject("WheelCol_" + name);
            colObj.transform.SetParent(root.transform, false);
            colObj.transform.localPosition = pos;
            var col = colObj.AddComponent<WheelCollider>();
            col.radius = 0.35f;
            col.suspensionDistance = 0.22f;
            col.mass = 22f;
            var susp = col.suspensionSpring;
            susp.spring = 26000f; susp.damper = 3200f; susp.targetPosition = 0.5f;
            col.suspensionSpring = susp;
            var friction = col.forwardFriction;
            friction.extremumSlip = 0.35f; friction.extremumValue = 1.0f;
            friction.asymptoteSlip = 0.7f; friction.asymptoteValue = 0.72f; friction.stiffness = 1.15f;
            col.forwardFriction = friction;
            var side = col.sidewaysFriction;
            side.extremumSlip = 0.32f; side.extremumValue = 1.0f;
            side.asymptoteSlip = 0.65f; side.asymptoteValue = 0.78f; side.stiffness = 1.1f;
            col.sidewaysFriction = side;

            // Visible wheel: dark cylinder + lighter hub.
            var meshObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            meshObj.name = "WheelMesh_" + name;
            meshObj.transform.SetParent(root.transform, false);
            meshObj.transform.localPosition = pos;
            meshObj.transform.localScale = new Vector3(0.7f, 0.28f, 0.7f);
            meshObj.transform.Rotate(0f, 0f, 90f);
            meshObj.GetComponent<Renderer>().material = GetDarkMaterial();
            Object.Destroy(meshObj.GetComponent<Collider>());

            if (front)
            {
                if (name == "FL") { c.wheelFL = col; c.wheelMeshFL = meshObj.transform; }
                else { c.wheelFR = col; c.wheelMeshFR = meshObj.transform; }
            }
            else
            {
                if (name == "RL") { c.wheelRL = col; c.wheelMeshRL = meshObj.transform; }
                else { c.wheelRR = col; c.wheelMeshRR = meshObj.transform; }
            }
        }

        private static GameObject AddBox(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().material = mat;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject AddCone(Transform parent, string name, Vector3 pos)
        {
            // Flame = small emissive cone pointing backwards.
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = BuildConeMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.material = GetEmissiveMaterial(new Color(0.2f, 0.8f, 1f));
            return go;
        }

        private static Mesh BuildConeMesh()
        {
            // Minimal 8-sided cone, 0.5 long, radius 0.16 — generated, no asset needed.
            var mesh = new Mesh();
            int sides = 8;
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            verts.Add(new Vector3(0f, 0.5f, 0f)); // tip
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                verts.Add(new Vector3(Mathf.Cos(a) * 0.16f, 0f, Mathf.Sin(a) * 0.16f));
            }
            verts.Add(new Vector3(0f, 0f, 0f)); // base centre
            for (int i = 0; i < sides; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % sides;
                tris.AddRange(new[] { 0, b, a });
                tris.AddRange(new[] { sides + 1, a, b });
            }
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Material GetBodyMaterial(string id, Color c)
        {
            if (!_bodyMaterials.TryGetValue(id, out var m))
            {
                m = new Material(Shader.Find("Standard"));
                m.color = c;
                m.SetFloat("_Metallic", 0.65f);
                m.SetFloat("_Smoothness", 0.75f);
                _bodyMaterials[id] = m;
            }
            return m;
        }

        private static Material GetDarkMaterial()
        {
            if (!_bodyMaterials.TryGetValue("__dark", out var m))
            {
                m = new Material(Shader.Find("Standard"));
                m.color = new Color(0.08f, 0.08f, 0.1f);
                m.SetFloat("_Metallic", 0.3f);
                m.SetFloat("_Smoothness", 0.4f);
                _bodyMaterials["__dark"] = m;
            }
            return m;
        }

        private static Material GetGlassMaterial()
        {
            if (!_bodyMaterials.TryGetValue("__glass", out var m))
            {
                m = new Material(Shader.Find("Standard"));
                m.color = new Color(0.1f, 0.16f, 0.28f);
                m.SetFloat("_Metallic", 0.9f);
                m.SetFloat("_Smoothness", 0.9f);
                _bodyMaterials["__glass"] = m;
            }
            return m;
        }

        private static Material GetEmissiveMaterial(Color c)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = Color.black;
            m.SetColor("_EmissionColor", c * 2.2f);
            m.EnableKeyword("_EMISSION");
            return m;
        }

        public static Color ParseHex(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return fallback;
        }
    }

    /// <summary>Enables/disables the nitro flame cones while boosting.</summary>
    public class NitroFlames : MonoBehaviour
    {
        private GameObject _l, _r;
        private CarController _car;

        public void Init(GameObject l, GameObject r) { _l = l; _r = r; }

        void Awake() { _car = GetComponent<CarController>(); }

        void Update()
        {
            bool on = _car != null && _car.Nitro != null && _car.Nitro.IsActive;
            if (_l != null) _l.SetActive(on);
            if (_r != null) _r.SetActive(on);
        }
    }
}
