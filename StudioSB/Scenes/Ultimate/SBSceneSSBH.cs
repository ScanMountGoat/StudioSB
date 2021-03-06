﻿using System;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.IO;
using StudioSB.Rendering;
using System.Collections.Generic;
using SFGraphics.GLObjects.Textures;
using SSBHLib;
using SSBHLib.Formats;
using SFGraphics.GLObjects.Shaders;
using SFGraphics.Cameras;
using StudioSB.IO.Models;
using SFGraphics.GLObjects.BufferObjects;

namespace StudioSB.Scenes.Ultimate
{
    [SceneFileInformation("NU Model File Binary", ".numdlb", "The model specification for Smash Ultimate models", sceneCode: "SSBU")]
    public class SBSceneSSBH : SBScene
    {
        /// <summary>
        /// 
        /// </summary>
        public ISBModel<SBUltimateMesh<UltimateVertex>> Model { get; set; }

        public Dictionary<string, SBSurface> nameToSurface = new Dictionary<string, SBSurface>();

        // Rendering
        public Dictionary<SBUltimateMesh<UltimateVertex>, UltimateRenderMesh> sbMeshToRenderMesh = new Dictionary<SBUltimateMesh<UltimateVertex>, UltimateRenderMesh>();
        public Dictionary<SBSurface, Texture> surfaceToRenderTexture = new Dictionary<SBSurface, Texture>();
        public BufferObject boneUniformBuffer;
        Matrix4[] boneBinds = new Matrix4[200];

        public SBSceneSSBH()
        {
            HasMesh = true;
            HasBones = true;
            boneUniformBuffer = new BufferObject(BufferTarget.UniformBuffer);
        }

        /// <summary>
        /// Loads the scene from a NUMDLB file
        /// </summary>
        /// <param name="FileName"></param>
        public override void LoadFromFile(string FileName)
        {
            string folderPath = Path.GetDirectoryName(FileName);

            ISSBH_File File;
            if (!SSBH.TryParseSSBHFile(FileName, out File))
                return;

            MODL modl = (MODL)File;

            string meshPath = "";
            string skelPath = "";
            string matlPath = "";
            foreach (string file in Directory.EnumerateFiles(folderPath))
            {
                // load textures
                if (file.EndsWith(".nutexb"))
                {
                    NUTEX_Loader.Open(file, this);
                }

                string fileName = Path.GetFileName(file);
                if (fileName.Equals(modl.SkeletonFileName))
                {
                    skelPath = file;
                }
                if (fileName.Equals(modl.MeshString))
                {
                    meshPath = file;
                }
                if (fileName.Equals(modl.MaterialFileNames[0].MaterialFileName))
                {
                    matlPath = file;
                }
            }
            // import order Skeleton+Textures->Materials->Mesh
            // mesh needs to be loaded after skeleton
            if (skelPath != "")
            {
                SBConsole.WriteLine($"Importing skeleton: {Path.GetFileName(skelPath)}");
                SKEL_Loader.Open(skelPath, this);
            }
            if(matlPath != "")
            {
                SBConsole.WriteLine($"Importing materials: {Path.GetFileName(matlPath)}");
                MATL_Loader.Open(matlPath, this);
            }
            if (meshPath != "")
            {
                SBConsole.WriteLine($"Importing mesh: {Path.GetFileName(meshPath)}");
                MESH_Loader.Open(meshPath, this);
                
                // set materials
                foreach(var entry in modl.ModelEntries)
                {
                    UltimateMaterial currentMaterial = null;
                    foreach (UltimateMaterial matentry in Materials)
                    {
                        if (matentry.Label.Equals(entry.MaterialName))
                        {
                            currentMaterial = matentry;
                            break;
                        }
                    }
                    if (currentMaterial == null)
                        continue;

                    int subindex = 0;
                    string prevMesh = "";
                    if (Model != null)
                    {
                        foreach (var mesh in Model.Meshes)
                        {
                            if (prevMesh.Equals(mesh.Name))
                                subindex++;
                            else
                                subindex = 0;
                            prevMesh = mesh.Name;
                            if (subindex == entry.SubIndex && mesh.Name.Equals(entry.MeshName))
                            {
                                mesh.Material = currentMaterial;
                                break;
                            }
                        }

                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="FileName"></param>
        public override void ExportSceneToFile(string FileName)
        {
            if (Model == null) return;

            string name = Path.GetDirectoryName(FileName) + "/" + Path.GetFileNameWithoutExtension(FileName);
            string simpleName = Path.GetFileNameWithoutExtension(FileName);

            SBConsole.WriteLine("Creating MODL...");
            var modl = MODL_Loader.CreateMODLFile((SBUltimateModel)Model);
            modl.ModelFileName = simpleName;
            modl.SkeletonFileName = $"{simpleName}.nusktb";
            modl.MeshString = $"{simpleName}.numshb";
            modl.UnknownFileName = "";
            modl.MaterialFileNames = new MODL_MaterialName[] { new MODL_MaterialName() { MaterialFileName = $"{simpleName}.numatb" } };
            SBConsole.WriteLine("Done");
            SSBH.TrySaveSSBHFile(FileName, modl);

            SBConsole.WriteLine($"Creating MESH... {name}.numshb");
            var mesh = MESH_Loader.CreateMESH((SBUltimateModel)Model, (SBSkeleton)Skeleton);
            SBConsole.WriteLine("Done");
            SSBH.TrySaveSSBHFile(name + ".numshb", mesh);

            SBConsole.WriteLine($"Creating SKEL.. {name}.nusktb");
            SKEL_Loader.Save(name + ".nusktb", this);
            SBConsole.WriteLine("Done");

            //SBConsole.WriteLine("Creating MATL...");

        }

        /// <summary>
        /// Gets the model information in this scene as an IO Model
        /// </summary>
        /// <returns></returns>
        public override IOModel GetIOModel()
        {
            IOModel iomodel = new IOModel();
            iomodel.Skeleton = (SBSkeleton)Skeleton;

            foreach (var mesh in Model.Meshes)
            {
                var iomesh = new IOMesh();
                iomesh.Name = mesh.Name;
                iomodel.Meshes.Add(iomesh);

                iomesh.HasPositions = mesh.ExportAttributes.Contains(SSBHLib.Tools.MESHAttribute.Position0);
                iomesh.HasNormals = mesh.ExportAttributes.Contains(SSBHLib.Tools.MESHAttribute.Normal0);
                iomesh.HasUV0 = mesh.ExportAttributes.Contains(SSBHLib.Tools.MESHAttribute.map1);
                iomesh.HasUV1 = mesh.ExportAttributes.Contains(SSBHLib.Tools.MESHAttribute.uvSet);
                iomesh.HasUV2 = mesh.ExportAttributes.Contains(SSBHLib.Tools.MESHAttribute.uvSet1);
                iomesh.HasUV3 = mesh.ExportAttributes.Contains(SSBHLib.Tools.MESHAttribute.uvSet2);
                iomesh.HasColor = mesh.ExportAttributes.Contains(SSBHLib.Tools.MESHAttribute.colorSet1);

                iomesh.HasBoneWeights = true;

                iomesh.Indices.AddRange(mesh.Indices);

                foreach (var vertex in mesh.Vertices)
                {
                    var iovertex = new IOVertex();

                    iovertex.Position = vertex.Position0;
                    iovertex.Normal = vertex.Normal0;
                    iovertex.Tangent = vertex.Tangent0;
                    iovertex.UV0 = vertex.Map1;
                    iovertex.UV1 = vertex.UvSet;
                    iovertex.UV2 = vertex.UvSet1;
                    iovertex.Color = vertex.ColorSet1;
                    iovertex.BoneIndices = new Vector4(vertex.BoneIndices.X, vertex.BoneIndices.Y, vertex.BoneIndices.Z, vertex.BoneIndices.W);
                    iovertex.BoneWeights = vertex.BoneWeights;

                    // single bind fix
                    if(mesh.ParentBone != "" && Skeleton != null)
                    {
                        var parentBone = Skeleton[mesh.ParentBone];
                        if (parentBone != null)
                        {
                            iovertex.Position = Vector3.TransformPosition(vertex.Position0, parentBone.WorldTransform);
                            iovertex.Normal = Vector3.TransformNormal(vertex.Normal0, parentBone.WorldTransform);
                            iovertex.BoneIndices = new Vector4(Skeleton.IndexOfBone(parentBone), 0, 0, 0);
                            iovertex.BoneWeights = new Vector4(1, 0, 0, 0);
                        }
                    }

                    iomesh.Vertices.Add(iovertex);
                }
            }

            return iomodel;
        }

        /// <summary>
        /// Imports information into this scene from an IO Model
        /// </summary>
        public override void FromIOModel(IOModel iomodel)
        {
            // copy skeleton
            Skeleton = iomodel.Skeleton;

            // make temp material
            UltimateMaterial material = new UltimateMaterial();
            material.Name = "SFX_PBS_0100080008008269_opaque";
            material.Label = "skin_sonic_001";

            //TODO more elegant material management
            Materials.Add(material);

            // convert meshes
            SBUltimateModel model = new SBUltimateModel();

            foreach(var iomesh in iomodel.Meshes)
            {
                SBUltimateMesh<UltimateVertex> mesh = new SBUltimateMesh<UltimateVertex>();
                mesh.Name = iomesh.Name;
                mesh.ParentBone = "";
                mesh.Material = material;

                model.Meshes.Add(mesh);

                mesh.Indices = iomesh.Indices;

                iomesh.GenerateTangentsAndBitangents();

                foreach(var vertex in iomesh.Vertices)
                    mesh.Vertices.Add(IOToUltimateVertex(vertex));

                //TODO: make more customizable through import settings
                mesh.ExportAttributes.Add(SSBHLib.Tools.MESHAttribute.Position0);
                mesh.ExportAttributes.Add(SSBHLib.Tools.MESHAttribute.Normal0);
                mesh.ExportAttributes.Add(SSBHLib.Tools.MESHAttribute.Tangent0);
                mesh.ExportAttributes.Add(SSBHLib.Tools.MESHAttribute.map1);
                mesh.ExportAttributes.Add(SSBHLib.Tools.MESHAttribute.colorSet1);
            }

            Model = model;
        }

        private static UltimateVertex IOToUltimateVertex(IOVertex iov)
        {
            return new UltimateVertex(iov.Position, iov.Normal, iov.Tangent, Vector3.Zero, iov.UV0, iov.UV1,
                iov.UV2, new IVec4() { X = (int)iov.BoneIndices.X,
                    Y = (int)iov.BoneIndices.Y,
                    Z = (int)iov.BoneIndices.Z,
                    W = (int)iov.BoneIndices.W }, 
                iov.BoneWeights, Vector2.Zero, Vector4.One, Vector4.One);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override ISBMesh[] GetMeshObjects()
        {
            return Model.Meshes.ToArray();
        }

        public override ISBMaterial[] GetMaterials()
        {
            return Materials.ToArray();
        }

        /// <summary>
        /// FOR THE LOVE OF SAKURAI DON'T USE THIS
        /// </summary>
        public override void RenderLegacy()
        {
            if(Model != null)
            foreach(var mesh in Model.Meshes)
                {
                    GL.PushMatrix();
                    Matrix4 transform = Matrix4.Identity;
                    if (Skeleton != null && mesh.ParentBone != "" && Skeleton.ContainsBone(mesh.ParentBone))
                        transform = Skeleton[mesh.ParentBone].WorldTransform;
                    GL.MultMatrix(ref transform);

                    GL.Begin(PrimitiveType.Triangles);
                    foreach(var index in mesh.Indices)
                    {
                        var vertex = mesh.Vertices[(int)index];
                        GL.Color3(vertex.Normal0);
                        GL.Vertex3(vertex.Position0);
                    }
                    GL.End();
                    GL.PopMatrix();
            }

            // the base SBScene can render the skeleton
            base.RenderLegacy();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ModelViewProjection"></param>
        public override void RenderShader(Camera camera)
        {
            RenderModelShader(camera);

            // the base SBScene can render the skeleton
            base.RenderShader(camera);
        }


        /// <summary>
        /// Updates the render meshes and textures to reflect changes
        /// </summary>
        public void RefreshRendering()
        {
            sbMeshToRenderMesh.Clear();

            if (Model != null)
            {
                foreach (var mesh in Model.Meshes)
                {
                    UltimateRenderMesh rmesh = new UltimateRenderMesh(mesh.Vertices, mesh.Indices);
                    sbMeshToRenderMesh.Add(mesh, rmesh);
                }
            }

            nameToSurface.Clear();
            surfaceToRenderTexture.Clear();
            foreach(var tex in Surfaces)
            {
                nameToSurface.Add(tex.Name, tex);
                surfaceToRenderTexture.Add(tex, tex.CreateRenderTexture());
            }
        }

        /// <summary>
        /// Renders the model using a shader
        /// </summary>
        /// <param name="ModelViewProjection"></param>
        private void RenderModelShader(Camera camera)
        {
            if (Model == null) return;

            var shader = GetShader();
            if (!shader.LinkStatusIsOk)
                return;

            shader.UseProgram();
            
            // Bones
            int blockIndex = GL.GetUniformBlockIndex(shader.Id, "Bones");
            boneUniformBuffer.BindBase(BufferRangeTarget.UniformBuffer, blockIndex);
            if (Skeleton != null)
            {
                boneBinds = Skeleton.GetBindTransforms();
            }
            boneUniformBuffer.SetData(boneBinds, BufferUsageHint.DynamicDraw);

            SetShaderUniforms(shader);
            SetShaderCamera(shader, camera);

            foreach (var mesh in Model.Meshes)
            {
                if (!mesh.Visible) continue;
                
                shader.SetBoolToInt("renderWireframe", mesh.Selected || ApplicationSettings.EnableWireframe);

                // refresh rendering if this not is not setup
                if (!sbMeshToRenderMesh.ContainsKey(mesh))
                {
                    RefreshRendering();
                }

                // bind material
                if (mesh.Material != null)
                    mesh.Material.Bind(this, shader);

                // single bind transforms
                Matrix4 transform = Matrix4.Identity;
                if (Skeleton != null && mesh.ParentBone != "" && Skeleton.ContainsBone(mesh.ParentBone))
                    transform = Skeleton[mesh.ParentBone].AnimatedWorldTransform;
                shader.SetMatrix4x4("transform", ref transform);

                // draw mesh
                var rmesh = sbMeshToRenderMesh[mesh];
                rmesh.Draw(shader, null);
            }

            //StudioSB.Rendering.Shapes.Sphere.DrawRectangularPrism(((SBUltimateModel)Model).VolumeCenter, size.X, size.Y, size.Z, true);

            //StudioSB.Rendering.Shapes.Sphere.DrawSphereLegacy(Model.BoundingSphere.Xyz, Model.BoundingSphere.W, 20, true);
        }

        private Shader GetShader()
        {
            if (ApplicationSettings.UseDebugShading)
                return ShaderManager.GetShader("UltimateModelDebug");
            else
                return ShaderManager.GetShader("UltimateModel");
        }

        private void SetShaderCamera(Shader shader, Camera camera)
        {
            Matrix4 mvp = camera.MvpMatrix;
            shader.SetMatrix4x4("mvp", ref mvp);

            shader.SetVector3("V", camera.ViewVector);
        }

        private void SetShaderUniforms(Shader shader)
        {
            shader.SetVector4("renderChannels", ApplicationSettings.RenderChannels);
            shader.SetInt("renderMode", (int)ApplicationSettings.ShadingMode);

            //TODO:
            // this is smash ultimate specific
            shader.SetInt("transitionEffect", 0);
            shader.SetFloat("transitionFactor", 0);

            shader.SetBoolToInt("renderDiffuse", ApplicationSettings.EnableDiffuse);
            shader.SetBoolToInt("renderSpecular", ApplicationSettings.EnableSpecular);
            shader.SetBoolToInt("renderEmission", ApplicationSettings.EnableEmission);
            shader.SetBoolToInt("renderRimLighting", ApplicationSettings.EnableRimLighting);

            shader.SetBoolToInt("renderNormalMaps", ApplicationSettings.RenderNormalMaps);
            shader.SetBoolToInt("renderVertexColor", ApplicationSettings.RenderVertexColor);
        }


    }
}
