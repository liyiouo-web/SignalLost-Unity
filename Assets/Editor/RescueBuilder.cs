using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
namespace SignalLost.Editor
{
    public static class RescueBuilder
    {
        static Material sea, foam, dark, orange, white, rock;
        static Material Mat(string name, Color c)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = c;
            m.SetFloat("_Glossiness", .22f);
            AssetDatabase.CreateAsset(m, "Assets/Art/" + name + ".mat");
            return m;
        }
        static GameObject Part(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.GetComponent < Renderer > ().sharedMaterial = mat;
            UnityEngine.Object.DestroyImmediate(g.GetComponent < Collider > ());
            return g;
        }
        static AnimationClip Clip(string name, string path, string property, float a, float b, float duration = 1)
        {
            var c = new AnimationClip
            {
                name = name
            };
            c.SetCurve(path, typeof(Transform), property, new AnimationCurve(new Keyframe(0, a), new Keyframe(duration * .5f, b), new Keyframe(duration, a)));
            AssetDatabase.CreateAsset(c, "Assets/Animations/" + name + ".anim");
            return c;
        }
        // Speed 控制航行，Turn 控制转向，触发器控制救援和碰撞。
        static RuntimeAnimatorController Controller()
        {
            var c = AnimatorController.CreateAnimatorControllerAtPath("Assets/Animations/RescueBoat.controller");
            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            c.AddParameter("Turn", AnimatorControllerParameterType.Float);
            c.AddParameter("Rescue", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            var root = c.layers[0].stateMachine;
            var idle = Clip("Idle", "Hull", "localPosition.y", 0, .025f);
            var move = Clip("Underway", "Hull", "localEulerAnglesRaw.x", 0, 4);
            var left = Clip("BankLeft", "Hull", "localEulerAnglesRaw.z", 0, 10);
            var right = Clip("BankRight", "Hull", "localEulerAnglesRaw.z", 0, - 10);
            var tree = new BlendTree
            {
                name = "SpeedBlend", blendParameter = "Speed", useAutomaticThresholds = false
            };
            tree.AddChild(idle, 0);
            tree.AddChild(move, 1);
            AssetDatabase.AddObjectToAsset(tree, c);
            var sailing = root.AddState("Sailing");
            sailing.motion = tree;
            root.defaultState = sailing;
            var l = root.AddState("TurningLeft");
            l.motion = left;
            var r = root.AddState("TurningRight");
            r.motion = right;
            foreach (var s in new[]
            {
                l, r
            }
            )
            {
                var t = sailing.AddTransition(s);
                t.hasExitTime = false;
                t.duration = .12f;
                t.AddCondition(s == l ? AnimatorConditionMode.Less : AnimatorConditionMode.Greater, s == l ? - .1f : .1f, "Turn");
                var back = s.AddTransition(sailing);
                back.hasExitTime = false;
                back.duration = .12f;
                back.AddCondition(s == l ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, s == l ? - .1f : .1f, "Turn");
            }
            var rescue = root.AddState("WinchRescue");
            rescue.motion = Clip("WinchLift", "Hull/Winch", "localPosition.y", .35f, 1.0f, 1.2f);
            var hit = root.AddState("ReefImpact");
            hit.motion = Clip("Impact", "Hull", "localEulerAnglesRaw.z", 0, 18, .4f);
            foreach (var s in new[]
            {
                rescue, hit
            }
            )
            {
                var t = root.AddAnyStateTransition(s);
                t.hasExitTime = false;
                t.canTransitionToSelf = false;
                t.duration = .08f;
                t.AddCondition(AnimatorConditionMode.If, 0, s == rescue ? "Rescue" : "Hit");
                var back = s.AddTransition(sailing);
                back.hasExitTime = true;
                back.exitTime = 1;
                back.duration = .1f;
            }
            return c;
        }
        // 构建时会重新生成场景。
        [MenuItem("Signal Lost/Build prototype")] public static void Build()
        {
            foreach (var d in new[]
            {
                "Assets/Art", "Assets/Scenes", "Assets/Animations"
            }
            )Directory.CreateDirectory(d);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            sea = Mat("Atlantic", new Color(.075f, .29f, .36f));
            foam = Mat("SeaGrid", new Color(.18f, .42f, .46f));
            dark = Mat("Ink", new Color(.055f, .12f, .16f));
            orange = Mat("SignalOrange", new Color(1, .51f, .19f));
            white = Mat("Ivory", new Color(.92f, .94f, .84f));
            rock = Mat("Slate", new Color(.27f, .32f, .32f));
            Part("Ocean", PrimitiveType.Cube, new Vector3(0, - .3f, 0), new Vector3(10.8f, .5f, 9.8f), sea);
            var jam = Mat("Interference", new Color(.29f, .31f, .22f));
            for (int x = 0; x < 10; x++)for (int z = 0; z < 9; z++)
            {
                var p = new Vector2Int(x, z);
                var w = RescueGame.World(p);
                w.y = - .035f;
                Part("Water_" + x + "_" + z, PrimitiveType.Cube, w, new Vector3(.97f, .035f, .97f), sea);
            }
            for (int x = 0; x <= 10; x++)Part("ChartMeridian", PrimitiveType.Cube, new Vector3(x - 5, - .005f, 0), new Vector3(.012f, .009f, 9), foam);
            for (int z = 0; z <= 9; z++)Part("ChartParallel", PrimitiveType.Cube, new Vector3(0, - .005f, z - 4.5f), new Vector3(10, .009f, .012f), foam);
            foreach (var p in RescueRules.Rocks)
            {
                var g = Part("Reef", PrimitiveType.Cube, RescueGame.World(p) + new Vector3(0, .05f, 0), new Vector3(.72f, .62f, .72f), rock);
                g.transform.rotation = Quaternion.Euler(8, p.x * 17, 12);
                Part("Foam", PrimitiveType.Cylinder, RescueGame.World(p) + new Vector3(0, - .2f, 0), new Vector3(.99f, .01f, .99f), white);
            }
            var harbour = RescueGame.World(RescueRules.Port);
            harbour.y = .015f;
            Part("Harbour", PrimitiveType.Cube, harbour, new Vector3(.98f, .05f, .98f), white);
            for (int i = 0; i < 4; i++)Part("PortStripe", PrimitiveType.Cube, harbour + new Vector3( - .35f + i * .23f, .03f, 0), new Vector3(.09f, .02f, .95f), orange);
            var lightRoot = new GameObject("Lighthouse").transform;
            lightRoot.position = new Vector3( - 5.3f, 0, - 3.5f);
            Part("Island", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.5f, .15f, 1.5f), rock, lightRoot);
            Part("Tower", PrimitiveType.Cylinder, new Vector3(0, .8f, 0), new Vector3(.5f, .8f, .5f), white, lightRoot);
            Part("Stripe", PrimitiveType.Cylinder, new Vector3(0, 1, 0), new Vector3(.51f, .15f, .51f), orange, lightRoot);
            Part("Lantern", PrimitiveType.Cylinder, new Vector3(0, 1.7f, 0), new Vector3(.67f, .12f, .67f), orange, lightRoot);
            var beam = new GameObject("RotatingLantern").transform;
            beam.SetParent(lightRoot, false);
            beam.localPosition = new Vector3(0, 1.7f, 0);
            Part("LampArm", PrimitiveType.Cube, new Vector3(0, 0, .5f), new Vector3(.08f, .05f, 1), white, beam);
            // 根节点负责移动，Hull 子节点负责表现动画。
            var boat = new GameObject("RescueBoat").transform;
            boat.position = RescueGame.World(RescueRules.Port);
            var hull = new GameObject("Hull").transform;
            hull.SetParent(boat, false);
            Part("Flotation", PrimitiveType.Capsule, Vector3.zero, new Vector3(.42f, .2f, .67f), orange, hull).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Part("Deck", PrimitiveType.Cube, new Vector3(0, .09f, 0), new Vector3(.35f, .12f, .65f), white, hull);
            Part("Cabin", PrimitiveType.Cube, new Vector3(0, .22f, .02f), new Vector3(.28f, .19f, .28f), white, hull);
            Part("Window", PrimitiveType.Cube, new Vector3(0, .23f, .17f), new Vector3(.23f, .10f, .035f), dark, hull);
            Part("Bow", PrimitiveType.Sphere, new Vector3(0, .11f, .36f), new Vector3(.27f, .13f, .3f), orange, hull);
            Part("Winch", PrimitiveType.Cube, new Vector3(.24f, .35f, - .16f), new Vector3(.035f, .23f, .035f), dark, hull);
            var animator = boat.gameObject.AddComponent < Animator > ();
            animator.runtimeAnimatorController = Controller();
            var people = new GameObject[2];
            for (int i = 0; i < 2; i++)
            {
                var root = new GameObject("Survivor_" + (i + 1)).transform;
                root.position = RescueGame.World(RescueRules.People[i]);
                Part("Lifebuoy", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.62f, .07f, .62f), orange, root);
                Part("Head", PrimitiveType.Sphere, new Vector3(0, .13f, 0), Vector3.one * .22f, white, root);
                Part("Beacon", PrimitiveType.Cylinder, new Vector3(.21f, .35f, 0), new Vector3(.025f, .36f, .025f), white, root);
                Part("Flag", PrimitiveType.Cube, new Vector3(.32f, .64f, 0), new Vector3(.26f, .16f, .025f), orange, root);
                people[i] = root.gameObject;
            }
            var systems = new GameObject("RescueSystems");
            var game = systems.AddComponent < RescueGame > ();
            game.boat = boat;
            game.beam = beam;
            game.animator = animator;
            game.survivors = people;
            systems.AddComponent < RescueUI > ().game = game;
            // 背景相机清屏，避免视口变化留下残影。
            var backdrop = new GameObject("Backdrop Camera").AddComponent < Camera > ();
            backdrop.depth = - 10;
            backdrop.cullingMask = 0;
            backdrop.clearFlags = CameraClearFlags.SolidColor;
            backdrop.backgroundColor = new Color(.055f, .115f, .14f);
            var camera = new GameObject("Main Camera").AddComponent < Camera > ();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(2, 13, - 11);
            camera.transform.LookAt(new Vector3(.8f, 0, 0));
            camera.orthographic = true;
            camera.orthographicSize = 6.7f;
            camera.backgroundColor = new Color(.045f, .10f, .13f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.gameObject.AddComponent < AudioListener > ();
            var sun = new GameObject("Sun").AddComponent < Light > ();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(45, - 30, 0);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientLight = new Color(.55f, .65f, .67f);
            QualitySettings.antiAliasing = 4;
            QualitySettings.vSyncCount = 0;
            PlayerSettings.companyName = "CoastSignalStudio";
            PlayerSettings.productName = "Signal Lost";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/Rescue.unity");
            AssetDatabase.SaveAssets();
            Tests();
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../SignalLost-Minimal"));
            Directory.CreateDirectory(folder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[]
                {
                    "Assets/Scenes/Rescue.unity"
                }
                , locationPathName = Path.Combine(folder, "SignalLost.exe"), target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            }
            );
            if (report.summary.result != BuildResult.Succeeded)throw new Exception("Build failed");
            Debug.Log("SIGNAL_BUILD_PASS");
        }
        // 检查概率边界与确认频道的准确性。
        static void Tests()
        {
            int n = 0;
            foreach (Command c in Enum.GetValues(typeof(Command)))foreach (double roll in new[]
            {
                0d, .499999d, .5d, .999999d
            }
            )foreach (bool reliable in new[]
            {
                false, true
            }
            )
            {
                var got = RescueRules.Decode(c, roll, reliable);
                bool inverted = !reliable && roll < .5 && (c == Command.Left || c == Command.Right);
                var expected = inverted ? (c == Command.Left ? Command.Right : Command.Left) : c;
                if (got != expected)throw new Exception("Decode boundary " + c + " " + roll + " " + reliable);
                n++;
            }
            var rng = new System.Random(1);
            bool normal = false, inverse = false;
            for (int i = 0; i < 30; i++)
            {
                var c = RescueRules.Decode(Command.Left, rng.NextDouble(), false);
                normal |= c == Command.Left;
                inverse |= c == Command.Right;
            }
            if (!normal || !inverse)throw new Exception("Seeded sequence lacks both outcomes");
            if (!RescueRules.Blocked(new Vector2Int( - 1, 0)) || RescueRules.Blocked(RescueRules.Port))throw new Exception("Bounds");
            File.WriteAllText("rule-tests.txt", "PASS " + n + " random-channel boundary cases; seeded mixed outcomes; bounds.");
            Debug.Log("SIGNAL_RULES_PASS");
        }
    }
}
