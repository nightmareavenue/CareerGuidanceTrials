using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Меню: Tools > Demo > Build Car Demo Scene
// Ставит модель на машинку (корпус + 4 колеса) и расставляет объекты на сцене.
// Скрипт можно запускать повторно: старые Arena / BodyModel / Wheel_* пересоздаются.
public static class BuildCarDemoScene
{
    const string GenDir = "Assets/DemoGenerated";
    static readonly List<string> missing = new List<string>();

    [MenuItem("Tools/Demo/Build Car Demo Scene")]
    public static void Build()
    {
        missing.Clear();

        var car = GameObject.Find("Car");
        if (car == null)
        {
            EditorUtility.DisplayDialog("Demo", "На сцене не найден объект Car.", "OK");
            return;
        }
        var ctrl = car.GetComponent<CarController>();
        if (ctrl == null)
        {
            EditorUtility.DisplayDialog("Demo", "На объекте Car нет компонента CarController.", "OK");
            return;
        }

        if (car.GetComponent<AirRingScore>() == null)
            car.AddComponent<AirRingScore>();

        var jetpack = car.GetComponent<JetpackController>();
        if (jetpack != null) Object.DestroyImmediate(jetpack);
        var nitro = car.GetComponent<NitroBoost>();
        if (nitro != null) Object.DestroyImmediate(nitro);

        // Машинку ставим в начало комнаты (позиция задаётся в BuildArena), смотрит вдоль +Z
        car.transform.rotation = Quaternion.identity;

        float track = BuildBody(car);
        BuildWheels(car, ctrl, track);
        BuildArena(car);

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(car.scene);
        AssetDatabase.SaveAssets();

        string msg = "Готово. Сохраните сцену (Ctrl+S).";
        if (missing.Count > 0)
            msg += "\n\nНе найдены модели (проверьте, что папка Pack лежит в Assets):\n" + string.Join(", ", missing);
        EditorUtility.DisplayDialog("Demo", msg, "OK");
    }

    // ---------- Машинка ----------

    static float BuildBody(GameObject car)
    {
        float track = 1.1f;

        var old = car.transform.Find("BodyModel");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var model = FindModel("body_banana", null);
        if (model == null) { missing.Add("body_banana"); return track; }

        var root = new GameObject("BodyModel");
        var inst = Spawn(model, root.transform);
        Paint(inst, "Banana");

        Bounds b;
        TryBounds(root, out b);
        // Длинная сторона модели должна идти вдоль Z
        if (b.size.x > b.size.z)
        {
            inst.transform.localRotation = Quaternion.Euler(0, 90, 0);
            TryBounds(root, out b);
        }
        float len = Mathf.Max(b.size.x, b.size.z);
        if (len > 0.0001f) inst.transform.localScale *= 3.8f / len;
        TryBounds(root, out b);
        inst.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
        TryBounds(root, out b);

        root.transform.SetParent(car.transform, false);
        root.transform.localPosition = new Vector3(0, 0.45f, 0);
        root.transform.localRotation = Quaternion.identity;

        // Старый куб оставляем только как коллайдер, подгоняем под модель
        var bodyCube = car.transform.Find("Body");
        if (bodyCube != null)
        {
            var mr = bodyCube.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            bodyCube.localPosition = new Vector3(0, 0.45f + b.size.y * 0.5f, 0);
            bodyCube.localScale = new Vector3(b.size.x, b.size.y, b.size.z);
        }

        track = Mathf.Clamp(b.size.x * 0.5f + 0.25f, 0.9f, 1.6f);
        return track;
    }

    static void BuildWheels(GameObject car, CarController ctrl, float track)
    {
        string[] ids = { "FL", "FR", "RL", "RR" };
        var pivots = new Transform[4];
        var model = FindModel("wheels_cap_visuals", null);
        if (model == null) missing.Add("wheels_cap_visuals");

        for (int i = 0; i < 4; i++)
        {
            float sx = (i % 2 == 0) ? -1f : 1f;
            float sz = (i < 2) ? 1f : -1f;

            var oldPivot = car.transform.Find("Wheel_" + ids[i]);
            if (oldPivot != null) Object.DestroyImmediate(oldPivot.gameObject);

            // Wheel Collider ставим по ширине кузова
            var wc = car.transform.Find("WC_" + ids[i]);
            if (wc != null) wc.localPosition = new Vector3(sx * track, 0.55f, sz * 1.3f);

            // Старые цилиндры прячем
            var oldMesh = car.transform.Find("WM_" + ids[i]);
            if (oldMesh != null) oldMesh.gameObject.SetActive(false);

            if (model == null) continue;

            var root = new GameObject("Wheel_" + ids[i]);
            var inst = Spawn(model, root.transform);
            Paint(inst, "Wheels");

            // В FBX лежит пара колёс (wheels_cap_1 слева и wheels_cap_2 справа), ось уже вдоль X.
            // Оставляем одно колесо нужной стороны, второе выключаем, поворачивать ничего не нужно.
            string keep = sx < 0 ? "wheels_cap_1" : "wheels_cap_2";
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                if (t.name == "wheels_cap_1" || t.name == "wheels_cap_2")
                    t.gameObject.SetActive(t.name == keep);

            Bounds b;
            TryBounds(root, out b);
            float dia = Mathf.Max(b.size.y, b.size.z);
            if (dia > 0.0001f) inst.transform.localScale *= 0.8f / dia;
            TryBounds(root, out b);
            inst.transform.position += -b.center;

            root.transform.SetParent(car.transform, false);
            root.transform.localPosition = new Vector3(sx * track, 0.4f, sz * 1.3f);
            root.transform.localRotation = Quaternion.identity;
            pivots[i] = root.transform;
        }

        if (pivots[0] != null)
        {
            ctrl.flMesh = pivots[0];
            ctrl.frMesh = pivots[1];
            ctrl.rlMesh = pivots[2];
            ctrl.rrMesh = pivots[3];
            ctrl.meshRotationOffset = Vector3.zero; // поворот уже учтён внутри Wheel_*
        }
    }

    // ---------- Комната ----------

    // Размеры комнаты в метрах (машинка 4 м: это «уменьшенная» комната, всё вокруг огромное)
    const float RoomW = 160f;   // по X
    const float RoomL = 200f;   // по Z
    const float RoomH = 50f;    // высота
    const float WallT = 2f;     // толщина стен
    const float StartZ = -85f;  // старт машинки

    // Если окна, двери и картины на стенах не видны (повёрнуты спиной), поставьте true
    const bool FlipWallDecor = false;

    static void BuildArena(GameObject car)
    {
        var oldArena = GameObject.Find("Arena");
        if (oldArena != null) Object.DestroyImmediate(oldArena);
        var arena = new GameObject("Arena").transform;

        BuildRoom(arena);
        BuildAirRings(arena);

        // Трамплины
        var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.name = "Ramp";
        ramp.transform.SetParent(arena, false);
        ramp.transform.localScale = new Vector3(8, 0.6f, 12);
        ramp.transform.localPosition = new Vector3(0, 1.1f, -45);
        ramp.transform.localRotation = Quaternion.Euler(-15, 0, 0);
        ramp.GetComponent<Renderer>().sharedMaterial = ColorMat("DemoRamp", new Color(0.95f, 0.55f, 0.15f));

        var ramp2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp2.name = "Ramp_small";
        ramp2.transform.SetParent(arena, false);
        ramp2.transform.localScale = new Vector3(6, 0.5f, 9);
        ramp2.transform.localPosition = new Vector3(32, 0.8f, 15);
        ramp2.transform.localRotation = Quaternion.Euler(-12, 20, 0);
        ramp2.GetComponent<Renderer>().sharedMaterial = ColorMat("DemoRamp", new Color(0.95f, 0.55f, 0.15f));

        // Большие предметы вдоль стен (все физические)
        Place(MakeProp("worktable", "worktable", 20, false, null, true), arena, -62, -20, 90);
        Place(MakeProp("stack_shelf", "stack_shelf", 12, false, null, true), arena, 66, -30, -90);
        Place(MakeProp("shelf_wide", "stack_shelf", 16, false, null, true), arena, 66, 28, -90);
        Place(MakeProp("bedside_drawer", "bedside_drawer", 9, false, null, true), arena, -68, 40, 90);
        Place(MakeProp("low_shelf", "bedside_drawer", 7, false, "/Prop/", true), arena, -68, 80, 90);
        Place(MakeProp("tv set", "prop_1", 7, false, "/Prop/", true), arena, -45, 92, 180);

        // Мелкие предметы возле старта
        Place(MakeProp("glue", "prop_1", 3, false, "/Prop/", true), arena, -12, -78, 20);
        Place(MakeProp("tape", "prop_1", 2.5f, false, "/Prop/", true), arena, 12, -80, -15);
        Place(MakeProp("scissors", "prop_1", 4, false, "/Prop/", true), arena, -22, -68, 40);
        Place(MakeProp("spray", "prop_1", 4, false, "/Prop/", true), arena, 22, -70, 0);
        Place(MakeProp("lamp", "prop_1", 7, true, "/Prop/", true), arena, -34, -76, 0);

        // Препятствия на трассе
        Place(MakeProp("crossing_sticks", "wings", 6, false, null, true), arena, -12, -25, 20);
        Place(MakeProp("crossing_sticks", "wings", 6, false, null, true), arena, 12, -12, -20);
        Place(MakeProp("crossing_sticks", "wings", 6, false, null, true), arena, -12, 5, 10);
        Place(MakeProp("book_1", "prop_2", 3.5f, false, "/Prop/", true), arena, -30, -35, 15);
        Place(MakeProp("book_2", "prop_2", 3.5f, false, "/Prop/", true), arena, 30, -25, -30);
        Place(MakeProp("book_3", "prop_3", 3.5f, false, "/Prop/", true), arena, -30, 25, 10);
        Place(MakeProp("book_4", "prop_3", 3.5f, false, "/Prop/", true), arena, 32, 40, -10);
        Place(MakeProp("cactus", "prop_3", 5f, true, "/Prop/", true), arena, -55, -55, 0);
        Place(MakeProp("cactus", "prop_3", 5f, true, "/Prop/", true), arena, 55, -50, 0);
        Place(MakeProp("cactus", "prop_3", 5f, true, "/Prop/", true), arena, -55, 62, 0);
        Place(MakeProp("cactus", "prop_3", 5f, true, "/Prop/", true), arena, 55, 70, 0);

        // Пирамида из стаканчиков, которую можно сбить
        float h = 1.8f;
        float[,] cupPos =
        {
            { -2.2f, 0f }, { 0f, 0f }, { 2.2f, 0f },
            { -1.1f, h }, { 1.1f, h },
            { 0f, 2f * h }
        };
        for (int i = 0; i < cupPos.GetLength(0); i++)
        {
            var cup = MakeProp("plastic_cup", "prop_1", h, true, null, true);
            if (cup == null) break;
            Place(cup, arena, cupPos[i, 0], 60, 0, cupPos[i, 1]);
            var rb = cup.GetComponent<Rigidbody>();
            if (rb != null) rb.mass = 3f;
        }

        // Финишная лента (без коллайдера и без физики: просто декорация, через неё можно проехать)
        Place(MakeProp("finish_tape", "prop_1", 24, false, null, false), arena, 0, 85, 0, 0.5f);

        // Украшения на стенах: окна, дверь, картины, часы (без физики)
        // wall: 0 = северная (z+), 1 = южная (z-), 2 = восточная (x+), 3 = западная (x-)
        PlaceOnWall(MakeProp("window", "prop_3", 16, true, "/Prop/", false), arena, 2, 0, 15);
        PlaceOnWall(MakeProp("window", "prop_3", 16, true, "/Prop/", false), arena, 0, -30, 15);
        PlaceOnWall(MakeProp("door", "bedside_drawer", 26, true, "/Prop/", false), arena, 3, -60, 0);
        PlaceOnWall(MakeProp("picture", "prop_2", 12, false, "/Prop/", false), arena, 1, -25, 22);
        PlaceOnWall(MakeProp("pano_1", "prop_3", 14, true, "/Prop/", false), arena, 0, 30, 18);
        PlaceOnWall(MakeProp("wall_clock", "prop_2", 10, false, "/Prop/", false), arena, 0, 0, 36);

        // Машинка и камера
        car.transform.position = new Vector3(0, 0.3f, StartZ);
        car.transform.rotation = Quaternion.identity;

        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.localScale = Vector3.one;
            cam.transform.position = new Vector3(0, 4, StartZ - 9);
            cam.transform.rotation = Quaternion.Euler(15, 0, 0);
            var followCamera = cam.GetComponent<FollowCamera>();
            if (followCamera != null) Object.DestroyImmediate(followCamera);
        }
    }

    static void BuildAirRings(Transform arena)
    {
        Vector3[] positions =
        {
            new Vector3(0, 5, -60),
            new Vector3(0, 8, -38),
            new Vector3(12, 10, -12),
            new Vector3(-12, 10, 18),
            new Vector3(0, 7, 50)
        };
        var ringMaterial = ColorMat("AirRing", new Color(1f, 0.72f, 0.12f));
        const int segments = 24;
        const float radius = 3.2f;

        for (int ringIndex = 0; ringIndex < positions.Length; ringIndex++)
        {
            var ring = new GameObject("AirRing_" + (ringIndex + 1));
            ring.transform.SetParent(arena, false);
            ring.transform.localPosition = positions[ringIndex];

            var trigger = ring.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.5f;
            ring.AddComponent<AirRing>();

            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment * Mathf.PI * 2f / segments;
                var bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bead.name = "RingSegment";
                bead.transform.SetParent(ring.transform, false);
                bead.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                bead.transform.localScale = Vector3.one * 0.9f;
                Object.DestroyImmediate(bead.GetComponent<Collider>());
                bead.GetComponent<Renderer>().sharedMaterial = ringMaterial;
            }
        }
    }

    // Пол, 4 стены и потолок
    static void BuildRoom(Transform arena)
    {
        float tile = 20f; // размер одного «повтора» текстуры, м

        var wallNS = TexMat("RoomWall_NS", "room_wallpaper", new Vector2(RoomW / tile, RoomH / tile), Color.white);
        var wallEW = TexMat("RoomWall_EW", "room_wallpaper", new Vector2(RoomL / tile, RoomH / tile), Color.white);
        var floorMat = TexMat("RoomFloor", "room_floor", new Vector2(RoomW / tile, RoomL / tile), Color.white);
        var ceilMat = ColorMat("RoomCeiling", new Color(0.96f, 0.93f, 0.86f));

        // Пол: плоскость Plane (с коллайдером) на всю комнату
        var plane = GameObject.Find("Plane");
        if (plane != null)
        {
            plane.transform.position = Vector3.zero;
            plane.transform.rotation = Quaternion.identity;
            plane.transform.localScale = new Vector3(RoomW / 10f, 1, RoomL / 10f);
            var r = plane.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = floorMat;
        }

        float hx = RoomW * 0.5f, hz = RoomL * 0.5f;
        RoomBox(arena, "Wall_N", new Vector3(0, RoomH * 0.5f, hz + WallT * 0.5f), new Vector3(RoomW + 2 * WallT, RoomH, WallT), wallNS, false);
        RoomBox(arena, "Wall_S", new Vector3(0, RoomH * 0.5f, -hz - WallT * 0.5f), new Vector3(RoomW + 2 * WallT, RoomH, WallT), wallNS, false);
        RoomBox(arena, "Wall_E", new Vector3(hx + WallT * 0.5f, RoomH * 0.5f, 0), new Vector3(WallT, RoomH, RoomL), wallEW, false);
        RoomBox(arena, "Wall_W", new Vector3(-hx - WallT * 0.5f, RoomH * 0.5f, 0), new Vector3(WallT, RoomH, RoomL), wallEW, false);

        // Потолок не отбрасывает тень, иначе комната будет тёмной
        RoomBox(arena, "Ceiling", new Vector3(0, RoomH + WallT * 0.5f, 0), new Vector3(RoomW + 2 * WallT, WallT, RoomL + 2 * WallT), ceilMat, false);
    }

    static GameObject RoomBox(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool castShadow)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = castShadow ? UnityEngine.Rendering.ShadowCastingMode.On
                                         : UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    // Ставит предмет на стену. along: координата вдоль стены (X для северной/южной, Z для восточной/западной)
    static void PlaceOnWall(GameObject root, Transform parent, int wall, float along, float y)
    {
        if (root == null) return;
        root.transform.SetParent(parent, false);

        float inset = 0.3f;
        float hx = RoomW * 0.5f, hz = RoomL * 0.5f;
        Vector3 pos;
        float yaw;
        switch (wall)
        {
            case 0: pos = new Vector3(along, y, hz - inset); yaw = 0f; break;
            case 1: pos = new Vector3(along, y, -hz + inset); yaw = 180f; break;
            case 2: pos = new Vector3(hx - inset, y, along); yaw = 90f; break;
            default: pos = new Vector3(-hx + inset, y, along); yaw = -90f; break;
        }
        if (FlipWallDecor) yaw += 180f;

        root.transform.localPosition = pos;
        root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
    }

    // ---------- Вспомогательные функции ----------

    // Модель, отмасштабированная до size (по высоте или по самой длинной стороне),
    // центр по X/Z в нуле, низ на y = 0. Возвращает объект-обёртку.
    static GameObject MakeProp(string modelName, string matName, float size, bool byHeight, string hint, bool collider)
    {
        var model = FindModel(modelName, hint);
        if (model == null) { if (!missing.Contains(modelName)) missing.Add(modelName); return null; }

        var root = new GameObject(modelName);
        var inst = Spawn(model, root.transform);
        Paint(inst, matName);

        Bounds b;
        TryBounds(root, out b);
        float cur = byHeight ? b.size.y : Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (cur > 0.0001f) inst.transform.localScale *= size / cur;
        TryBounds(root, out b);
        inst.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
        TryBounds(root, out b);

        if (collider)
        {
            var bc = root.AddComponent<BoxCollider>();
            bc.center = b.center;
            bc.size = b.size;
        }
        return root;
    }

    static void Place(GameObject root, Transform parent, float x, float z, float yaw, float y = 0f)
    {
        if (root == null) return;
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(x, y, z);
        root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        MakePhysical(root);
    }

    // Все предметы получают Rigidbody: их можно толкать машинкой.
    // Масса зависит от объёма (в пределах 3..600 кг, машинка весит 1500 кг).
    static void MakePhysical(GameObject root)
    {
        var bc = root.GetComponent<BoxCollider>();
        if (bc == null || root.GetComponent<Rigidbody>() != null) return;

        float volume = bc.size.x * bc.size.y * bc.size.z;
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = Mathf.Clamp(volume * 8f, 3f, 600f);
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.4f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    static void Border(Transform parent, string name, Vector3 pos, Vector3 size)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        var bc = g.AddComponent<BoxCollider>();
        bc.size = size;
    }

    static GameObject Spawn(GameObject model, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        return go;
    }

    static bool TryBounds(GameObject go, out Bounds b)
    {
        b = new Bounds(go.transform.position, Vector3.zero);
        bool has = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        return has;
    }

    static GameObject FindModel(string name, string folderHint)
    {
        string found = null;
        foreach (var g in AssetDatabase.FindAssets(name + " t:Model"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) != name) continue;
            if (folderHint != null && !p.Replace('\\', '/').Contains(folderHint)) continue;
            found = p;
            break;
        }
        if (found == null && folderHint != null) return FindModel(name, null);
        return found == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(found);
    }

    static Material FindMaterial(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Material"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == name)
                return AssetDatabase.LoadAssetAtPath<Material>(p);
        }
        return null;
    }

    // Назначает материал из пакета; если его шейдер не работает, делает запасной URP/Lit с той же текстурой
    static void Paint(GameObject go, string matName)
    {
        if (matName == null) return;
        var m = FindMaterial(matName);
        if (m == null) return;

        bool bad = m.shader == null || m.shader.name.Contains("InternalError");
        if (bad)
        {
            EnsureGenDir();
            string path = GenDir + "/" + matName + "_fallback.mat";
            var fm = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (fm == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                fm = new Material(sh);
                if (m.HasProperty("_MainTex") && m.mainTexture != null)
                {
                    fm.mainTexture = m.mainTexture;
                    if (fm.HasProperty("_BaseMap")) fm.SetTexture("_BaseMap", m.mainTexture);
                }
                AssetDatabase.CreateAsset(fm, path);
            }
            m = fm;
        }

        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < arr.Length; i++) arr[i] = m;
            r.sharedMaterials = arr;
        }
    }

    static Texture2D FindTexture(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == name)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        missing.Add("текстура " + name);
        return null;
    }

    // Материал URP/Lit с текстурой и повторением (tiling)
    static Material TexMat(string name, string texName, Vector2 tiling, Color tint)
    {
        EnsureGenDir();
        string path = GenDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        var tex = FindTexture(texName);
        if (tex != null)
        {
            if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", tiling); }
            if (m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", tex); m.SetTextureScale("_MainTex", tiling); }
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material ColorMat(string name, Color c)
    {
        EnsureGenDir();
        string path = GenDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureGenDir()
    {
        if (!AssetDatabase.IsValidFolder(GenDir))
            AssetDatabase.CreateFolder("Assets", "DemoGenerated");
    }
}
