﻿using UnityEngine;
using System.Collections.Generic;
using System;
using System.Reflection;
using UnityEngine.EventSystems;

namespace RackMount
{
    public class ModuleRackMountPart : PartModule
    {
        [KSPField]
        public bool partRackmountable = true;

        [KSPField]
        public bool autoCalculateVolume = true;

        [KSPField]
        public string requiresPartType = "";

        [KSPField]
        public int crewSeat = 0;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            ModuleCargoPart cargo = (ModuleCargoPart)part.Modules.GetModule("ModuleCargoPart");

            if (autoCalculateVolume && cargo != null)
            {
                if (cargo.packedVolume == 0)
                {
                    Bounds bounds = default(Bounds);
                    foreach (var bound in part.GetRendererBounds())
                        bounds.Encapsulate(bound);
                    float vol = ((float)Math.Round(bounds.size.x * bounds.size.y * bounds.size.z, 2));
                    cargo.packedVolume = vol * 1000f;
                    autoCalculateVolume = false;
                }
            }
        }
    }
}