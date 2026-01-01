using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MVoxelizer.MVEditor
{
    [CustomEditor(typeof(VoxelGroup))]
    public class VoxelGroupEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            VoxelGroup voxelGroup = (VoxelGroup)target;

            if (GUILayout.Button("Rebuild Voxels"))
            {
                voxelGroup.RebuildVoxels();
            }

            if (GUILayout.Button("Reset Voxels"))
            {
                voxelGroup.ResetVoxels();
            }
        }
    }
}