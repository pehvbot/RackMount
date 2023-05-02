using System;
using UnityEngine;
using System.Collections.Generic;
using KerbalConstructionTime;
using System.Reflection;
using HarmonyLib;
using RackMount;
using System.IO;
using System.Linq;

[assembly: KSPAssemblyDependency("KerbalConstructionTime", 0, 0)]
namespace RackMount
{
    [KSPAddon(KSPAddon.Startup.FlightEditorAndKSC, true)]
    public class RackMountRP0KCTPatches : MonoBehaviour
    {
        private void Start()
        {
            if (AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.assembly.GetName().Name == "LRTRKCT") != null ||
                AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.assembly.GetName().Name == "RP0KCT") != null)
            {
                var harmony = new Harmony("com.rackmount.rackmountrp0kct");
                harmony.PatchAll();
            }
        }
    }

    [HarmonyPatch(typeof(KerbalConstructionTime.Utilities), "RecoverActiveVesselToStorage")]
    internal class RecoverActiveVesselToStoragePatch
    {
        public static void Postfix()
        {
            if (KCTGameStates.RecoveredVessel.ShipNode != null)
            {
                foreach (ConfigNode part in KCTGameStates.RecoveredVessel.ShipNode.GetNodes("PART"))
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