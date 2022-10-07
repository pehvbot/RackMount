/*
using System;
using KSP.UI.Screens;
using UnityEngine;
namespace RackMount
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class EditorBehavior : MonoBehaviour
    {
        public void Awake()
        {
            Debug.Log("[RM] events!");
            GameEvents.onEditorLoad.Add(OnShipLoaded);
            GameEvents.onEditorShipModified.Add(OnShipModified);
            GameEvents.onEditorPartEvent.Add(OnEditorPartEvent);
            GameEvents.onEditorShipCrewModified.Add(OnEditorShipCrewModified);
        }

        private void OnEditorShipCrewModified(VesselCrewManifest m)
        {
            Debug.Log("[RM] manifest:" + m);
        }
        private void OnShipLoaded(ShipConstruct construct, CraftBrowserDialog.LoadType loadType)
        {
            try
            {
                Debug.Log("Ship loaded, " + construct.Count + " parts. ");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnShipModified(ShipConstruct construct)
        {
            try
            {
                foreach(Part p in construct.parts)
                {
                    if (p.CrewCapacity > 0)
                        Debug.Log("[RM] Crew!");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnEditorPartEvent(ConstructionEventType eventType, Part part)
        {
            try
            {
                Debug.Log("[RM] eventType:" + eventType + " Part:" + part);
                //if (eventType != ConstructionEventType.PartAttached) return;
                //Debug.Log("[RM] crew:" + part.CrewCapacity);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

    }


}
*/
