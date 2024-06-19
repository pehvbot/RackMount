using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace RackMount
{
    /// <summary>
    /// Manange loading up the assets
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class VABOrganizerAssets : MonoBehaviour
    {
        public static Dictionary<string, Sprite> Sprites { get; private set; }

        internal static string ASSET_PATH = "GameData/RackMount/Assets/vaborganizer.dat";
        internal static string SPRITE_ATLAS_NAME = "vab-organizer";

        private void Awake()
        {

            Debug.Log("[RM]: Loading UI Prefabs");
            AssetBundle prefabs = AssetBundle.LoadFromFile(Path.Combine(KSPUtil.ApplicationRootPath, ASSET_PATH));


            Sprite[] spriteSheet = prefabs.LoadAssetWithSubAssets<Sprite>(SPRITE_ATLAS_NAME);
            Sprites = new Dictionary<string, Sprite>();
            foreach (Sprite subSprite in spriteSheet)
            {
                Sprites.Add(subSprite.name, subSprite);
            }

            Debug.Log("[RM]: Loaded UI Prefabs");
        }
    }
}
