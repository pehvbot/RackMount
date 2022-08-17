using UnityEngine;
using System.Collections.Generic;
using System;
using System.Reflection;
using UnityEngine.EventSystems;

namespace RackMount
{
    public class ModuleRMInventoryPart : ModuleInventoryPart
    {
        [KSPField]
        public float evaDistance = 3;

        [KSPField]
        public bool requiresEngineer = true;

        [KSPField]
        public bool canAlwaysRackmount = false;

        //removes existing ModuleCommand.  ModuleCommand is a special snowflake and needs additional logic to mount properly
        //this starts with having an existing ModuleCommand then removing it.  This fixes issues with launch checks and on rails issues.
        //disable if you don't plan on mounting ModuleCommand
        [KSPField]
        public bool removeModuleCommand = true;

        [KSPField]
        public bool autoCalculateVolume = true;

        [KSPField]
        public float volumeAdjustPercent = 0.7f;

        [KSPField]
        public string partType = "";

        Dictionary<uint, int> rackMountableParts = new Dictionary<uint, int>();
        Dictionary<uint, int> unMountableParts = new Dictionary<uint, int>();

        private BasePAWGroup rackmountGroup = new BasePAWGroup("rackmountGroup", "Rackmount Inventory", false);

        private bool onLoad;
        private bool previousCanRackmount = true;

        //needed to turn on vessel renaming when a ModuleCommand part is installed.
        //For some reason putting it in OnUpdate() allows it to be displayed
        //on load.
        private bool displayVesselNaming = false;

        public override void OnLoad(ConfigNode node)
        {
            //checks for no volume set
            if (autoCalculateVolume && packedVolumeLimit == 0)
            {
                Bounds bounds = default(Bounds);
                foreach (var bound in part.GetRendererBounds())
                {
                    bounds.Encapsulate(bound);
                }
                float vol = ((float)Math.Round(bounds.size.x * bounds.size.y * bounds.size.z * volumeAdjustPercent, 2));
                packedVolumeLimit = vol * 1000f;
            }

            base.OnLoad(node);

            //disable existing ModuleCommand
            if (removeModuleCommand)
            {
                PartModule p = part.Modules.GetModule<ModuleCommand>();
                if (p != null)
                    part.RemoveModule(p);
            }

            //modules need to be added after base.OnLoad();
            onLoad = true;
            if (storedParts != null)
            {
                //add buttons
                for (int i = 0; i < storedParts.Count; i++)
                {
                    AddRackmountButton(storedParts.At(i));
                }
                //add mounted modules
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
            if(HighLogic.LoadedSceneIsFlight)
                Fields["InventorySlots"].group.startCollapsed = true;
        }

        public override void OnUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                //fugly way to display Vessel Renaming when ModuleCommand is
                //mounted OnLoad 
                if (displayVesselNaming)
                {
                    part.Events["SetVesselNaming"].guiActive = true;
                    displayVesselNaming = false;
                }

                //show or hide rackmount buttons
                if (!previousCanRackmount && CanRackmount())
                {
                    foreach (var button in Events.FindAll(x => x.name.Contains("RackmountButton")))
                        button.active = true;
                    previousCanRackmount = true;
                }
                else if (previousCanRackmount && !CanRackmount())
                {
                    foreach (var button in Events.FindAll(x => x.name.Contains("RackmountButton")))
                        button.active = false;
                    previousCanRackmount = false;
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
                if (!rackMountableParts.ContainsKey(storedParts.At(i).snapshot.persistentId) && !unMountableParts.ContainsKey(storedParts.At(i).snapshot.persistentId))
                    AddRackmountButton(storedParts.At(i));
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
                RemoveRackmountButton(button.Key, button.Value);
            }

            //locks mounted parts.  Done in Update() because doing an EVA and going into Engineering mode re-enables the slotButton.
            if (part.PartActionWindow != null)
            {
                UIPartActionInventory inventoryUI = (UIPartActionInventory)part.PartActionWindow.ListItems.Find(x => x.GetType() == typeof(UIPartActionInventory));
                for (int i = 0; i < storedParts.Count; i++)
                {
                    bool mounted = false;
                    storedParts.At(i).snapshot.partData.TryGetValue("partRackmounted", ref mounted);
                    if (mounted)
                    {
                        inventoryUI.slotButton[storedParts.At(i).slotIndex].enabled = false;
                    }
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

            //fugly way to display Vessel Renaming when ModuleCommand is
            //mounted OnLoad in the Editor
            if (HighLogic.LoadedSceneIsEditor && displayVesselNaming)
            {
                part.Events["SetVesselNaming"].guiActiveEditor = true;
                displayVesselNaming = false;
            }
        }

        //adds button for mounting and unmounting parts
        private void AddRackmountButton(StoredPart storedPart)
        {
            ConfigNode partConfig = storedPart.snapshot.partInfo.partConfig;
            ConfigNode moduleRackMount = partConfig.GetNode("MODULE", "name", "ModuleRackMount");

            string requiresPartType = "";

            if (moduleRackMount != null)
            {
                moduleRackMount.TryGetValue("requiresPartType", ref requiresPartType);
                if (requiresPartType == "" || partType.ToUpper() == "ANY" || requiresPartType.ToUpper() == partType.ToUpper())
                {
                    rackMountableParts.Add(storedPart.snapshot.persistentId, storedPart.slotIndex);

                    KSPEvent mount = new KSPEvent
                    {
                        name = "RackmountButton" + storedPart.slotIndex,
                        active = previousCanRackmount,
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
                else if (!unMountableParts.ContainsKey(storedPart.snapshot.persistentId))
                {
                    unMountableParts.Add(storedPart.snapshot.persistentId, storedPart.slotIndex);
                    if (partType == "")
                        ScreenMessages.PostScreenMessage("<color=orange>Part " + storedPart.snapshot.partInfo.title + " can only be mounted on part type of " +
                            moduleRackMount.GetValue("requiresPartType") + "!</color>\nIt is currently stored in part without a part type.", 7);
                    else
                        ScreenMessages.PostScreenMessage("<color=orange>Part " + storedPart.snapshot.partInfo.title + " can only be mounted on part type of " +
                            moduleRackMount.GetValue("requiresPartType") + "!</color>\nIt is currently stored in a part type of " + partType + ".", 7);
                }
            }
        }

        private void RemoveRackmountButton(uint id, int slot)
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

                    //ModuleCommand fixes
                    if (partModule.GetType() == typeof(ModuleCommand))
                    {

                        part.Events["SetVesselNaming"].guiActive = true;
                        part.Events["SetVesselNaming"].guiActiveEditor = true;
                        //Here so it display the Vessel Naming from OnLoad using Update
                        displayVesselNaming = true;
                        ModuleCommand c = (ModuleCommand)partModule;
                        DictionaryValueList<string, ControlPoint> controlPoints = new DictionaryValueList<string, ControlPoint>();

                        ControlPoint _default = new ControlPoint("_default", c.defaultControlPointDisplayName, part.transform, new Vector3(0, 0, 0));
                        controlPoints.Add(_default.name, _default);
                        Debug.Log("[RM] c");

                        foreach (var node in moduleConfigNode.GetNodes("CONTROLPOINT"))
                        {
                            Vector3 orientation = new Vector3(0, 0, 0);
                            node.TryGetValue("orientation", ref orientation);
                            ControlPoint point = new ControlPoint(node.GetValue("name"), node.GetValue("displayName"), part.transform, orientation);
                            controlPoints.Add(point.name, point);
                        }
                        c.controlPoints = controlPoints;
                    }

                    //Modules loaded with OnLoad() already includes modulePersistentID from save file
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

            //magic?!  It works, don't know why
            part.ModulesOnActivate();
            part.ModulesOnStart();
            part.ModulesOnStartFinished();

            UIPartActionWindow paw = part.PartActionWindow;
            if (paw != null)
                paw.UpdateWindow();
        }

        private void UnmountPart(StoredPart storedPart)
        {
            List<PartModule> removeModules = new List<PartModule>();

            //likely needs a better way of storing/retrieving moduleValues
            if (HighLogic.LoadedSceneIsFlight)
                GamePersistence.SaveGame("RackMount", HighLogic.SaveFolder, SaveMode.BACKUP);

            foreach (PartModule partModule in part.Modules)
            {
                foreach (var module in storedPart.snapshot.modules)
                {
                    if (module.moduleValues.GetValue("modulePersistentId") == partModule.GetPersistentId().ToString())
                    {
                        //moduleValues only accessible in flight?
                        if (HighLogic.LoadedSceneIsFlight)
                            module.moduleValues = partModule.snapshot.moduleValues;
                        removeModules.Add(partModule);
                        module.moduleValues.RemoveValue("modulePersistentId");
                        
                        if (module.GetType() == typeof(ModuleCommand))
                        {
                            part.Events["SetVesselNaming"].guiActive = false;
                            part.Events["SetVesselNaming"].guiActiveEditor = false;
                            displayVesselNaming = false;
                        }
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
                if (storedResource != null && rackMountable)
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

        private bool CanRackmount()
        {
            //for debugging
            if (canAlwaysRackmount)
                return true;

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