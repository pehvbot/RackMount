﻿using System;

namespace RackMount
{
    public class ModuleCargoPartRM : ModuleCargoPart
    {
        [KSPField(isPersistant = true)]
        float savedPackedVolume = 0;

        [KSPField(isPersistant = true)]
        bool cargoActive = false;

        bool firstRun = true;

        [KSPEvent(active = true, guiActive = false, guiActiveEditor = true, advancedTweakable = true)]
        private void ToggleCargoPart()
        {
            if (cargoActive)
            {
                packedVolume = -1;
                Events["ToggleCargoPart"].guiName = "Enable as cargo";
                cargoActive = false;
            }
            else
            {
                packedVolume = savedPackedVolume;
                Events["ToggleCargoPart"].guiName = "Disable as cargo";
                cargoActive = true;
            }
        }


        private void Start()
        {
            if (firstRun)
            {
                if (packedVolume == -1)
                    Events["ToggleCargoPart"].active = false;

                savedPackedVolume = packedVolume;
                firstRun = false;
            }

            if (!cargoActive)
            {
                packedVolume = -1;
                Events["ToggleCargoPart"].guiName = "Enable as cargo";
            }
            else
                Events["ToggleCargoPart"].guiName = "Disable as cargo";

        }

        public override void OnStoredInInventory(ModuleInventoryPart moduleInventoryPart)
        {
            float cost = 0;
            ModuleInventoryPart inventory = part.Modules.GetModule<ModuleInventoryPart>();
            if (inventory.storedParts.Count > 0)
            {
                for (int i = 0; i < inventory.storedParts.Count; i++)
                {
                    cost += inventory.storedParts.At(i).snapshot.partInfo.cost;
                }

                string p = "part";
                if (inventory.storedParts.Count > 1)
                    p = "parts";

                ScreenMessages.PostScreenMessage($"<color=orange>WARNING:</color> This {part.partInfo.title} has {inventory.storedParts.Count} {p} in it's inventory worth {cost:n0}.  You will lose those funds if you recover this vessel with this part still stored on this vessel", 7);

            }
            base.OnStoredInInventory(moduleInventoryPart);
        }
    }
}