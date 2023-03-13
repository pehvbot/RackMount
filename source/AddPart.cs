using UnityEngine;
using System.Collections.Generic;
using System;
using System.Reflection;
using static UrlDir;

namespace RackMount
{

    public class AddPart
    {
        public delegate AvailablePart ParsePartOpenDelegate(PartLoader partLoader, UrlDir.UrlConfig urlConfig, ConfigNode node);
        internal static ParsePartOpenDelegate ParsePart = (ParsePartOpenDelegate)Delegate.CreateDelegate(typeof(ParsePartOpenDelegate), null, typeof(PartLoader).GetMethod("ParsePart", BindingFlags.Instance | BindingFlags.NonPublic));

        public delegate void CompilePartInfoDelegate(PartLoader partLoader, AvailablePart newPartInfo, Part part);
        internal static CompilePartInfoDelegate CompilePartInfo = (CompilePartInfoDelegate)Delegate.CreateDelegate(typeof(CompilePartInfoDelegate), null, typeof(PartLoader).GetMethod("CompilePartInfo", BindingFlags.Instance | BindingFlags.NonPublic));

        public delegate void AddVariantsDelegate(PartLoader partLoader, List<PartVariant> variants, AvailablePart part);
        internal static AddVariantsDelegate AddVariants = (AddVariantsDelegate)Delegate.CreateDelegate(typeof(AddVariantsDelegate), null, typeof(PartLoader).GetMethod("AddVariants", BindingFlags.Instance | BindingFlags.NonPublic));

        //create a new part based on an available part
        public static AvailablePart CreatePart(AvailablePart available, string partName = null)
        {
            if(available == null)
            {
                Debug.Log("[RM] no AvailablePart found for CreatePart(AvailablePart, string)");
                return null;
            }
           
            ConfigNode partConfig = available.partConfig.CreateCopy();

            //creates a new name if necessary
            if (partName == null)
                partName = available.name + "-" + Guid.NewGuid();

            //adds values to the ConfigNode stripped out by ParsePart
            partConfig.SetValue("name", partName, true);
            partConfig.SetValue("title", available.title, true);
            partConfig.SetValue("manufacturer", available.manufacturer);
            partConfig.SetValue("description", available.description, true);
            partConfig.SetValue("cost", available.cost, true);
            partConfig.SetValue("bulkheadProfiles", available.bulkheadProfiles, true);
            partConfig.SetValue("tags", available.tags, true);
            partConfig.SetValue("category", "none", true);
            partConfig.SetValue("TechHidden", "True", true);

            return CreatePart(available.partUrlConfig, partConfig);
        }

        //create a new part based on a confignode
        public static AvailablePart CreatePart(UrlConfig urlConfig, ConfigNode partConfig)
        {
            AvailablePart available = ParsePart(PartLoader.Instance, urlConfig, partConfig);

            //not sure if necessary, cargo cult like programming...
            if ((bool)FlightGlobals.fetch)
                FlightGlobals.PersistentLoadedPartIds.Remove(available.partPrefab.persistentId);

            if (available.partPrefab.DragCubes.Cubes.Count == 0)
                DragCubeSystem.Instance.SetupDragCubeCoroutine(available.partPrefab);
            else
                PartLoader.Instance.SetDatabaseConfig(available.partPrefab, available.partPrefab.DragCubes.SaveCubes());

            CompilePartInfo(PartLoader.Instance, available, available.partPrefab);

            if ((bool)FlightGlobals.fetch)
                FlightGlobals.PersistentLoadedPartIds.Remove(available.partPrefab.persistentId);

            PartLoader.Instance.parts.Add(available);
            PartLoader.Instance.loadedParts.Add(available);

            if (available.Variants != null)
                if (available.Variants.Count > 0)
                    AddVariants(PartLoader.Instance, available.Variants, available);

            var ro = PartLoader.Instance.GetType().GetField("APFinderByName", BindingFlags.NonPublic | BindingFlags.Instance);
            var roValue = ro.GetValue(PartLoader.Instance);
            var APFinderByName = roValue.GetType().GetProperty("Item");
            APFinderByName.SetValue(roValue, available, new[] { available.name });

            ro = PartLoader.Instance.GetType().GetField("APFinderByIcon", BindingFlags.NonPublic | BindingFlags.Instance);
            roValue = ro.GetValue(PartLoader.Instance);
            var APFinderByIcon = roValue.GetType().GetProperty("Item");
            APFinderByIcon.SetValue(roValue, available, new[] { available.iconPrefab });

            //wake up any Kerbalism Experiments
            foreach(var module in available.partPrefab.Modules)
            {
                if (module.moduleName == "Experiment")
                    module.OnStart(PartModule.StartState.None);
            }

            Debug.Log("[RM] CreatePart: " + PartLoader.getPartInfoByName(available.name).name);

            return available;
        }
    }
}