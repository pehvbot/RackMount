using System;
using UnityEngine;

namespace RackMount
{
    public static class Utilities
    {
        public static float CalculateVolume(Part part, float volumeAdjustPercent=1f, bool useSmaller = true)
        {
            float meshVolume = 0f;

            var meshes = part.FindModelMeshRenderersCached();

            //calculates the volume for each mesh
            //finds the mesh with largest volume
            foreach (var mesh in meshes)
            {
                foreach (var meshFilter in mesh.GetComponents<MeshFilter>())
                {
                    meshVolume = Math.Max(VolumeOfMesh(meshFilter.sharedMesh), meshVolume);
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
            while (meshVolume * 10 < boundsVolume)
                meshVolume *= 10;

            float returnVolume;
            if (useSmaller)
                returnVolume = Math.Min(meshVolume, boundsVolume);
            else
                returnVolume = Math.Max(meshVolume, boundsVolume);

            int round = 3;
            if (returnVolume > 0.005)
                round = 2;

            //always returns at least 1
            return (float)Math.Max(1, Math.Round(returnVolume, round) * 1000f * volumeAdjustPercent);
        }

        private static float SignedVolumeOfTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            float v321 = p3.x * p2.y * p1.z;
            float v231 = p2.x * p3.y * p1.z;
            float v312 = p3.x * p1.y * p2.z;
            float v132 = p1.x * p3.y * p2.z;
            float v213 = p2.x * p1.y * p3.z;
            float v123 = p1.x * p2.y * p3.z;

            return (1.0f / 6.0f) * (-v321 + v231 + v312 - v132 - v213 + v123);
        }

        private static float VolumeOfMesh(Mesh mesh)
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
    }
}

