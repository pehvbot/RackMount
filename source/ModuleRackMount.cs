using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System;
using CommNet;
using Expansions.Missions.Adjusters;

namespace RackMount
{
    public class ModuleRackMount : PartModule
    {
        [KSPField]
        public bool autoCalculateVolume = true;

        [KSPField]
        public float volumeAdjustPercent = 1.0f;

        [KSPField]
        public bool autoCalculateEmptyMass = true;

        [KSPField]
        public float massSurfaceArea = 0.03f;

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

        //airlock configs
        //base code taken from RP-1
        //https://github.com/KSP-RO/RP-0
        [KSPField(isPersistant =true)]
        public bool enableAirlocks = false;
        [KSPField(isPersistant = true)]
        int partHasAirlock = 0;

        protected List<Collider> airlocks = new List<Collider>();
        protected Transform airlock = null;

        public override void OnLoad(ConfigNode node)
        {
            if (!this.enabled)
                return;

            inv = part.Modules.GetModule<ModuleInventoryPart>();

            //aborts module if the part doesn't have an inventory
            if (inv == null)
            {
                return;
            }

            //saves initial crew capacity
            if (startingCrewCapacity == -1)
                startingCrewCapacity = part.CrewCapacity;

            //resets airlock count
            if (HighLogic.LoadedSceneIsEditor)
                partHasAirlock = 0;

            //checks for no volume set
            if (autoCalculateVolume && inv.packedVolumeLimit == 0)
                inv.packedVolumeLimit = Utilities.CalculateVolume(part, volumeAdjustPercent);

            if (autoCalculateEmptyMass && createPart && part.mass == 0)
            {
                //bullshit number for 'thickness' of the walls based on temp and crash tolerance
                //defaults used to normalize maxTemp and crashTolerance
                //weighs crashTolerance at 4x maxTemp for mass
                double partThickness = (((part.maxTemp / 2000) + (part.crashTolerance / 9) * 4) / 5);
                partThickness = 2.5 / (1 + Math.Exp(-0.5 * (partThickness - 3))) + .5;

                double calculatedMass = Utilities.CalculateSurfaceArea(part) * partThickness * massSurfaceArea;
                int round = 1;
                if (calculatedMass < 1) round = 2;
                if (calculatedMass < 0.1) round = 3;
                part.mass = (float)Math.Round(calculatedMass, round);
            }

            base.OnLoad(node);

            //checks for stored parts
            if (inv.storedParts != null)
            {
                //add buttons
                for (int i = 0; i < inv.storedParts.Count; i++)
                {
                    if (PartLoader.getPartInfoByName(inv.storedParts.At(i).partName) == null)
                    {
                        Debug.Log("[RM] The part " + inv.storedParts.At(i).partName + " no longer exists and is being removed from the inventory.");
                        ScreenMessages.PostScreenMessage($"<color=orange>The part " + inv.storedParts.At(i).partName + " no longer exists and is being removed from the inventory</color>.\nAny rackmounted modules or settings have been removed.", 7);

                        inv.storedParts.Remove(i);
                    }
                    else
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
                            if (HighLogic.LoadedSceneIsEditor || !createPart)
                            {
                                RackmountPart(inv.storedParts.At(i));
                            }
                        }
                    }
                }
            }
        }

       
        public override void OnInitialize()
        {
            if (!this.enabled)
                return;

            if (inv == null)
                inv = part.Modules.GetModule<ModuleInventoryPart>();

            //aborts module if the part doesn't have an inventory
            if (inv == null)
            {
                this.enabled = false;
                return;
            }

            //adds CrewCapacity for launch
            AddCrew();
            base.OnInitialize();
        }

        public override void OnAwake()
        {
            if (!this.enabled)
                return;

            base.OnAwake();

            airlocks.Clear();

            foreach (var c in part.GetComponentsInChildren<Collider>())
                if (c.gameObject.tag == "Airlock")
                    airlocks.Add(c);

            airlock = part.airlock;
        }

        private void Start()
        {
            //adds CrewCapacity for loading in Editor
            if (this.enabled && (HighLogic.LoadedSceneIsEditor || !createPart))
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
                this.enabled = false;
                return;
            }

            base.OnStart(state);

            inv.Fields["InventorySlots"].group = rackmountGroup;
            inv.Fields["InventorySlots"].guiName = null;

        }

        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);

            //activates vessel naming
            if (part.Modules.Contains<ModuleCommand>())
            {
                part.Events["SetVesselNaming"].guiActive = true;
                part.Events["SetVesselNaming"].guiActiveEditor = true;
            }
        }

        private void Update()
        {
            if (!this.enabled)
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
        }


        protected void FixedUpdate()
        {
            //airlock configs
            //base code taken from RP-1
            //https://github.com/KSP-RO/RP-0

            if (!enableAirlocks || !HighLogic.LoadedSceneIsFlight || vessel == null || vessel.mainBody == null || airlocks == null)
                return;

            bool evaOK = partHasAirlock > 0 || (vessel.mainBody == Planetarium.fetch.Home &&
                (vessel.situation == Vessel.Situations.LANDED
                    || vessel.situation == Vessel.Situations.PRELAUNCH
                    || vessel.situation == Vessel.Situations.SPLASHED
                    || (vessel.situation == Vessel.Situations.FLYING && vessel.altitude < 20000)));

            foreach (var c in airlocks)
            {
                if (evaOK)
                {
                    if (c.gameObject.tag != "Airlock")
                        c.gameObject.tag = "Airlock";
                }
                else
                {
                    if (c.gameObject.tag == "Airlock")
                        c.gameObject.tag = "Untagged";
                }
            }

            if (evaOK)
            {
                if (part.airlock != airlock)
                    part.airlock = airlock;
            }
            else
            {
                part.airlock = null;
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
        }

        private void RackmountButtonPressed(StoredPart storedPart)
        {
            if (CanRackmount())
            {
                bool mounted = false;
                storedPart.snapshot.partData.TryGetValue("partRackmounted", ref mounted);
                if (mounted)
                {
                    //checks for running KERBALISM.ProcessControllers
                    //can't unmount if they are running.
                    if(HasRunningProcessController(storedPart))
                        ScreenMessages.PostScreenMessage("<color=orange>A Kerbalism Process Controller is running!</color>\n\nYou cannot unmount this part until you turn this process off.", 7);
                    else
                        UnmountPart(storedPart);
                }
                else
                    RackmountPart(storedPart);
            }
            else
            {
                if (HighLogic.CurrentGame.Parameters.CustomParams<RackMountSettings>().requiresEngineer)
                    ScreenMessages.PostScreenMessage("<color=orange>You need a Kerbal Engineer to rackmount or unmount parts while in flight!</color>\n\nYou need a Kerbal Engineer either on the vessel or on EVA near by", 7);
                else
                    ScreenMessages.PostScreenMessage("<color=orange>You need a Kerbal to rackmount or unmount parts while in flight!</color>\n\nYou need a Kerbal either on the vessel or on EVA near by", 7);
            }

        }

        //ModuleRackMountPart adjustments and
        //changes to the host part
        private void RackMountAdjusters(ConfigNode moduleConfigNode)
        {
            //icrements hasAirlock
            //done like this in case multiple airlocks are added
            if(moduleConfigNode.HasValue("hasAirlock"))
            {
                bool hasAirlock = false;
                moduleConfigNode.TryGetValue("hasAirlock", ref hasAirlock);
                if (hasAirlock)
                    partHasAirlock++;
            }

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
                    manifest.PartInfo.partPrefab.isControlSource = Vessel.ControlLevel.FULL;

                    //adds crew to the first command part
                    if (HighLogic.LoadedSceneIsEditor)
                    {
                        var defaulRoster = HighLogic.CurrentGame.CrewRoster.DefaultCrewForVessel(ShipConstruction.ShipConfig).PartManifests[0];
                        if (defaulRoster.PartID == manifest.PartID)
                        {
                            int seatNumber = 0;
                            foreach (var crewMember in defaulRoster.GetPartCrew())
                            {
                                //checks to see if there is crew available and hasn't already been assigned to the part
                                if (crewMember != null && !manifest.Contains(crewMember))
                                    manifest.AddCrewToSeat(crewMember, seatNumber);
                                seatNumber++;
                            }
                        }
                    }
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
                        resource.amount = 0;
                        if(part.PartActionWindow != null)
                            part.PartActionWindow.displayDirty = true;
                    }
                    else if (rackMountable)
                    {
                        resource.Load(part);
                        resource.amount = 0;
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
                    ScreenMessages.PostScreenMessage($"<color=orange>The seat is still being used! </color>\n\nYou cannot unmount this part, a kerbal is still sitting in it.", 7);
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

            bool hasAirlock = false;
            moduleConfigNode.TryGetValue("hasAirlock", ref hasAirlock);
            if (hasAirlock)
                partHasAirlock--;

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
            }
            storedPart.snapshot.partData.SetValue("partRackmounted", false, true);

            //unlocking always happens from paw?
            UIPartActionInventory inventoryUI = (UIPartActionInventory)part.PartActionWindow.ListItems.Find(x => x.GetType() == typeof(UIPartActionInventory));
            inventoryUI.slotButton[storedPart.slotIndex].enabled = true;
            inventoryUI.slotButton[storedPart.slotIndex].gameObject.SetActive(true);

            BaseEvent button = (BaseEvent)Events.Find(x => x.name == "RackmountButton" + storedPart.slotIndex);
            button.guiName = "<b><color=green>Rackmount</color> " + storedPart.snapshot.partInfo.title + "</b>";

        }

        //checks for running KERBALISM.ProcessControllers
        //Unmounting them while running creates problems with phantom resources.
        private bool HasRunningProcessController(StoredPart storedPart)
        {
            foreach (PartModule partModule in part.Modules)
            {
                if (partModule.moduleName == "ProcessController")
                {
                    foreach (var module in storedPart.snapshot.modules)
                    {
                        if (module.moduleValues.GetValue("modulePersistentId") == partModule.GetPersistentId().ToString())
                        {
                            if (partModule.Fields.GetValue<bool>("running") == true)
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool CanRackmount()
        {
            //for editor or when debugging
            if (HighLogic.CurrentGame.Parameters.CustomParams<RackMountSettings>().canAlwaysRackmount || HighLogic.LoadedSceneIsEditor)
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