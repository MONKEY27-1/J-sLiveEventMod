using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AstronomicalEvent
{
    [BepInPlugin("local.astronomical.event", "J'sLiveEventsMod", "0.1.0")]
    public class AstronomicalEventMod : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        AstronomicalEventManager mgr;
        AstronomicalUI ui;

        public static AstronomicalEventMod Inst;
        internal AstronomicalAssetLoader Loader;

        public static void ParkModel(GameObject g)
        {
            if (Inst != null && Inst.Loader != null && g != null)
                Inst.Loader.Park(g);
        }

        void Awake()
        {
            Inst = this;
            Log = Logger;
            AstronomicalSettings.Init(Config);
            AstronomicalAssetLoader.Dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), "AstronomicalAssets");
            AstronomicalEvents.Scan(AstronomicalAssetLoader.Dir);
            var loader = new AstronomicalAssetLoader();
            Loader = loader;
            StartCoroutine(loader.LoadAll());
            mgr = new AstronomicalEventManager(loader);
            ui = new AstronomicalUI(mgr);
            StartCoroutine(AstronomicalNet.Poll(mgr));
            AstronomicalDebug.Init(System.IO.Path.GetDirectoryName(Info.Location));
            AstronomicalEventApi.Mgr = mgr;
            AstronomicalEventApi.Ui = ui;
            SceneManager.activeSceneChanged += OnSceneChanged;
            Application.quitting += Shutdown;
            Log.LogInfo(
                "Astronomical Event loaded (client-side only). Unity "
                    + Application.unityVersion
                    + ", scene "
                    + SceneManager.GetActiveScene().name
                    + ". Press P in Forest."
            );
        }

        void OnSceneChanged(Scene a, Scene b)
        {
            if (mgr.State != EventState.Idle)
            {
                mgr.Message = "Scene changed - event stopped.";
                mgr.Stop();
            }
            try
            {
                AstronomicalKlombo.Cleanup();
                AstronomicalCube.ClearPersistent();
                AstronomicalCubeLake.ClearPersistent();
                AstronomicalIceStorm.ClearPersistent();
            }
            catch { }
        }

        void Update()
        {
            if (AstronomicalSettings.BuiltInMenu.Value && PPressed())
                ui.Toggle();
            // each part on its own, so one that throws does not skip the others for that frame
            Guard("Event", mgr.Tick);
            Guard("Test console", () => AstronomicalDebug.Tick(mgr));
            Guard("Kevin", AstronomicalCube.PersistTick);
            Guard("Purple lake", AstronomicalCubeLake.PersistTick);
            Guard("Ice Storm snow", AstronomicalIceStorm.PersistTick);
            Guard("Klombo", AstronomicalKlombo.Tick);
            AstronomicalEventApi.Pump();
        }

        static readonly System.Collections.Generic.Dictionary<string, float> lastWarn = new System.Collections.Generic.Dictionary<string, float>();

        static void Guard(string what, System.Action a)
        {
            try
            {
                a();
            }
            catch (System.Exception e)
            {
                float t;
                if (lastWarn.TryGetValue(what, out t) && Time.unscaledTime - t < 5f)
                    return; // at most one warning per part every 5 s
                lastWarn[what] = Time.unscaledTime;
                Log.LogWarning(what + ": " + e);
            }
        }

        static bool PPressed()
        {
            try
            {
                var k = UnityEngine.InputSystem.Keyboard.current;
                if (k != null)
                    return k.pKey.wasPressedThisFrame;
            }
            catch { }
            try
            {
                return Input.GetKeyDown(KeyCode.P);
            }
            catch
            {
                return false;
            }
        }

        void OnGUI()
        {
            if (AstronomicalSettings.BuiltInMenu.Value)
                ui.Draw();
        }

        void Shutdown()
        {
            try
            {
                ui.Close();
                mgr.Shutdown();
            }
            catch { }
            AstronomicalEventApi.Mgr = null;
            AstronomicalEventApi.Ui = null;
        }

        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            Application.quitting -= Shutdown;
            Shutdown();
        }
    }
}
