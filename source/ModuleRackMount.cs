﻿using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace RackMount
{
    public class ModuleRackMount : PartModule
    {
        [KSPField]
        public bool autoCalculateVolume = true;

        [KSPField]
        public float volumeAdjustPercent = 1.0f;

        [KSPField]
        public string partType = "";

        [KSPField(isPersistant =true)]
        public bool createPart = false;

        [KSPField(isPersistant = true)]
        public string originalPart = "";

        [KSPField(isPersistant = true)]
        public int startingCrewCapacity = -1;

        Dictionary<uint, int> rackMountableParts = new Dictionary<uint, int>();
        Dictionary<uint, int> unMountableParts = new Dictionary<uint, int>();

        private ModuleInventoryPart inv;

        private BasePAWGroup rackmountGroup = new BasePAWGroup("rackmountGroup", "Rackmount Inventory", false);

        //needed to turn on vessel renaming when a ModuleCommand part is installed.
        //For some reason putting it in OnUpdate() allows it to be displayed
        //on load.
        private bool displayVesselNaming = false;

        //keeps track to make sure ModuleInventoryPart is available
        private bool invLoaded = false;

        public override void OnLoad(ConfigNode node)
        {
            inv = part.Modules.GetModule<ModuleInventoryPart>();

            //aborts module if the part doesn't have an inventory
            if (inv == null)
                return;

            invLoaded = true;

            //saves initial crew capacity
            if (startingCrewCapacity == -1)
                startingCrewCapacity = part.CrewCapacity;

            //checks for no volume set
            if (autoCalculateVolume && inv.packedVolumeLimit == 0)
                inv.packedVolumeLimit = Utilities.CalculateVolume(part, volumeAdjustPercent);

            base.OnLoad(node);

            //checks for stored parts
            if (inv.storedParts != null)
            {
                //add buttons
                for (int i = 0; i < inv.storedParts.Count; i++)
                {
                    AddRackmountButton(inv.storedParts.At(i));

                    bool mounted = false;
                    inv.storedParts.At(i).snapshot.partData.TryGetValue("partRackmounted", ref mounted);
                    if (mounted)
                    {
                        BaseEvent button = (BaseEvent)Events.Find(x => x.name == "RackmountButton" + inv.storedParts.At(i).slotIndex);
                        button.guiName = "<b><color=orange>Unmount</color> " + inv.storedParts.At(i).snapshot.partInfo.title + "</b>";

                        //used for craft files with pre-rackmounted parts
                        //or if a new part isn't created
                        if(HighLogic.LoadedSceneIsEditor || !createPart)
                        {
                            RackmountPart(inv.storedParts.At(i));
                        }
                    }
                }
            }
        }

       
        public override void OnInitialize()
        {
            if (inv == null)
                inv = part.Modules.GetModule<ModuleInventoryPart>();

            //aborts module if the part doesn't have an inventory
            if (inv == null)
                return;

            invLoaded = true;

            //adds CrewCapacity for launch
            AddCrew();
            base.OnInitialize();
        }

        private void Start()
        {
            //adds CrewCapacity for loading in Editor
            if (invLoaded)
                AddCrew();
        }

        private void AddCrew()
        {
            //sets crew to starting capacity.
            part.CrewCapacity = startingCrewCapacity;

            //checks inventory for mounted seats.
            if (inv.storedParts != null)
            {
                //look through mounted modules
                for (int i = 0; i < inv.storedParts.Count; i++)
                {
                    bool mounted = false;
                    inv.storedParts.At(i).snapshot.partData.TryGetValue("partRackmounted", ref mounted);
                    if (mounted)
                    {
                        ConfigNode partConfig = inv.storedParts.At(i).snapshot.partInfo.partConfig;

                        //iterates through all modules on the stored part.
                        foreach (ConfigNode moduleConfigNode in partConfig.GetNodes("MODULE"))
                        {
                            //ModuleRackMountPart adjustments
                            if (moduleConfigNode.GetValue("name") == "ModuleRackMountPart")
                                //add crew seat.
                                if (moduleConfigNode.HasValue("crewSeat"))
                                    part.CrewCapacity += int.Parse(moduleConfigNode.GetValue("crewSeat"));
                        }
                    }
                }
            }

            //unclear if it should be this or HighLogic.LoadedSceneIsEditor
            if (ShipConstruction.ShipConfig != null)
            {
                //gets part manifest
                if (ShipConstruction.ShipManifest == null)
                    ShipConstruction.ShipManifest = VesselCrewManifest.FromConfigNode(ShipConstruction.ShipConfig);
                PartCrewManifest manifest = ShipConstruction.ShipManifest.GetPartCrewManifest(part.craftID);

                //creates a PartCrewManifest object if it doesn't already exist
                if (manifest == null)
                {
                    ConfigNode node = new ConfigNode();
                    node.AddValue("part", part.name + "_" + part.craftID);
                    node.AddValue("CrewCapacity", part.CrewCapacity);
                    ShipConstruction.ShipManifest.SetPartManifest(part.craftID, PartCrewManifest.FromConfigNode(node, ShipConstruction.ShipManifest));
                    manifest = ShipConstruction.ShipManifest.GetPartCrewManifest(part.craftID);
                }

                var newCrew = new string[part.CrewCapacity];

                for (int i = 0; i < newCrew.Length; i++)
                {
                    if (i < manifest.partCrew.Length)
                        newCrew[i] = manifest.partCrew[i];
                    else
                        newCrew[i] = "";
                }
                manifest.partCrew = newCrew;
                manifest.PartInfo.partPrefab.CrewCapacity = part.CrewCapacity;
                manifest.PartInfo.partConfig.SetValue("CrewCapacity", part.CrewCapacity);
            }
            
        }

        public override void OnStart(StartState state)
        {
            if (inv == null)
                inv = part.Modules.GetModule<ModuleInventoryPart>();

            //aborts module if the part doesn't have an inventory
            if (inv == null)
            {
                Debug.LogWarning("[RM] No ModuleInventoryPart found on part:" + part.name);
                return;
            }

            invLoaded = true;

            base.OnStart(state);

            inv.Fields["InventorySlots"].group = rackmountGroup;
            inv.Fields["InventorySlots"].guiName = null;
            //if (HighLogic.LoadedSceneIsFlight)
            //    inv.Fields["InventorySlots"].group.startCollapsed = true;

        }

        public override void OnUpdate()
        {
            if (!invLoaded)
                return;

            if (HighLogic.LoadedSceneIsFlight)
            {
                //fugly way to display Vessel Renaming when ModuleCommand is
                //mounted OnLoad 
                if (displayVesselNaming)
                {
                    part.Events["SetVesselNaming"].guiActive = true;
                    displayVesselNaming = false;
                }

                //show or hide rackmount buttons when PAW is open
                if (part.PartActionWindow != null && part.PartActionWindow.isActiveAndEnabled)
                {
                    if (CanRackmount())
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
            }
            base.OnUpdate();
        }

        private void Update()
        {
            if (!invLoaded)
                return;

            List<uint> currentParts = new List<uint>();
            Dictionary<uint, int> buttonsToRemove = new Dictionary<uint, int>();

            //looks for any changes when the PAW is open
            if (part.PartActionWindow != null && part.PartActionWindow.isActiveAndEnabled)
            {
                //looks for new parts
                for (int i = 0; i < inv.storedParts.Count; i++)
                {
                    if (!rackMountableParts.ContainsKey(inv.storedParts.At(i).snapshot.persistentId) && !unMountableParts.ContainsKey(inv.storedParts.At(i).snapshot.persistentId))
                        AddRackmountButton(inv.storedParts.At(i));
                    currentParts.Add(inv.storedParts.At(i).snapshot.persistentId);
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
                UIPartActionInventory inventoryUI = (UIPartActionInventory)part.PartActionWindow.ListItems.Find(x => x.GetType() == typeof(UIPartActionInventory));
                if (inventoryUI != null)
                {
                    for (int i = 0; i < inv.storedParts.Count; i++)
                    {
                        bool mounted = false;
                        inv.storedParts.At(i).snapshot.partData.TryGetValue("partRackmounted", ref mounted);
                        if (mounted)
                        {
                            inventoryUI.slotButton[inv.storedParts.At(i).slotIndex].enabled = false;
                            inventoryUI.slotButton[inv.storedParts.At(i).slotIndex].gameObject.SetActive(false);
                        }
                    }
                }
            }

            //checks for construction mode and locks/unlocks mounted items
            if (inv.constructorModeInventory != null)
            {
                for (int i = 0; i < inv.storedParts.Count; i++)
                {
                    bool mounted = false;
                    inv.storedParts.At(i).snapshot.partData.TryGetValue("partRackmounted", ref mounted);
                    if (mounted)
                    {
                        inv.constructorModeInventory.slotButton[inv.storedParts.At(i).slotIndex].enabled = false;
                        inv.constructorModeInventory.slotButton[inv.storedParts.At(i).slotIndex].gameObject.SetActive(false);
                    }
                    else
                    {
                        inv.constructorModeInventory.slotButton[inv.storedParts.At(i).slotIndex].enabled = true;
                        inv.constructorModeInventory.slotButton[inv.storedParts.At(i).slotIndex].gameObject.SetActive(true);
                    }

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
            ConfigNode rackMountPart = partConfig.GetNode("MODULE", "name", "ModuleRackMountPart");

            string requiresPartType = "";

            if (rackMountPart != null)
            {
                rackMountPart.TryGetValue("requiresPartType", ref requiresPartType);
                if (requiresPartType == "" || partType.ToUpper() == "ANY" || requiresPartType.ToUpper() == partType.ToUpper())
                {
                    rackMountableParts.Add(storedPart.snapshot.persistentId, storedPart.slotIndex);

                    KSPEvent mount = new KSPEvent
                    {
                        name = "RackmountButton" + storedPart.slotIndex,
                        active = true,
                        guiActive = true,
                        guiActiveEditor = true,
                        guiActiveUnfocused = true,
                        guiActiveUncommand = true,
                        unfocusedRange = HighLogic.CurrentGame.Parameters.CustomParams<RackMountSettings>().evaDistance,
                        guiName = "<b><color=green>Rackmount</color> " + storedPart.snapshot.partInfo.title + "</b>"
                    };
                    BaseEvent RackmountButton = new BaseEvent(Events, mount.name, () => RackmountButtonPressed(storedPart), mount);
                    RackmountButton.group = rackmountGroup;
                    Events.Add(RackmountButton);
                }
                else if (!unMountableParts.ContainsKey(storedPart.snapshot.persistentId))
                {
                    unMountableParts.Add(storedPart.snapshot.persistentId, storedPart.slotIndex);
                    if (partType == "")
                        ScreenMessages.PostScreenMessage("<color=orange>Part " + storedPart.snapshot.partInfo.title + " can only be mounted on part type of " +
                            rackMountPart.GetValue("requiresPartType") + "!</color>\nIt is currently stored in part without a part type.", 7);
                    else
                        ScreenMessages.PostScreenMessage("<color=orange>Part " + storedPart.snapshot.partInfo.title + " can only be mounted on part type of " +
                            rackMountPart.GetValue("requiresPartType") + "!</color>\nIt is currently stored in a part type of " + partType + ".", 7);
                }
            }
        }

        private void RemoveRackmountButton(uint id, int slot)
        {
            Events.Find(x => x.name == "RackmountButton" + slot).active = false;
            Events.Remove(Events.Find(x => x.name == "RackmountButton" + slot));

            rackMountableParts.Remove(id);
            if (part.PartActionWindow != null)
                part.PartActionWindow.UpdateWindow();
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

        //ModuleRackMountPart adjustments and
        //changes to the host part
        private void RackMountAdjusters(ConfigNode moduleConfigNode)
        {
            //add crew seat.
            if (moduleConfigNode.HasValue("crewSeat"))
            {
                part.CrewCapacity += int.Parse(moduleConfigNode.GetValue("crewSeat"));

                //checks if there's a ShipConfig to work with
                if (ShipConstruction.ShipConfig != null)
                {
                    //gets the existing part manifest for this part
                    if (ShipConstruction.ShipManifest == null)
                        ShipConstruction.ShipManifest = VesselCrewManifest.FromConfigNode(ShipConstruction.ShipConfig);
                    PartCrewManifest manifest = ShipConstruction.ShipManifest.GetPartCrewManifest(part.craftID);

                    //creates a PartCrewManifest object if it doesn't already exist
                    if (manifest == null)
                    {
                        ConfigNode node = new ConfigNode();
                        node.AddValue("part", part.name + "_" + part.craftID);
                        node.AddValue("CrewCapacity", part.CrewCapacity);
                        ShipConstruction.ShipManifest.SetPartManifest(part.craftID, PartCrewManifest.FromConfigNode(node, ShipConstruction.ShipManifest));
                        manifest = ShipConstruction.ShipManifest.GetPartCrewManifest(part.craftID);
                    }

                    var newCrew = new string[part.CrewCapacity];

                    for (int i = 0; i < newCrew.Length; i++)
                    {
                        if (i < manifest.partCrew.Length)
                            newCrew[i] = manifest.partCrew[i];
                        else
                            newCrew[i] = "";
                    }
                    manifest.partCrew = newCrew;
                    manifest.PartInfo.partPrefab.CrewCapacity = part.CrewCapacity;
                    manifest.PartInfo.partConfig.SetValue("CrewCapacity", part.CrewCapacity);
                }
            }
        }

        //checks mounted part for rackmountable modules as well as adjusters to the part
        private void RackmountPart(StoredPart storedPart)
        {
            bool rackMountable;
            ConfigNode partConfig = storedPart.snapshot.partInfo.partConfig;
            
            ConfigNode addedModules = new ConfigNode();
            int storedPartModuleIndex = 0;

            //iterates through all modules on the stored part.
            foreach (ConfigNode moduleConfigNode in partConfig.GetNodes("MODULE"))
            {
                //ModuleRackMountPart adjustments
                if (moduleConfigNode.GetValue("name") == "ModuleRackMountPart")
                    RackMountAdjusters(moduleConfigNode);

                //mount modules
                rackMountable = false;
                moduleConfigNode.TryGetValue("rackMountable", ref rackMountable);
                if (rackMountable)
                {
                    //ModuleScienceExperiment fixes
                    if (moduleConfigNode.GetValue("name") == "ModuleScienceExperiment")
                    {
                        moduleConfigNode.RemoveValue("FxModules");
                    }

                    PartModule partModule = part.AddModule(moduleConfigNode, true);

                    //add module to AvailablePart but only if it's not the 'real' part
                    if (createPart && !HighLogic.LoadedSceneIsEditor && !string.IsNullOrEmpty(originalPart) && originalPart != part.name)
                    {
                        AvailablePart availablePart = PartLoader.getPartInfoByName(part.partInfo.name);
                        availablePart.partPrefab.Awake();

                        var availablePartModule = availablePart.partPrefab.AddModule(moduleConfigNode, true);

                        //wake up Kerbalism Experiments
                        if (availablePartModule.moduleName == "Experiment")
                        {
                            availablePartModule.OnStart(StartState.None);
                            RackMountKerbalism.CompileModuleInfos(availablePartModule);
                        }
                        
                        availablePart.partPrefab.gameObject.SetActive(value: false);
                        availablePart.partConfig.AddNode(moduleConfigNode);
                    }
                    partModule.Awake();
                    partModule.OnActive();

                    int moduleIndex = part.Modules.IndexOf(partModule);

                    ProtoPartModuleSnapshot moduleSnapshot = storedPart.snapshot.FindModule(partModule, storedPartModuleIndex);

                    //ACTIONS (from https://github.com/Angel-125/WildBlueTools/blob/master/Switchers/WBIModuleSwitcher.cs)
                    if (moduleSnapshot.moduleValues.HasNode("ACTIONS"))
                    {
                        ConfigNode actionsNode = moduleSnapshot.moduleValues.GetNode("ACTIONS");

                        foreach (ConfigNode node in actionsNode.nodes)
                        {
                            if (partModule.Actions[node.name] != null)
                            {
                                partModule.Actions[node.name].actionGroup = (KSPActionGroup)Enum.Parse(typeof(KSPActionGroup), node.GetValue("actionGroup"));
                            }
                        }
                    }

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

                        foreach (var node in moduleConfigNode.GetNodes("CONTROLPOINT"))
                        {
                            Vector3 orientation = new Vector3(0, 0, 0);
                            node.TryGetValue("orientation", ref orientation);
                            ControlPoint point = new ControlPoint(node.GetValue("name"), node.GetValue("displayName"), part.transform, orientation);
                            controlPoints.Add(point.name, point);
                        }
                        c.controlPoints = controlPoints;
                    }

                    //craft files may already have the modulePersistentId set
                    if (!moduleSnapshot.moduleValues.HasValue("modulePersistentId"))
                        moduleSnapshot.moduleValues.SetValue("modulePersistentId", partModule.GetPersistentId(), true);
                    partModule.OnStart(part.GetModuleStartState());
                    partModule.OnStartFinished(part.GetModuleStartState());

                }
                storedPartModuleIndex++;
            }
            
            //add or increases resource on part
            foreach (var resource in storedPart.snapshot.resources)
            {
                rackMountable = false;
                var configNode = storedPart.snapshot.partInfo.partConfig.GetNode("RESOURCE", "name", resource.resourceName);
                if (configNode != null)
                {
                    configNode.TryGetValue("rackMountable", ref rackMountable);
                    var partResource = part.Resources.Get(resource.resourceName);

                    if (partResource != null && rackMountable)
                    {
                        partResource.maxAmount += resource.maxAmount;
                        partResource.amount += resource.amount;
                        if(part.PartActionWindow != null)
                            part.PartActionWindow.displayDirty = true;
                    }
                    else if (rackMountable)
                    {
                        resource.Load(part);
                        if (part.PartActionWindow != null)
                            part.PartActionWindow.displayDirty = true;
                    }
                }
            }

            //rackmounted!
            storedPart.snapshot.partData.SetValue("partRackmounted", true, true);

            //sets button and updates PAW
            BaseEvent button = (BaseEvent)Events.Find(x => x.name == "RackmountButton" + storedPart.slotIndex);
            button.guiName = "<b><color=orange>Unmount</color> " + storedPart.snapshot.partInfo.title + "</b>";
            if (part.PartActionWindow != null)
                part.PartActionWindow.UpdateWindow();
        }

        //removes part adjusters
        private bool UnmountAdjusters(ConfigNode moduleConfigNode)
        {
            //tries to remove a seat, returns false if occupied
            //will need to be rethought if other part adjusters can
            //also fail to avoid partial removal
            if (moduleConfigNode.HasValue("crewSeat"))
            {
                //checks to see if seat is full
                if (part.protoModuleCrew.Count > part.CrewCapacity - int.Parse(moduleConfigNode.GetValue("crewSeat")))
                {
                    ScreenMessages.PostScreenMessage($"<color=orange>The seat is still being used! </color> You cannot unmount this part, a kerbal is still sitting in it.", 7);
                    return false;
                }

                part.CrewCapacity -= int.Parse(moduleConfigNode.GetValue("crewSeat"));

                //adjusts crew manifest
                if (ShipConstruction.ShipManifest != null)
                {
                    PartCrewManifest manifest = ShipConstruction.ShipManifest.GetPartCrewManifest(part.craftID);
                    var newCrew = new string[part.CrewCapacity];

                    //creates new manifest, dropping the 'end' of the list
                    for (int i = 0; i < newCrew.Length; i++)
                        newCrew[i] = manifest.partCrew[i];

                    //returns 'end' crew to free roster
                    for (int i = manifest.partCrew.Length - int.Parse(moduleConfigNode.GetValue("crewSeat")); i < manifest.partCrew.Length; i++)
                        ShipConstruction.ShipManifest.RemoveCrewMember(manifest.partCrew[i]);

                    manifest.partCrew = newCrew;
                    manifest.PartInfo.partPrefab.CrewCapacity = part.CrewCapacity;
                    manifest.PartInfo.partConfig.SetValue("CrewCapacity", part.CrewCapacity);
                }
            }

            return true;
        }

        //unmounting mounted modules and undo part adjusters
        private void UnmountPart(StoredPart storedPart)
        {
            //undo ModuleRackMountPart adjustments
            ConfigNode partConfig = storedPart.snapshot.partInfo.partConfig;
            foreach (ConfigNode moduleConfigNode in partConfig.GetNodes("MODULE"))
            {
                //adjust host part.
                //returning false aborts unmount.
                if (moduleConfigNode.GetValue("name") == "ModuleRackMountPart")
                    if (!UnmountAdjusters(moduleConfigNode))
                        return;
            }

            //create list of items to remove
            List<PartModule> removeModules = new List<PartModule>();
            List<ConfigNode> removeNodes = new List<ConfigNode>();

            //update save
            HighLogic.CurrentGame.Updated(GameScenes.FLIGHT);

            //find modules to remove
            foreach (PartModule partModule in part.Modules)
            {
                foreach (var module in storedPart.snapshot.modules)
                {
                    if (module.moduleValues.GetValue("modulePersistentId") == partModule.GetPersistentId().ToString())
                    {
                        //moduleValues only accessible in flight?
                        if (HighLogic.LoadedSceneIsFlight)
                            module.moduleValues = partModule.snapshot.moduleValues;

                        //add to list of modules to remove
                        removeModules.Add(partModule);

                        //add to list of confignodes to remove
                        int i = 1;
                        foreach(ConfigNode node in part.partInfo.partConfig.GetNodes("MODULE"))
                        {
                            if(part.Modules.IndexOf(partModule) == i)
                            {
                                removeNodes.Add(node);
                            }
                            i++;
                        }
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

            if (originalPart != null && originalPart != "" && part.name != originalPart)
            {
                foreach (ConfigNode node in removeNodes)
                {
                    part.protoPartSnapshot.partInfo.partConfig.RemoveNode(node);
                }
            }

            foreach (PartModule partModule in removeModules)
            {
                if (originalPart != null && originalPart != "" && part.name != originalPart)
                {
                    part.partInfo.partPrefab.RemoveModule(part.partInfo.partPrefab.Modules.GetModule(part.Modules.IndexOf(partModule)));
                }
                partModule.OnInactive();
                part.RemoveModule(partModule);
            }

            List<ProtoPartResourceSnapshot> removeResources = new List<ProtoPartResourceSnapshot>();

            foreach (ProtoPartResourceSnapshot resource in storedPart.snapshot.resources)
            {
                bool rackMountable = false;
                var configNode = storedPart.snapshot.partInfo.partConfig.GetNode("RESOURCE", "name", resource.resourceName);
                //checks for resources in storedPart config
                if (configNode != null)
                {
                    configNode.TryGetValue("rackMountable", ref rackMountable);

                    PartResource storedResource = part.Resources.Get(resource.resourceName);
                    if (storedResource != null && rackMountable)
                    {
                        resource.amount = storedResource.amount * (resource.maxAmount / storedResource.maxAmount);
                        storedResource.amount -= resource.amount;
                        storedResource.maxAmount -= resource.maxAmount;
                        if (part.PartActionWindow != null)
                            part.PartActionWindow.displayDirty = true;
                    }
                }
                /*
                else
                {
                    //removes a resource added directly by the module
                    //possible bug since this is to remove Kerbalism process resources
                    part.Resources.Remove(resource.resourceName);
                }
                */
            }

            storedPart.snapshot.partData.SetValue("partRackmounted", false, true);

            //unlocking always happens from paw?
            UIPartActionInventory inventoryUI = (UIPartActionInventory)part.PartActionWindow.ListItems.Find(x => x.GetType() == typeof(UIPartActionInventory));
            inventoryUI.slotButton[storedPart.slotIndex].enabled = true;
            inventoryUI.slotButton[storedPart.slotIndex].gameObject.SetActive(true);

            BaseEvent button = (BaseEvent)Events.Find(x => x.name == "RackmountButton" + storedPart.slotIndex);
            button.guiName = "<b><color=green>Rackmount</color> " + storedPart.snapshot.partInfo.title + "</b>";

            if (part.PartActionWindow != null)
                part.PartActionWindow.UpdateWindow();
        }

        private bool CanRackmount()
        {
            //for debugging
            if (HighLogic.CurrentGame.Parameters.CustomParams<RackMountSettings>().canAlwaysRackmount)
                return true;

            //next check for kerbal on EVA.  
            if (FlightGlobals.ActiveVessel.isEVA)
            {
                ProtoCrewMember crew = FlightGlobals.ActiveVessel.GetVesselCrew()[0];
                float kerbalDistanceToPart = Vector3.Distance(FlightGlobals.ActiveVessel.transform.position, part.collider.ClosestPointOnBounds(FlightGlobals.ActiveVessel.transform.position));
                if (kerbalDistanceToPart < HighLogic.CurrentGame.Parameters.CustomParams<RackMountSettings>().evaDistance && (!HighLogic.CurrentGame.Parameters.CustomParams<RackMountSettings>().requiresEngineer || crew.trait == "Engineer"))
                    return true;
                else
                    return false;
            }

            //finally check onboard crew
            foreach (var crew in vessel.GetVesselCrew())
                if (!HighLogic.CurrentGame.Parameters.CustomParams<RackMountSettings>().requiresEngineer || crew.trait == "Engineer")
                    return true;

            return false;
        }
    }
}