using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MVoxelizer
{
    [ExecuteInEditMode]
    public class VoxelGroup : MonoBehaviour
    {
        public Mesh voxelMesh = null;
        public Material[] voxelMaterials;
        public Material centerMaterial;
        public MeshVoxelizer.UVConversion uvType = MeshVoxelizer.UVConversion.SourceMesh;
        public Vector3 voxelScale;
        public Vector3 voxelRotation;
        [HideInInspector] public float ratio = 1.0f;
        [HideInInspector] public Voxel[] voxels;
        [HideInInspector] public Vector3[] voxelPosition;
        [HideInInspector] public Vector2[] uvs;
        [HideInInspector] public int[] submesh;
        [HideInInspector] public GameObject[] centerVoxels;
        [HideInInspector] public Vector3[] centerVoxelPosition;

        [HideInInspector] public Mesh m_mesh = null;
        Dictionary<Material, Texture2D> tex2D;

        private void Awake()
        {
            if (m_mesh == null) RebuildVoxels();
        }

        public void RebuildVoxels()
        {
            if (voxelMesh == null) return;
            UpdateVoxel();
            if (uvType == MeshVoxelizer.UVConversion.SourceMesh)
            {
                for (int i = 0; i < voxels.Length; ++i)
                {
                    if (voxels[i] == null) { CreateVoxel(i); }
                    voxels[i].UpdateVoxel(m_mesh, uvs[i]);
                }
            }
            else
            {
                for (int i = 0; i < voxels.Length; ++i)
                {
                    if (voxels[i] == null) { CreateVoxel(i); }
                    voxels[i].GetComponent<MeshFilter>().sharedMesh = m_mesh;
                }
            }
            if (centerVoxels != null)
            {
                for (int i = 0; i < centerVoxels.Length; ++i)
                {
                    if (centerVoxels[i] == null) { CreateCenterVoxel(i); }
                    centerVoxels[i].GetComponent<MeshRenderer>().sharedMaterial = centerMaterial;
                }
            }
        }

        public void ResetVoxels()
        {
            for (int i = 0; i < voxels.Length; ++i)
            {
                if (voxels[i] == null) continue;
                voxels[i].transform.localPosition = voxelPosition[i];
                voxels[i].transform.localScale = Vector3.one;
                voxels[i].transform.localRotation = Quaternion.identity;
            }
            if (centerVoxels != null)
            {
                for (int i = 0; i < centerVoxels.Length; ++i)
                {
                    if (centerVoxels[i] == null) continue;
                    centerVoxels[i].transform.localPosition = centerVoxelPosition[i];
                    centerVoxels[i].transform.localScale = Vector3.one;
                    centerVoxels[i].transform.localRotation = Quaternion.identity;
                }
            }
        }

        public void CreateVoxel(int i)
        {
            GameObject voxelObject = new GameObject("voxel");
            voxelObject.AddComponent<MeshFilter>();
            voxelObject.AddComponent<MeshRenderer>().sharedMaterial = voxelMaterials[submesh[i]];
            voxelObject.transform.parent = transform;
            voxelObject.transform.localPosition = voxelPosition[i];
            voxels[i] = voxelObject.AddComponent<Voxel>();
        }

        public void CreateCenterVoxel(int i)
        {
            GameObject voxelObject = new GameObject("center voxel");
            voxelObject.AddComponent<MeshFilter>().sharedMesh = m_mesh;
            voxelObject.AddComponent<MeshRenderer>().sharedMaterial = centerMaterial;
            voxelObject.transform.parent = transform;
            voxelObject.transform.localPosition = centerVoxelPosition[i];
            centerVoxels[i] = voxelObject;
        }

        void UpdateVoxel()
        {
            if (m_mesh == null) m_mesh = new Mesh();
            m_mesh.Clear();
            Vector3[] vertices = voxelMesh.vertices;
            Vector2[] uvs = voxelMesh.uv;
            Vector3 r = ratio * voxelScale;
            Quaternion rotation = Quaternion.Euler(voxelRotation);
            if (uvType == MeshVoxelizer.UVConversion.None)
            {
                for (int i = 0; i < vertices.Length; ++i)
                {
                    Vector3 v = new Vector3(vertices[i].x * r.x, vertices[i].y * r.y, vertices[i].z * r.z);
                    v = rotation * v;
                    vertices[i] = v;
                    uvs[i] = Vector2.zero;
                }
            }
            else
            {
                for (int i = 0; i < vertices.Length; ++i)
                {
                    Vector3 v = new Vector3(vertices[i].x * r.x, vertices[i].y * r.y, vertices[i].z * r.z);
                    v = rotation * v;
                    vertices[i] = v;
                }
            }
            m_mesh.vertices = vertices;
            m_mesh.uv = uvs;
            m_mesh.normals = voxelMesh.normals;
            m_mesh.triangles = voxelMesh.triangles;
        }

        public Color GetVoxelColor(Voxel v)
        {
            if (tex2D == null) tex2D = new Dictionary<Material, Texture2D>();
            Material m = v.GetComponent<MeshRenderer>().material;
            if (!tex2D.ContainsKey(m)) tex2D[m] = Util.MVHelper.GetTexture2D(m.mainTexture);
            Vector2 uv = v.m_mesh.uv[0];
            Color color = tex2D[m].GetPixel((int)(tex2D[m].width * uv.x), (int)(tex2D[m].height * uv.y));
            return color;
        }

        private void OnDestroy()
        {
            if (m_mesh != null)
            {
                DestroyImmediate(m_mesh);
            }
            if (tex2D != null)
            {
                foreach (var v in tex2D.Values) if (v != null) GameObject.DestroyImmediate(v);
                tex2D = null;
            }
        }
    }
}