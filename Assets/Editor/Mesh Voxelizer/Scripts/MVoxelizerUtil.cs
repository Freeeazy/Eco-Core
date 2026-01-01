using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MVoxelizer.Util
{
    public static class MVMathLib
    {
        public static bool LineIntersectsTriangle(Vector3 linePoint, Vector3 lineDir, Vector3 v0, Vector3 v1, Vector3 v2, out Vector3 intersection)
        {
            intersection = new Vector3();

            // Find the normal of the triangle
            Vector3 u = v1 - v0;
            Vector3 v = v2 - v0;
            Vector3 normal = Vector3.Cross(u, v);

            // Check if line and plane are parallel
            float dot = Vector3.Dot(normal, lineDir);
            if (Mathf.Abs(dot) < 1e-8) return false;

            // Compute the distance along the line to the intersection point
            float d = Vector3.Dot(normal, v0 - linePoint) / dot;
            intersection = linePoint + d * lineDir;

            // Check if the intersection point lies inside the triangle
            return IsPointInTriangle(intersection, v0, v1, v2);
        }

        public static bool LineIntersectsTriangle(Vector3 linePoint, Vector3 lineDir, Vector3 normal, Vector3 v0, Vector3 v1, Vector3 v2, out Vector3 intersection)
        {
            intersection = new Vector3();

            // Check if line and plane are parallel
            float dot = Vector3.Dot(normal, lineDir);
            if (Mathf.Abs(dot) < 1e-8) return false;

            // Compute the distance along the line to the intersection point
            float d = Vector3.Dot(normal, v0 - linePoint) / dot;
            intersection = linePoint + d * lineDir;

            // Check if the intersection point lies inside the triangle
            return IsPointInTriangle(intersection, v0, v1, v2);
        }

        public static bool IsPointInTriangle(Vector3 p, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            // Compute vectors
            Vector3 v0v1 = v1 - v0;
            Vector3 v0v2 = v2 - v0;
            Vector3 v0p = p - v0;

            // Compute dot products
            float dot00 = Vector3.Dot(v0v2, v0v2);
            float dot01 = Vector3.Dot(v0v2, v0v1);
            float dot02 = Vector3.Dot(v0v2, v0p);
            float dot11 = Vector3.Dot(v0v1, v0v1);
            float dot12 = Vector3.Dot(v0v1, v0p);

            // Compute barycentric coordinates
            float invDenom = 1 / (dot00 * dot11 - dot01 * dot01);
            float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

            // Check if point is in triangle
            return (u >= 0) && (v >= 0) && (u + v < 1);
        }
    }

    public static class MVHelper
    {
        public static Mesh GetDefaultVoxelMesh()
        {
            Mesh m = (Mesh)Resources.Load("DefaultVoxelCube", typeof(Mesh));
            return m;
        }

        public static Material GetDefaultVoxelMaterial()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Material m = go.GetComponent<MeshRenderer>().sharedMaterial;
            GameObject.DestroyImmediate(go);
            return m;
        }

        public static Texture2D GetTexture2D(Texture tex)
        {
            Texture2D texture2D = new Texture2D(tex.width, tex.height);
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 32);
            Graphics.Blit(tex, rt);
            RenderTexture curr = RenderTexture.active;
            RenderTexture.active = rt;
            texture2D.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = curr;
            RenderTexture.ReleaseTemporary(rt);
            return texture2D;
        }
    }

    public class MVInt2
    {
        public int x, y;

        public MVInt2() : this(0, 0) { }

        public MVInt2(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public override bool Equals(object obj)
        {
            if ((obj == null) || !this.GetType().Equals(obj.GetType())) return false;
            MVInt2 p = (MVInt2)obj;
            return (x == p.x) && (y == p.y);
        }

        public override int GetHashCode()
        {
            return ShiftAndWrap(x.GetHashCode(), 2) ^ y.GetHashCode();
        }

        public int ShiftAndWrap(int value, int positions)
        {
            positions = positions & 0x1F;

            uint number = System.BitConverter.ToUInt32(System.BitConverter.GetBytes(value), 0);
            uint wrapped = number >> (32 - positions);
            return System.BitConverter.ToInt32(System.BitConverter.GetBytes((number << positions) | wrapped), 0);
        }

        public override string ToString()
        {
            return System.String.Format("({0}, {1})", x, y);
        }
    }

    public class MVInt3
    {
        public int x, y, z;

        public MVInt3() : this(0, 0, 0) { }

        public MVInt3(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public override bool Equals(object obj)
        {
            if ((obj == null) || !this.GetType().Equals(obj.GetType())) return false;
            MVInt3 p = (MVInt3)obj;
            return (x == p.x) && (y == p.y) && (z == p.z);
        }

        public override int GetHashCode()
        {
            return ShiftAndWrap(x.GetHashCode(), 4) ^ ShiftAndWrap(y.GetHashCode(), 2) ^ z.GetHashCode();
        }

        public int ShiftAndWrap(int value, int positions)
        {
            positions = positions & 0x1F;

            uint number = System.BitConverter.ToUInt32(System.BitConverter.GetBytes(value), 0);
            uint wrapped = number >> (32 - positions);
            return System.BitConverter.ToInt32(System.BitConverter.GetBytes((number << positions) | wrapped), 0);
        }

        public override string ToString()
        {
            return System.String.Format("({0}, {1}, {2})", x, y, z);
        }
    }

    public class MVTextureInfo
    {
        public int textureSize = 0;
        public Dictionary<MVInt2, Vector2> uvData = new Dictionary<MVInt2, Vector2>();
        
        public Texture CreateTexture(Texture texture)
        {
            int multi = Mathf.CeilToInt(512.0f / textureSize);
            Texture2D texture2D = MVHelper.GetTexture2D(texture);
            Texture2D t2d = new Texture2D(textureSize * multi, textureSize * multi);
            t2d.name = texture.name + "_Voxelized";
            //t2d.filterMode = FilterMode.Point;
            for (int i = 0; i < textureSize; ++i)
            {
                for (int j = 0; j < textureSize; ++j)
                {
                    MVInt2 pos = new MVInt2(i, j);
                    if (uvData.ContainsKey(pos))
                    {
                        Color color = texture2D.GetPixel((int)(texture2D.width * uvData[pos].x), (int)(texture2D.height * uvData[pos].y));
                        for (int x = 0; x < multi; ++x)
                        {
                            for (int y = 0; y < multi; ++y)
                            {
                                t2d.SetPixel(i * multi + x, j * multi + y, color);
                            }
                        }
                    }
                }
            }
            t2d.Apply();
            GameObject.DestroyImmediate(texture2D);
            return t2d;
        }
    }

    public class MVSlice
    {
        public int subMesh;
        public MVInt2 size = new MVInt2(1, 1);
        public Vector3[] vertices = new Vector3[4];
        public List<Vector2> uvCoord = new List<Vector2>();

        public void CombineV(MVSlice data)
        {
            vertices[1] = data.vertices[1];
            vertices[2] = data.vertices[2];
            uvCoord.Add(data.uvCoord[0]);
            size.y++;
        }

        public void CombineH(MVSlice data)
        {
            vertices[2] = data.vertices[2];
            vertices[3] = data.vertices[3];
            uvCoord.AddRange(data.uvCoord);
            size.x++;
        }
    }

    public class MVVoxel
    {
        public Vector3 centerPos;
        public Vector3 vertPos;
        public int index;
        public int subMesh;
        public Vector3 ratio;
        public int verticeCount = 0;
        public int sampleCount = 0;
        public MVVoxel v_forward = null;
        public MVVoxel v_up = null;
        public MVVoxel v_back = null;
        public MVVoxel v_down = null;
        public MVVoxel v_left = null;
        public MVVoxel v_right = null;
        public bool forward = false;
        public bool up = false;
        public bool back = false;
        public bool down = false;
        public bool left = false;
        public bool right = false;

        public void UpdateNormal(Vector3 normal)
        {
            back = back || normal.z <= 0.0f;
            forward = forward || normal.z >= 0.0f;
            left = left || normal.x <= 0.0f;
            right = right || normal.x >= 0.0f;
            up = up || normal.y >= 0.0f;
            down = down || normal.y <= 0.0f;
        }
    }
    
    public class MVSource
    {
        public Transform transform = null;
        public Mesh mesh = null;
        public Material[] materials = null;
        public MeshFilter meshFilter = null;
        public MeshRenderer meshRenderer = null;
        public SkinnedMeshRenderer skinnedMeshRenderer = null;

        public void Init(GameObject sourceGameObject)
        {
            transform = sourceGameObject.transform;
            skinnedMeshRenderer = sourceGameObject.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer != null)
            {
                mesh = sourceGameObject.GetComponent<SkinnedMeshRenderer>().sharedMesh;
                materials = sourceGameObject.GetComponent<SkinnedMeshRenderer>().sharedMaterials;
            }
            else
            {
                meshFilter = sourceGameObject.GetComponent<MeshFilter>();
                meshRenderer = sourceGameObject.GetComponent<MeshRenderer>();
                if (meshFilter != null) mesh = meshFilter.sharedMesh;
                if (meshRenderer != null) materials = meshRenderer.sharedMaterials;
            }
        }

        public Vector2 GetUVCoord(MVVoxel voxel)
        {
            Vector2 p0 = mesh.uv[mesh.triangles[voxel.index]];
            Vector2 p1 = mesh.uv[mesh.triangles[voxel.index + 1]];
            Vector2 p2 = mesh.uv[mesh.triangles[voxel.index + 2]];
            float sum = voxel.ratio.x + voxel.ratio.y + voxel.ratio.z;
            Vector2 uv = p0 * (voxel.ratio.x / sum) + p1 * (voxel.ratio.y / sum) + p2 * (voxel.ratio.z / sum);
            return uv;
        }

        public Vector2 GetUV2Coord(MVVoxel voxel)
        {
            Vector2 p0 = mesh.uv2[mesh.triangles[voxel.index]];
            Vector2 p1 = mesh.uv2[mesh.triangles[voxel.index + 1]];
            Vector2 p2 = mesh.uv2[mesh.triangles[voxel.index + 2]];
            float sum = voxel.ratio.x + voxel.ratio.y + voxel.ratio.z;
            Vector2 uv = p0 * (voxel.ratio.x / sum) + p1 * (voxel.ratio.y / sum) + p2 * (voxel.ratio.z / sum);
            return uv;
        }

        public Vector2 GetUV3Coord(MVVoxel voxel)
        {
            Vector2 p0 = mesh.uv3[mesh.triangles[voxel.index]];
            Vector2 p1 = mesh.uv3[mesh.triangles[voxel.index + 1]];
            Vector2 p2 = mesh.uv3[mesh.triangles[voxel.index + 2]];
            float sum = voxel.ratio.x + voxel.ratio.y + voxel.ratio.z;
            Vector2 uv = p0 * (voxel.ratio.x / sum) + p1 * (voxel.ratio.y / sum) + p2 * (voxel.ratio.z / sum);
            return uv;
        }

        public Vector2 GetUV4Coord(MVVoxel voxel)
        {
            Vector2 p0 = mesh.uv4[mesh.triangles[voxel.index]];
            Vector2 p1 = mesh.uv4[mesh.triangles[voxel.index + 1]];
            Vector2 p2 = mesh.uv4[mesh.triangles[voxel.index + 2]];
            float sum = voxel.ratio.x + voxel.ratio.y + voxel.ratio.z;
            Vector2 uv = p0 * (voxel.ratio.x / sum) + p1 * (voxel.ratio.y / sum) + p2 * (voxel.ratio.z / sum);
            return uv;
        }
    }

    public class MVGrid
    {
        public float unitSize = 1.0f;
        public MVInt3 unitCount = new MVInt3();
        public Vector3 unitVoxelRatio = Vector3.one;
        public Quaternion voxelRotation = Quaternion.identity;
        public Vector3 origin = Vector3.zero;
        
        public MVInt3 GetGridCoordinate(Vector3 p)
        {
            MVInt3 pos = new MVInt3();
            pos.x = Mathf.FloorToInt(p.x / unitSize);
            pos.y = Mathf.FloorToInt(p.y / unitSize);
            pos.z = Mathf.FloorToInt(p.z / unitSize);
            return pos;
        }

        public Vector3 GetPosition(MVInt3 coord)
        {
            Vector3 pos = new Vector3();
            pos.x = unitSize * (coord.x + 0.5f);
            pos.y = unitSize * (coord.y + 0.5f);
            pos.z = unitSize * (coord.z + 0.5f);
            return pos;
        }
        
        public Vector3 GetVertex(Vector3 v, Vector3 offset)
        {
            Vector3 vert = v;
            vert.x *= unitVoxelRatio.x;
            vert.y *= unitVoxelRatio.y;
            vert.z *= unitVoxelRatio.z;
            vert = voxelRotation * vert;
            return vert + offset;
        }

        public void Init(MeshVoxelizer voxelizer, MVSource source)
        {
            Vector3 sourceMeshSize = source.mesh.bounds.size;
            Vector3 sourceMeshMin = source.mesh.bounds.min;
            if (voxelizer.applyScaling)
            {
                sourceMeshSize.x *= source.transform.lossyScale.x;
                sourceMeshSize.y *= source.transform.lossyScale.y;
                sourceMeshSize.z *= source.transform.lossyScale.z;
                sourceMeshMin.x  *= source.transform.lossyScale.x;
                sourceMeshMin.y  *= source.transform.lossyScale.y;
                sourceMeshMin.z  *= source.transform.lossyScale.z;
            }

            float maxBBoxSize = Mathf.Max(sourceMeshSize.x, sourceMeshSize.y, sourceMeshSize.z);
            if (voxelizer.voxelSizeType == MeshVoxelizer.VoxelSizeType.Subdivision)
            {
                voxelizer.subdivisionLevel = Mathf.Clamp(voxelizer.subdivisionLevel, 1, MeshVoxelizer.MAX_SUBDIVISION);
                unitSize = maxBBoxSize / voxelizer.subdivisionLevel;
            }
            else
            {
                voxelizer.absoluteVoxelSize = Mathf.Clamp(voxelizer.absoluteVoxelSize, maxBBoxSize / MeshVoxelizer.MAX_SUBDIVISION, maxBBoxSize);
                unitSize = voxelizer.absoluteVoxelSize;
            }
            unitSize *= 1.00001f;

            unitCount.x = Mathf.CeilToInt(sourceMeshSize.x / unitSize);
            unitCount.y = Mathf.CeilToInt(sourceMeshSize.y / unitSize);
            unitCount.z = Mathf.CeilToInt(sourceMeshSize.z / unitSize);
            if (unitCount.x == 0) unitCount.x = 1;
            if (unitCount.y == 0) unitCount.y = 1;
            if (unitCount.z == 0) unitCount.z = 1;

            if (voxelizer.modifyVoxel)
            {
                unitVoxelRatio.x = unitSize * voxelizer.voxelScale.x / voxelizer.voxelMesh.bounds.size.x;
                unitVoxelRatio.y = unitSize * voxelizer.voxelScale.y / voxelizer.voxelMesh.bounds.size.y;
                unitVoxelRatio.z = unitSize * voxelizer.voxelScale.z / voxelizer.voxelMesh.bounds.size.z;
                voxelRotation = Quaternion.Euler(voxelizer.voxelRotation);
            }
            else
            {
                voxelizer.voxelMesh = MVHelper.GetDefaultVoxelMesh();
                unitVoxelRatio.x = unitSize / voxelizer.voxelMesh.bounds.size.x;
                unitVoxelRatio.y = unitSize / voxelizer.voxelMesh.bounds.size.y;
                unitVoxelRatio.z = unitSize / voxelizer.voxelMesh.bounds.size.z;
            }

            Vector3 offset = new Vector3();
            offset.x = sourceMeshSize.x > unitSize ? 0.0f : (unitSize - sourceMeshSize.x) * 0.5f;
            offset.y = sourceMeshSize.y > unitSize ? 0.0f : (unitSize - sourceMeshSize.y) * 0.5f;
            offset.z = sourceMeshSize.z > unitSize ? 0.0f : (unitSize - sourceMeshSize.z) * 0.5f;
            origin = sourceMeshMin - offset;
        }
    }

    public struct MVTriangleData
    {
        public Vector3 p0;
        public Vector3 p1;
        public Vector3 p2;
        public Vector3 normal;
        public Vector3 normalABS;
        public Vector3 v01;
        public Vector3 v12;
        public Vector3 v20;
        public Bounds bound;
        public int index;
        public int subMesh;
        float triangleStep;

        MVSource m_source;
        MVGrid m_grid;
        Dictionary<MVInt3, MVVoxel> voxelDict;

        public MVTriangleData(MVSource source, MVGrid grid, Dictionary<MVInt3, MVVoxel> vDict, int index, int subMesh, float triangleStep, bool applyScaling)
        {
            m_source = source;
            m_grid = grid;
            voxelDict = vDict;
            this.index = index;
            this.subMesh = subMesh;
            this.triangleStep = triangleStep;

            p0 = m_source.mesh.vertices[m_source.mesh.triangles[index]];
            p1 = m_source.mesh.vertices[m_source.mesh.triangles[index + 1]];
            p2 = m_source.mesh.vertices[m_source.mesh.triangles[index + 2]];
            if (applyScaling)
            {
                p0.x *= m_source.transform.lossyScale.x;
                p0.y *= m_source.transform.lossyScale.y;
                p0.z *= m_source.transform.lossyScale.z;
                p1.x *= m_source.transform.lossyScale.x;
                p1.y *= m_source.transform.lossyScale.y;
                p1.z *= m_source.transform.lossyScale.z;
                p2.x *= m_source.transform.lossyScale.x;
                p2.y *= m_source.transform.lossyScale.y;
                p2.z *= m_source.transform.lossyScale.z;
            }

            v01 = p1 - p0;
            v12 = p2 - p1;
            v20 = p0 - p2;

            normal = Vector3.Cross(v01, -v20).normalized;
            normalABS = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            if (normalABS.x < 0.00001f) normal.x = 0.0f;
            if (normalABS.y < 0.00001f) normal.y = 0.0f;
            if (normalABS.z < 0.00001f) normal.z = 0.0f;

            Vector3 min = new Vector3();
            Vector3 max = new Vector3();
            min.x = Mathf.Min(p0.x, p1.x, p2.x);
            min.y = Mathf.Min(p0.y, p1.y, p2.y);
            min.z = Mathf.Min(p0.z, p1.z, p2.z);
            max.x = Mathf.Max(p0.x, p1.x, p2.x);
            max.y = Mathf.Max(p0.y, p1.y, p2.y);
            max.z = Mathf.Max(p0.z, p1.z, p2.z);
            bound = new Bounds((min + max) * 0.5f, max - min);
        }

        public void Scan()
        {
            if (normalABS.x > normalABS.y && normalABS.x > normalABS.z)
            {
                Vector3 center = bound.center - new Vector3(bound.extents.x + 1.0f, 0.0f, 0.0f);
                Vector3 rayDir = Vector3.right;
                float w = 0.0f;
                while (w <= bound.extents.z)
                {
                    float h = 0.0f;
                    while (h <= bound.extents.y)
                    {
                        Vector3 rayPoint1 = center + new Vector3(0.0f,  h,  w);
                        Vector3 rayPoint2 = center + new Vector3(0.0f, -h,  w);
                        Vector3 rayPoint3 = center + new Vector3(0.0f, -h, -w);
                        Vector3 rayPoint4 = center + new Vector3(0.0f,  h, -w);

                        CheckRay(rayPoint1, rayDir);
                        CheckRay(rayPoint2, rayDir);
                        CheckRay(rayPoint3, rayDir);
                        CheckRay(rayPoint4, rayDir);

                        h += triangleStep;
                    }
                    w += triangleStep;
                }
            }
            else if (normalABS.y > normalABS.x && normalABS.y > normalABS.z)
            {
                Vector3 center = bound.center - new Vector3(0.0f, bound.extents.y + 1.0f, 0.0f);
                Vector3 rayDir = Vector3.up;
                float w = 0.0f;
                while (w <= bound.extents.x)
                {
                    float h = 0.0f;
                    while (h <= bound.extents.z)
                    {
                        Vector3 rayPoint1 = center + new Vector3( w, 0.0f,  h);
                        Vector3 rayPoint2 = center + new Vector3(-w, 0.0f,  h);
                        Vector3 rayPoint3 = center + new Vector3(-w, 0.0f, -h);
                        Vector3 rayPoint4 = center + new Vector3( w, 0.0f, -h);

                        CheckRay(rayPoint1, rayDir);
                        CheckRay(rayPoint2, rayDir);
                        CheckRay(rayPoint3, rayDir);
                        CheckRay(rayPoint4, rayDir);

                        h += triangleStep;
                    }
                    w += triangleStep;
                }
            }
            else
            {
                Vector3 center = bound.center - new Vector3(0.0f, 0.0f, bound.extents.z + 1.0f);
                Vector3 rayDir = Vector3.forward;
                float w = 0.0f;
                while (w <= bound.extents.x)
                {
                    float h = 0.0f;
                    while (h <= bound.extents.y)
                    {
                        Vector3 rayPoint1 = center + new Vector3( w,  h, 0.0f);
                        Vector3 rayPoint2 = center + new Vector3(-w,  h, 0.0f);
                        Vector3 rayPoint3 = center + new Vector3(-w, -h, 0.0f);
                        Vector3 rayPoint4 = center + new Vector3( w, -h, 0.0f);

                        CheckRay(rayPoint1, rayDir);
                        CheckRay(rayPoint2, rayDir);
                        CheckRay(rayPoint3, rayDir);
                        CheckRay(rayPoint4, rayDir);

                        h += triangleStep;
                    }
                    w += triangleStep;
                }
            }
        }

        void CheckRay(Vector3 rayPoint, Vector3 rayDir)
        {
            if (MVMathLib.LineIntersectsTriangle(rayPoint, rayDir, normal, p0, p1, p2, out Vector3 p))
            {
                Vector3 ratio = new Vector3(Vector3.Cross((p - p1), v12).magnitude, Vector3.Cross((p - p2), v20).magnitude, Vector3.Cross((p - p0), v01).magnitude);
                CheckPoint(p, ratio);
            }
        }

        void CheckPoint(Vector3 p, Vector3 ratio)
        {
            p -= m_grid.origin;
            MVVoxel voxel;
            MVInt3 pos = m_grid.GetGridCoordinate(p);
            if (voxelDict.TryGetValue(pos, out voxel))
            {
                Vector3 v = p - voxelDict[pos].centerPos;
                if (v.sqrMagnitude < voxelDict[pos].vertPos.sqrMagnitude)
                {
                    voxelDict[pos].vertPos = v;
                    voxelDict[pos].ratio = ratio;
                    voxelDict[pos].index = index;
                    voxelDict[pos].subMesh = subMesh;
                }
                voxelDict[pos].sampleCount++;
                voxelDict[pos].UpdateNormal(normal);
            }
            else
            {
                voxel = new MVVoxel();
                voxel.centerPos = m_grid.GetPosition(pos);
                voxel.vertPos = p - voxel.centerPos;
                voxel.ratio = ratio;
                voxel.index = index;
                voxel.subMesh = subMesh;
                voxel.sampleCount = 1;
                voxel.UpdateNormal(normal);
                voxelDict.Add(pos, voxel);
            }
        }
    }

    public class MVResult
    {
        public List<Vector3> vertices = new List<Vector3>();
        public List<List<int>> triangles = new List<List<int>>();
        public List<Vector3> normals = new List<Vector3>();
        public List<Vector2> uv = new List<Vector2>();
        public List<Vector2> uv2 = new List<Vector2>();
        public List<Vector2> uv3 = new List<Vector2>();
        public List<Vector2> uv4 = new List<Vector2>();
        public List<BoneWeight> boneWeights = new List<BoneWeight>();
        public Mesh voxelizedMesh = null;
        public Material[] voxelizedMaterials = null;

        public MVGrid grid = null;
        public Mesh voxelMesh = null;

        public void Init(int subMeshCount)
        {
            for (int i = 0; i < subMeshCount; ++i) triangles.Add(new List<int>());
        }

        public void AddFaceVertices(MVVoxel voxel, int vIndex, int index)
        {
            int v = 4 * vIndex;
            int t = 6 * vIndex;
            index = index - v + voxel.verticeCount;
            vertices.Add(grid.GetVertex(voxelMesh.vertices[v + 0], voxel.centerPos));
            vertices.Add(grid.GetVertex(voxelMesh.vertices[v + 1], voxel.centerPos));
            vertices.Add(grid.GetVertex(voxelMesh.vertices[v + 2], voxel.centerPos));
            vertices.Add(grid.GetVertex(voxelMesh.vertices[v + 3], voxel.centerPos));
            normals.Add(voxelMesh.normals[v + 0]);
            normals.Add(voxelMesh.normals[v + 1]);
            normals.Add(voxelMesh.normals[v + 2]);
            normals.Add(voxelMesh.normals[v + 3]);
            triangles[voxel.subMesh].Add(voxelMesh.triangles[t + 0] + index);
            triangles[voxel.subMesh].Add(voxelMesh.triangles[t + 1] + index);
            triangles[voxel.subMesh].Add(voxelMesh.triangles[t + 2] + index);
            triangles[voxel.subMesh].Add(voxelMesh.triangles[t + 3] + index);
            triangles[voxel.subMesh].Add(voxelMesh.triangles[t + 4] + index);
            triangles[voxel.subMesh].Add(voxelMesh.triangles[t + 5] + index);
            voxel.verticeCount += 4;
        }

        public void AddFaceUV(int v)
        {
            uv.Add(voxelMesh.uv[v + 0]);
            uv.Add(voxelMesh.uv[v + 1]);
            uv.Add(voxelMesh.uv[v + 2]);
            uv.Add(voxelMesh.uv[v + 3]);
        }
    }

    public class MVOptimization
    {
        public MVTextureInfo tInfo = new MVTextureInfo();
        public Dictionary<int, Dictionary<MVInt2, MVSlice>> sliceBack = new Dictionary<int, Dictionary<MVInt2, MVSlice>>();
        public Dictionary<int, Dictionary<MVInt2, MVSlice>> sliceForward = new Dictionary<int, Dictionary<MVInt2, MVSlice>>();
        public Dictionary<int, Dictionary<MVInt2, MVSlice>> sliceLeft = new Dictionary<int, Dictionary<MVInt2, MVSlice>>();
        public Dictionary<int, Dictionary<MVInt2, MVSlice>> sliceRight = new Dictionary<int, Dictionary<MVInt2, MVSlice>>();
        public Dictionary<int, Dictionary<MVInt2, MVSlice>> sliceUp = new Dictionary<int, Dictionary<MVInt2, MVSlice>>();
        public Dictionary<int, Dictionary<MVInt2, MVSlice>> sliceDown = new Dictionary<int, Dictionary<MVInt2, MVSlice>>();

        public MVResult result = null;
        public MVGrid grid = null;
        public Mesh voxelMesh = null;
        
        public MVSlice CreateSlice(int v, MVVoxel voxel, Vector2 uvCoord)
        {
            MVSlice slice = new MVSlice();
            slice.subMesh = voxel.subMesh;
            slice.vertices[0] = grid.GetVertex(voxelMesh.vertices[v + 0], voxel.centerPos);
            slice.vertices[1] = grid.GetVertex(voxelMesh.vertices[v + 1], voxel.centerPos);
            slice.vertices[2] = grid.GetVertex(voxelMesh.vertices[v + 2], voxel.centerPos);
            slice.vertices[3] = grid.GetVertex(voxelMesh.vertices[v + 3], voxel.centerPos);
            slice.uvCoord.Add(uvCoord);
            return slice;
        }

        public void OptimizeSlice(Dictionary<int, Dictionary<MVInt2, MVSlice>> sliceDict, int lenX, int lenY)
        {
            foreach (var slice in sliceDict.Values)
            {
                for (int i = 0; i < lenX; ++i)
                {
                    for (int j = 0; j < lenY - 1; ++j)
                    {
                        MVInt2 pos = new MVInt2(i, j);
                        if (slice.ContainsKey(pos))
                        {
                            MVInt2 nextPos = new MVInt2(i, j + 1);
                            while (slice.ContainsKey(nextPos) && slice[pos].subMesh == slice[nextPos].subMesh)
                            {
                                slice[pos].CombineV(slice[nextPos]);
                                slice.Remove(nextPos);
                                j++;
                                nextPos = new MVInt2(i, j + 1);
                            }
                        }
                    }
                }

                for (int i = 0; i < lenX - 1; ++i)
                {
                    for (int j = 0; j < lenY; ++j)
                    {
                        MVInt2 pos = new MVInt2(i, j);
                        if (slice.ContainsKey(pos))
                        {
                            int temp = i;
                            MVInt2 nextPos = new MVInt2(i + 1, j);
                            while (slice.ContainsKey(nextPos) && slice[pos].subMesh == slice[nextPos].subMesh && slice[pos].size.y == slice[nextPos].size.y)
                            {
                                slice[pos].CombineH(slice[nextPos]);
                                slice.Remove(nextPos);
                                temp++;
                                nextPos = new MVInt2(temp + 1, j);
                            }
                        }
                    }
                }
            }
        }

        public void ComputeSlice(Dictionary<int, Dictionary<MVInt2, MVSlice>> sliceDict)
        {
            foreach (var slice in sliceDict.Values)
            {
                foreach (var point in slice)
                {
                    bool found = false;
                    for (int i = 0; i < tInfo.textureSize; ++i)
                    {
                        for (int j = 0; j < tInfo.textureSize; ++j)
                        {
                            if (i + point.Value.size.x <= tInfo.textureSize && j + point.Value.size.y <= tInfo.textureSize)
                            {
                                bool occupied = false;
                                for (int x = 0; x < point.Value.size.x; ++x)
                                {
                                    for (int y = 0; y < point.Value.size.y; ++y)
                                    {
                                        occupied = tInfo.uvData.ContainsKey(new MVInt2(x + i, y + j));
                                        if (occupied)
                                        {
                                            j += y;
                                            break;
                                        }
                                    }
                                    if (occupied) break;
                                }

                                if (!occupied)
                                {
                                    point.Key.x = i;
                                    point.Key.y = j;
                                    for (int x = 0; x < point.Value.size.x; ++x)
                                    {
                                        for (int y = 0; y < point.Value.size.y; ++y)
                                        {
                                            tInfo.uvData.Add(new MVInt2(x + i, y + j), point.Value.uvCoord[point.Value.size.y * x + y]);
                                        }
                                    }
                                    found = true;
                                    break;
                                }
                            }
                        }
                        if (found) break;
                    }
                    if (!found)
                    {
                        point.Key.x = 0;
                        point.Key.y = tInfo.textureSize;
                        for (int x = 0; x < point.Value.size.x; ++x)
                        {
                            for (int y = 0; y < point.Value.size.y; ++y)
                            {
                                tInfo.uvData.Add(new MVInt2(x, y + tInfo.textureSize), point.Value.uvCoord[point.Value.size.y * x + y]);
                            }
                        }
                        tInfo.textureSize += point.Value.size.x > point.Value.size.y ? point.Value.size.x : point.Value.size.y;
                    }
                }
            }
        }
        
        public void AddSliceVertices(Dictionary<int, Dictionary<MVInt2, MVSlice>> sliceDict, Vector3 normal)
        {
            foreach (var slice in sliceDict.Values)
            {
                foreach (var point in slice.Values)
                {
                    int index = result.vertices.Count;
                    result.vertices.Add(point.vertices[0]);
                    result.vertices.Add(point.vertices[1]);
                    result.vertices.Add(point.vertices[2]);
                    result.vertices.Add(point.vertices[3]);
                    result.normals.Add(normal);
                    result.normals.Add(normal);
                    result.normals.Add(normal);
                    result.normals.Add(normal);
                    result.triangles[point.subMesh].Add(0 + index);
                    result.triangles[point.subMesh].Add(1 + index);
                    result.triangles[point.subMesh].Add(2 + index);
                    result.triangles[point.subMesh].Add(2 + index);
                    result.triangles[point.subMesh].Add(3 + index);
                    result.triangles[point.subMesh].Add(0 + index);
                }
            }
        }

        public void AddSliceUVVoxel(Dictionary<int, Dictionary<MVInt2, MVSlice>> sliceDict)
        {
            foreach (var slice in sliceDict.Values)
            {
                foreach (var point in slice.Values)
                {
                    result.uv.Add(new Vector2(0.0f, 0.0f));
                    result.uv.Add(new Vector2(0.0f, point.size.y));
                    result.uv.Add(new Vector2(point.size.x, point.size.y));
                    result.uv.Add(new Vector2(point.size.x, 0.0f));
                }
            }
        }

        public void AddSliceUVSource(Dictionary<int, Dictionary<MVInt2, MVSlice>> sliceDict)
        {
            float offset = 0.02f * Mathf.Ceil(512.0f / tInfo.textureSize);
            foreach (var slice in sliceDict.Values)
            {
                foreach (var point in slice)
                {
                    result.uv.Add(new Vector2((point.Key.x + offset) / tInfo.textureSize, (point.Key.y + offset) / tInfo.textureSize));
                    result.uv.Add(new Vector2((point.Key.x + offset) / tInfo.textureSize, (point.Key.y + point.Value.size.y - offset) / tInfo.textureSize));
                    result.uv.Add(new Vector2((point.Key.x + point.Value.size.x - offset) / tInfo.textureSize, (point.Key.y + point.Value.size.y - offset) / tInfo.textureSize));
                    result.uv.Add(new Vector2((point.Key.x + point.Value.size.x - offset) / tInfo.textureSize, (point.Key.y + offset) / tInfo.textureSize));
                }
            }
        }
    }
}