-Basic usage: 
    -Open "Window -> Mesh Voxelizer" window
    -Select source GameObject from currently opened scene
    -Adjust settings and voxelize the mesh
    -Voxelized result will be automatically saved as a prefab in "Assets/MeshVoxlizer/Temp" folder

*Note: 
-For skinnedMeshRenderer, only the voxelized mesh object will be saved, please replace sourceGameObject's "skinnedMeshRenderer.Mesh" manually.
-To convert 2D image into 3D voxelized mesh: 
    1.Create a material using the image as texture.
    2.Create a plane and assign the material created in step 1.
    3.Enable "Alpha Cutout" (optional) and Voxelize the plane.

-Properties:
    -Generation Type: 
        -Single Mesh: Generated result as a single mesh.
        -Separate Voxels: Generate a group of individual voxel game objects.
    -Voxel Size Type: 
        -Subdivision: Set voxel size by subdivision level. 
        -Absolute Size: Set voxel size by absolute voxel size.
    -Precision: 
        Precision level, the result will be more accurate with higher precision, while voxelization time will increase.
    -Approximation:
        Approximate voxels around original mesh's edge/corner, make voxelization result more smooth.
        This is useful when voxelizing origanic objects
    -Ignore Scaling:
        Ignore source GameObject's local scale.
    -Alpha Cutout:
        Discard transparent voxels.
    -UV Conversion Type: 
        -None: Generated mesh will not have any UV.
        -Source Mesh: Convert UVs from the source mesh. 
        -Voxel Mesh: Keep individual voxel's UV.
    -Convert Bone Weights (SkinnedMeshRenderer only): 
        Convert bone weight from the source mesh.
    -Backface Culling (for Generation Type = Single Mesh): 
        Cull backface.
    -Optimization (for Generation Type = Single Mesh): 
        Optimize voxelization result.
    -Fill Center Space (for Generation Type = Separate Voxels)
        Fill model's center space with voxels. Try different axis if the result is incorrect.
    -Center Material (for Generation Type = Separate Voxels):
        Material for center voxels.
    -Modify Voxel: 
        Use custom voxel instead of default cube voxel. 
        Enabling this will disable Backface Culling and Optimization.
    -Voxel Mesh: 
        Basic mesh for voxel.
    -Voxel Scale: 
        Scale individual voxel.
    -Voxel Rotation:
        Rotate individual voxel.

-Advanced Options:
    -Show Progress Bar:
        Show/Hide progress bar.
    -Compact Output:
        Zip all generated assets in a single game object.
    -Export Voxelized Texture:
        Export generated textures as png images.