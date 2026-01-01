using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MVoxelizer.MVEditor
{
    [System.Serializable]
    public class MeshVoxelizerSetting
    {
        public string presetName;
        public bool showProgressBar;

        //public GameObject sourceGameObject = null;
        public MeshVoxelizer.GenerationType generationType;
        public MeshVoxelizer.VoxelSizeType voxelSizeType;
        public int subdivisionLevel;
        public float absoluteVoxelSize;
        public MeshVoxelizer.Precision precision;
        public MeshVoxelizer.UVConversion uvConversion;
        public bool edgeSmoothing;
        public bool applyScaling;
        public bool alphaCutoff;
        public float cutoffThreshold;

        public bool modifyVoxel;
        public Mesh voxelMesh;
        public Vector3 voxelScale;
        public Vector3 voxelRotation;

        //single mesh
        public bool boneWeightConversion;
        public bool innerfaceCulling;
        public bool backfaceCulling;
        public bool optimization;
        public bool compactOutput;

        //separate voxels
        public bool fillCenter;
        public MeshVoxelizer.FillCenterMethod fillMethod;
        public Material centerMaterial;

        public void RecordSetting(MeshVoxelizerEditor meshVoxelizer)
        {
            generationType      = meshVoxelizer.generationType;
            voxelSizeType       = meshVoxelizer.voxelSizeType;
            subdivisionLevel    = meshVoxelizer.subdivisionLevel;
            absoluteVoxelSize   = meshVoxelizer.absoluteVoxelSize;
            precision           = meshVoxelizer.precision;
            uvConversion        = meshVoxelizer.uvConversion;
            edgeSmoothing       = meshVoxelizer.edgeSmoothing;
            applyScaling        = meshVoxelizer.applyScaling;
            alphaCutoff         = meshVoxelizer.alphaCutoff;
            cutoffThreshold     = meshVoxelizer.cutoffThreshold;
            modifyVoxel         = meshVoxelizer.modifyVoxel;
            voxelMesh           = meshVoxelizer.voxelMesh;
            voxelScale          = meshVoxelizer.voxelScale;
            boneWeightConversion= meshVoxelizer.boneWeightConversion;
            innerfaceCulling    = meshVoxelizer.innerfaceCulling;
            backfaceCulling     = meshVoxelizer.backfaceCulling;
            optimization        = meshVoxelizer.optimization;
            fillCenter          = meshVoxelizer.fillCenter;
            fillMethod          = meshVoxelizer.fillMethod;
            centerMaterial      = meshVoxelizer.centerMaterial;
            compactOutput       = meshVoxelizer.compactOutput;
            showProgressBar     = meshVoxelizer.showProgressBar;
        }

        public void SetPresetName(string name)
        {
            presetName = name;
        }
    }

    //[CreateAssetMenu(fileName = "MeshVoxelizerPreset", menuName = "MeshVoxelizerWindow.meshVoxelizer/MeshVoxelizerPreset", order = 10)]
    public class MeshVoxelizerPreset : ScriptableObject
    {
        public List<MeshVoxelizerSetting> settings = new List<MeshVoxelizerSetting>();
    }
}