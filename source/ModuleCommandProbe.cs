using System;
using CommNet;
using KSP.Localization;
using UnityEngine;

namespace RackMount
{
	public class ModuleCommandProbe : ModuleCommand
    {
        private static string cacheAutoLOC_7001411;

        private static string cacheAutoLOC_217448;

        private static string cacheAutoLOC_217464;

        private static string cacheAutoLOC_217408;

        private static string cacheAutoLOC_217417;

        private static string cacheAutoLOC_217429;

        private static string cacheAutoLOC_217437;

        private static string cacheAutoLOC_217509;

        private static string cacheAutoLOC_217513;

        private static string cacheAutoLOC_217517;

        private static string cacheAutoLOC_6003031;

        public override void OnStart(StartState state)
        {
            if (minimumCrew > 0)
                Debug.LogWarning($"[RM] ModuleCommandProbe on Part:{part.name} has minimumCrew = {minimumCrew}.  ModuleCommandProbe ignores this setting.");
            base.OnStart(state);
        }

        public override VesselControlState UpdateControlSourceState()
        {

            ModuleResourceHandler moduleResourceHandler = resHandler;
            ref string error = ref controlSrcStatusText;
            double rateMultiplier;

            if (!IsHibernating)
            {
                rateMultiplier = 1.0;
            }
            else
            {
                rateMultiplier = hibernationMultiplier;
            }

            if (!moduleResourceHandler.UpdateModuleResourceInputs(ref error, rateMultiplier, 0.9, returnOnFirstLack: true))
            {
                moduleState = ModuleControlState.NotEnoughResources;
                return VesselControlState.Probe;
            }

            if (CommNetScenario.CommNetEnabled)
            {
                if (CommNetScenario.Instance != null)
                {
                    if (!(Connection != null && Connection.IsConnected))
                    {
                        if (!SignalRequired && remoteControl && requiresTelemetry)
                        {
                            if (controlSrcStatusText != "Partial Control")
                            {
                                controlSrcStatusText = cacheAutoLOC_217437;
                            }
                            moduleState = ModuleControlState.PartialProbe;
                            return VesselControlState.ProbePartial;
                        }
                        else
                        {
                            if (controlSrcStatusText != "No Telemetry")
                            {
                                controlSrcStatusText = cacheAutoLOC_217429;
                            }
                            moduleState = ModuleControlState.NoControlPoint;
                            return VesselControlState.Probe;
                        }
                    }
                }
            }

            if (IsHibernating)
            {
                controlSrcStatusText = cacheAutoLOC_217448;
                moduleState = ModuleControlState.PartialProbe;
                return VesselControlState.ProbePartial;
            }

            controlSrcStatusText = cacheAutoLOC_217464;

            moduleState = ModuleControlState.Nominal;

            return VesselControlState.ProbeFull;
        }

        internal static void CacheLocalStrings()
        {
            cacheAutoLOC_7001411 = Localizer.Format("#autoLOC_7001411");
            cacheAutoLOC_217448 = Localizer.Format("#autoLOC_217448");
            cacheAutoLOC_217464 = Localizer.Format("#autoLOC_217464");
            cacheAutoLOC_217408 = Localizer.Format("#autoLOC_217408");
            cacheAutoLOC_217417 = Localizer.Format("#autoLOC_217417");
            cacheAutoLOC_217429 = Localizer.Format("#autoLOC_217429");
            cacheAutoLOC_217437 = Localizer.Format("#autoLOC_217437");
            cacheAutoLOC_217509 = Localizer.Format("#autoLOC_217509");
            cacheAutoLOC_217513 = Localizer.Format("#autoLOC_217513");
            cacheAutoLOC_217517 = Localizer.Format("#autoLOC_217517");
            cacheAutoLOC_6003031 = Localizer.Format("#autoLoc_6003031");
        }
    }
}

