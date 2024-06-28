using UnityEngine;

namespace RackMount
{
    public class ModuleRackMountPart : PartModule
    {
        [KSPField]
        public bool partRackmountable = true;

        [KSPField]
        public bool autoCalculateVolume = true;

        [KSPField]
        public string requiresPartType = "";

        [KSPField]
        public int numCrewSeats = 0;

        [KSPField]
        public bool hasAirlock;

        [KSPField]
        public bool mountInEditorOnly = false;

        //used so B9PartSwitch resources can be added.
        [KSPField]
        public bool allResourcesRackmountable = false;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            ModuleCargoPart cargo = (ModuleCargoPart)part.Modules.GetModule<ModuleCargoPart>();

            if (autoCalculateVolume && cargo != null)
            {
                if (cargo.packedVolume == 0)
                {
                    cargo.packedVolume = Utilities.CalculateVolume(part);
                    autoCalculateVolume = false;
                }
            }
        }
    }
}