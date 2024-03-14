using NetGL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GenesisMath.Math;
using GlmSharp;
using OpenObjectLoader;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private NetGL.OpenGL gl;
        private float rotate;
        private Animation.Animator animator;

        /// <summary>
        /// Initial the Windows Form
        /// </summary>
        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
            DoubleBuffered = false;
        }       

        /// <summary>
        /// Our rendering thread
        /// </summary>
        private void loop()
        {
            //First we need to define where our models located in
            String modelspath = new System.IO.FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location).Directory + "\\Models";

            Model m = new Model(modelspath + "\\Vampire\\vampire.dae");

            //Now we create our shader
            string vertexShaderCode = @"
                #version 430 core

                layout(location = 0) in vec3 pos;
                layout(location = 1) in vec3 norm;
                layout(location = 2) in vec2 tex;
                layout(location = 3) in ivec4 boneIds; 
                layout(location = 4) in vec4 weights;
	
                uniform mat4 projection;
                uniform mat4 view;
                uniform mat4 model;
	
                const int MAX_BONES = 100;
                const int MAX_BONE_INFLUENCE = 4;
                uniform mat4 finalBonesMatrices[MAX_BONES];
	
                out vec2 texCoord;
	
                void main()
                {
                    vec4 totalPosition = vec4(0.0f);
                    for(int i = 0 ; i < MAX_BONE_INFLUENCE ; i++)
                    {
                        if(boneIds[i] == -1) 
                            continue;
                        if(boneIds[i] >=MAX_BONES) 
                        {
                            totalPosition = vec4(pos,1.0f);
                            break;
                        }
                        vec4 localPosition = finalBonesMatrices[boneIds[i]] * vec4(pos,1.0f);
                        totalPosition += localPosition * weights[i];
                        vec3 localNormal = mat3(finalBonesMatrices[boneIds[i]]) * norm;
                    }
		
                    mat4 viewModel = view * model;
                    gl_Position =  projection * viewModel * totalPosition;
                    texCoord = tex;
                }
            ";

            //Creating the fragment shader
            string fragmentShaderCode = @"
                #version 430 core

                in vec3 fragPos;
                in vec3 color;
                in vec2 texCoord;

                out vec4 fragColor;
                uniform sampler2D textureSampler;
                uniform sampler2D normalMap;

                void main()
                {
                    vec2 flippedTexCoord = vec2(texCoord.x, 1.0 - texCoord.y);
                    vec4 texColor = texture(textureSampler, flippedTexCoord);

                    fragColor = texColor * vec4(1.0, 1.0, 1.0, 1.0);
                }
            ";

            //Create a new instance from netgl
            gl = new NetGL.OpenGL();
            gl.modernGL = true;
            gl.Initial(this.panel1.Handle);
            gl.SwapIntervalEXT(0);
            gl.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
            
            //Enable depthtest
            gl.Enable(OpenGL.DepthTest);
            gl.DepthFunc(OpenGL.Less);
            
            //Compile the vertex shader
            int vertexShader = gl.CreateShader(OpenGL.VertexShader);
            gl.SetShaderSource(vertexShader, 1, vertexShaderCode);
            gl.CompileShader(vertexShader);

            //Compile the fragment shader
            int fragmentShader = gl.CreateShader(OpenGL.FragmentShader);
            gl.SetShaderSource(fragmentShader, 1, fragmentShaderCode);
            gl.CompileShader(fragmentShader);

            //Create the shader program
            int program = gl.CreateProgram();
            gl.AttachShader(program, vertexShader);
            gl.AttachShader(program, fragmentShader);
            gl.LinkProgram(program);

            //After linking the program, we can delete the shader source
            gl.DeleteShader(vertexShader);
            gl.DeleteShader(fragmentShader);


            m.InitModel(gl);
            
            //Now we create the projection and the view matrix
            mat4 p_mat = mat4.Perspective(Matrix4x4.DegreesToRadians(45.0f), (float)this.ClientSize.Width / (float)this.ClientSize.Height, 0.1f, 100f);
            mat4 v_mat = mat4.LookAt(new vec3(0f, 0f, 1f), new vec3(0f, 0f, 0f), new vec3(0f, 1f, 0f));

            while (true) {
                Thread.Sleep(10);

                //Clear the render context
                gl.Clear(NetGL.OpenGL.ColorBufferBit | NetGL.OpenGL.DepthBufferBit);

                //Create the modelview matrix
                mat4 mt_mat = mat4.Translate(new vec3(0f, -1f, -2f));
                mat4 mr_mat = mat4.RotateX(0f) * mat4.RotateY(rotate) * mat4.RotateZ(0f);
                mat4 ms_mat = mat4.Scale(new vec3(1f, 1f, 1f));
                mat4 m_mat = mt_mat * mr_mat * ms_mat;

                //Create the mvp matrix
                mat4 mvp = p_mat * v_mat * m_mat;

                m.Draw(gl, program, m_mat, v_mat, p_mat);

                gl.Flush();
                gl.SwapLayerBuffers(NetGL.OpenGL.SwapMainPlane);
                Console.WriteLine(gl.GetError());
                //rotate += 0.05f;
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            Thread renderThread = new Thread(new ThreadStart(loop));
            renderThread.Start();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
