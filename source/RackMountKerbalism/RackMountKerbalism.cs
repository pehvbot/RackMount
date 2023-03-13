using System;
using UnityEngine;
using KERBALISM;
using System.Collections.Generic;

[assembly: KSPAssemblyDependency("KerbalismBootstrap", 0, 0)]
namespace RackMount
{

    public static class RackMountKerbalism
    {
        static public void CompileModuleInfos(PartModule experiment)
        {
            Experiment e = (Experiment)experiment;
            e.ExpInfo.CompileModuleInfos();
        }
    }
}
