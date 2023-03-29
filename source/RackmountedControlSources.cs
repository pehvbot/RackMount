using KSP.Localization;
using PreFlightTests;
using UnityEngine;

namespace RackMount
{
	public class RackmountedControlSources: DesignConcernBase, IPreFlightTest
	{
        public RackmountedControlSources()
        {

        }
        public override bool TestCondition()
        {
            Part root = EditorLogic.RootPart;

            ShipConstruct ship = root.ship;
            VesselCrewManifest manifest = ShipConstruction.ShipManifest;

            if (ship == null || manifest == null)
                return false;

            foreach(Part part in ship.Parts)
            {
                var commands = part.FindModulesImplementing<ModuleCommand>();
                if (commands != null)
                {
                    foreach (var command in commands)
                    {
                        if (command.minimumCrew == 0)
                            return true;
                        PartCrewManifest pcm = manifest.GetPartCrewManifest(part.craftID);
                        if (pcm != null && pcm.CountCrewNotType(ProtoCrewMember.KerbalType.Tourist, HighLogic.CurrentGame.CrewRoster) >= command.minimumCrew)
                            return true;
                    }
                }

                var seat = part.FindModuleImplementing<KerbalSeat>();
                if(seat != null)
                {
                    PartCrewManifest pcm = manifest.GetPartCrewManifest(part.craftID);
                    if (pcm != null && pcm.CountCrewNotType(ProtoCrewMember.KerbalType.Tourist, HighLogic.CurrentGame.CrewRoster) > 0)
                        return true;
                }
            }
            return false;
        }

        public override DesignConcernSeverity GetSeverity()
        {
            return DesignConcernSeverity.CRITICAL;
        }

        public override string GetConcernTitle()
        {
            return Localizer.Format("#autoLOC_253474");
        }

        public override string GetConcernDescription()
        {
            return Localizer.Format("#autoLOC_253479");
        }

        public string GetWarningTitle()
        {
            return Localizer.Format("#autoLOC_253486");
        }

        public string GetWarningDescription()
        {
            return Localizer.Format("#autoLOC_253491");
        }

        public string GetProceedOption()
        {
            return Localizer.Format("#autoLOC_253496");
        }

        public string GetAbortOption()
        {
            return Localizer.Format("#autoLOC_253501");
        }

        public string GetTestName()
        {
            return "No Control Sources";
        }
    }
}

