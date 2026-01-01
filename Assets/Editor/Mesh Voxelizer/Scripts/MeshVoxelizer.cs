using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using MVoxelizer.Util;

namespace MVoxelizer
{
    public class MeshVoxelizer
    {
        public enum Precision
        {
            Low, Standard, High
        }

        public enum VoxelSizeType
        {
            Subdivision, AbsoluteSize
        }

        public enum GenerationType
        {
            SingleMesh, SeparateVoxels
        }

        public enum UVConversion
        {
            None, SourceMesh, VoxelMesh
        }

        public enum FillCenterMethod
        {
            ScanlineXAxis, ScanlineYAxis, ScanlineZAxis
        }

        public const int MAX_SUBDIVISION = 500;
        public const float SAMPLE_THRESHOLD = 0.05f;
        public const float EDGE_SMOOTHING_THRESHOLD = 0.90f;

        //settings
        public GameObject sourceGameObject = null;
        public GenerationType generationType = GenerationType.SingleMesh;
        public VoxelSizeType voxelSizeType = VoxelSizeType.Subdivision;
        public int subdivisionLevel = 1; 
        public float absoluteVoxelSize = 10000;
        public Precision precision = Precision.Standard;
        public UVConversion uvConversion = UVConversion.SourceMesh;
        public bool edgeSmoothing = false;
        public bool applyScaling = false;
        public bool alphaCutoff = false;
        public float cutoffThreshold = 0.5f;

        //voxel
        public bool modifyVoxel = false;
        public Mesh voxelMesh = null;
        public Vector3 voxelScale = Vector3.one;
        public Vector3 voxelRotation = Vector3.zero;

        //single mesh
        public bool boneWeightConversion = true;
        public bool innerfaceCulling = true;
        public bool backfaceCulling = false;
        public bool optimization = false;   //do not optimize for animated objects
        private bool instantiateResult = true;

        //separate voxels
        public bool fillCenter = false;
        public FillCenterMethod fillMethod = FillCenterMethod.ScanlineYAxis;
        public Material centerMaterial = null;

        //voxelization data
        protected MVSource m_source;
        protected MVGrid m_grid;
        protected MVResult m_result;
        protected MVOptimization m_opt;
        protected Dictionary<MVInt3, MVVoxel> voxelDict = new Dictionary<MVInt3, MVVoxel>();

        public GameObject VoxelizeMesh(bool instantiateResult)
        {
            this.instantiateResult = instantiateResult;
            GameObject go = VoxelizeMesh();
            this.instantiateResult = true;
            return go;
        }

        public virtual GameObject VoxelizeMesh()
        {
            if (sourceGameObject == null) return null;

            GameObject result = null;
            Clear();

            if (!Initialization()) { Clear(); return null; };
            if (!AnalyzeMesh()) { Clear(); return null; };
            if (!ProcessVoxelData()) { Clear(); return null; };

            //separate voxels
            if (generationType == GenerationType.SeparateVoxels)
            {
                List<Vector3> centerVoxels = new List<Vector3>();
                if (!FillCenterSpace(centerVoxels)) { Clear(); return null; };
                result = GenerateVoxels(centerVoxels);
            }

            //single mesh
            else
            {
                if (!CullFaces()) { Clear(); return null; };
                m_result = new MVResult();
                m_result.voxelMesh = voxelMesh;
                m_result.grid = m_grid;
                m_result.Init(m_source.mesh.subMeshCount);
                if (optimization && !modifyVoxel)
                {
                    m_opt = new MVOptimization();
                    m_opt.voxelMesh = voxelMesh;
                    m_opt.grid = m_grid;
                    m_opt.result = m_result;
                    if (!DoOptimization()) { Clear(); return null; };
                    if (!GenerateMeshVerticesOpt()) { Clear(); return null; };
                    if (!GenerateMeshUVsOpt()) { Clear(); return null; };
                    if (!GenerateMeshMaterialsOpt()) { Clear(); return null; };
                }
                else
                {
                    m_result.voxelizedMaterials = m_source.materials;
                    if (!GenerateMeshVertices()) { Clear(); return null; };
                    if (!GenerateMeshUVs()) { Clear(); return null; };
                    if (!GenerateMeshBoneWeights()) { Clear(); return null; };
                }
                result = GenerateResult();
            }

            Clear();
            return result;
        }

        void Clear()
        {
            m_source = null;
            m_grid = null;
            m_result = null;
            m_opt = null;
            voxelDict.Clear();
        }

        protected virtual bool Initialization()
        {
            if (CancelProgress("Initializing... ", 0)) { return false; }

            m_source = new MVSource();
            m_source.Init(sourceGameObject);
            if (m_source.mesh == null) return false;

            m_grid = new MVGrid();
            m_grid.Init(this, m_source);

            if (voxelMesh == null) voxelMesh = MVHelper.GetDefaultVoxelMesh();
            if (centerMaterial == null) centerMaterial = MVHelper.GetDefaultVoxelMaterial();

            return true;
        }

        protected virtual bool AnalyzeMesh()
        {
            if (CancelProgress("Analyzing mesh... ", 0)) { return false; }
            int counter = 0;
            int total = m_source.mesh.triangles.Length / 3;
            int rem = Mathf.CeilToInt(total * 0.05f);

            float triangleStep = 0.0f;
            switch (precision)
            {
                case Precision.Low:
                    triangleStep = m_grid.unitSize * 0.25f;
                    break;
                case Precision.High:
                    triangleStep = m_grid.unitSize * 0.0625f;
                    break;
                default:
                    triangleStep = m_grid.unitSize * 0.125f;
                    break;
            }
            
            for (int subMesh = 0; subMesh < m_source.mesh.subMeshCount; ++subMesh)
            {
                int start = (int)m_source.mesh.GetIndexStart(subMesh);
                int end = start + (int)m_source.mesh.GetIndexCount(subMesh);
                for (int i = start; i < end; i += 3)
                {
                    if (counter % rem == 0 && CancelProgress("Analyzing mesh... ", (float)counter / total)) { return false; }
                    else counter++;

                    MVTriangleData tData = new MVTriangleData(m_source, m_grid, voxelDict, i, subMesh, triangleStep, applyScaling);
                    tData.Scan();
                }
            }
            return true;
        }

        protected virtual bool ProcessVoxelData()
        {
            if (CancelProgress("Processing voxels... ", 0)) { return false; }

            float avg = voxelDict.First().Value.sampleCount;
            foreach (var v in voxelDict)
            {
                v.Value.centerPos += m_grid.origin;
                avg = (avg + v.Value.sampleCount) * 0.5f;
                voxelDict.TryGetValue(new MVInt3(v.Key.x,     v.Key.y,     v.Key.z - 1), out v.Value.v_back);
                voxelDict.TryGetValue(new MVInt3(v.Key.x,     v.Key.y,     v.Key.z + 1), out v.Value.v_forward);
                voxelDict.TryGetValue(new MVInt3(v.Key.x - 1, v.Key.y,     v.Key.z),     out v.Value.v_left);
                voxelDict.TryGetValue(new MVInt3(v.Key.x + 1, v.Key.y,     v.Key.z),     out v.Value.v_right);
                voxelDict.TryGetValue(new MVInt3(v.Key.x,     v.Key.y + 1, v.Key.z),     out v.Value.v_up);
                voxelDict.TryGetValue(new MVInt3(v.Key.x,     v.Key.y - 1, v.Key.z),     out v.Value.v_down);
            }

            Texture2D[] tex2D = null;
            if (alphaCutoff)
            {
                tex2D = new Texture2D[m_source.materials.Length];
                for (int i = 0; i < m_source.materials.Length; ++i)
                {
                    Texture tex = m_source.materials[i].mainTexture;
                    tex2D[i] = tex == null ? null : MVHelper.GetTexture2D(tex);
                }
            }

            List<MVInt3> approxList = new List<MVInt3>();
            List<MVInt3> discardList = new List<MVInt3>();
            int minSample = Mathf.FloorToInt(avg * SAMPLE_THRESHOLD);
            float bound = m_grid.unitSize * 0.5f * EDGE_SMOOTHING_THRESHOLD;
            foreach (var v in voxelDict)
            {
                if (v.Value.sampleCount <= minSample)
                {
                    discardList.Add(v.Key);
                    continue;
                }

                if (tex2D != null && tex2D[v.Value.subMesh] != null)
                {
                    Vector2 uv = m_source.GetUVCoord(v.Value);
                    Color color = tex2D[v.Value.subMesh].GetPixel((int)(tex2D[v.Value.subMesh].width * uv.x), (int)(tex2D[v.Value.subMesh].height * uv.y));
                    if (color.a <= cutoffThreshold)
                    {
                        discardList.Add(v.Key);
                        continue;
                    }
                }

                if (edgeSmoothing)
                {
                    bool replace = false;
                    bool remove = true;
                    if (v.Value.vertPos.z > bound && v.Value.back)
                    {
                        replace = true;
                        remove = remove && v.Value.v_forward != null;
                    }
                    if (v.Value.vertPos.z < -bound && v.Value.forward)
                    {
                        replace = true;
                        remove = remove && v.Value.v_back != null;
                    }
                    if (v.Value.vertPos.x > bound && v.Value.left)
                    {
                        replace = true;
                        remove = remove && v.Value.v_right != null;
                    }
                    if (v.Value.vertPos.x < -bound && v.Value.right)
                    {
                        replace = true;
                        remove = remove && v.Value.v_left != null;
                    }
                    if (v.Value.vertPos.y < -bound && v.Value.up)
                    {
                        replace = true;
                        remove = remove && v.Value.v_down != null;
                    }
                    if (v.Value.vertPos.y > bound && v.Value.down)
                    {
                        replace = true;
                        remove = remove && v.Value.v_up != null;
                    }
                    if (replace && remove)
                    {
                        approxList.Add(v.Key);
                    }
                }
            }

            foreach (var v in discardList)
            {
                if (voxelDict[v].v_forward != null) { voxelDict[v].v_forward.v_back = null; }
                if (voxelDict[v].v_back != null) { voxelDict[v].v_back.v_forward = null; }
                if (voxelDict[v].v_left != null) { voxelDict[v].v_left.v_right = null; }
                if (voxelDict[v].v_right != null) { voxelDict[v].v_right.v_left = null; }
                if (voxelDict[v].v_up != null) { voxelDict[v].v_up.v_down = null; }
                if (voxelDict[v].v_down != null) { voxelDict[v].v_down.v_up = null; }
                voxelDict.Remove(v);
            }

            foreach (var v in approxList)
            {
                if (voxelDict[v].v_forward != null) { voxelDict[v].v_forward.back = true; voxelDict[v].v_forward.v_back = null; }
                if (voxelDict[v].v_back != null) { voxelDict[v].v_back.forward = true; voxelDict[v].v_back.v_forward = null; }
                if (voxelDict[v].v_left != null) { voxelDict[v].v_left.right = true; voxelDict[v].v_left.v_right = null; }
                if (voxelDict[v].v_right != null) { voxelDict[v].v_right.left = true; voxelDict[v].v_right.v_left = null; }
                if (voxelDict[v].v_up != null) { voxelDict[v].v_up.down = true; voxelDict[v].v_up.v_down = null; }
                if (voxelDict[v].v_down != null) { voxelDict[v].v_down.up = true; voxelDict[v].v_down.v_up = null; }
                voxelDict.Remove(v);
            }

            discardList.Clear();
            approxList.Clear();
            if (tex2D != null) foreach (var v in tex2D) if (v != null) GameObject.DestroyImmediate(v);

            return true;
        }

        protected virtual bool FillCenterSpace(List<Vector3> centerVoxels)
        {
            if (CancelProgress("Filling center space... ", 0)) { return false; }

            switch (fillMethod)
            {
                case FillCenterMethod.ScanlineXAxis:
                    for (int y = 0; y < m_grid.unitCount.y; ++y)
                    {
                        for (int z = 0; z < m_grid.unitCount.z; ++z)
                        {
                            List<int> indice = new List<int>();
                            for (int x = 0; x < m_grid.unitCount.x; ++x)
                            {
                                MVInt3 pos = new MVInt3(x, y, z);
                                if (voxelDict.ContainsKey(pos)) indice.Add(x);
                            }
                            for (int i = 0; i < indice.Count - 1; ++i)
                            {
                                MVInt3 start = new MVInt3(indice[i], y, z);
                                if (voxelDict[start].right) continue;
                                MVInt3 end = new MVInt3(indice[i + 1], y, z);
                                if (voxelDict[end].left) continue;
                                for (int j = indice[i] + 1; j < indice[i + 1]; ++j)
                                {
                                    Vector3 pos = new Vector3();
                                    pos.x = m_grid.unitSize * (j + 0.5f);
                                    pos.y = m_grid.unitSize * (y + 0.5f);
                                    pos.z = m_grid.unitSize * (z + 0.5f);
                                    pos += m_grid.origin;
                                    centerVoxels.Add(pos);
                                }
                            }
                        }
                    }
                    break;
                case FillCenterMethod.ScanlineYAxis:
                    for (int z = 0; z < m_grid.unitCount.z; ++z)
                    {
                        for (int x = 0; x < m_grid.unitCount.x; ++x)
                        {
                            List<int> indice = new List<int>();
                            for (int y = 0; y < m_grid.unitCount.y; ++y)
                            {
                                MVInt3 pos = new MVInt3(x, y, z);
                                if (voxelDict.ContainsKey(pos)) indice.Add(y);
                            }
                            for (int i = 0; i < indice.Count - 1; ++i)
                            {
                                MVInt3 start = new MVInt3(x, indice[i], z);
                                if (voxelDict[start].up) continue;
                                MVInt3 end = new MVInt3(x, indice[i + 1], z);
                                if (voxelDict[end].down) continue;
                                for (int j = indice[i] + 1; j < indice[i + 1]; ++j)
                                {
                                    Vector3 pos = new Vector3();
                                    pos.x = m_grid.unitSize * (x + 0.5f);
                                    pos.y = m_grid.unitSize * (j + 0.5f);
                                    pos.z = m_grid.unitSize * (z + 0.5f);
                                    pos += m_grid.origin;
                                    centerVoxels.Add(pos);
                                }
                            }
                        }
                    }
                    break;
                case FillCenterMethod.ScanlineZAxis:
                    for (int x = 0; x < m_grid.unitCount.x; ++x)
                    {
                        for (int y = 0; y < m_grid.unitCount.y; ++y)
                        {
                            List<int> indice = new List<int>();
                            for (int z = 0; z < m_grid.unitCount.z; ++z)
                            {
                                MVInt3 pos = new MVInt3(x, y, z);
                                if (voxelDict.ContainsKey(pos)) indice.Add(z);
                            }
                            for (int i = 0; i < indice.Count - 1; ++i)
                            {
                                MVInt3 start = new MVInt3(x, y, indice[i]);
                                if (voxelDict[start].forward) continue;
                                MVInt3 end = new MVInt3(x, y, indice[i + 1]);
                                if (voxelDict[end].back) continue;
                                for (int j = indice[i] + 1; j < indice[i + 1]; ++j)
                                {
                                    Vector3 pos = new Vector3();
                                    pos.x = m_grid.unitSize * (x + 0.5f);
                                    pos.y = m_grid.unitSize * (y + 0.5f);
                                    pos.z = m_grid.unitSize * (j + 0.5f);
                                    pos += m_grid.origin;
                                    centerVoxels.Add(pos);
                                }
                            }
                        }
                    }
                    break;
                default:
                    break;
            }
            return true;
        }

        protected virtual GameObject GenerateVoxels(List<Vector3> centerVoxels)
        {
            if (CancelProgress("Generating voxels... ", 0)) { return null; }
            int counter = 0;
            int total = voxelDict.Count;
            int rem = Mathf.CeilToInt(total * 0.05f);

            VoxelGroup voxelGroup = new GameObject(sourceGameObject.name + " Voxels").AddComponent<VoxelGroup>();
            voxelGroup.voxelMesh = voxelMesh;
            voxelGroup.ratio = modifyVoxel ? m_grid.unitVoxelRatio.x / voxelScale.x : m_grid.unitVoxelRatio.x;
            voxelGroup.voxelScale = modifyVoxel ? voxelScale : Vector3.one;
            voxelGroup.voxelRotation = modifyVoxel ? voxelRotation : Vector3.zero;
            voxelGroup.uvType = uvConversion;
            voxelGroup.voxelMaterials = m_source.materials;
            voxelGroup.voxels = new Voxel[voxelDict.Count];
            voxelGroup.voxelPosition = new Vector3[voxelDict.Count];
            voxelGroup.submesh = new int[voxelDict.Count];
            if (uvConversion == UVConversion.SourceMesh)
            {
                voxelGroup.uvs = new Vector2[voxelDict.Count];
            }
            if (fillCenter)
            {
                voxelGroup.centerMaterial = centerMaterial;
                voxelGroup.centerVoxelPosition = centerVoxels.ToArray();
                voxelGroup.centerVoxels = new GameObject[centerVoxels.Count];
            }

            int temp = 0;
            foreach (MVVoxel voxel in voxelDict.Values)
            {
                if (counter % rem == 0 && CancelProgress("Generating voxels... ", (float)counter / total)) { return null; }
                else counter++;

                voxelGroup.voxelPosition[temp] = voxel.centerPos;
                voxelGroup.submesh[temp] = voxel.subMesh;
                if (uvConversion == UVConversion.SourceMesh) voxelGroup.uvs[temp] = m_source.GetUVCoord(voxel);
                ++temp;
            }

            voxelGroup.RebuildVoxels();
            voxelGroup.transform.parent = sourceGameObject.transform.parent;
            voxelGroup.transform.SetSiblingIndex(sourceGameObject.transform.GetSiblingIndex() + 1);
            voxelGroup.transform.localPosition = sourceGameObject.transform.localPosition;
            if (!applyScaling) voxelGroup.transform.localScale = sourceGameObject.transform.localScale;
            voxelGroup.transform.localRotation = sourceGameObject.transform.localRotation;

            return voxelGroup.gameObject;
        }

        protected virtual bool CullFaces()
        {
            if (modifyVoxel || (!backfaceCulling && !innerfaceCulling)) return true;

            if (CancelProgress("Culling faces... ", 0)) { return false; }

            if (backfaceCulling && innerfaceCulling)
            {
                foreach (MVVoxel voxel in voxelDict.Values)
                {
                    voxel.forward = voxel.v_forward == null && voxel.forward;
                    voxel.back = voxel.v_back == null && voxel.back;
                    voxel.left = voxel.v_left == null && voxel.left;
                    voxel.right = voxel.v_right == null && voxel.right;
                    voxel.up = voxel.v_up == null && voxel.up;
                    voxel.down = voxel.v_down == null && voxel.down;
                }
            }
            else if (innerfaceCulling)
            {
                foreach (MVVoxel voxel in voxelDict.Values)
                {
                    voxel.forward = voxel.v_forward == null;
                    voxel.back = voxel.v_back == null;
                    voxel.left = voxel.v_left == null;
                    voxel.right = voxel.v_right == null;
                    voxel.up = voxel.v_up == null;
                    voxel.down = voxel.v_down == null;
                }
            }
            return true;
        }

        protected virtual bool GenerateMeshVerticesOpt()
        {
            if (CancelProgress("Generating mesh... ", 0)) { return false; }

            m_opt.AddSliceVertices(m_opt.sliceBack, Vector3.back);
            m_opt.AddSliceVertices(m_opt.sliceForward, Vector3.forward);
            m_opt.AddSliceVertices(m_opt.sliceLeft, Vector3.left);
            m_opt.AddSliceVertices(m_opt.sliceRight, Vector3.right);
            m_opt.AddSliceVertices(m_opt.sliceUp, Vector3.up);
            m_opt.AddSliceVertices(m_opt.sliceDown, Vector3.down);
            return true;
        }

        protected virtual bool GenerateMeshVertices()
        {
            if (CancelProgress("Generating mesh... ", 0)) { return false; }

            if (modifyVoxel || (!backfaceCulling && !innerfaceCulling))
            {
                foreach (MVVoxel voxel in voxelDict.Values)
                {
                    int index = m_result.vertices.Count;
                    foreach (Vector3 v in voxelMesh.vertices) m_result.vertices.Add(m_grid.GetVertex(v, voxel.centerPos));
                    foreach (int i in voxelMesh.triangles) m_result.triangles[voxel.subMesh].Add(i + index);
                    m_result.normals.AddRange(voxelMesh.normals);
                    voxel.verticeCount = voxelMesh.vertices.Length;
                }
            }
            else
            {
                foreach (MVVoxel voxel in voxelDict.Values)
                {
                    int index = m_result.vertices.Count;
                    if (voxel.back)     m_result.AddFaceVertices(voxel, 0, index);
                    if (voxel.forward)  m_result.AddFaceVertices(voxel, 1, index);
                    if (voxel.left)     m_result.AddFaceVertices(voxel, 2, index);
                    if (voxel.right)    m_result.AddFaceVertices(voxel, 3, index);
                    if (voxel.up)       m_result.AddFaceVertices(voxel, 4, index);
                    if (voxel.down)     m_result.AddFaceVertices(voxel, 5, index);
                }
            }
            return true;
        }

        protected virtual bool DoOptimization()
        {
            if (CancelProgress("Optimizing result... ", 0)) { return false; }
            int counter = 0;
            int total = voxelDict.Count;
            int rem = Mathf.CeilToInt(total * 0.05f);

            for (int x = 0; x < m_grid.unitCount.x; ++x)
            {
                for (int y = 0; y < m_grid.unitCount.y; ++y)
                {
                    for (int z = 0; z < m_grid.unitCount.z; ++z)
                    {
                        MVInt3 pos = new MVInt3(x, y, z);
                        if (voxelDict.ContainsKey(pos))
                        {
                            if (counter % rem == 0 && CancelProgress("Optimizing result... ", (float)counter / total)) { return false; }
                            else counter++;

                            int xInv = m_grid.unitCount.x - x - 1;
                            int yInv = m_grid.unitCount.y - y - 1;
                            int zInv = m_grid.unitCount.z - z - 1;
                            MVVoxel voxel = voxelDict[pos];
                            Vector2 uv = m_source.GetUVCoord(voxel);
                            if (voxel.back)
                            {
                                if (!m_opt.sliceBack.ContainsKey(z)) m_opt.sliceBack.Add(z, new Dictionary<MVInt2, MVSlice>());
                                MVSlice data = m_opt.CreateSlice(0, voxel, uv);
                                m_opt.sliceBack[z].Add(new MVInt2(x, y), data);
                            }
                            if (voxel.forward)
                            {
                                if (!m_opt.sliceForward.ContainsKey(zInv)) m_opt.sliceForward.Add(zInv, new Dictionary<MVInt2, MVSlice>());
                                MVSlice data = m_opt.CreateSlice(4, voxel, uv);
                                m_opt.sliceForward[zInv].Add(new MVInt2(xInv, y), data);
                            }
                            if (voxel.left)
                            {
                                if (!m_opt.sliceLeft.ContainsKey(x)) m_opt.sliceLeft.Add(x, new Dictionary<MVInt2, MVSlice>());
                                MVSlice data = m_opt.CreateSlice(8, voxel, uv);
                                m_opt.sliceLeft[x].Add(new MVInt2(zInv, y), data);
                            }
                            if (voxel.right)
                            {
                                if (!m_opt.sliceRight.ContainsKey(xInv)) m_opt.sliceRight.Add(xInv, new Dictionary<MVInt2, MVSlice>());
                                MVSlice data = m_opt.CreateSlice(12, voxel, uv);
                                m_opt.sliceRight[xInv].Add(new MVInt2(z, y), data);
                            }
                            if (voxel.up)
                            {
                                if (!m_opt.sliceUp.ContainsKey(yInv)) m_opt.sliceUp.Add(yInv, new Dictionary<MVInt2, MVSlice>());
                                MVSlice data = m_opt.CreateSlice(16, voxel, uv);
                                m_opt.sliceUp[yInv].Add(new MVInt2(x, z), data);
                            }
                            if (voxel.down)
                            {
                                if (!m_opt.sliceDown.ContainsKey(y)) m_opt.sliceDown.Add(y, new Dictionary<MVInt2, MVSlice>());
                                MVSlice data = m_opt.CreateSlice(20, voxel, uv);
                                m_opt.sliceDown[y].Add(new MVInt2(xInv, z), data);
                            }
                        }
                    }
                }
            }

            if (CancelProgress("Rebuilding voxels... ", 0)) { return false; }
            m_opt.OptimizeSlice(m_opt.sliceBack, m_grid.unitCount.x, m_grid.unitCount.y);

            if (CancelProgress("Rebuilding voxels... ", 1.0f / 6.0f)) { return false; }
            m_opt.OptimizeSlice(m_opt.sliceForward, m_grid.unitCount.x, m_grid.unitCount.y);

            if (CancelProgress("Rebuilding voxels... ", 2.0f / 6.0f)) { return false; }
            m_opt.OptimizeSlice(m_opt.sliceLeft, m_grid.unitCount.z, m_grid.unitCount.y);

            if (CancelProgress("Rebuilding voxels... ", 3.0f / 6.0f)) { return false; }
            m_opt.OptimizeSlice(m_opt.sliceRight, m_grid.unitCount.z, m_grid.unitCount.y);

            if (CancelProgress("Rebuilding voxels... ", 4.0f / 6.0f)) { return false; }
            m_opt.OptimizeSlice(m_opt.sliceUp, m_grid.unitCount.x, m_grid.unitCount.z);

            if (CancelProgress("Rebuilding voxels... ", 5.0f / 6.0f)) { return false; }
            m_opt.OptimizeSlice(m_opt.sliceDown, m_grid.unitCount.x, m_grid.unitCount.z);
            return true;
        }

        protected virtual bool GenerateMeshUVsOpt()
        {
            if (uvConversion == UVConversion.SourceMesh)
            {
                if (CancelProgress("Computing UVs... ", 0)) { return false; }
                m_opt.ComputeSlice(m_opt.sliceBack);

                if (CancelProgress("Computing UVs... ", 1.0f / 6.0f)) { return false; }
                m_opt.ComputeSlice(m_opt.sliceForward);

                if (CancelProgress("Computing UVs... ", 2.0f / 6.0f)) { return false; }
                m_opt.ComputeSlice(m_opt.sliceLeft);

                if (CancelProgress("Computing UVs... ", 3.0f / 6.0f)) { return false; }
                m_opt.ComputeSlice(m_opt.sliceRight);

                if (CancelProgress("Computing UVs... ", 4.0f / 6.0f)) { return false; }
                m_opt.ComputeSlice(m_opt.sliceUp);

                if (CancelProgress("Computing UVs... ", 5.0f / 6.0f)) { return false; }
                m_opt.ComputeSlice(m_opt.sliceDown);

                if (CancelProgress("Generating UVs... ", 0)) { return false; }
                m_opt.AddSliceUVSource(m_opt.sliceBack);

                if (CancelProgress("Generating UVs... ", 1.0f / 6.0f)) { return false; }
                m_opt.AddSliceUVSource(m_opt.sliceForward);

                if (CancelProgress("Generating UVs... ", 2.0f / 6.0f)) { return false; }
                m_opt.AddSliceUVSource(m_opt.sliceLeft);

                if (CancelProgress("Generating UVs... ", 3.0f / 6.0f)) { return false; }
                m_opt.AddSliceUVSource(m_opt.sliceRight);

                if (CancelProgress("Generating UVs... ", 4.0f / 6.0f)) { return false; }
                m_opt.AddSliceUVSource(m_opt.sliceUp);

                if (CancelProgress("Generating UVs... ", 5.0f / 6.0f)) { return false; }
                m_opt.AddSliceUVSource(m_opt.sliceDown);
            }
            else if (uvConversion == UVConversion.VoxelMesh)
            {
                if (CancelProgress("Generating UVs... ", 0)) { return false; }
                m_opt.AddSliceUVVoxel(m_opt.sliceBack);

                if (CancelProgress("Generating UVs... ", 1.0f / 6.0f)) { return false; }
                m_opt.AddSliceUVVoxel(m_opt.sliceForward);

                if (CancelProgress("Generating UVs... ", 2.0f / 6.0f)) { return false; }
                m_opt.AddSliceUVVoxel(m_opt.sliceLeft);

                if (CancelProgress("Generating UVs... ", 3.0f / 6.0f)) { return false; }
                m_opt.AddSliceUVVoxel(m_opt.sliceRight);

                if (CancelProgress("Generating UVs... ", 4.0f / 6.0f)) { return false; }
                m_opt.AddSliceUVVoxel(m_opt.sliceUp);

                if (CancelProgress("Generating UVs... ", 5.0f / 6.0f)) { return false; }
                m_opt.AddSliceUVVoxel(m_opt.sliceDown);
            }
            return true;
        }

        protected virtual bool GenerateMeshUVs()
        {
            if (CancelProgress("Generating UVs... ", 0)) { return false; }

            if (uvConversion == UVConversion.SourceMesh)
            {
                if (m_source.mesh.uv.Length == m_source.mesh.vertices.Length)
                {
                    foreach (MVVoxel voxel in voxelDict.Values)
                    {
                        Vector2 uv = m_source.GetUVCoord(voxel);
                        for (int i = 0; i < voxel.verticeCount; ++i) m_result.uv.Add(uv);
                    }
                }
                if (m_source.mesh.uv2.Length == m_source.mesh.vertices.Length)
                {
                    foreach (MVVoxel voxel in voxelDict.Values)
                    {
                        Vector2 uv = m_source.GetUV2Coord(voxel);
                        for (int i = 0; i < voxel.verticeCount; ++i) m_result.uv2.Add(uv);
                    }
                }
                if (m_source.mesh.uv3.Length == m_source.mesh.vertices.Length)
                {
                    foreach (MVVoxel voxel in voxelDict.Values)
                    {
                        Vector2 uv = m_source.GetUV3Coord(voxel);
                        for (int i = 0; i < voxel.verticeCount; ++i) m_result.uv3.Add(uv);
                    }
                }
                if (m_source.mesh.uv4.Length == m_source.mesh.vertices.Length)
                {
                    foreach (MVVoxel voxel in voxelDict.Values)
                    {
                        Vector2 uv = m_source.GetUV4Coord(voxel);
                        for (int i = 0; i < voxel.verticeCount; ++i) m_result.uv4.Add(uv);
                    }
                }
            }
            else if (uvConversion == UVConversion.VoxelMesh)
            {
                if (modifyVoxel || (!backfaceCulling && !innerfaceCulling))
                {
                    foreach (MVVoxel voxel in voxelDict.Values) m_result.uv.AddRange(voxelMesh.uv);
                }
                else
                {
                    foreach (MVVoxel voxel in voxelDict.Values)
                    {
                        if (voxel.forward)  m_result.AddFaceUV(0);
                        if (voxel.up)       m_result.AddFaceUV(4);
                        if (voxel.back)     m_result.AddFaceUV(8);
                        if (voxel.down)     m_result.AddFaceUV(12);
                        if (voxel.left)     m_result.AddFaceUV(16);
                        if (voxel.right)    m_result.AddFaceUV(20);
                    }
                }
            }
            return true;
        }

        protected virtual bool GenerateMeshMaterialsOpt()
        {
            if (CancelProgress("Generating textures... ", 0)) { return false; }

            m_result.voxelizedMaterials = new Material[m_source.materials.Length];
            for (int i = 0; i < m_source.materials.Length; ++i)
            {
                m_result.voxelizedMaterials[i] = GameObject.Instantiate(m_source.materials[i]);
                m_result.voxelizedMaterials[i].name = m_source.materials[i].name + "_Voxelized";
            }

            foreach (var mat in m_result.voxelizedMaterials)
            {
                Texture tex = mat.mainTexture;
                if (tex != null)
                {
                    if (uvConversion == UVConversion.SourceMesh)
                        mat.mainTexture = m_opt.tInfo.CreateTexture(tex);
                    else
                        mat.mainTexture = tex;
                }
            }
            return true;
        }

        protected virtual bool GenerateMeshBoneWeights()
        {
            if (!boneWeightConversion || m_source.mesh.boneWeights.Length != m_source.mesh.vertices.Length) return true;

            if (CancelProgress("Generating BoneWeights...", 0)) { return false; }
            int counter = 0;
            int total = voxelDict.Count;
            int rem = Mathf.CeilToInt(total * 0.05f);

            Dictionary<int, float> temp = new Dictionary<int, float>();
            KeyValuePair<int, float>[] bwArray = new KeyValuePair<int, float>[4];
            foreach (MVVoxel voxel in voxelDict.Values)
            {
                if (counter % rem == 0 && CancelProgress("Generating BoneWeights...", (float)counter / total)) { return false; }
                else counter++;

                BoneWeight bw0 = m_source.mesh.boneWeights[m_source.mesh.triangles[voxel.index]];
                BoneWeight bw1 = m_source.mesh.boneWeights[m_source.mesh.triangles[voxel.index + 1]];
                BoneWeight bw2 = m_source.mesh.boneWeights[m_source.mesh.triangles[voxel.index + 2]];
                float sum = voxel.ratio.x + voxel.ratio.y + voxel.ratio.z;

                float px = voxel.ratio.x / sum;
                if (temp.ContainsKey(bw0.boneIndex0)) temp[bw0.boneIndex0] += bw0.weight0 * px;
                else temp.Add(bw0.boneIndex0, bw0.weight0 * px);
                if (temp.ContainsKey(bw0.boneIndex1)) temp[bw0.boneIndex1] += bw0.weight1 * px;
                else temp.Add(bw0.boneIndex1, bw0.weight1 * px);
                if (temp.ContainsKey(bw0.boneIndex2)) temp[bw0.boneIndex2] += bw0.weight2 * px;
                else temp.Add(bw0.boneIndex2, bw0.weight2 * px);
                if (temp.ContainsKey(bw0.boneIndex3)) temp[bw0.boneIndex3] += bw0.weight3 * px;
                else temp.Add(bw0.boneIndex3, bw0.weight3 * px);

                float py = voxel.ratio.y / sum;
                if (temp.ContainsKey(bw1.boneIndex0)) temp[bw1.boneIndex0] += bw1.weight0 * py;
                else temp.Add(bw1.boneIndex0, bw1.weight0 * py);
                if (temp.ContainsKey(bw1.boneIndex1)) temp[bw1.boneIndex1] += bw1.weight1 * py;
                else temp.Add(bw1.boneIndex1, bw1.weight1 * py);
                if (temp.ContainsKey(bw1.boneIndex2)) temp[bw1.boneIndex2] += bw1.weight2 * py;
                else temp.Add(bw1.boneIndex2, bw1.weight2 * py);
                if (temp.ContainsKey(bw1.boneIndex3)) temp[bw1.boneIndex3] += bw1.weight3 * py;
                else temp.Add(bw1.boneIndex3, bw1.weight3 * py);

                float pz = voxel.ratio.z / sum;
                if (temp.ContainsKey(bw2.boneIndex0)) temp[bw2.boneIndex0] += bw2.weight0 * pz;
                else temp.Add(bw2.boneIndex0, bw2.weight0 * pz);
                if (temp.ContainsKey(bw2.boneIndex1)) temp[bw2.boneIndex1] += bw2.weight1 * pz;
                else temp.Add(bw2.boneIndex1, bw2.weight1 * pz);
                if (temp.ContainsKey(bw2.boneIndex2)) temp[bw2.boneIndex2] += bw2.weight2 * pz;
                else temp.Add(bw2.boneIndex2, bw2.weight2 * pz);
                if (temp.ContainsKey(bw2.boneIndex3)) temp[bw2.boneIndex3] += bw2.weight3 * pz;
                else temp.Add(bw2.boneIndex3, bw2.weight3 * pz);

                var order = temp.OrderByDescending(x => x.Value).ToArray();
                int limit = order.Length < 4 ? order.Length : 4;
                int index;
                for (index = 0; index < limit; ++index)
                {
                    bwArray[index] = order[index];
                }
                for (index = order.Length; index < 4; ++index)
                {
                    bwArray[index] = new KeyValuePair<int, float>();
                }
                sum = bwArray[0].Value + bwArray[1].Value + bwArray[2].Value + bwArray[3].Value;

                BoneWeight bw = new BoneWeight();
                bw.boneIndex0 = bwArray[0].Key;
                bw.boneIndex1 = bwArray[1].Key;
                bw.boneIndex2 = bwArray[2].Key;
                bw.boneIndex3 = bwArray[3].Key;
                bw.weight0 = bwArray[0].Value / sum;
                bw.weight1 = bwArray[1].Value / sum;
                bw.weight2 = bwArray[2].Value / sum;
                bw.weight3 = bwArray[3].Value / sum;
                for (int i = 0; i < voxel.verticeCount; ++i) m_result.boneWeights.Add(bw);
                temp.Clear();
            }
            return true;
        }

        protected virtual GameObject GenerateResult()
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

            GameObject go = null;
            if (m_source.skinnedMeshRenderer != null)
            {
                if (instantiateResult)
                {
                    go = GameObject.Instantiate(sourceGameObject);
                    go.name = sourceGameObject.name + " Voxelized";
                    SkinnedMeshRenderer skrenderer = go.GetComponent<SkinnedMeshRenderer>();
                    skrenderer.sharedMesh = m_result.voxelizedMesh;
                    skrenderer.sharedMaterials = m_result.voxelizedMaterials;
                }
                else
                {
                    m_source.skinnedMeshRenderer.sharedMesh = m_result.voxelizedMesh;
                    m_source.skinnedMeshRenderer.sharedMaterials = m_result.voxelizedMaterials;
                    go = sourceGameObject;
                }
            }
            else
            {
                if (instantiateResult)
                {
                    go = GameObject.Instantiate(sourceGameObject);
                    go.name = sourceGameObject.name + " Voxelized";
                    if (m_source.meshFilter != null) go.GetComponent<MeshFilter>().sharedMesh = m_result.voxelizedMesh;
                    if (m_source.meshRenderer != null) go.GetComponent<MeshRenderer>().sharedMaterials = m_result.voxelizedMaterials;
                }
                else
                {
                    if (m_source.meshFilter != null) m_source.meshFilter.sharedMesh = m_result.voxelizedMesh;
                    if (m_source.meshRenderer != null) m_source.meshRenderer.sharedMaterials = m_result.voxelizedMaterials;
                    go = sourceGameObject;
                }
            }
            return go;
        }

        protected virtual bool CancelProgress(string msg, float value) { return false; }
    }
}