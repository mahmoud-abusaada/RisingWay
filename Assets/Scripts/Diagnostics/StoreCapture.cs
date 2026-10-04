// Renders store screenshots from real gameplay: the game plays itself (auto-pilot) and the main
// camera is rendered to a high-resolution texture at chosen moments. Editor batch mode, the same
// way as MovementProbe:
//
//   Unity.exe -batchmode -projectPath <project> -executeMethod MovementProbeRunner.Run
//             -storeCapture <output folder> -shotName run1 -shotAt 4,9,15 [options] -logFile <log>
//
// Options:
//   -shotBall <id>        ball to play with (MaterialsManager ids; 53 Earth, 63 Saturn, 67 Bright Star...)
//   -shotFloor <id>       floor (colour floors 1-12, pattern floors 101+)
//   -shotSize <w>x<h>     output size, default 1440x2560 (9:16)
//   -shotBoltAt <s>       switch a bolt on at this many seconds into the run
//   -shotDoubleAt <s>     switch double points on
//   -shotPatterns         every path pattern from the start (spirals, climbs)
//   -shotUI               keep the in-game UI (score) in the picture
//   -shotLook <pitch>,<yaw>  point the camera that way for the shot (to look at the sky)
//   -shotKeepBursts       keep diamond-pickup bursts (cleared by default: frozen in a still
//                         picture they read as loose purple chips around the ball)
//   -shotMenu             a picture of the main menu first
//   -shotMenus Upgrade,Settings  then each of these menus (Menus enum names, "Menu" optional)
//   -shotPowerUps <n>     own n of each power-up (the in-game buttons show their counts)
//   -shotUpgrades 5,2,1   the upgrade levels (double points, bolt, chance), for the menu shots
//   -shotDiamonds <n>     the diamonds owned, for the menu shots
//   -shotDump             each menu's objects as they are at run time, in the log
//   -shotScrollEnd        each menu also scrolled to its end
//   -shotUpgradeTap Bolt  buy that upgrade on the Upgrade menu and shoot its animation
//   -shotVideo <s>        a silent clip of the run instead of pictures (VideoRecorder): frames in
//                         <folder>/<shotName>/; -videoFps 30, -videoFrom <s> (start that many game
//                         seconds into the run; a run plays at timeScale 1.5), -videoBalls 53,63,73 -videoBallEvery 2 (change ball as it goes)
//   -shotMenuVideo <s>    a clip of the main menu first (<shotName>_menu)
//
// The menus are a canvas this camera draws, laid out for the screen it renders to: each shot
// renders to its texture for a few frames first, so the UI is laid out for the picture's size.
//
// Times are game seconds after the ball starts rolling. Writes <folder>/<shotName>_<t>.png, then
// quits. The player's own selections are put back afterwards (the Editor's save file).
//
// Deliberately excluded from release builds.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class StoreCapture : MonoBehaviour
{
    private const string TAG = "[SC] ";

    private string folder, shotName;
    private int width = 1440, height = 2560;
    private float[] times;
    private float boltAt = -1f, doubleAt = -1f;
    private bool keepUI, keepBursts;
    private static readonly System.Reflection.FieldInfo DiamondEffectField =
        typeof(DiamondPickUp).GetField("diamondEffect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    private static string Arg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
            if (args[i] == name)
                return i + 1 < args.Length ? args[i + 1] : "";
        return null;
    }

    private static float F(string s)
    {
        return float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Arg("-storeCapture") == null)
            return;
        GameObject go = new GameObject("~StoreCapture");
        DontDestroyOnLoad(go);
        go.AddComponent<StoreCapture>();
    }

    private IEnumerator Start()
    {
        folder = Arg("-storeCapture");
        shotName = Arg("-shotName") ?? "shot";
        times = (Arg("-shotAt") ?? "5").Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        string size = Arg("-shotSize");
        if (!string.IsNullOrEmpty(size))
        {
            string[] wh = size.Split('x');
            width = int.Parse(wh[0]);
            height = int.Parse(wh[1]);
        }
        if (Arg("-shotBoltAt") != null) boltAt = float.Parse(Arg("-shotBoltAt"), System.Globalization.CultureInfo.InvariantCulture);
        if (Arg("-shotDoubleAt") != null) doubleAt = float.Parse(Arg("-shotDoubleAt"), System.Globalization.CultureInfo.InvariantCulture);
        keepUI = Arg("-shotUI") != null;
        keepBursts = Arg("-shotKeepBursts") != null;
        PathMaker.PreviewAllPatterns = Arg("-shotPatterns") != null;
        Directory.CreateDirectory(folder);
        Time.captureDeltaTime = 1f / (Arg("-videoFps") != null ? F(Arg("-videoFps")) : 60f);

        PlayerStats stats = PlayerStats.Instance;
        bool autoPilot = stats.isAutoPilotOn(), tutorials = stats.isTutorialsOn();
        int ballBefore = stats.getSelectedBallId(), floorBefore = stats.getSelectedFloorId();
        stats.setAutoPilotState(true);
        stats.setTutorialsState(false);

        // Let the scene settle; answer the Editor's placeholder consent form (see MovementProbe).
        for (int i = 0; i < 90; i++)
        {
            foreach (UnityEngine.UI.Button b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
                if (b.transform.root.name.StartsWith("ConsentForm")) { b.onClick.Invoke(); break; }
            yield return null;
        }

        MaterialsManager materials = FindAnyObjectByType<MaterialsManager>();
        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        int ball, floor;
        if (int.TryParse(Arg("-shotBall") ?? "", out ball)) { materials.setSelectedBallMaterial(ball); player.setPlayerMaterial(); }
#if UNITY_EDITOR
        // -shotBallMaterial <asset path>: a ball that is not in the list yet (a new one to try).
        if (Arg("-shotBallMaterial") != null)
        {
            Material m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(Arg("-shotBallMaterial"));
            if (m == null)
                Debug.LogWarning(TAG + "no material at " + Arg("-shotBallMaterial"));
            else
            {
                player.GetComponent<MeshRenderer>().material = m;
                player.GetComponent<Moons>().setMoons();
                player.GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects(false);
                player.GetComponent<Outline>().enabled = !MaterialsManager.isSolarBall(m);
            }
        }
#endif
        if (int.TryParse(Arg("-shotFloor") ?? "", out floor)) { materials.setSelectedFloorMaterial(floor); FindAnyObjectByType<PartsPool>().refreshMaterials(); }

        // -shotMenu: one picture of the main menu's ball first (without the UI, as always).
        if (Arg("-shotMenu") != null)
        {
            for (int i = 0; i < 60; i++)
                yield return null;
            if (Arg("-shotUpgrades") != null)
            {
                int[] l = Arg("-shotUpgrades").Split(',').Select(int.Parse).ToArray();
                stats.setDoublePointsLevel(l[0]);
                stats.setBoltLevel(l[1]);
                stats.setChanceLevel(l[2]);
            }
            if (Arg("-shotDiamonds") != null)
            {
                stats.subtractDiamonds(stats.getDiamondsCount());
                stats.addDiamonds(int.Parse(Arg("-shotDiamonds")));
            }
            // -shotDumpNames A,B: those canvases' objects in the log too (the dialogs).
            if (Arg("-shotDumpNames") != null)
                foreach (string n in Arg("-shotDumpNames").Split(','))
                    Dump(null, n);
            GameObject update = GameObject.Find("UpdateDialog");
            if (update != null)
                update.SetActive(false); // the Editor's version check thinks it is out of date
            yield return Capture(shotName + "_menu");
            if (Arg("-shotMenus") != null)
            {
                MenusController menus = FindAnyObjectByType<MenusController>();
                foreach (string name in Arg("-shotMenus").Split(','))
                {
                    string full = name.EndsWith("Menu") ? name : name + "Menu";
                    menus.hideStackMenus();
                    menus.showAndAddMenuToStack((Menus)Enum.Parse(typeof(Menus), full));
                    for (int i = 0; i < 70; i++)
                        yield return null;
                    yield return Capture(shotName + "_" + full);
                    // -shotScrollEnd: the same menu scrolled to its end too (the shop's last balls).
                    if (Arg("-shotScrollEnd") != null)
                    {
                        // (the shop adds items as the list nears its end: keep going)
                        for (int round = 0; round < 12; round++)
                        {
                            foreach (UnityEngine.UI.ScrollRect sr in FindObjectsByType<UnityEngine.UI.ScrollRect>(FindObjectsSortMode.None))
                                sr.verticalNormalizedPosition = 0;
                            for (int i = 0; i < 20; i++)
                                yield return null;
                        }
                        yield return Capture(shotName + "_" + full + "_end");
                    }
                    if (Arg("-shotDump") != null)
                        Dump(menus.transform.root, full);
                    // -shotPurchaseJump Bolts: a tap on a counter, and the list where it goes.
                    if (full == "PurchaseMenu" && Arg("-shotPurchaseJump") != null)
                    {
                        FindAnyObjectByType<PurchaseJump>().To(Arg("-shotPurchaseJump"));
                        float until = Time.realtimeSinceStartup + 0.8f;
                        while (Time.realtimeSinceStartup < until)
                            yield return null;
                        yield return Capture(shotName + "_" + full + "_jump");
                    }
                    // -shotShopSteps own:73,close,open,ball:73,ball:5,floors,wait:40,shot:a ...
                    if (full == "ShopMenu" && Arg("-shotShopSteps") != null)
                        yield return ShopSteps(menus, materials, stats, Arg("-shotShopSteps"));
                    // -shotBox: open a mystery box in the shop and shoot the reveal as it goes.
                    if (full == "ShopMenu" && Arg("-shotBox") != null)
                    {
                        FindAnyObjectByType<ShopMenu>().MysteryBoxClick();
                        // (the reveal runs on real time)
                        float started = Time.realtimeSinceStartup;
                        foreach (float t in new[] { 0.5f, 3f, 5f, 6.5f, 8f, 10f })
                        {
                            while (Time.realtimeSinceStartup - started < t)
                                yield return null;
                            yield return Capture(shotName + "_box_" + t.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
                        }
                    }
                    // -shotUpgradeTap Bolt: buy that upgrade and catch its animation on the way.
                    if (full == "UpgradeMenu" && Arg("-shotUpgradeTap") != null)
                    {
                        FindAnyObjectByType<UpgradeMenu>().SendMessage("Upgrade" + Arg("-shotUpgradeTap"));
                        for (int i = 0; i < 9; i++)
                            yield return null;
                        yield return Capture(shotName + "_" + full + "_tap1");
                        for (int i = 0; i < 12; i++)
                            yield return null;
                        yield return Capture(shotName + "_" + full + "_tap2");
                    }
                    menus.hideStackMenus();
                    menus.showAndAddMenuToStack(Menus.MainMenu);
                    for (int i = 0; i < 20; i++)
                        yield return null;
                }
            }
        }
        // -shotMenuVideo <seconds>: a clip of the main menu (its ball turning, the UI over it),
        // then the run as usual.
        if (Arg("-shotMenuVideo") != null)
        {
            GameObject update = GameObject.Find("UpdateDialog");
            if (update != null)
                update.SetActive(false);
            if (Arg("-shotDiamonds") != null)
            {
                stats.subtractDiamonds(stats.getDiamondsCount());
                stats.addDiamonds(int.Parse(Arg("-shotDiamonds")));
                FindAnyObjectByType<MainMenuSkin>()?.Refresh();
            }
            for (int i = 0; i < 60; i++)
                yield return null;
            VideoRecorder rec = gameObject.AddComponent<VideoRecorder>();
            rec.Begin(folder, shotName + "_menu", width, height, true);
            int total = Mathf.RoundToInt(F(Arg("-shotMenuVideo")) / Time.captureDeltaTime);
            while (rec.Frames < total)
                yield return null;
            rec.End();
            Destroy(rec);
            if (Arg("-shotVideo") == null)
            {
                Restore(materials, stats, ballBefore, floorBefore, autoPilot, tutorials);
                yield break;
            }
        }
        if (Arg("-shotPowerUps") != null)
        {
            int n = int.Parse(Arg("-shotPowerUps"));
            stats.addBolts(n - stats.getBoltsCount());
            stats.addDoublePoints(n - stats.getDoublePointsCount());
            stats.addChances(n - stats.getChancesCount());
        }
        FindAnyObjectByType<MenusOperations>().StartGame();
        while (!Utility.gameStarted)
            yield return null;
        float start = Time.time;
        Debug.Log(TAG + "run started; shots at " + string.Join(",", times));

        PickUpsManager pickUps = FindAnyObjectByType<PickUpsManager>();
        int next = 0;
        bool boltDone = boltAt < 0, doubleDone = doubleAt < 0;

        // -shotVideo <seconds>: a clip of the run instead of pictures (see the header).
        if (Arg("-shotVideo") != null)
        {
            float seconds = F(Arg("-shotVideo")), from = Arg("-videoFrom") != null ? F(Arg("-videoFrom")) : 0f;
            string[] balls = Arg("-videoBalls") != null ? Arg("-videoBalls").Split(',') : new string[0];
            float every = Arg("-videoBallEvery") != null ? F(Arg("-videoBallEvery")) : 2f;
            VideoRecorder rec = null;
            int total = Mathf.RoundToInt(seconds / Time.captureDeltaTime), ballShown = -1;
            while (rec == null || rec.Frames < total)
            {
                float t = Time.time - start;
                if (!boltDone && t >= boltAt) { boltDone = true; pickUps.activateBolt(); Debug.Log(TAG + "bolt on at " + t.ToString("0.0")); }
                if (!doubleDone && t >= doubleAt) { doubleDone = true; pickUps.activateDoublePoint(); Debug.Log(TAG + "double points on at " + t.ToString("0.0")); }
                if (!Utility.camFollowPlayer)
                {
                    Debug.LogWarning(TAG + "the ball fell at " + t.ToString("0.0") + "s; stopping");
                    break;
                }
                if (rec == null && t >= from)
                {
                    rec = gameObject.AddComponent<VideoRecorder>();
                    rec.Begin(folder, shotName, width, height, keepUI);
                    if (Arg("-videoZoom") != null)
                        rec.Zoom = F(Arg("-videoZoom"));
                }
                // (by the clip's own clock: a run plays at Time.timeScale 1.5, so game time runs ahead of it)
                float clipTime = rec != null ? rec.Frames * Time.captureDeltaTime : 0f;
                int b = balls.Length > 0 ? Mathf.Min((int)(clipTime / every), balls.Length - 1) : -1;
                if (b != ballShown && b >= 0)
                {
                    ballShown = b;
                    materials.setSelectedBallMaterial(int.Parse(balls[b]));
                    player.setPlayerMaterial();
                }
                yield return null;
            }
            if (rec != null)
                rec.End();
            Restore(materials, stats, ballBefore, floorBefore, autoPilot, tutorials);
            yield break;
        }

        while (next < times.Length)
        {
            float t = Time.time - start;
            if (!boltDone && t >= boltAt) { boltDone = true; pickUps.activateBolt(); Debug.Log(TAG + "bolt on at " + t.ToString("0.0")); }
            if (!doubleDone && t >= doubleAt) { doubleDone = true; pickUps.activateDoublePoint(); Debug.Log(TAG + "double points on at " + t.ToString("0.0")); }
            if (!Utility.camFollowPlayer)
            {
                Debug.LogWarning(TAG + "the ball fell at " + t.ToString("0.0") + "s; stopping");
                break;
            }
            if (t >= times[next])
            {
                string at = times[next].ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                // -shotBalls 53,54,...: the same moment with each of these balls in turn (with
                // -shotCloseup, a look at every ball's material in one run).
                if (Arg("-shotBalls") != null)
                    foreach (string id in Arg("-shotBalls").Split(','))
                    {
                        materials.setSelectedBallMaterial(int.Parse(id));
                        player.setPlayerMaterial();
                        yield return null;
                        yield return Capture(shotName + "_" + at + "_ball" + id);
                    }
                else
                    yield return Capture(shotName + "_" + at);
                next++;
            }
            yield return null;
        }

        Restore(materials, stats, ballBefore, floorBefore, autoPilot, tutorials);
    }

    private void Restore(MaterialsManager materials, PlayerStats stats, int ballBefore, int floorBefore, bool autoPilot, bool tutorials)
    {
        materials.setSelectedBallMaterial(ballBefore);
        materials.setSelectedFloorMaterial(floorBefore);
        stats.setAutoPilotState(autoPilot);
        stats.setTutorialsState(tutorials);
        Debug.Log(TAG + "done");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(0);
#endif
    }

    // Drives the shop step by step for a look at a sequence: own:<ball> ownfloor:<floor> (owned,
    // never used: NEW), ball:<id> floor:<id> (select), balls, floors, close, open (the shop again,
    // as the player would), end (scrolled to the end), box (open a mystery box), tap (a press on
    // the reveal), wait:<frames>, shot:<name> (a picture), real:<seconds> (wait in real time).
    private IEnumerator ShopSteps(MenusController menus, MaterialsManager materials, PlayerStats stats, string steps)
    {
        foreach (string step in steps.Split(','))
        {
            string[] kv = step.Split(':');
            string k = kv[0], v = kv.Length > 1 ? kv[1] : "";
            ShopMenu shop = FindAnyObjectByType<ShopMenu>();
            Debug.Log(TAG + "shop step " + step + (shop == null ? " (the shop is not open)" : ""));
            if (shop == null && k != "open" && k != "wait" && k != "real" && k != "shot" && k != "own" && k != "ownfloor")
                continue;
            switch (k)
            {
                case "own": stats.unlockBall(int.Parse(v)); break;
                case "ownfloor": stats.unlockFloor(int.Parse(v)); break;
                case "ball": shop.selectBall(int.Parse(v)); break;
                case "floor": shop.SendMessage("selectFloor", int.Parse(v)); break;
                case "balls": shop.SetBallsList(); break;
                case "floors": shop.SetFloorsList(); break;
                case "close": menus.hideStackMenus(); menus.showAndAddMenuToStack(Menus.MainMenu); break;
                case "open": menus.hideStackMenus(); menus.showAndAddMenuToStack(Menus.ShopMenu); break;
                case "box": shop.MysteryBoxClick(); break;
                case "tap": shop.revealBack(); break;
                case "end":
                    for (int round = 0; round < 12; round++)
                    {
                        foreach (UnityEngine.UI.ScrollRect sr in FindObjectsByType<UnityEngine.UI.ScrollRect>(FindObjectsSortMode.None))
                            sr.verticalNormalizedPosition = 0;
                        for (int i = 0; i < 20; i++)
                            yield return null;
                    }
                    break;
                case "wait":
                    for (int i = 0; i < int.Parse(v); i++)
                        yield return null;
                    break;
                case "real":
                    float until = Time.realtimeSinceStartup + F(v);
                    while (Time.realtimeSinceStartup < until)
                        yield return null;
                    break;
                case "shot": yield return Capture(shotName + "_" + v); break;
            }
            yield return null;
        }
    }

    // -shotDump: each menu's objects in the log, as they are at run time (prefab contents too).
    private static void Dump(Transform unused, string menu)
    {
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == menu && t.GetComponent<Canvas>() != null)
            {
                List<string> lines = new List<string>();
                DumpTree(t, 0, lines);
                foreach (string line in lines)
                    Debug.Log(TAG + "DUMP " + line);
            }
    }

    private static void DumpTree(Transform t, int depth, List<string> lines)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(new string(' ', depth * 2)).Append(t.name).Append(t.gameObject.activeSelf ? "" : " [off]");
        RectTransform r = t as RectTransform;
        if (r != null)
            sb.Append(" size ").Append(r.rect.size).Append(" at ").Append(r.anchoredPosition);
        foreach (Component c in t.GetComponents<Component>())
        {
            if (c is Transform) continue;
            sb.Append(" |").Append(c.GetType().Name);
            if (c is UnityEngine.UI.Image img) sb.Append("(").Append(img.sprite != null ? img.sprite.name : "-").Append(" ").Append(img.color).Append(")");
            if (c is TMPro.TMP_Text tx) sb.Append("('").Append(tx.text.Replace("\n", " ")).Append("' ").Append(tx.fontSize).Append(")");
        }
        lines.Add(sb.ToString());
        foreach (Transform child in t)
            DumpTree(child, depth + 1, lines);
    }

    private IEnumerator Capture(string name)
    {
        // HDR: URP renders an offscreen camera in its target's format, and in an 8-bit one nothing
        // is brighter than white - no bloom (the bright stars, the Sun's glow). Read back through
        // an sRGB copy, which also does the linear-to-sRGB step the screen would.
        foreach (ShowFPS counter in FindObjectsByType<ShowFPS>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            counter.SetVisible(false); // never the debug frame counter in a picture
        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf);
        rt.antiAliasing = 4;
        Camera cam = Camera.main;
        RenderTexture previous = cam.targetTexture;
        // A few frames into the texture first: the canvas lays itself out for its camera's target.
        // In a run the canvas is drawn by a UI camera stacked on the main one (MenusController),
        // which lays it out for the Editor's own small screen: for the shot the main camera
        // draws it, as in the menus.
        cam.targetTexture = rt;
        Canvas uiCanvas = null;
        Camera uiCamera = null;
        int mask = cam.cullingMask;
        GameObject update = GameObject.Find("UpdateDialog");
        if (update != null)
            update.SetActive(false); // the Editor's version check thinks it is out of date
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (keepUI && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera != null && c.worldCamera != cam)
            {
                uiCanvas = c;
                uiCamera = c.worldCamera;
                c.worldCamera = cam;
                cam.cullingMask |= 1 << 5;
                cam.GetUniversalAdditionalCameraData().cameraStack.Remove(uiCamera);
            }
        for (int i = 0; i < 3; i++)
        {
            Canvas.ForceUpdateCanvases();
            yield return null;
        }

        if (!keepBursts && DiamondEffectField != null)
            foreach (DiamondPickUp d in FindObjectsByType<DiamondPickUp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                (DiamondEffectField.GetValue(d) as ParticleSystem)?.Clear(true);

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        List<Camera> stack = data.cameraStack != null ? new List<Camera>(data.cameraStack) : new List<Camera>();
        if (!keepUI && data.cameraStack != null)
            data.cameraStack.Clear(); // the menu/UI camera is stacked on the main one

        // -shotCloseup: the ball from close by, to see its look (the Earth's layers) - the camera
        // is put back straight after.
        Vector3 camPos = cam.transform.position;
        Quaternion camRot = cam.transform.rotation;
        PlayerMovement inPlay = FindAnyObjectByType<PlayerMovement>();
        GameObject shatter = null;
        if (Arg("-shotCloseup") != null && inPlay != null && Utility.gameStarted) // not in the menus
        {
            Transform ball = inPlay.transform;
            Vector3 back = (camPos - ball.position).normalized;
            cam.transform.position = ball.position + back * 1.6f + Vector3.up * 0.3f;
            cam.transform.LookAt(ball.position);
            // A picked diamond's shards in flight: from this close, in front of a see-through
            // track part, they showed the sky behind it - black blots over the picture.
            shatter = GameObject.Find("DiamondShatter");
            if (shatter != null)
                shatter.SetActive(false);
        }

        // -shotLook pitch,yaw: look that way instead (the sky: -shotLook -30,0 looks up).
        if (Arg("-shotLook") != null)
        {
            string[] py = Arg("-shotLook").Split(',');
            cam.transform.rotation = Quaternion.Euler(float.Parse(py[0], System.Globalization.CultureInfo.InvariantCulture),
                                                      float.Parse(py[1], System.Globalization.CultureInfo.InvariantCulture), 0);
        }

        // -shotLookAt <object name> [-shotFov <degrees>]: look at that object, zoomed in.
        float fov = cam.fieldOfView;
        if (Arg("-shotLookAt") != null)
        {
            GameObject target = GameObject.Find(Arg("-shotLookAt"));
            if (target != null)
                cam.transform.LookAt(target.transform.position);
            else
                Debug.LogWarning(TAG + "nothing called " + Arg("-shotLookAt"));
        }
        if (Arg("-shotFov") != null)
            cam.fieldOfView = float.Parse(Arg("-shotFov"), System.Globalization.CultureInfo.InvariantCulture);

        // The canvas is put in front of its camera once a frame, and this one moves: put it there
        // now.
        if (uiCanvas != null)
            uiCanvas.transform.SetPositionAndRotation(cam.transform.position + cam.transform.forward * uiCanvas.planeDistance, cam.transform.rotation);
        cam.Render();
        cam.targetTexture = previous;
        if (uiCanvas != null)
        {
            uiCanvas.worldCamera = uiCamera;
            cam.cullingMask = mask;
            cam.GetUniversalAdditionalCameraData().cameraStack.Add(uiCamera);
        }
        cam.transform.SetPositionAndRotation(camPos, camRot);
        cam.fieldOfView = fov;
        if (shatter != null)
            shatter.SetActive(true);

        RenderTexture shown = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(rt, shown);
        RenderTexture.active = shown;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(shown);
        string path = Path.Combine(folder, name + ".png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);
        rt.Release();

        if (!keepUI && data.cameraStack != null)
            foreach (Camera c in stack) data.cameraStack.Add(c);
        Debug.Log(TAG + "wrote " + path);
    }
}
#endif
