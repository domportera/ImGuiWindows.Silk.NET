using ImGuiWindows.Contracts;
using Silk.NET.Core;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace ImGuiWindows.OpenGL;

public sealed class OpenGLWindow : ImGuiRenderer
{
    protected override void OnInitialize(Surface surface)
    {
        if (surface.OpenGL is null)
        {
            throw new PlatformNotSupportedException("OpenGL is not supported.");
        }

        if (surface.OpenGL == null)
            throw new NullReferenceException("OpenGL is null");

        surface.OpenGL.Profile = OpenGLContextProfile.Core;
        surface.OpenGL.Version = new Version32(3, 2);
    }

    protected override void BeginDrawContext(int width, int height)
    {
        GL.Viewport(0, 0, (uint)width, (uint)height);
        GL.GetError().LogE();
        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.GetError().LogE();
    }

    protected override void OnTerminate()
    {
        GL.DeleteVertexArray(_vao);
        GL.GetError().LogE();
        GL.DeleteBuffer(_vbo);
        GL.GetError().LogE();
        GL.DeleteProgram(_prog);
        GL.GetError().LogE();
        GL.DeleteShader(_vert);
        GL.GetError().LogE();
        GL.DeleteShader(_frag);
        GL.GetError().LogE();
    }

    private uint _vbo, _vao, _prog, _vert, _frag;

    /// <summary>
    /// Create the ImGui implementation for OpenGL
    /// </summary>
    protected override IImguiImplementation Create()
    {
        ImGuiLog.Debug($"Creating {nameof(OpenGLImGuiController)}");
        InitializeRenderer(out _vao, out _vbo, out _prog, out _vert, out _frag);
        return new OpenGLImGuiController(new GLShaderInfo(_prog, _vert, _frag));
    }

    /// <summary>
    /// Initialize the general-purpose OpenGL renderer.
    /// </summary>
    /// <param name="vao"></param>
    /// <param name="vbo"></param>
    /// <param name="prog"></param>
    /// <param name="vert"></param>
    /// <param name="frag"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private static void InitializeRenderer(out uint vao, out uint vbo, out uint prog, out uint vert,
        out uint frag)
    {
        GL.GetError().LogE();
        ImGuiLog.Debug("=== BEGIN OPENGL INFORMATION");
        foreach (var val in Enum.GetValues<Silk.NET.OpenGL.StringName>())
        {
            ImGuiLog.Debug($"{val} = {GL.GetString(val).ReadToString()}");
            GL.GetError().LogW();
        }

        ImGuiLog.Debug("=== END OPENGL INFORMATION");

        vbo = GL.GenBuffer();
        GL.GetError().LogE();
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.GetError().LogE();

        float[] vertices = [-0.5f, -0.5f, 0.0f, 0.5f, -0.5f, 0.0f, 0.0f, 0.5f, 0.0f];
        GL.BufferData(BufferTarget.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)),
            vertices[0].AsRef(),
            BufferUsage.StaticDraw);
        GL.GetError().LogE();
        vao = GL.GenVertexArray();
        GL.GetError().LogE();
        GL.BindVertexArray(vao);
        GL.GetError().LogE();
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), default);
        GL.GetError().LogE();
        GL.EnableVertexAttribArray(0);
        GL.GetError().LogE();
        vert = GL.CreateShader(ShaderType.VertexShader);
        GL.GetError().LogE();
        frag = GL.CreateShader(ShaderType.FragmentShader);
        GL.GetError().LogE();

        const string vertSource = """
                                  #version 150

                                  in vec2 Position;
                                  in vec2 UV;
                                  in vec4 Color;

                                  uniform mat4 ProjMtx;

                                  out vec2 Frag_UV;
                                  out vec4 Frag_Color;

                                  void main()
                                  {
                                      Frag_UV = UV;
                                      Frag_Color = Color;
                                      gl_Position = ProjMtx * vec4(Position.xy, 0.0, 1.0);
                                  }
                                  """;

        const string fragSource = """
                                  #version 150

                                  in vec2 Frag_UV;
                                  in vec4 Frag_Color;

                                  uniform sampler2D Texture;

                                  out vec4 Out_Color;

                                  void main()
                                  {
                                      Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
                                  }
                                  """;

        var vertSourceLength = vertSource.Length;
        GL.ShaderSource(vert, 1, new[] { vertSource }, vertSourceLength.AsRef());
        GL.GetError().LogE();
        var fragSourceLength = fragSource.Length;
        GL.ShaderSource(frag, 1, new[] { fragSource }, fragSourceLength.AsRef());
        GL.GetError().LogE();

        GL.CompileShader(vert);
        GL.GetError().LogE();
        var vertCompiled = 0;
        GL.GetShader(vert, ShaderParameterName.CompileStatus, vertCompiled.AsRef());
        GL.GetError().LogE();
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (vertCompiled == 0)
        {
            var logLen = 0u;
            GL.GetError().LogE();
            var res = GL.GetShaderInfoLog(vert, logLen.AsRef());
            GL.GetError().LogE();
            throw new InvalidOperationException($"Vertex shader compilation failed: {res} | {logLen}");
        }

        GL.CompileShader(frag);
        GL.GetError().LogE();
        var fragCompiled = 0;
        GL.GetShader(frag, ShaderParameterName.CompileStatus, fragCompiled.AsRef());
        GL.GetError().LogE();
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (fragCompiled == 0)
        {
            var logLen = 0u;
            var res = GL.GetShaderInfoLog(vert, logLen.AsRef());
            GL.GetError().LogE();
            throw new InvalidOperationException($"Fragment shader compilation failed: {res} | {logLen}");
        }

        prog = GL.CreateProgram();
        GL.AttachShader(prog, vert);
        GL.GetError().LogE();
        GL.AttachShader(prog, frag);
        GL.GetError().LogE();
        GL.LinkProgram(prog);
        GL.GetError().LogE();

       // GL.DeleteShader(vert);
       // GL.DeleteShader(frag);
        GL.UseProgram(prog);
        GL.GetError().LogE();
    }
}