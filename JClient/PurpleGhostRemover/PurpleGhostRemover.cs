using BepInEx;
using GorillaTagScripts;
using UnityEngine;

namespace JsPurpleGhostRemover
{
    [BepInPlugin("j.purpleghostremover", "J's Purple Ghost Remover", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        float nextScan;

        void Update()
        {
            if (Time.time < nextScan) return;
            nextScan = Time.time + 300f; // re-check every 5 minutes in case the ghost spawns later
            foreach (var ghost in FindObjectsByType<LurkerGhost>(FindObjectsSortMode.None))
            {
                foreach (var r in ghost.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = true;
                foreach (var a in ghost.GetComponentsInChildren<AudioSource>(true)) a.mute = true;
            }
        }
    }
}
