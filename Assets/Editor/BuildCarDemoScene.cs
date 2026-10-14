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

        // Машинка в начале арены, смотрит вдоль +Z
        car.transform.position = new Vector3(0, 0.3f, 0);
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

    // ---------- Арена ----------

    static void BuildArena(GameObject car)
    {
        var oldArena = GameObject.Find("Arena");
        if (oldArena != null) Object.DestroyImmediate(oldArena);
        var arena = new GameObject("Arena").transform;

        // Земля 100 x 100 м
        var plane = GameObject.Find("Plane");
        if (plane != null)
        {
            plane.transform.position = Vector3.zero;
            plane.transform.rotation = Quaternion.identity;
            plane.transform.localScale = new Vector3(10, 1, 10);
            var r = plane.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = ColorMat("DemoGround", new Color(0.62f, 0.68f, 0.55f));
        }

        // Невидимые борта, чтобы машинка не уехала за край
        Border(arena, "Border_N", new Vector3(0, 2, 50), new Vector3(100, 4, 1));
        Border(arena, "Border_S", new Vector3(0, 2, -50), new Vector3(100, 4, 1));
        Border(arena, "Border_E", new Vector3(50, 2, 0), new Vector3(1, 4, 100));
        Border(arena, "Border_W", new Vector3(-50, 2, 0), new Vector3(1, 4, 100));

        // Трамплин
        var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.name = "Ramp";
        ramp.transform.SetParent(arena, false);
        ramp.transform.localScale = new Vector3(6, 0.6f, 10);
        ramp.transform.localPosition = new Vector3(0, 0.9f, 14);
        ramp.transform.localRotation = Quaternion.Euler(-15, 0, 0);
        ramp.GetComponent<Renderer>().sharedMaterial = ColorMat("DemoRamp", new Color(0.95f, 0.55f, 0.15f));

        // Большие предметы по краям арены
        Place(MakeProp("worktable", "worktable", 16, false, null, true), arena, -36, 10, 90);
        Place(MakeProp("stack_shelf", "stack_shelf", 10, false, null, true), arena, 38, 8, -90);
        Place(MakeProp("shelf_wide", "stack_shelf", 14, false, null, true), arena, 38, 34, -90);
        Place(MakeProp("bedside_drawer", "bedside_drawer", 8, false, null, true), arena, -36, 36, 90);
        Place(MakeProp("low_shelf", "bedside_drawer", 6, false, "/Prop/", true), arena, -38, -20, 90);
        Place(MakeProp("tv set", "prop_1", 6, false, "/Prop/", true), arena, -22, 46, 180);

        // Мелкие предметы возле старта
        Place(MakeProp("glue", "prop_1", 3, false, "/Prop/", true), arena, -8, -6, 20);
        Place(MakeProp("tape", "prop_1", 2.5f, false, "/Prop/", true), arena, 8, -8, -15);
        Place(MakeProp("scissors", "prop_1", 4, false, "/Prop/", true), arena, -14, -14, 40);
        Place(MakeProp("spray", "prop_1", 4, false, "/Prop/", true), arena, 14, -12, 0);
        Place(MakeProp("lamp", "prop_1", 7, true, "/Prop/", true), arena, -24, -4, 0);

        // Препятствия на трассе
        Place(MakeProp("crossing_sticks", "wings", 6, false, null, true), arena, -7, 22, 20);
        Place(MakeProp("crossing_sticks", "wings", 6, false, null, true), arena, 7, 27, -20);
        Place(MakeProp("book_1", "prop_2", 3.5f, false, "/Prop/", true), arena, -12, 12, 15);
        Place(MakeProp("book_2", "prop_2", 3.5f, false, "/Prop/", true), arena, 12, 20, -30);
        Place(MakeProp("book_3", "prop_3", 3.5f, false, "/Prop/", true), arena, -14, 30, 10);
        Place(MakeProp("book_4", "prop_3", 3.5f, false, "/Prop/", true), arena, 13, 32, -10);
        Place(MakeProp("cactus", "prop_3", 4.5f, true, "/Prop/", true), arena, -20, 42, 0);
        Place(MakeProp("cactus", "prop_3", 4.5f, true, "/Prop/", true), arena, 20, 40, 0);

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
            Place(cup, arena, cupPos[i, 0], 36, 0, cupPos[i, 1]);
            var rb = cup.GetComponent<Rigidbody>();
            if (rb != null) rb.mass = 3f;
        }

        // Финишная лента (без коллайдера и без физики: просто декорация, через неё можно проехать)
        Place(MakeProp("finish_tape", "prop_1", 18, false, null, false), arena, 0, 44, 0, 0.5f);

        // Камера
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.localScale = Vector3.one;
            cam.transform.position = new Vector3(0, 4, -9);
            cam.transform.rotation = Quaternion.Euler(15, 0, 0);
            var t = Type.GetType("FollowCamera, Assembly-CSharp");
            if (t != null)
            {
                var c = cam.GetComponent(t);
                if (c == null) c = cam.gameObject.AddComponent(t);
                var f = t.GetField("target");
                if (f != null) f.SetValue(c, car.transform);
            }
        }
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
