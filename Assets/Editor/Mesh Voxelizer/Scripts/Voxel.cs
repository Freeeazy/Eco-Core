using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MVoxelizer
{
    [ExecuteInEditMode]
    public class Voxel : MonoBehaviour
    {
        [HideInInspector] public Mesh m_mesh = null;

        public void UpdateVoxel(Mesh mesh, Vector2 uv)
        {
            if (m_mesh == null) m_mesh = new Mesh();
            m_mesh.Clear();
            Vector2[] uvs = new Vector2[mesh.uv.Length]; 
            for (int i = 0; i < uvs.Length; ++i) uvs[i] = uv;
            m_mesh.vertices = mesh.vertices;
            m_mesh.uv = uvs;
            m_mesh.normals = mesh.normals;
            m_mesh.triangles = mesh.triangles;
            GetComponent<MeshFilter>().sharedMesh = m_mesh;
        }

        private void OnDestroy()
        {
            if (m_mesh != null)
            {
                DestroyImmediate(m_mesh);
            }
        }
    }
}