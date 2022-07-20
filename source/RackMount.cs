using UnityEngine;
using System.Collections.Generic;

namespace RackMount
{
    public class ModuleRMCargoPart : ModuleCargoPart
    {
        public override void OnPartCreatedFomInventory(ModuleInventoryPart moduleInventoryPart)
        {
            if (moduleInventoryPart.GetType() == typeof(ModuleRMInventoryPart))
            {
                ModuleRMInventoryPart moduleRMInventoryPart = (ModuleRMInventoryPart)moduleInventoryPart;
                foreach(PartModule partModule in part.Modules)
                {
                    string modulePersistentId = "";
                    partModule.snapshot.moduleValues.TryGetValue("modulePersistentId", ref modulePersistentId);

                    foreach (PartModule storedModule in moduleInventoryPart.part.Modules)
                    {
                        if(modulePersistentId == storedModule.PersistentId.ToString())
                        {
                            GamePersistence.SaveGame("persistent.sfs", HighLogic.SaveFolder, SaveMode.BACKUP);
                            partModule.Load(storedModule.snapshot.moduleValues);
                        }
                    }
                }
            }
            base.OnPartCreatedFomInventory(moduleInventoryPart);
        }
    }

    public class ModuleRMInventoryPart : ModuleInventoryPart
    {
        public ConfigNode AddedParts;
        
        public override void OnLoad(ConfigNode node)
        {

            base.OnLoad(node);
            AddedParts = new ConfigNode();
            if (storedParts != null)
            {
                for (int i = 0; i < storedParts.Count; i++)
                {
                    AddModules(storedParts.At(i));
                }
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }

        public override void OnUpdate()
        {
            ConfigNode check = new ConfigNode();
            ConfigNode currentParts = new ConfigNode();
            for (int i = 0; i < storedParts.Count; i++)
            {
                currentParts.AddNode(storedParts.At(i).snapshot.persistentId.ToString());
                if (!AddedParts.TryGetNode(storedParts.At(i).snapshot.persistentId.ToString(), ref check))
                    AddModules(storedParts.At(i));
            }
            foreach (var p in AddedParts.GetNodes())
            {
                if (!currentParts.TryGetNode(p.name, ref check))
                    RemoveModules(p);
            }

            base.OnUpdate();
        }

        //adds rackmountable modules for added storedPart
        private void AddModules(StoredPart storedPart)
        {
            bool rackMountable = true;
            ConfigNode partConfig = storedPart.snapshot.partInfo.partConfig;
            AddedParts.AddNode(storedPart.snapshot.persistentId.ToString());
            foreach (ConfigNode moduleConfigNode in partConfig.GetNodes("MODULE"))
            {
                if (moduleConfigNode.TryGetValue("rackMountable", ref rackMountable))
                {
                    PartModule partModule = part.AddModule(moduleConfigNode, true);
                    int moduleIndex = part.Modules.IndexOf(partModule);
                    ProtoPartModuleSnapshot moduleSnapshot = storedPart.snapshot.FindModule(partModule, moduleIndex);

                    part.LoadModule(moduleSnapshot.moduleValues, ref moduleIndex);
                    moduleSnapshot.moduleValues.AddValue("modulePersistentId", partModule.GetPersistentId());
                    AddedParts.GetNode(storedPart.snapshot.persistentId.ToString()).AddValue("modulePersistentId", partModule.GetPersistentId());
                }
            }
            
            part.ModulesOnStart();
        }

        //removed rackmounted modules for removed part
        private void RemoveModules(ConfigNode storedPart)
        {
            List<PartModule> removeModules = new List<PartModule>();

            foreach(PartModule partModule in part.Modules)
            {
                foreach (string moduleId in storedPart.GetValues())
                {
                    if(moduleId == partModule.GetPersistentId().ToString())
                    {
                        removeModules.Add(partModule);
                    }
                }
            }
            foreach (PartModule partModule in removeModules)
            {
                part.RemoveModule(partModule);
            }
            AddedParts.RemoveNode(storedPart.name);
        }
    }
}