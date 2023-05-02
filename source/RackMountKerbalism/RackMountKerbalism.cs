using System;
using UnityEngine;
using KERBALISM;
using System.Collections.Generic;
using HarmonyLib;
using KSP.Localization;
using static RackMount.RackMountKerbalism;
using System.Reflection;

[assembly: KSPAssemblyDependency("KerbalismBootstrap", 0, 0)]
namespace RackMount
{

    public static class RackMountKerbalism
    {
        [KSPAddon(KSPAddon.Startup.Instantly, true)]
        public class OnKerbalismPatch : MonoBehaviour
        {
            private void Start()
            {
                var harmony = new Harmony("com.rackmount.kerbalism");
                harmony.PatchAll();
            }
        }
    }

    [HarmonyPatch(typeof(KERBALISM.Lib), nameof(Lib.SetProcessEnabledDisabled))]
    internal class SetProcessEnabledDisabledPatch
    {
        //resets the process capacity.
        public static void Postfix(Part p, string res_name, bool enable, double process_capacity)
        {
            double currentCapacity = 0.0;
            double maxCapacity = 0.0;

            foreach(ProcessController controller in p.Modules.GetModules<ProcessController>())
            {
                if(controller.resource == res_name)
                {
                    Type t = controller.GetType();
                    FieldInfo info = t.GetField("broken", BindingFlags.NonPublic | BindingFlags.Instance);
                    bool broken = (bool)info.GetValue(controller);

                    maxCapacity += controller.capacity;
                    if (controller.running && !broken)
                        currentCapacity += controller.capacity;
                }
            }
            if (currentCapacity > 0.0 && maxCapacity > 0.0)
            {
                Lib.SetResource(p, res_name, currentCapacity, currentCapacity);
            }      
        }
    }
}
