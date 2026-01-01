using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor;

namespace MVoxelizer.MVEditor
{
    public class MeshVoxelizerEditor : MeshVoxelizer
    {
        public const string OUTPUT_PATH = "Assets/Voxelized Meshes";

        public bool showProgressBar = true;
        public bool compactOutput = true;
        public bool exportVoxelizedTexture = false;
        
        public void ApplySetting(MeshVoxelizerSetting setting)
        {
            generationType      = setting.generationType;
            voxelSizeType       = setting.voxelSizeType;
            subdivisionLevel    = setting.subdivisionLevel;
            absoluteVoxelSize   = setting.absoluteVoxelSize;
            precision           = setting.precision;
            uvConversion        = setting.uvConversion;
            edgeSmoothing       = setting.edgeSmoothing;
            applyScaling        = setting.applyScaling;
            alphaCutoff         = setting.alphaCutoff;
            cutoffThreshold     = setting.cutoffThreshold;
            modifyVoxel         = setting.modifyVoxel;
            voxelMesh           = setting.voxelMesh;
            voxelScale          = setting.voxelScale;
            voxelRotation       = setting.voxelRotation;
            boneWeightConversion= setting.boneWeightConversion;
            innerfaceCulling    = setting.innerfaceCulling;
            backfaceCulling     = setting.backfaceCulling;
            optimization        = setting.optimization;
            fillCenter          = setting.fillCenter;
            fillMethod          = setting.fillMethod;
            centerMaterial      = setting.centerMaterial;
            compactOutput       = setting.compactOutput;
            showProgressBar     = setting.showProgressBar;
        }

        public override GameObject VoxelizeMesh()
        {
            GameObject go = base.VoxelizeMesh();
            if (go != null)
            {
                go.transform.SetSiblingIndex(sourceGameObject.transform.GetSiblingIndex() + 1);
                go.transform.localPosition = sourceGameObject.transform.localPosition;
                go.transform.localRotation = sourceGameObject.transform.localRotation;
                if (!applyScaling) go.transform.localScale = sourceGameObject.transform.localScale;
                Undo.RegisterCreatedObjectUndo(go, "Undo instantiating " + go.name);
                Selection.activeGameObject = go;
            }

            if (showProgressBar) EditorUtility.ClearProgressBar();
            return go;
        }

        protected override bool GenerateMeshMaterialsOpt()
        {
            if (CancelProgress("Generating textures... ", 0)) { return false; }

            m_result.voxelizedMaterials = new Material[m_source.materials.Length];
            for (int i = 0; i < m_source.materials.Length; ++i)
            {
                m_result.voxelizedMaterials[i] = GameObject.Instantiate(m_source.materials[i]);
                m_result.voxelizedMaterials[i].name = m_source.materials[i].name + "_Voxelized";
            }

            Dictionary<Texture, Texture> texDict = new Dictionary<Texture, Texture>();
            foreach (var mat in m_result.voxelizedMaterials)
            {
                Shader shader = mat.shader;
                for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
                {
                    if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                    {
                        string tName = ShaderUtil.GetPropertyName(shader, i);
                        Texture tex = mat.GetTexture(tName);
                        if (tex != null && uvConversion == UVConversion.SourceMesh)
                        {
                            if (texDict.ContainsKey(tex))
                            {
                                mat.SetTexture(tName, texDict[tex]);
                            }
                            else
                            {
                                Texture newTex = m_opt.tInfo.CreateTexture(tex);
                                mat.SetTexture(tName, newTex);
                                texDict.Add(tex, newTex);
                            }
                        }
                    }
                }
            }

            return true;
        }

        protected override GameObject GenerateResult()
        {
            if (CancelProgress("Generating result... ", 0)) { return null; }
            
            m_result.voxelizedMesh = new Mesh();
            m_result.voxelizedMesh.name = m_source.mesh.name + " Voxelized";
#if UNITY_2017_3_OR_NEWER
            if (m_result.vertices.Count > 65535) m_result.voxelizedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
#endif
            m_result.voxelizedMesh.SetVertices(m_result.vertices);
            m_result.voxelizedMesh.SetNormals(m_result.normals);
            m_result.voxelizedMesh.subMeshCount = m_result.triangles.Count;
            for (int i = 0; i < m_result.triangles.Count; ++i)
            {
                m_result.voxelizedMesh.SetTriangles(m_result.triangles[i], i);
            }
            m_result.voxelizedMesh.SetUVs(0, m_result.uv);
            if (m_result.uv2.Count != 0) m_result.voxelizedMesh.SetUVs(1, m_result.uv2);
            if (m_result.uv3.Count != 0) m_result.voxelizedMesh.SetUVs(2, m_result.uv3);
            if (m_result.uv4.Count != 0) m_result.voxelizedMesh.SetUVs(3, m_result.uv4);
            if (m_result.boneWeights.Count != 0)
            {
                m_result.voxelizedMesh.boneWeights = m_result.boneWeights.ToArray();
                m_result.voxelizedMesh.bindposes = m_source.mesh.bindposes;
            }

            //save result
            int counter = 1;
            string folderPath = OUTPUT_PATH;
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string guid = AssetDatabase.CreateFolder("Assets", "Voxelized Meshes");
                folderPath = AssetDatabase.GUIDToAssetPath(guid);
            }
            string assetName = sourceGameObject.name.Trim();
            if (!compactOutput)
            {
                counter = 1;
                string tempPath = assetName + " Voxelized";
                while (AssetDatabase.IsValidFolder(folderPath + "/" + tempPath))
                {
                    counter++;
                    tempPath = assetName + " Voxelized " + counter.ToString();
                }
                string guid = AssetDatabase.CreateFolder("Assets/Voxelized Meshes", tempPath);
                folderPath = AssetDatabase.GUIDToAssetPath(guid);
            }

            GameObject go = null;

            //skinned mesh renderer
            //save mesh asset only
            if (m_source.skinnedMeshRenderer != null)
            {
                counter = 1;
                string assetPath = folderPath + "/" + assetName + " Voxelized.asset";
                while (File.Exists(assetPath))
                {
                    counter++;
                    assetPath = folderPath + "/" + assetName + " Voxelized " + counter.ToString() + ".asset";
                }

                AssetDatabase.CreateAsset(m_result.voxelizedMesh, assetPath);
                AssetDatabase.SaveAssets();

                go = GameObject.Instantiate(sourceGameObject, sourceGameObject.transform.parent);
                go.GetComponent<SkinnedMeshRenderer>().sharedMesh = m_result.voxelizedMesh;
                go.GetComponent<SkinnedMeshRenderer>().sharedMaterials = m_result.voxelizedMaterials;
                go.name = assetName + " Voxelized";
                if (counter > 1) go.name += " " + counter.ToString();
            }
            //mesh renderer
            //save prefab and instantiate
            else
            {
                counter = 1;
                string assetPath = folderPath + "/" + assetName + " Voxelized.prefab";
                while (File.Exists(assetPath))
                {
                    counter++;
                    assetPath = folderPath + "/" + assetName + " Voxelized " + counter.ToString() + ".prefab";
                }

                go = new GameObject(assetName + " Voxelized");
                if (counter > 1) go.name += " " + counter.ToString();
#if UNITY_2018_3_OR_NEWER
                GameObject result = PrefabUtility.SaveAsPrefabAssetAndConnect(go, assetPath, InteractionMode.UserAction);
#else
                GameObject result = PrefabUtility.CreatePrefab(assetPath, go);
#endif
                GameObject.DestroyImmediate(go);

                //save mesh
                if (compactOutput) AssetDatabase.AddObjectToAsset(m_result.voxelizedMesh, assetPath);
                else AssetDatabase.CreateAsset(m_result.voxelizedMesh, folderPath + "/" + m_result.voxelizedMesh.name + ".asset");

                //save materials and textures
                if (m_result.voxelizedMaterials != null && m_result.voxelizedMaterials.Length != 0)
                {
                    foreach (var mat in m_result.voxelizedMaterials)
                    {
                        Shader shader = mat.shader;
                        for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
                        {
                            if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                            {
                                string tName = ShaderUtil.GetPropertyName(shader, i);
                                Texture tex = mat.GetTexture(tName);
                                if (tex != null && !AssetDatabase.Contains(tex))
                                {
                                    if (compactOutput) AssetDatabase.AddObjectToAsset(tex, assetPath);
                                    else
                                    {
                                        AssetDatabase.CreateAsset(tex, folderPath + "/" + tex.name + ".asset");
                                        if (exportVoxelizedTexture)
                                        {
                                            byte[] img = ImageConversion.EncodeToPNG((Texture2D)tex);
                                            File.WriteAllBytes(folderPath + "/" + tex.name + ".png", img);
                                        }
                                    }
                                }
                            }
                        }
                        if (!AssetDatabase.Contains(mat))
                        {
                            if (compactOutput) AssetDatabase.AddObjectToAsset(mat, assetPath);
                            else AssetDatabase.CreateAsset(mat, folderPath + "/" + mat.name + ".mat");
                        }
                    }
                }

                if (m_source.meshFilter != null)
                {
                    result.AddComponent<MeshFilter>().sharedMesh = m_result.voxelizedMesh;
                }
                if (m_source.meshRenderer != null)
                {
                    result.AddComponent<MeshRenderer>().sharedMaterials = m_result.voxelizedMaterials;
                }
                
                AssetDatabase.SaveAssets();
#if UNITY_2018_3_OR_NEWER
                PrefabUtility.SavePrefabAsset(result);
#endif

                go = GameObject.Instantiate(result, sourceGameObject.transform.parent);
                go.name = result.name;
            }
            return go;
        }

        protected override bool CancelProgress(string msg, float value)
        {
            return showProgressBar && EditorUtility.DisplayCancelableProgressBar("Mesh Voxelizer", msg, value);
        }
    }
}