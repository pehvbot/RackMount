using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;
using UnityEngine.UI;
using KSP.UI.Screens.Editor;

namespace RackMount
{
    public class ModuleRackMount : PartModule
    {
        [KSPField]
        public bool partRackmountable = true;
    }

    public class ModuleRMInventoryPart : ModuleInventoryPart
    {
        [KSPField]
        public float evaDistance = 3;

        [KSPField]
        public bool requiresEngineer = true;

        Dictionary<uint, int> rackMountableParts = new Dictionary<uint, int>();

        private BasePAWGroup rackmountGroup = new BasePAWGroup("rackmountGroup", "Rackmount Inventory", false);

        private bool onLoad;


        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            onLoad = true;
            if (storedParts != null)
            {
                for (int i = 0; i < storedParts.Count; i++)
                {
                    AddRackmountButtons(storedParts.At(i));
                }
                for (int i = 0; i < storedParts.Count; i++)
                {
                    bool mounted = false;
                    storedParts.At(i).snapshot.partData.TryGetValue("partRackmounted", ref mounted);
                    if (mounted)
                        RackmountPart(storedParts.At(i));
                }
            }
            onLoad = false;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            Fields["InventorySlots"].group = rackmountGroup;
            Fields["InventorySlots"].guiName = null;
        }

        public override void OnUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (CrewPresent())
                {
                    foreach (var button in Events.FindAll(x => x.name.Contains("RackmountButton")))
                        button.active = true;
                }
                else
                {
                    foreach (var button in Events.FindAll(x => x.name.Contains("RackmountButton")))
                        button.active = false;
                }
            }
            base.OnUpdate();
        }

        private void Update()
        {
            List<uint> currentParts = new List<uint>();
            Dictionary<uint, int> buttonsToRemove = new Dictionary<uint, int>();

            //looks for new parts
            for (int i = 0; i < storedParts.Count; i++)
            {
                if (!rackMountableParts.ContainsKey(storedParts.At(i).snapshot.persistentId))
                    AddRackmountButtons(storedParts.At(i));
                currentParts.Add(storedParts.At(i).snapshot.persistentId);
            }

            //looks for removed parts
            foreach (KeyValuePair<uint, int> rackMountablePart in rackMountableParts)
            {
                if (!currentParts.Contains(rackMountablePart.Key))
                    buttonsToRemove.Add(rackMountablePart.Key, rackMountablePart.Value);
            }
            foreach (KeyValuePair<uint, int> button in buttonsToRemove)
            {
                RemoveRackmountButtons(button.Key, button.Value);
            }

            //locks mounted items
            if (part.PartActionWindow != null)
            {
                UIPartActionInventory inventoryUI = (UIPartActionInventory)part.PartActionWindow.ListItems.Find(x => x.GetType() == typeof(UIPartActionInventory));
                for (int i = 0; i < storedParts.Count; i++)
                {
                    bool mounted = false;
                    storedParts.At(i).snapshot.partData.TryGetValue("partRackmounted", ref mounted);
                    if (mounted)
                        inventoryUI.slotButton[storedParts.At(i).slotIndex].enabled = false;
                }
            }
            //checks for construction mode and locks/unlocks mounted items
            UIPartActionInventory constructionUI = constructorModeInventory;
            if (constructionUI != null)
            {
                for (int i = 0; i < storedParts.Count; i++)
                {
                    bool mounted = false;
                    storedParts.At(i).snapshot.partData.TryGetValue("partRackmounted", ref mounted);
                    if (mounted)
                        constructionUI.slotButton[storedParts.At(i).slotIndex].enabled = false;
                    else
                        constructionUI.slotButton[storedParts.At(i).slotIndex].enabled = true;
                }
            }
        }

        //adds button for mounting and unmounting parts
        private void AddRackmountButtons(StoredPart storedPart)
        {
            ConfigNode partConfig = storedPart.snapshot.partInfo.partConfig;
            var p = storedPart.snapshot.modules.Find(x=>x.moduleName == "ModuleRackMount");

            if (p!=null)
            {
                rackMountableParts.Add(storedPart.snapshot.persistentId,storedPart.slotIndex);

                KSPEvent mount = new KSPEvent
                {
                    name = "RackmountButton" + storedPart.slotIndex,
                    guiActive = true,
                    guiActiveEditor = true,
                    guiActiveUnfocused = true,
                    guiActiveUncommand = true,
                    unfocusedRange = evaDistance,
                    guiName = "<b><color=green>Rackmount</color> " + storedPart.snapshot.partInfo.title + "</b>"
                };
                BaseEvent RackmountButton = new BaseEvent(Events, mount.name, () => RackmountButtonPressed(storedPart), mount);
                RackmountButton.group = rackmountGroup;
                Events.Add(RackmountButton);

                UIPartActionWindow paw = part.PartActionWindow;
                if (paw != null)
                    paw.UpdateWindow();
            }
        }

        private void RemoveRackmountButtons(uint id, int slot)
        {
            Events.Find(x => x.name == "RackmountButton" + slot).active = false;
            Events.Remove(Events.Find(x => x.name == "RackmountButton" + slot));

            rackMountableParts.Remove(id);

            UIPartActionWindow paw = part.PartActionWindow;
            if (paw != null)
                paw.UpdateWindow();
        }

        private void RackmountButtonPressed(StoredPart storedPart)
        {
            bool mounted = false;
            storedPart.snapshot.partData.TryGetValue("partRackmounted", ref mounted);
            if (mounted)
                UnmountPart(storedPart);
            else
                RackmountPart(storedPart);
        }

        private void RackmountPart(StoredPart storedPart)
        {
            bool rackMountable = true;
            ConfigNode partConfig = storedPart.snapshot.partInfo.partConfig;

            foreach (ConfigNode moduleConfigNode in partConfig.GetNodes("MODULE"))
            {
                if (moduleConfigNode.TryGetValue("rackMountable", ref rackMountable))
                {
                    PartModule partModule = part.AddModule(moduleConfigNode, true);
                    int moduleIndex = part.Modules.IndexOf(partModule);
                    ProtoPartModuleSnapshot moduleSnapshot = storedPart.snapshot.FindModule(partModule, moduleIndex);
                    part.LoadModule(moduleSnapshot.moduleValues, ref moduleIndex);

                    if (!onLoad)
                        moduleSnapshot.moduleValues.AddValue("modulePersistentId", partModule.GetPersistentId());
                }
            }

            foreach (var resource in storedPart.snapshot.resources)
            {
                rackMountable = false;
                var configNode = storedPart.snapshot.partInfo.partConfig.GetNode("RESOURCE", "name", resource.resourceName);
                configNode.TryGetValue("rackMountable", ref rackMountable);
                var partResource = part.Resources.Get(resource.resourceName);
                if (partResource != null && rackMountable)
                {
                    partResource.maxAmount += resource.maxAmount;
                    partResource.amount += resource.amount;
                }
                else if (rackMountable)
                    resource.Load(part);
            }

            storedPart.snapshot.partData.SetValue("partRackmounted", true, true);

            BaseEvent button = (BaseEvent)Events.Find(x => x.name == "RackmountButton" + storedPart.slotIndex);
            button.guiName = "<b><color=red>Unmount</color> " + storedPart.snapshot.partInfo.title + "</b>";

            UIPartActionWindow paw = part.PartActionWindow;
            if (paw != null)
                paw.UpdateWindow();

            //magic?!  It works, don't know why
            part.ModulesOnActivate();
            part.ModulesOnStart();
            part.ModulesOnStartFinished();

        }
        private void UnmountPart(StoredPart storedPart)
        {
            List<PartModule> removeModules = new List<PartModule>();

            //likely needs a better way of storing/retrieving moduleValues
            if(HighLogic.LoadedSceneIsFlight)
                GamePersistence.SaveGame("persistent.sfs", HighLogic.SaveFolder, SaveMode.BACKUP);

            foreach (PartModule partModule in part.Modules)
            {
                foreach (var module in storedPart.snapshot.modules)
                {
                    if (module.moduleValues.GetValue("modulePersistentId") == partModule.GetPersistentId().ToString())
                    {
                        //moduleValues only accessible in flight?
                        if(HighLogic.LoadedSceneIsFlight)
                            module.moduleValues = partModule.snapshot.moduleValues;
                        removeModules.Add(partModule);
                        module.moduleValues.RemoveValue("modulePersistentId");
                    }
                }
            }
            foreach (PartModule partModule in removeModules)
                part.RemoveModule(partModule);

            foreach (ProtoPartResourceSnapshot resource in storedPart.snapshot.resources)
            { 
                bool rackMountable = false;
                var configNode = storedPart.snapshot.partInfo.partConfig.GetNode("RESOURCE", "name", resource.resourceName);
                configNode.TryGetValue("rackMountable", ref rackMountable);

                PartResource storedResource = part.Resources.Get(resource.resourceName);
                if(storedResource != null && rackMountable)
                {
                    resource.amount = storedResource.amount * (resource.maxAmount / storedResource.maxAmount);
                    storedResource.amount -= resource.amount;
                    storedResource.maxAmount -= resource.maxAmount;
                }
            }

            storedPart.snapshot.partData.SetValue("partRackmounted", false, true);

            //unlocking always happens from paw?
            UIPartActionInventory inventoryUI = (UIPartActionInventory)part.PartActionWindow.ListItems.Find(x => x.GetType() == typeof(UIPartActionInventory));
            inventoryUI.slotButton[storedPart.slotIndex].enabled = true;

            BaseEvent button = (BaseEvent)Events.Find(x => x.name == "RackmountButton" + storedPart.slotIndex);
            button.guiName = "<b><color=green>Rackmount</color> " + storedPart.snapshot.partInfo.title + "</b>";

            UIPartActionWindow paw = part.PartActionWindow;
            if (paw != null)
                paw.UpdateWindow();

            part.ModulesOnDeactivate();
        }

        private bool CrewPresent()
        {
            //First check for kerbal on EVA.  If EVA doesn't qualify, return false.
            if (FlightGlobals.ActiveVessel.isEVA)
            {
                ProtoCrewMember crew = FlightGlobals.ActiveVessel.GetVesselCrew()[0];
                float kerbalDistanceToPart = Vector3.Distance(FlightGlobals.ActiveVessel.transform.position, part.collider.ClosestPointOnBounds(FlightGlobals.ActiveVessel.transform.position));
                if (kerbalDistanceToPart < evaDistance && (!requiresEngineer || crew.trait == "Engineer"))
                    return true;
                else
                    return false;
            }

            //Next check onboard crew
            foreach (var crew in vessel.GetVesselCrew())
                if (!requiresEngineer || crew.trait == "Engineer")
                    return true;

            return false;
        }
    }
}