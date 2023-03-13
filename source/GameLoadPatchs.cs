using HarmonyLib;
using UnityEngine;
using System.Reflection;
using KSP.Localization;
using System.IO;
using static PartModule;
using PreFlightTests;
using System.Collections.Generic;

namespace RackMount
{
    //scrubs out RackMount part names for originalPart names when a ship is loaded from cache
    //removes old .RM craft files
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class OnEditorLoad : MonoBehaviour
    {
        private void Awake()
        {
            if(ShipConstruction.ShipConfig != null)
            {
                foreach(ConfigNode part in ShipConstruction.ShipConfig.GetNodes("PART"))
                {
                    string[] splitName = part.GetValue("part").Split('_');

                    //i don't understand this name!
                    if (splitName.Length != 2)
                    {
                        Debug.Log("[RM] Malformed part name for part:" + part.GetValue("part"));
                        return;
                    }

                    foreach (ConfigNode module in part.GetNodes("MODULE"))
                    {
                        if(module.GetValue("name") == "ModuleRackMount")
                        {
                            if(!string.IsNullOrEmpty(module.GetValue("originalPart")))
                            {
                                part.SetValue("part", module.GetValue("originalPart") + "_" + splitName[1]);
                                module.SetValue("originalPart", "");
                            }
                        }
                    }
                }
            }
            string saveFolder = KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder;
            DirectoryInfo VAB = new DirectoryInfo(saveFolder + "/Ships/VAB");
            DirectoryInfo SPH = new DirectoryInfo(saveFolder + "/Ships/SPH");

            FileInfo[] VABShips = VAB.GetFiles("*.RM");
            FileInfo[] SPHShips = SPH.GetFiles("*.RM");

            foreach(var ship in VABShips)
            {
                File.Delete(ship.ToString());
            }

            foreach (var ship in SPHShips)
            {
                File.Delete(ship.ToString());
            }
        }
    }

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class OnGameLoadPatch : MonoBehaviour
    {
        private void Start()
        {
            var harmony = new Harmony("com.rackmount.ongameload");
            harmony.PatchAll();
        }

        public static ConfigNode AddPartsFromSave(ConfigNode saveFile)
        {
            ConfigNode game = saveFile.GetNode("GAME");

            if (game == null)
                return null;

            ConfigNode flightState = game.GetNode("FLIGHTSTATE");

            if (flightState == null)
                return null;

            foreach (ConfigNode vessel in flightState.GetNodes("VESSEL"))
            {
                foreach (ConfigNode part in vessel.GetNodes("PART"))
                {
                    AvailablePart available = null;
                    ConfigNode rackMount = part.GetNode("MODULE", "name", "ModuleRackMount");
                    if (rackMount != null)
                    {
                        bool createPart = false;
                        rackMount.TryGetValue("createPart", ref createPart);
                        if (createPart)
                        {
                            string originalPart = rackMount.GetValue("originalPart");
                            if (!string.IsNullOrEmpty(originalPart))
                            {
                                available = AddPart.CreatePart(PartLoader.getPartInfoByName(originalPart));
                                part.SetValue("name", available.name);
                            }

                            AddRackmountedModules(part, available);
                        }
                    }
                }
            }
            return saveFile;
        }

        public static void AddRackmountedModules(ConfigNode part, AvailablePart available)
        {
            ConfigNode inventory = part.GetNode("MODULE", "name", "ModuleInventoryPart");
            if (inventory != null && available != null)
            {
                foreach (ConfigNode stored in inventory.GetNode("STOREDPARTS").GetNodes("STOREDPART"))
                {
                    bool rackMounted = false;
                    stored.GetNode("PART").GetNode("PARTDATA").TryGetValue("partRackmounted", ref rackMounted);
                    if (rackMounted)
                    {
                        ConfigNode partConfig = PartLoader.getPartInfoByName(stored.GetValue("partName")).partConfig;

                        foreach (ConfigNode moduleConfigNode in partConfig.GetNodes("MODULE"))
                        {
                            bool moduleRackmountable = false;
                            moduleConfigNode.TryGetValue("rackMountable", ref moduleRackmountable);
                            if (moduleRackmountable)
                            {
                                available.partPrefab.Awake();
                                var availablePartModule = available.partPrefab.AddModule(moduleConfigNode, true);

                                available.partPrefab.gameObject.SetActive(false);
                                available.partConfig.AddNode(moduleConfigNode);

                                //wakes up Kerbalism Experiment modules
                                if (availablePartModule.moduleName == "Experiment")
                                {
                                    availablePartModule.OnStart(StartState.None);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    
    [HarmonyPatch(typeof(QuickSaveLoad), "quickLoad")]
    internal class QuickLoadPrefix
    {
        public static void Prefix(QuickSaveLoad __instance)
        {
            ConfigNode saveFile = ConfigNode.Load(KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/" + Localizer.Format("#autoLOC_6002266") + ".sfs");
            saveFile = OnGameLoadPatch.AddPartsFromSave(saveFile);
            saveFile.Save(KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/" + Localizer.Format("#autoLOC_6002266") + ".sfs");
        }
    }

    [HarmonyPatch(typeof(LoadGameDialog), "OnButtonLoad")]
    internal class OnButtonLoadPrefix
    {
        public static void Prefix(LoadGameDialog __instance)
        {
            string selectedGame = "persistent";
            string directory = "";

            var persistentObject = __instance.GetType().GetField("persistent", BindingFlags.NonPublic | BindingFlags.Instance);
            bool persistent = (bool)persistentObject.GetValue(__instance);

            if (!persistent)
            {
                var selectedGameObject = __instance.GetType().GetField("selectedGame", BindingFlags.NonPublic | BindingFlags.Instance);
                selectedGame = (string)selectedGameObject.GetValue(__instance);

                var directoryObject = __instance.GetType().GetField("directory", BindingFlags.NonPublic | BindingFlags.Instance);
                directory = (string)directoryObject.GetValue(__instance);
            }
            else
            {
                var selectedGameObject = __instance.GetType().GetField("selectedGame", BindingFlags.NonPublic | BindingFlags.Instance);
                directory = (string)selectedGameObject.GetValue(__instance);
            }

            ConfigNode saveFile = ConfigNode.Load(KSPUtil.ApplicationRootPath + "saves/" + directory + "/" + selectedGame + ".sfs");
            saveFile = OnGameLoadPatch.AddPartsFromSave(saveFile);
            saveFile.Save(KSPUtil.ApplicationRootPath + "saves/" + directory + "/" + selectedGame + ".sfs");
        }
    }

    [HarmonyPatch(typeof(EditorLogic), "GetStockPreFlightCheck")]
    internal class GetStockPreFlightCheck
    {
        public static void Postfix(PreFlightCheck __result)
        {
            var testsObject = __result.GetType().GetField("tests", BindingFlags.NonPublic | BindingFlags.Instance);
            List<IPreFlightTest> tests = (List<IPreFlightTest>)testsObject.GetValue(__result);
            IPreFlightTest remove = null;
            foreach (var test in tests)
            {
                if (test.GetType() == typeof(PreFlightTests.NoControlSources))
                    remove = test;
            }
            if (remove != null)
            {
                tests.Remove(remove);
                tests.Add(new RackmountedControlSources());
            }
        }
    }


    [HarmonyPatch(typeof(EditorLogic), "goForLaunch")]
    internal class GoForLaunchPatch
    {
        public static void Prefix(EditorLogic __instance)
        {
            //Get savedCraftPath via reflection so we can write to it as well
            var savedCraftPathObject = __instance.GetType().GetField("savedCraftPath", BindingFlags.NonPublic | BindingFlags.Instance);
            string savedCraftPath = (string)savedCraftPathObject.GetValue(__instance);

            //load original craft file and copies it
            ConfigNode saveFile = ConfigNode.Load(savedCraftPath).CreateCopy();

            if (saveFile == null)
                return;

            bool saveCraftfile = false;

            //iterates over the list of parts in the vessel
            foreach (ConfigNode part in saveFile.GetNodes("PART"))
            {
                string[] splitName = part.GetValue("part").Split('_');

                //i don't understand this name!
                if (splitName.Length != 2)
                {
                    Debug.Log("[RM] Malformed part name for part:" + part.GetValue("part"));
                    return;
                }

                //looks for ModuleRackMount which should generate a new part on launch
                //potentially spams a lot of new parts for reverted vessels
                //unclear how KCT will react to this
                ConfigNode rackMountModule = part.GetNode("MODULE", "name", "ModuleRackMount");
                if (rackMountModule != null)
                {
                    bool createPart = false;
                    rackMountModule.TryGetValue("createPart", ref createPart);

                    if (createPart)
                    {
                        if (string.IsNullOrEmpty(rackMountModule.GetValue("originalPart")))
                        {
                            //sets originalPart value for 'new' parts
                            rackMountModule.SetValue("originalPart", splitName[0]);
                            AvailablePart available = AddPart.CreatePart(PartLoader.getPartInfoByName(rackMountModule.GetValue("originalPart")));
                            if (available != null)
                            {
                                saveCraftfile = true;
                                part.SetValue("part", available.name + "_" + splitName[1]);
                            }
                            OnGameLoadPatch.AddRackmountedModules(part, available);
                        }
                    }
                }
            }
            
            if (saveCraftfile)
            {
                //change path to new craft file
                savedCraftPath = savedCraftPath + ".RM";
                savedCraftPathObject.SetValue(__instance, savedCraftPath);

                //save new file
                saveFile.Save(savedCraftPath);
            }
        }
    }
}