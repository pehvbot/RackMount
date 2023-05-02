using System;
using UnityEngine;
using System.Collections.Generic;
using KerbalConstructionTime;
using System.Reflection;
using HarmonyLib;
using RackMount;
using System.IO;
using System.Linq;
using Steamworks;

[assembly: KSPAssemblyDependency("KerbalConstructionTime", 0, 0)]
namespace RackMount
{
    [KSPAddon(KSPAddon.Startup.FlightEditorAndKSC, true)]
    public class RackMountKCTPatches : MonoBehaviour
    {
        private void Start()
        {
            if (AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.assembly.GetName().Name == "KerbalConstructionTime") != null)
            {
                var harmony = new Harmony("com.rackmount.rackmountkct");
                harmony.PatchAll();
            }
        }
    }

    [HarmonyPatch(typeof(KCT_Utilities), "RecoverActiveVesselToStorage")]
    internal class RecoverActiveVesselToStoragePatch
    {
        public static void Postfix()
        {
            if (KCT_GameStates.recoveredVessel.shipNode != null)
            {
                foreach (ConfigNode part in KCT_GameStates.recoveredVessel.shipNode.GetNodes("PART"))
                {
                    string[] splitName = part.GetValue("part").Split('_');

                    //i don't understand this name!
                    if (splitName.Length != 2)
                    {
                        Debug.Log("[RM] Malformed part name for part:" + part.GetValue("part"));
                        return;
                    }

                    foreach (ConfigNode module in part.GetNodes("MODULE"))
                    {
                        if (module.GetValue("name") == "ModuleRackMount")
                        {
                            if (!string.IsNullOrEmpty(module.GetValue("originalPart")))
                            {
                                part.SetValue("part", module.GetValue("originalPart") + "_" + splitName[1]);
                                module.SetValue("originalPart", "");
                            }
                        }
                    }
                }
            }
        }
    }
}