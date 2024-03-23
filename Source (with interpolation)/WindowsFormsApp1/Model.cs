using Assimp.Unmanaged;
using Assimp;
using GlmSharp;
using NetGL;
using OpenObjectLoader;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using GenesisMath.Math;

namespace WindowsFormsApp1
{
    public class Model
    {
        public List<Material> Materials { get; set; }
        public Dictionary<String, boneinfo> BoneInfoMap { get; set; }
        public List<Animation.Animation> Animations { get; set; }
        public int BoneCounter { get; set; }
        public List<Mesh> Meshes { get; set; }
        public String FileDirectory { get; set; }
        public String FileName { get; set; }

        private Animation.Animation animation;
        private Animation.Animator animator;
        private List<mat4> test;

        public Model(String filename)
        {
            test = new List<mat4>();
            for(int i = 0; i < 100; i++)
            {
                test.Add(mat4.Identity);
            }

            BoneInfoMap = new Dictionary<string, boneinfo>();
            FileInfo fileInfo = new FileInfo(filename);
            this.FileDirectory = fileInfo.DirectoryName;
            this.FileName = fileInfo.Name;

            Assimp.Scene model;
            Assimp.AssimpContext importer = new Assimp.AssimpContext();
            importer.SetConfig(new Assimp.Configs.NormalSmoothingAngleConfig(66.0f));
            model = importer.ImportFile(filename, Assimp.PostProcessSteps.Triangulate | Assimp.PostProcessSteps.CalculateTangentSpace | Assimp.PostProcessSteps.JoinIdenticalVertices);

            this.ExtractMaterials(model);
            this.ExtractMeshes(model);
            this.ExtractAnimations(model);
        }

        public void ExtractMaterials(Assimp.Scene scene)
        {
            this.Materials = new List<Material>();
            foreach(var aiMaterial in scene.Materials)
            {
                Material material = new Material();
                material.Name = aiMaterial.Name;

                if(aiMaterial.HasTextureDiffuse)
                {
                    material.TextureDiffuse = (Bitmap)Bitmap.FromFile(this.FileDirectory + "\\" + aiMaterial.TextureDiffuse.FilePath);
                }
                else
                {
                    material.TextureDiffuse = CreateEmptyTexture(1, 1, Color.White);
                }
                this.Materials.Add(material);
            }
        }

        public void ExtractMeshes(Assimp.Scene scene)
        {
            this.Meshes = new List<Mesh>();
            foreach(var aiMesh in scene.Meshes)
            {
                var mesh = new Mesh();
                mesh.Name = aiMesh.Name;
                mesh.Material = this.Materials[aiMesh.MaterialIndex];
                mesh.Indices.AddRange(aiMesh.GetIndices());
                for(int i = 0; i < aiMesh.VertexCount; i++)
                {
                    var vertex = new vertex();
                    vertex.position = new GlmSharp.vec3(aiMesh.Vertices[i].X, aiMesh.Vertices[i].Y, aiMesh.Vertices[i].Z);
                    SetVertexBoneDataToDefault(ref vertex);
                    if (aiMesh.TextureCoordinateChannels[0] != null)
                    {
                        vertex.textcoords = new vec2(aiMesh.TextureCoordinateChannels[0][i].X, aiMesh.TextureCoordinateChannels[0][i].Y);
                    }
                    else
                    {
                        vertex.textcoords = new vec2(0f);
                    }
                    mesh.Vertices.Add(vertex);
                }
                ExtractBoneWeightForVertices(aiMesh, scene, mesh);
                this.Meshes.Add(mesh);
            }
        }

        public void ExtractAnimations(Assimp.Scene scene)
        {
            Animations = new List<Animation.Animation>();
            animation = new Animation.Animation(scene, this);
            animator = new Animation.Animator(animation);
        }

        private void SetVertexBoneDataToDefault(ref vertex vertex)
        {
            vertex.BoneIDs = new ivec4(-1);
            vertex.BoneWeights = new vec4(0.0f);
        }

        private void SetVertexBoneData(ref vertex v, int boneId, float weight)
        {
            for (int i = 0; i < 4; ++i)
            {
                if (v.BoneIDs[i] < 0)
                {
                    v.BoneWeights[i] = weight;
                    v.BoneIDs[i] = boneId;
                    break;
                }
            }
        }

        private void ExtractBoneWeightForVertices(Assimp.Mesh mesh, Assimp.Scene scene, Mesh gmesh)
        {
            for(int boneIndex = 0; boneIndex < mesh.BoneCount; boneIndex++)
            {
                int boneId = -1;
                var boneName = mesh.Bones[boneIndex].Name;
                if(!BoneInfoMap.ContainsKey(boneName))
                {
                    var boneInfo = new boneinfo();
                    boneInfo.id = BoneCounter;
                    boneInfo.offset = ConvertToGlmMat4(mesh.Bones[boneIndex].OffsetMatrix);
                    BoneInfoMap.Add(boneName, boneInfo);
                    boneId = BoneCounter;
                    BoneCounter++;
                }
                else
                {
                    boneId = BoneInfoMap[boneName].id;
                }

                var weights = mesh.Bones[boneIndex].VertexWeights;
                var numWeights = mesh.Bones[boneIndex].VertexWeightCount;
                for(int weigthIndex = 0; weigthIndex < numWeights; weigthIndex++)
                {
                    int vertexId = weights[weigthIndex].VertexID;
                    float weight = weights[weigthIndex].Weight;
                    Debug.Assert(vertexId <= gmesh.Indices.Count);
                    var vertex = gmesh.Vertices[vertexId];
                    SetVertexBoneData(ref vertex, boneId, weight);
                    gmesh.Vertices[vertexId] = vertex;
                }
            }
        }



        public void InitModel(OpenGL gl)
        {
            foreach (var mesh in this.Meshes)
            {
                mesh.Material.TextureDiffuseID = this.InitTexture(gl, mesh.Material.TextureDiffuse);

                Console.WriteLine("Loading mesh " + mesh.Name);
                Console.WriteLine("---------------------------------------------------------------------------------");

                var verices = mesh.Vertices.ToArray();
                var vertexSize = Marshal.SizeOf<vertex>();
                var indices = mesh.Indices.ToArray();

                mesh.VAO = gl.GenVertexArrays(1);
                mesh.VBO = gl.GenBuffer(1);
                mesh.EBO = gl.GenBuffer(1);

                gl.BindVertexArray(mesh.VAO);

                gl.BindBuffer(OpenGL.ArrayBuffer, mesh.VBO);
                Console.WriteLine("Generating vertex buffer object for mesh " + mesh.Name);
                gl.BufferData(OpenGL.ArrayBuffer, verices.Length * vertexSize, verices, OpenGL.StaticDraw);
                Console.WriteLine("Generated vertex buffer object for mesh " + mesh.Name + " with error " + gl.GetError());

                gl.BindBuffer(OpenGL.ElementArrayBuffer, mesh.EBO);
                Console.WriteLine("Generating index buffer object for mesh " + mesh.Name);
                gl.BufferData(OpenGL.ElementArrayBuffer, indices.Length * sizeof(int), indices, OpenGL.StaticDraw);
                Console.WriteLine("Generated index buffer object for mesh " + mesh.Name + " with error " + gl.GetError());

                gl.EnableVertexAttribArray(0);
                gl.VertexAttribPointer(0, 3, OpenGL.Float, false, vertexSize, IntPtr.Zero);

                gl.EnableVertexAttribArray(2);
                gl.VertexAttribPointer(2, 2, OpenGL.Float, false, vertexSize, Marshal.OffsetOf<vertex>("textcoords"));

                gl.EnableVertexAttribArray(3);
                gl.VertexAtrribIPointer(3, 4, OpenGL.Int, vertexSize, Marshal.OffsetOf<vertex>("BoneIDs"));

                // weights
                gl.EnableVertexAttribArray(4);
                gl.VertexAttribPointer(4, 4, OpenGL.Float, false, vertexSize, Marshal.OffsetOf<vertex>("BoneWeights"));

                gl.BindVertexArray(0);
            }
        }

        private int InitTexture(OpenGL gl, Bitmap bitmap)
        {
            int texid = gl.GenTextures(1);
            gl.BindTexture(NetGL.OpenGL.Texture2D, texid);
            gl.TexParameteri(NetGL.OpenGL.Texture2D, NetGL.OpenGL.TextureMinFilter, NetGL.OpenGL.Nearest);
            gl.TexParameteri(NetGL.OpenGL.Texture2D, NetGL.OpenGL.TextureMagFilter, NetGL.OpenGL.Linear);
            gl.TexParameteri(NetGL.OpenGL.Texture2D, NetGL.OpenGL.TextureWrapS, NetGL.OpenGL.Repeate);
            gl.TexParameteri(NetGL.OpenGL.Texture2D, NetGL.OpenGL.TextureWrapT, NetGL.OpenGL.Repeate);
            gl.TexImage2D(NetGL.OpenGL.Texture2D, 0, NetGL.OpenGL.RGBA, bitmap.Width, bitmap.Height, 0, NetGL.OpenGL.BGRAExt, NetGL.OpenGL.UnsignedByte, bitmap);
            return texid;
        }

        public void Draw(OpenGL gl, int program, mat4 m_mat, mat4 v_mat, mat4 p_mat)
        {
            foreach (var mesh in this.Meshes)
            {
                gl.UseProgram(program);
                gl.UniformMatrix4fv(gl.GetUniformLocation(program, "projection"), 1, false, p_mat.ToArray());
                gl.UniformMatrix4fv(gl.GetUniformLocation(program, "view"), 1, false, v_mat.ToArray());
                gl.UniformMatrix4fv(gl.GetUniformLocation(program, "model"), 1, false, m_mat.ToArray());

                animator.UpdateAnimation(0.02f);

                for(int i = 0; i < 100; i++)
                {
                    gl.UniformMatrix4fv(gl.GetUniformLocation(program, "finalBonesMatrices[" + i.ToString() + "]"), 1, false, animator.FinalBoneMatrices[i].ToArray());
                }

                gl.ActiveTexture(OpenGL.Texture0);
                gl.BindTexture(OpenGL.Texture2D, mesh.Material.TextureDiffuseID);
                gl.Uniform1I(gl.GetUniformLocation(program, "textureSampler"), 0);

                gl.BindVertexArray(mesh.VAO);
                gl.BindBuffer(OpenGL.ElementArrayBuffer, mesh.EBO);
                gl.DrawElements(OpenGL.Triangles, mesh.Indices.Count, OpenGL.UnsignedInt);
            }
        }

        public static Color ConvertDrawingColor(float a, float r, float g, float b)
        {
            return Color.FromArgb((int)a * 255, (int)r * 255, (int)g * 255, (int)b * 255);
        }

        public static Bitmap CreateEmptyTexture(int width, int height, Color color)
        {
            Bitmap bitmap = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bitmap.SetPixel(x, y, color);
                }
            }

            return bitmap;
        }

        public static mat4 ConvertToGlmMat4(Assimp.Matrix4x4 matrix)
        {
            var mat = new mat4();
            mat.m00 = matrix.A1; // col 0, row 0
            mat.m01 = matrix.B1; // col 0, row 1
            mat.m02 = matrix.C1; // col 0, row 2
            mat.m03 = matrix.D1; // col 0, row 3

            mat.m10 = matrix.A2; // col 1, row 0
            mat.m11 = matrix.B2; // col 1, row 1
            mat.m12 = matrix.C2; // col 1, row 2
            mat.m13 = matrix.D2; // col 1, row 3

            mat.m20 = matrix.A3; // col 2, row 0
            mat.m21 = matrix.B3; // col 2, row 1
            mat.m22 = matrix.C3; // col 2, row 2
            mat.m23 = matrix.D3; // col 2, row 3

            mat.m30 = matrix.A4; // col 3, row 0
            mat.m31 = matrix.B4; // col 3, row 1
            mat.m32 = matrix.C4; // col 3, row 2
            mat.m33 = matrix.D4; // col 3, row 3

            return mat;
        }

        public static vec3 GetGLMVec(Assimp.Vector3D vec)
        {
            return new vec3(vec.X, vec.Y, vec.Z);
        }

        public static quat GetGLMQuat(Assimp.Quaternion pOrientation)
	    {
		    return new quat(pOrientation.X, pOrientation.Y, pOrientation.Z, pOrientation.W);
	    }

}
}
