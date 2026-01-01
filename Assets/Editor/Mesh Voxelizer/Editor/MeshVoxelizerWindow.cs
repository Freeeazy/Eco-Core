using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MVoxelizer.Util;

namespace MVoxelizer.MVEditor
{
    public class MeshVoxelizerWindow : EditorWindow
    {
        class TargetInfo
        {
            public GameObject go;
            public Mesh mesh = null;
            public Vector3 meshBBoxSize = new Vector3();
            public MVInt3 voxelBBoxSize = new MVInt3();
            public MeshRenderer meshRenderer = null;
            public SkinnedMeshRenderer skinnedMeshRenderer = null;
            public float maxBBoxSize = 0.0f;

            public TargetInfo(GameObject go)
            {
                this.go = go;
                meshRenderer = go.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    mesh = go.GetComponent<MeshFilter>().sharedMesh;
                }
                else
                {
                    skinnedMeshRenderer = go.GetComponent<SkinnedMeshRenderer>();
                    if (skinnedMeshRenderer != null)
                    {
                        mesh = skinnedMeshRenderer.sharedMesh;
                    }
                }
            }

            public string GetMeshBBoxString()
            {
                string x = GetFloatString(meshBBoxSize.x);
                string y = GetFloatString(meshBBoxSize.y);
                string z = GetFloatString(meshBBoxSize.z);
                return "X:" + x + " Y:" + y + " Z:" + z;
            }

            public string GetVoxelBBoxString()
            {
                return "X:" + voxelBBoxSize.x + " Y:" + voxelBBoxSize.y + " Z:" + voxelBBoxSize.z;
            }

            string GetFloatString(float f)
            {
                return Mathf.Abs(f) < 1.0f ? f.ToString("f2") :
                    Mathf.Abs(f) < 10.0f ? f.ToString("f1") :
                    f.ToString("f0");
            }
        }

        const float windowMinWidth = 600.0f;
        const float itemHeight = 26.0f;
        const float itemSpacing = 20.0f;
        const float contentHeight = 24.0f;
        const float contentSpacing = 5.0f;
        const float meshSizeWidth = 160.0f;
        const float voxelCountWidth = 150.0f;
        const float removeBtmWidth = 30.0f;
        const float mainLabelWidth = 150.0f;

        const string MV_PATH = "Assets/Ice Pond/Mesh Voxelizer/";

        const string tooltip_GenerationType = 
            "-Single Mesh: Generated result as a single mesh. " +
            "-Separate Voxels: Generate a group of individual voxel game objects";
        const string tooltip_VoxelSizeType =
            "-Subdivision: Set voxel size by subdivision level. " +
            "-Absolute Size: Set voxel size by absolute voxel size.";
        const string tooltip_Precision = 
            "Precision level, the result will be more accurate with higher precision, while voxelization time will increase.";
        const string tooltip_Approximation = 
            "Approximate voxels around original mesh's edge/corner, make voxelization result more smooth. " +
            "This is useful when voxelizing origanic objects";
        const string tooltip_ApplyScaling = 
            "Apply source GameObject's scaling.";
        const string tooltip_AlphaCutout = 
            "Discard transparent voxels.";
        const string tooltip_UVConversionType = 
            "-None: Generated mesh will not have any UV." +
            "-Source Mesh: Convert UVs from the source mesh." +
            "-Voxel Mesh: Keep individual voxel's UV.";
        const string tooltip_ConvertBoneWeights =
            "Convert bone weight from the source mesh.";
        const string tooltip_BackfaceCulling =
            "Cull backface.";
        const string tooltip_InnerfaceCulling =
            "Cull faces between voxels, since those faces are not visiable.";
        const string tooltip_Optimization = 
            "Optimize voxelization result.";
        const string tooltip_FillCenterSpace =
            "Fill model's center space with voxels.";
        const string tooltip_FillCenterMethod =
            "Try different axis if the result is incorrect.";
        const string tooltip_CenterMaterial = 
            "Material for center voxels.";
        const string tooltip_ModifyVoxel =
            "Use custom voxel instead of default cube voxel.";
        const string tooltip_VoxelMesh = 
            "Basic mesh for voxel.";
        const string tooltip_VoxelScale = 
            "Scale individual voxel.";
        const string tooltip_VoxelRotation =
            "Rotate individual voxel.";
        const string tooltip_ShowProgressBar = 
            "Show/Hide progress bar.";
        const string tooltip_CompactOutput = 
            "Zip all generated assets in a single game object.";
        const string tooltip_ExportVoxelizedTexture = 
            "Export generated textures as png images.";

        static Vector2 scrollPos;
        static bool settingsFold = true;
        static bool voxelFold = false;
        static bool generationFold = false;
        static bool presetFold = false;

        GUIStyle styleLabelMidAlign;

        MeshVoxelizerEditor meshVoxelizer;
        MeshVoxelizerPreset preset;
        string presetName;
        int maxSubdivision = 0;
        float maxAbsoluteVoxelSize = 0.0f;
        bool needRefreshTargets = false;
        Dictionary<GameObject, TargetInfo> targetDict = new Dictionary<GameObject, TargetInfo>();

        [MenuItem("Window/Mesh Voxelizer")]
        static void Init()
        {
            MeshVoxelizerWindow window = (MeshVoxelizerWindow)GetWindow(typeof(MeshVoxelizerWindow), false, "Mesh Voxelizer");
            window.Show();
        }

        private void OnDestroy()
        {
            preset = null;
            meshVoxelizer = null;
        }

        void OnGUI()
        {
            InitVoxelizer();

            //begin scroll view
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            //settings
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            settingsFold = EditorGUILayout.BeginFoldoutHeaderGroup(settingsFold, "Settigns");
            if (settingsFold) SettingsGUI();
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.EndVertical();

            //Voxel
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            voxelFold = EditorGUILayout.BeginFoldoutHeaderGroup(voxelFold, "Voxel");
            if (voxelFold) VoxelGUI();
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.EndVertical();

            //Generation
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            generationFold = EditorGUILayout.BeginFoldoutHeaderGroup(generationFold, "Generation");
            if (generationFold) GenerationGUI();
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.EndVertical();

            //preset
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            presetFold = EditorGUILayout.BeginFoldoutHeaderGroup(presetFold, "Presets");
            if (presetFold) PresetsGUI();
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.EndVertical();

            //Buttons
            EditorGUILayout.BeginHorizontal();
            GUILayoutOption[] sgbOptions = new GUILayoutOption[] { GUILayout.MinWidth(windowMinWidth / 2.0f), GUILayout.Height(32) };
            bool addButton = GUILayout.Button("Add Selected", sgbOptions);
            if (addButton)
            {
                foreach (GameObject go in Selection.gameObjects)
                {
                    if (!VerifyObject(go)) continue;
                    AddObject(go);
                }
            }
            bool removeButton = GUILayout.Button("Remove All", sgbOptions);
            if (removeButton) targetDict.Clear();
            EditorGUILayout.EndHorizontal();

            //Drop area
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DropAreaGUI();
            EditorGUILayout.EndVertical();

            //end scroll view
            EditorGUILayout.EndScrollView();

            //Voxelize button
            if (maxSubdivision > 200) EditorGUILayout.HelpBox("High subdivision level will take longer time to process", MessageType.Info);
            EditorGUI.BeginDisabledGroup(targetDict.Count == 0);
            GUILayoutOption[] vbOptions = new GUILayoutOption[] { GUILayout.Height(48) };
            string vbText = 
                targetDict.Count > 1 ? "Voxelize " + targetDict.Count + " meshes" :
                targetDict.Count == 1 ? "Voxelize 1 mesh" : "Voxelize 0 mesh";
            bool doVoxelization = GUILayout.Button(vbText, vbOptions);
            if (doVoxelization) DoVozelization();
            EditorGUI.EndDisabledGroup();
        }

        void InitVoxelizer()
        {
            if (meshVoxelizer != null) return;

            styleLabelMidAlign = new GUIStyle(GUI.skin.label);
            styleLabelMidAlign.alignment = TextAnchor.MiddleCenter;

            meshVoxelizer = new MeshVoxelizerEditor();
            meshVoxelizer.centerMaterial = MVHelper.GetDefaultVoxelMaterial();
            meshVoxelizer.voxelMesh = MVHelper.GetDefaultVoxelMesh();
        }

        void DoVozelization()
        {
            foreach(TargetInfo target in targetDict.Values)
            {
                meshVoxelizer.sourceGameObject = target.go;
                meshVoxelizer.VoxelizeMesh();
            }
        }

        bool MakeHighlightButton(string text, bool isHighlight, params GUILayoutOption[] options)
        {
            bool clicked = false;
            if (isHighlight)
            {
                Color temp = GUI.backgroundColor;
                GUI.backgroundColor = Color.green;
                clicked = GUILayout.Button(text, options);
                GUI.backgroundColor = temp;
            }
            else
            {
                clicked = GUILayout.Button(text, options);
            }
            return clicked;
        }

        int MakeToggleGroup(string label, string tooltip, string[] texts, int selectedIndex, float labelWidth, float buttonWidth, bool horizontalGroup = true)
        {
            if (horizontalGroup) EditorGUILayout.BeginHorizontal();
            if (string.IsNullOrEmpty(tooltip)) EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
            else EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.Width(labelWidth));
            int index = -1;
            for (int i = 0; i < texts.Length; ++i)
            {
                bool clicked = MakeHighlightButton(texts[i], i == selectedIndex, GUILayout.Width(buttonWidth));
                if (clicked) index = i;
            }
            if (horizontalGroup) EditorGUILayout.EndHorizontal();
            if (index >= 0) return index;
            else return selectedIndex;
        }

        bool MakeToggle(string label, string tooltip, bool isOn, float labelWidth, float buttonWidth, bool horizontalGroup = true)
        {
            if (horizontalGroup) EditorGUILayout.BeginHorizontal();
            if (string.IsNullOrEmpty(tooltip)) EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
            else EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.Width(labelWidth));
            int index = -1;
            bool clickOn = MakeHighlightButton("On", isOn, GUILayout.Width(buttonWidth));
            if (clickOn) index = 1;
            bool clickOff = MakeHighlightButton("Off", !isOn, GUILayout.Width(buttonWidth));
            if (clickOff) index = 0;
            if (horizontalGroup) EditorGUILayout.EndHorizontal();
            if (index >= 0) return index == 1;
            else return isOn;
        }

        void SettingsGUI()
        {
            //generation type
            meshVoxelizer.generationType = (MeshVoxelizer.GenerationType)MakeToggleGroup("Generation Type", tooltip_GenerationType,
                new string[] { "Single Mesh", "Separate Voxels" }, (int)meshVoxelizer.generationType, mainLabelWidth, 150.0f);

            //voxel size type
            MeshVoxelizer.VoxelSizeType voxelSizeType = (MeshVoxelizer.VoxelSizeType)MakeToggleGroup("Subdivision Method", tooltip_VoxelSizeType,
                new string[] { "Subdivison", "Absolute Size" }, (int)meshVoxelizer.voxelSizeType, mainLabelWidth, 150.0f);
            if (voxelSizeType != meshVoxelizer.voxelSizeType) needRefreshTargets = true;
            meshVoxelizer.voxelSizeType = voxelSizeType;

            //subdivision slider
            if (meshVoxelizer.voxelSizeType == MeshVoxelizer.VoxelSizeType.Subdivision)
            {
                int subdivisionLevel = EditorGUILayout.IntSlider("Subdivision Level", 
                    meshVoxelizer.subdivisionLevel, 1, MeshVoxelizer.MAX_SUBDIVISION, GUILayout.Width(609.0f));
                if (subdivisionLevel != meshVoxelizer.subdivisionLevel) needRefreshTargets = true;
                meshVoxelizer.subdivisionLevel = subdivisionLevel;
            }
            else
            {
                float absoluteVoxelSize = EditorGUILayout.Slider("Absolute Voxel Size", 
                    meshVoxelizer.absoluteVoxelSize, maxAbsoluteVoxelSize / MeshVoxelizer.MAX_SUBDIVISION, maxAbsoluteVoxelSize, GUILayout.Width(609.0f));
                if (Mathf.Approximately(absoluteVoxelSize, meshVoxelizer.absoluteVoxelSize)) needRefreshTargets = true;
                meshVoxelizer.absoluteVoxelSize = absoluteVoxelSize;
            }

            //UV conversion
            meshVoxelizer.uvConversion = (MeshVoxelizer.UVConversion)MakeToggleGroup("UV Conversion", tooltip_UVConversionType,
                new string[] { "None", "From Source Mesh", "From Voxel Mesh" }, (int)meshVoxelizer.uvConversion, mainLabelWidth, 150.0f);

            //precision
            meshVoxelizer.precision = (MeshVoxelizer.Precision)MakeToggleGroup("Mesh Scanning Precision", tooltip_Precision,
                new string[] { "Low", "Standard", "High" }, (int)meshVoxelizer.precision, mainLabelWidth, 150.0f);

            //Alpha Cutoff
            EditorGUILayout.BeginHorizontal();
            meshVoxelizer.alphaCutoff = MakeToggle("Alpha Cutoff", tooltip_AlphaCutout, meshVoxelizer.alphaCutoff, mainLabelWidth, 75.0f, false);
            EditorGUILayout.Space(itemSpacing, false);

            EditorGUI.BeginDisabledGroup(!meshVoxelizer.alphaCutoff);
            //Cutoff threshold
            meshVoxelizer.cutoffThreshold = EditorGUILayout.Slider("Cutoff Threshold", meshVoxelizer.cutoffThreshold, 0.0f, 1.0f, GUILayout.Width(306.0f));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            //Ignore Scaling
            bool applyScaling = MakeToggle("Apply Scaling", tooltip_ApplyScaling, meshVoxelizer.applyScaling, mainLabelWidth, 75.0f, false);
            if (applyScaling != meshVoxelizer.applyScaling) needRefreshTargets = true;
            meshVoxelizer.applyScaling = applyScaling;
            EditorGUILayout.Space(itemSpacing, false);

            //Approximation
            meshVoxelizer.edgeSmoothing = MakeToggle("Edge Smoothing", tooltip_Approximation, meshVoxelizer.edgeSmoothing, mainLabelWidth, 75.0f, false);
            EditorGUILayout.EndHorizontal();

            if (meshVoxelizer.generationType == MeshVoxelizer.GenerationType.SingleMesh)
            {
                //Innerface Culling
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(meshVoxelizer.modifyVoxel);
                meshVoxelizer.innerfaceCulling = MakeToggle("Innerface Culling", tooltip_InnerfaceCulling, meshVoxelizer.innerfaceCulling, mainLabelWidth, 75.0f, false);
                EditorGUILayout.Space(itemSpacing, false);

                //Backface Culling
                meshVoxelizer.backfaceCulling = MakeToggle("Backface Culling", tooltip_BackfaceCulling, meshVoxelizer.backfaceCulling, mainLabelWidth, 75.0f, false);
                EditorGUILayout.EndHorizontal();

                //Optimization
                EditorGUILayout.BeginHorizontal();
                meshVoxelizer.optimization = MakeToggle("Optimize Result", tooltip_Optimization, meshVoxelizer.optimization, mainLabelWidth, 75.0f, false);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.Space(itemSpacing, false);

                //Bone Weight Conversion
                EditorGUI.BeginDisabledGroup(meshVoxelizer.optimization && !meshVoxelizer.modifyVoxel);
                meshVoxelizer.boneWeightConversion = MakeToggle("Bone Weight Conversion", tooltip_ConvertBoneWeights, meshVoxelizer.boneWeightConversion, mainLabelWidth, 75.0f);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                //Fill Center
                meshVoxelizer.fillCenter = MakeToggle("Fill Center Space", tooltip_FillCenterSpace, meshVoxelizer.fillCenter, mainLabelWidth, 75.0f, false);
                EditorGUILayout.Space(itemSpacing, false);

                EditorGUI.BeginDisabledGroup(!meshVoxelizer.fillCenter);
                //Fill Method
                meshVoxelizer.fillMethod = (MeshVoxelizer.FillCenterMethod)EditorGUILayout.EnumPopup(new GUIContent("Fill Method", tooltip_FillCenterMethod), meshVoxelizer.fillMethod, GUILayout.Width(305.0f));
                EditorGUILayout.EndHorizontal();

                //Center Material
                meshVoxelizer.centerMaterial = (Material)EditorGUILayout.ObjectField(new GUIContent("Center Material", tooltip_CenterMaterial), meshVoxelizer.centerMaterial, typeof(Material), true, GUILayout.Width(305.0f));
                EditorGUI.EndDisabledGroup();
            }

            if (needRefreshTargets) { RefreshTargets(); needRefreshTargets = false; }
        }

        void VoxelGUI()
        {
            //Modify Voxel
            meshVoxelizer.modifyVoxel = MakeToggle("Modify Voxel", tooltip_ModifyVoxel, meshVoxelizer.modifyVoxel, mainLabelWidth, 75.0f);
            if (meshVoxelizer.modifyVoxel) EditorGUILayout.HelpBox("Modifying voxel will force voxelizer to generate using full voxel, this will disable face culling and result optimization.", MessageType.None);

            EditorGUI.BeginDisabledGroup(!meshVoxelizer.modifyVoxel);
            //voxel mesh
            meshVoxelizer.voxelMesh = (Mesh)EditorGUILayout.ObjectField(new GUIContent("Voxel Mesh", tooltip_VoxelMesh), meshVoxelizer.voxelMesh, typeof(Mesh), true);
            EditorGUIUtility.wideMode = true;
            //voxel scale
            meshVoxelizer.voxelScale = EditorGUILayout.Vector3Field(new GUIContent("Voxel Scale", tooltip_VoxelScale), meshVoxelizer.voxelScale);
            //voxel rotation
            meshVoxelizer.voxelRotation = EditorGUILayout.Vector3Field(new GUIContent("Voxel Rotation", tooltip_VoxelRotation), meshVoxelizer.voxelRotation);
            EditorGUIUtility.wideMode = false;
            EditorGUI.EndDisabledGroup();
        }

        void GenerationGUI()
        {
            //path
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Output Path", MeshVoxelizerEditor.OUTPUT_PATH);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.BeginHorizontal();
            //compact output
            meshVoxelizer.compactOutput = MakeToggle("Compact Output", tooltip_CompactOutput, meshVoxelizer.compactOutput, mainLabelWidth, 75.0f, false);
            EditorGUI.BeginDisabledGroup(meshVoxelizer.compactOutput || !meshVoxelizer.optimization);
            EditorGUILayout.Space(itemSpacing, false);
            //Export Optimized Texture
            meshVoxelizer.exportVoxelizedTexture = MakeToggle("Export Optimized Texture", tooltip_ExportVoxelizedTexture, meshVoxelizer.exportVoxelizedTexture, mainLabelWidth, 75.0f, false);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            //Show Progress Bar
            meshVoxelizer.showProgressBar = MakeToggle("Show Progress Bar", tooltip_ShowProgressBar, meshVoxelizer.showProgressBar, mainLabelWidth, 75.0f);
        }

        void PresetsGUI()
        {
            EditorGUILayout.HelpBox("Presets help you record Mesh Voxelizer settings", MessageType.None);
            if (preset == null)
            {
                preset = AssetDatabase.LoadAssetAtPath<MeshVoxelizerPreset>(MV_PATH + "Resources/MeshVoxelizerPreset.asset");
                if (preset == null)
                {
                    preset = ScriptableObject.CreateInstance<MeshVoxelizerPreset>();
                    AssetDatabase.CreateAsset(preset, MV_PATH + "Resources/MeshVoxelizerPreset.asset");
                }
            }
            if (preset == null) return;
            if (preset.settings == null) preset.settings = new List<MeshVoxelizerSetting>();
            if (string.IsNullOrEmpty(presetName)) presetName = "New Preset";

            //Preset list
            int counter = 1;
            List<MeshVoxelizerSetting> toDelete = new List<MeshVoxelizerSetting>();
            foreach (MeshVoxelizerSetting setting in preset.settings)
            {
                EditorGUILayout.BeginHorizontal();

                //preset name
                string pName = EditorGUILayout.TextField("Preset " + counter, setting.presetName, GUILayout.Width(304.0f));
                setting.SetPresetName(pName);

                //load preset
                if (GUILayout.Button("Load")) meshVoxelizer.ApplySetting(setting);

                //overwrite preset
                if (GUILayout.Button("Overwrite"))
                {
                    setting.RecordSetting(meshVoxelizer);
                    RefreshPreset();
                }

                //delete preset
                if (GUILayout.Button("Delete")) toDelete.Add(setting);

                EditorGUILayout.EndHorizontal();
                counter++;
            }

            //Add new preset
            EditorGUILayout.BeginHorizontal();
            presetName = EditorGUILayout.TextField("New Preset", presetName, GUILayout.Width(304.0f));
            if (GUILayout.Button("Add"))
            {
                MeshVoxelizerSetting setting = new MeshVoxelizerSetting();
                setting.SetPresetName(presetName);
                setting.RecordSetting(meshVoxelizer);
                preset.settings.Add(setting);
                RefreshPreset();
                presetName = "New Preset";
            }
            EditorGUILayout.EndHorizontal();

            //remove presets
            if (toDelete.Count > 0)
            {
                foreach (MeshVoxelizerSetting setting in toDelete)
                {
                    preset.settings.Remove(setting);
                }
                RefreshPreset();
            }
        }

        void RefreshPreset()
        {
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        void DropAreaGUI()
        {
            Event evt = Event.current;
            GUILayoutOption[] dropAreaOptions = new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true) };
            float areaHeight = targetDict.Count > 0 ? itemHeight * (targetDict.Count + 1): 38.0f;
            Rect drop_area = GUILayoutUtility.GetRect(0.0f, areaHeight, dropAreaOptions);
            GUI.Box(drop_area, "");

            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!drop_area.Contains(evt.mousePosition)) return;
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (Object dragged_object in DragAndDrop.objectReferences)
                        {
                            if (!(dragged_object is GameObject)) continue;
                            GameObject go = (GameObject)dragged_object;
                            if (!VerifyObject(go)) continue;
                            AddObject(go);
                        }
                    }
                    break;
            }

            if (targetDict.Count > 0)
            {
                SelectedItemsGUI(drop_area);
            }
            else
            {
                Rect dropInfoRect = new Rect(drop_area.x, drop_area.y, drop_area.width, 38.0f);
                EditorGUI.HelpBox(dropInfoRect, "Drag and drop GameObjects/Prefabs here", MessageType.Warning);
            }
        }

        void SelectedItemsGUI(Rect drop_area)
        {
            //title rect
            Rect titleRect = new Rect(drop_area.x + drop_area.width, drop_area.y, drop_area.width, contentHeight);

            //refresh button
            Rect refBtmRect = new Rect(titleRect.width - removeBtmWidth + 5.0f, titleRect.y, removeBtmWidth, contentHeight - 4.0f);
            bool refreshItems = GUI.Button(refBtmRect, new GUIContent("R", "Refresh"));
            if (refreshItems) RefreshTargets();

            //voxel count
            titleRect.x -= removeBtmWidth + voxelCountWidth + contentSpacing;
            titleRect.width = voxelCountWidth;
            GUI.Button(titleRect, "Voxel Bounding Box", EditorStyles.toolbarButton);

            //mesh size
            titleRect.x -= meshSizeWidth + contentSpacing;
            titleRect.width = meshSizeWidth;
            string meshSizeLabel = meshVoxelizer.applyScaling ? "Scaled Bounding Box" : "Mesh Bounding Box";
            GUI.Button(titleRect, meshSizeLabel, EditorStyles.toolbarButton);

            //target
            titleRect.width = drop_area.width - removeBtmWidth - voxelCountWidth - meshSizeWidth - contentSpacing * 3;
            titleRect.x = drop_area.x;
            GUI.Button(titleRect, "Target", EditorStyles.toolbarButton);

            //item list
            VerifyTargets();
            GameObject toRemove = null;
            Rect itemRect = new Rect(drop_area.x, drop_area.y + contentHeight, drop_area.width, contentHeight);
            foreach (TargetInfo target in targetDict.Values)
            {
                //item rect
                GUI.Box(itemRect, "", EditorStyles.helpBox);
                Rect contentRect = new Rect(itemRect.x + itemRect.width, itemRect.y, itemRect.width, contentHeight);

                //voxel count
                contentRect.x -= removeBtmWidth + voxelCountWidth + contentSpacing;
                contentRect.width = voxelCountWidth;
                EditorGUI.LabelField(contentRect, target.GetVoxelBBoxString(), styleLabelMidAlign);

                //mesh size
                contentRect.x -= meshSizeWidth + contentSpacing;
                contentRect.width = meshSizeWidth;
                EditorGUI.LabelField(contentRect, target.GetMeshBBoxString(), styleLabelMidAlign);

                //target
                contentRect.width = drop_area.width - removeBtmWidth - voxelCountWidth - meshSizeWidth - contentSpacing * 3;
                contentRect.x = itemRect.x;
                //this button will prevent object picker
                Rect maskRect = new Rect(contentRect.x + contentRect.width - 20.0f, contentRect.y, 20.0f, contentRect.height);
                GUI.Button(maskRect, "");
                EditorGUI.ObjectField(contentRect, target.go, typeof(GameObject), true);

                //remove button
                Rect rmBtmRect = new Rect(itemRect.width - removeBtmWidth + 5.0f, contentRect.y + 2.0f, removeBtmWidth, contentHeight - 4.0f);
                bool removeItem = GUI.Button(rmBtmRect, new GUIContent("-", "Remove"));
                if (removeItem) toRemove = target.go;

                //next line
                itemRect.y += itemHeight;
            }

            //remove deleted object
            if (toRemove != null) RemoveObject(toRemove);
        }

        void RefreshTargets()
        {
            maxAbsoluteVoxelSize = 0.0f;
            VerifyTargets();
            foreach (TargetInfo target in targetDict.Values)
            {
                ComputeMeshBoundingBox(target);
            }
            meshVoxelizer.absoluteVoxelSize = Mathf.Clamp(meshVoxelizer.absoluteVoxelSize, maxAbsoluteVoxelSize / MeshVoxelizer.MAX_SUBDIVISION, maxAbsoluteVoxelSize);

            maxSubdivision = 0;
            foreach (TargetInfo target in targetDict.Values)
            {
                ComputeVoxelBoundingBox(target);
            }
        }

        void VerifyTargets()
        {
            List<GameObject> toRemove = null;
            foreach (TargetInfo target in targetDict.Values)
            {
                if (target.go != null) continue;
                if (toRemove == null) toRemove = new List<GameObject>();
                toRemove.Add(target.go);
            }

            //remove invalid objects from object list
            if (toRemove != null)
            {
                foreach (GameObject go in toRemove)
                {
                    RemoveObject(go);
                }
            }
        }

        bool VerifyObject(GameObject go)
        {
            if (go.GetComponent<MeshRenderer>() == null && go.GetComponent<SkinnedMeshRenderer>() == null) return false;
            if (targetDict.ContainsKey(go)) return false;
            return true;
        }

        void AddObject(GameObject go)
        {
            targetDict.Add(go, new TargetInfo(go));
            RefreshTargets();
        }

        void RemoveObject(GameObject go)
        {
            targetDict.Remove(go);
            RefreshTargets();
        }

        void ComputeMeshBoundingBox(TargetInfo target)
        {
            if (target.mesh == null) return;
            target.meshBBoxSize.x = target.mesh.bounds.size.x;
            target.meshBBoxSize.y = target.mesh.bounds.size.y;
            target.meshBBoxSize.z = target.mesh.bounds.size.z;
            if (meshVoxelizer.applyScaling)
            {
                target.meshBBoxSize.x *= target.go.transform.lossyScale.x;
                target.meshBBoxSize.y *= target.go.transform.lossyScale.y;
                target.meshBBoxSize.z *= target.go.transform.lossyScale.z;
            }
            target.maxBBoxSize = Mathf.Max(target.meshBBoxSize.x, target.meshBBoxSize.y, target.meshBBoxSize.z);
            maxAbsoluteVoxelSize = Mathf.Max(maxAbsoluteVoxelSize, target.maxBBoxSize);
        }

        void ComputeVoxelBoundingBox(TargetInfo target)
        {
            if (target.mesh != null)
            {
                float unit = 1.0f;
                if (meshVoxelizer.voxelSizeType == MeshVoxelizer.VoxelSizeType.Subdivision)
                {
                    unit = target.maxBBoxSize / meshVoxelizer.subdivisionLevel;
                }
                else
                {
                    unit = meshVoxelizer.absoluteVoxelSize;
                }
                unit *= 1.00001f;
                target.voxelBBoxSize.x = Mathf.CeilToInt(target.meshBBoxSize.x / unit);
                target.voxelBBoxSize.y = Mathf.CeilToInt(target.meshBBoxSize.y / unit);
                target.voxelBBoxSize.z = Mathf.CeilToInt(target.meshBBoxSize.z / unit);
            }
            if (target.voxelBBoxSize.x == 0) target.voxelBBoxSize.x = 1;
            if (target.voxelBBoxSize.y == 0) target.voxelBBoxSize.y = 1;
            if (target.voxelBBoxSize.z == 0) target.voxelBBoxSize.z = 1;
            if (target.voxelBBoxSize.x > maxSubdivision) maxSubdivision = target.voxelBBoxSize.x;
            if (target.voxelBBoxSize.y > maxSubdivision) maxSubdivision = target.voxelBBoxSize.y;
            if (target.voxelBBoxSize.z > maxSubdivision) maxSubdivision = target.voxelBBoxSize.z;
        }
    }
}