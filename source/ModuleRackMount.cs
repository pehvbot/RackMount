﻿using UnityEngine;
using System.Collections.Generic;
using System;
using System.Reflection;
using UnityEngine.EventSystems;

namespace RackMount
{
    public class ModuleRackMount : PartModule
    {
        //removes existing ModuleCommand.  ModuleCommand is a special snowflake and needs additional logic to mount properly
        //this starts with having an existing ModuleCommand then removing it.  This fixes issues with launch checks and on rails issues.
        //enable if you plan on mounting ModuleCommand
        [KSPField]
        public bool removeModuleCommand = false;

        [KSPField]
        public bool autoCalculateVolume = true;

        [KSPField]
        public float volumeAdjustPercent = 1.0f;

        [KSPField]
        public string partType = "";

        [KSPField(isPersistant = true)]
        public int startingCrewCapacity = -1;

        Dictionary<uint, int> rackMountableParts = new Dictionary<uint, int>();
        Dictionary<uint, int> unMountableParts = new Dictionary<uint, int>();

        private ModuleInventoryPart inv;

        private BasePAWGroup rackmountGroup = new BasePAWGroup("rackmountGroup", "Rackmount Inventory", false);

        private bool onLoad;

        //needed to turn on vessel renaming when a ModuleCommand part is installed.
        //For some reason putting it in OnUpdate() allows it to be displayed
        //on load.
        private bool displayVesselNaming = false;

        public override void OnLoad(ConfigNode node)
        {
            inv = part.Modules.GetModule<ModuleInventoryPart>();

            //saves initial crew capacity
            if (startingCrewCapacity == -1)
                startingCrewCapacity = part.CrewCapacity;

            //sets it to 1 so the crew manifest is visible for loaded vessels
            //if it's 0 the crew panels are not visible by default
            //gets set to original value during OnInitialization
            if(HighLogic.LoadedSceneIsFlight)
                part.CrewCapacity = 1;

            //checks for no volume set
            if (autoCalculateVolume && inv.packedVolumeLimit == 0)
                inv.packedVolumeLimit = CalculateVolume();


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
            if (inv.storedParts != null)
            {
                //add buttons
                for (int i = 0; i < inv.storedParts.Count; i++)
                {
                    AddRackmountButton(inv.storedParts.At(i));
                }
                //add mounted modules
                for (int i = 0; i < inv.storedParts.Count; i++)
                {
                    bool mounted = false;
                    inv.storedParts.At(i).snapshot.partData.TryGetValue("partRackmounted", ref mounted);
                    if (mounted)
                        RackmountPart(inv.storedParts.At(i));
                }
            }
            onLoad = false;
        }

        public override void OnInitialize()
        {
            part.CrewCapacity = startingCrewCapacity;

            if(inv == null)
               inv = part.Modules.GetModule<ModuleInventoryPart>();

            if (inv.storedParts != null)
            {
                //add mounted modules
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
                
                if (ShipConstruction.ShipConfig != null)
                {
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
            base.OnInitialize();
        }

        public override void OnStart(StartState state)
        {
            if (inv == null)
                inv = part.Modules.GetModule<ModuleInventoryPart>();

            base.OnStart(state);

            inv.Fields["InventorySlots"].group = rackmountGroup;
            inv.Fields["InventorySlots"].guiName = null;
            if (HighLogic.LoadedSceneIsFlight)
                inv.Fields["InventorySlots"].group.startCollapsed = true;
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
            base.OnUpdate();
        }

        private void Update()
        {
            List<uint> currentParts = new List<uint>();
            Dictionary<uint, int> buttonsToRemove = new Dictionary<uint, int>();


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
            if (part.PartActionWindow != null)
            {
                UIPartActionInventory inventoryUI = (UIPartActionInventory)part.PartActionWindow.ListItems.Find(x => x.GetType() == typeof(UIPartActionInventory));
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
            //checks for construction mode and locks/unlocks mounted items
            UIPartActionInventory constructionUI = inv.constructorModeInventory;
            if (constructionUI != null)
            {
                for (int i = 0; i < inv.storedParts.Count; i++)
                {
                    bool mounted = false;
                    inv.storedParts.At(i).snapshot.partData.TryGetValue("partRackmounted", ref mounted);
                    if (mounted)
                    {
                        constructionUI.slotButton[inv.storedParts.At(i).slotIndex].enabled = false;
                        constructionUI.slotButton[inv.storedParts.At(i).slotIndex].gameObject.SetActive(false);
                    }
                    else
                    {
                        constructionUI.slotButton[inv.storedParts.At(i).slotIndex].enabled = true;
                        constructionUI.slotButton[inv.storedParts.At(i).slotIndex].gameObject.SetActive(true);
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

        public float CalculateVolume()
        {
            float maxVolume = 0f;
            var meshes = part.FindModelMeshRenderersCached();

            //calculates the volume for each mesh
            //finds the mesh with largest volume
            foreach (var mesh in meshes)
            {
                foreach (var meshFilter in mesh.GetComponents<MeshFilter>())
                {
                    maxVolume = Math.Max(VolumeOfMesh(meshFilter.sharedMesh), maxVolume);
                }
            }

            //gets the bounds as a sanity check
            Bounds bounds = default(Bounds);
            foreach (var bound in part.GetRendererBounds())
            {
                bounds.Encapsulate(bound);
            }
            float boundsVolume = bounds.size.x * bounds.size.y * bounds.size.z;

            //ugly way of making sure the mesh volume is scaled correctly
            //some numbers look correct but are orders of magnitude too small
            while (maxVolume * 10 < boundsVolume)
                maxVolume *= 10;

            //returns the sanest number
            return (float)Math.Round(Math.Min(boundsVolume, maxVolume), 2) * 1000f * volumeAdjustPercent;
        }


        public float SignedVolumeOfTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            float v321 = p3.x * p2.y * p1.z;
            float v231 = p2.x * p3.y * p1.z;
            float v312 = p3.x * p1.y * p2.z;
            float v132 = p1.x * p3.y * p2.z;
            float v213 = p2.x * p1.y * p3.z;
            float v123 = p1.x * p2.y * p3.z;

            return (1.0f / 6.0f) * (-v321 + v231 + v312 - v132 - v213 + v123);
        }

        public float VolumeOfMesh(Mesh mesh)
        {
            float volume = 0;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 p1 = vertices[triangles[i + 0]];
                Vector3 p2 = vertices[triangles[i + 1]];
                Vector3 p3 = vertices[triangles[i + 2]];
                volume += SignedVolumeOfTriangle(p1, p2, p3);
            }
            return Mathf.Abs(volume);
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
                //checks if crew addition has already happened
                if (!onLoad)
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
        }

        //checks all stored parts for rackmountable modules as well as adjusters to the part
        private void RackmountPart(StoredPart storedPart)
        {
            bool rackMountable = true;
            ConfigNode partConfig = storedPart.snapshot.partInfo.partConfig;

            //iterates through all modules on the stored part.
            foreach (ConfigNode moduleConfigNode in partConfig.GetNodes("MODULE"))
            {
                //ModuleRackMountPart adjustments
                if (moduleConfigNode.GetValue("name") == "ModuleRackMountPart")
                    RackMountAdjusters(moduleConfigNode);

                //mount modules
                if (moduleConfigNode.TryGetValue("rackMountable", ref rackMountable))
                {
                    //ModuleScienceExperiment fixes
                    if (moduleConfigNode.GetValue("name") == "ModuleScienceExperiment")
                    {
                        moduleConfigNode.RemoveValue("FxModules");
                    }

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
                    //and are already active and started
                    if (!onLoad)
                    {
                        moduleSnapshot.moduleValues.AddValue("modulePersistentId", partModule.GetPersistentId());
                        partModule.OnActive();
                        partModule.OnStart(part.GetModuleStartState());
                        partModule.OnStartFinished(part.GetModuleStartState());
                    }
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
                if (part.protoModuleCrew.Count > part.CrewCapacity - int.Parse(moduleConfigNode.GetValue("crewSeat")))
                {
                    ScreenMessages.PostScreenMessage($"<color=orange>The seat is still being used! </color> You cannot unmount this part, a kerbal is still sitting in it.", 7);
                    return false;
                }

                part.CrewCapacity -= int.Parse(moduleConfigNode.GetValue("crewSeat"));

                if (ShipConstruction.ShipManifest != null)
                {
                    PartCrewManifest manifest = ShipConstruction.ShipManifest.GetPartCrewManifest(part.craftID);
                    var newCrew = new string[part.CrewCapacity];
                    for (int i = 0; i < newCrew.Length; i++)
                        newCrew[i] = manifest.partCrew[i];
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
            {
                partModule.OnInactive();
                part.RemoveModule(partModule);
            }

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
            inventoryUI.slotButton[storedPart.slotIndex].gameObject.SetActive(true);

            BaseEvent button = (BaseEvent)Events.Find(x => x.name == "RackmountButton" + storedPart.slotIndex);
            button.guiName = "<b><color=green>Rackmount</color> " + storedPart.snapshot.partInfo.title + "</b>";
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