// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using ImGuiNET;
using ImGuiWindows;
using ImGuiWindows.Contracts;
using Silk.NET.OpenGL;

namespace ImGuiWindows.OpenGL;

public readonly record struct GLShaderInfo(uint Program, uint Vert, uint Frag);

/// <summary>
/// The Dear ImGui renderer for OpenGL, implemented as an <see cref="IImguiImplementation"/>.
/// </summary>
internal sealed class OpenGLImGuiController : IImguiImplementation
{
    private readonly int _attribLocationTex;
    private readonly int _attribLocationProjMtx;
    private readonly int _attribLocationVtxPos;
    private readonly int _attribLocationVtxUV;
    private readonly int _attribLocationVtxColor;
    private readonly uint _vboHandle;
    private readonly uint _elementsHandle;
    private uint _vertexArrayObject;

    private uint _fontTexture;
    private readonly GLShaderInfo _shader;
    private const bool _useGles = false;

    public OpenGLImGuiController(GLShaderInfo shaderInfo)
    {
        _shader = shaderInfo;
        
        // Get uniform locations
        _attribLocationTex = GL.GetUniformLocation(_shader.Program, "Texture");
        _attribLocationProjMtx = GL.GetUniformLocation(_shader.Program, "ProjMtx");
        
        // Get attribute locations
        _attribLocationVtxPos = GL.GetAttribLocation(_shader.Program, "Position");
        _attribLocationVtxUV = GL.GetAttribLocation(_shader.Program, "UV");
        _attribLocationVtxColor = GL.GetAttribLocation(_shader.Program, "Color");
        
        // Create buffers
        _vboHandle = GL.GenBuffer();
        _elementsHandle = GL.GenBuffer();
    }

    public void Init(ImGuiIOPtr io)
    {
        ImGuiLog.Debug($"[OPENGL] Init");
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        _fontTexture = RecreateFontDeviceTexture(io);

        return;

        static unsafe uint RecreateFontDeviceTexture(ImGuiIOPtr io)
        {
            ImGuiLog.Debug("[OPENGL] Entering font creation");
            io.Fonts.GetTexDataAsRGBA32(
                out IntPtr pixels,
                out int width,
                out int height,
                out _);
            ImGuiLog.Debug($"[OPENGL] Creating font texture: size={width}x{height}");

            int lastTexture = 0;
            GL.GetError().LogE();
            GL.GetInteger(GLEnum.TextureBinding2D, &lastTexture);
            GL.GetError().LogE();

            var texId = GL.GenTexture();
            GL.GetError().LogE();
            ImGuiLog.Debug($"[OPENGL] Font TexID = {(int)texId}");
            
            GL.BindTexture(GLEnum.Texture2D, texId);
            GL.GetError().LogE();

            GL.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            GL.GetError().LogE();
            GL.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            GL.GetError().LogE();
            GL.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            GL.GetError().LogE();
            GL.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
            GL.GetError().LogE();

            GL.PixelStore(GLEnum.UnpackRowLength, 0);
            GL.GetError().LogE();
            GL.TexImage2D(
                GLEnum.Texture2D,
                0,
                (int)GLEnum.Rgba,
                (uint)width,
                (uint)height,
                0,
                GLEnum.Rgba,
                GLEnum.UnsignedByte,
                (void*)pixels);
            GL.GetError().LogE();

            io.Fonts.SetTexID((nint)texId);

            GL.BindTexture(GLEnum.Texture2D, (uint)lastTexture);
            
            // Check for errors
            GL.GetError().LogE();
            return texId;
        }
    }

    private unsafe void SetupRenderState(ImDrawDataPtr drawDataPtr, int framebufferWidth, int framebufferHeight)
    {
        GL.GetError().LogE();
        GL.Viewport(0, 0, (uint)framebufferWidth, (uint)framebufferHeight);
        GL.GetError().LogE();

        GL.Enable(GLEnum.Blend);
        GL.GetError().LogE();
        GL.BlendEquation(GLEnum.FuncAdd);
        GL.GetError().LogE();
        GL.BlendFuncSeparate(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha, GLEnum.One, GLEnum.OneMinusSrcAlpha);
        GL.GetError().LogE();
        GL.Disable(GLEnum.CullFace);
        GL.GetError().LogE();
        GL.Disable(GLEnum.DepthTest);
        GL.GetError().LogE();
        GL.Disable(GLEnum.StencilTest);
        GL.GetError().LogE();
        GL.Enable(GLEnum.ScissorTest);
        GL.GetError().LogE();
        
        if (!_useGles)
        {
            GL.Disable(GLEnum.PrimitiveRestart);
            GL.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);
        }
        
        var displayPos = drawDataPtr.DisplayPos;
        var displaySize = drawDataPtr.DisplaySize;
        var framebufferScale = drawDataPtr.FramebufferScale;

        if (displayPos.X < 0.0f || displayPos.Y < 0.0f || displaySize.X <= 0.0f || displaySize.Y <= 0.0f || framebufferScale.X <= 0.0f || framebufferScale.Y <= 0.0f)
        {
            ImGuiLog.Error($"Invalid values? " +
                           $"DisplayPos = {displayPos}, " +
                           $"DisplaySize = {displaySize}, " +
                           $"FramebufferScale = {drawDataPtr.FramebufferScale}," +
                           $"Provided framebuffer sizes = {framebufferWidth}x{framebufferHeight}");
        }

        var left = drawDataPtr.DisplayPos.X;
        var right = drawDataPtr.DisplayPos.X + drawDataPtr.DisplaySize.X;
        var top = drawDataPtr.DisplayPos.Y;
        var bottom = drawDataPtr.DisplayPos.Y + drawDataPtr.DisplaySize.Y;

        Span<float> orthoProjection =
        [
            2.0f / (right - left), 0.0f, 0.0f, 0.0f,
            0.0f, 2.0f / (top - bottom), 0.0f, 0.0f,
            0.0f, 0.0f, -1.0f, 0.0f,
            (right + left) / (left - right), (top + bottom) / (bottom - top), 0.0f, 1.0f
        ];

        GL.ActiveTexture(GLEnum.Texture0);
        GL.GetError().LogE();
        GL.UseProgram(_shader.Program);
        GL.GetError().LogE();
        GL.Uniform1(_attribLocationTex, 0);
        GL.GetError().LogE();
        GL.UniformMatrix4(_attribLocationProjMtx, 1, false, orthoProjection);
        GL.GetError().LogE();
        GL.BindSampler(0, 0);
        GL.GetError().LogE();

        _vertexArrayObject = GL.GenVertexArray();
        GL.GetError().LogE();
        GL.BindVertexArray(_vertexArrayObject);
        GL.GetError().LogE();

        GL.BindBuffer(GLEnum.ArrayBuffer, _vboHandle);
        GL.GetError().LogE();
        GL.BindBuffer(GLEnum.ElementArrayBuffer, _elementsHandle);
        GL.GetError().LogE();

        GL.EnableVertexAttribArray((uint)_attribLocationVtxPos);
        GL.GetError().LogE();
        GL.EnableVertexAttribArray((uint)_attribLocationVtxUV);
        GL.GetError().LogE();
        GL.EnableVertexAttribArray((uint)_attribLocationVtxColor);
        GL.GetError().LogE();

        GL.VertexAttribPointer((uint)_attribLocationVtxPos, 2, GLEnum.Float, false, (uint)sizeof(ImDrawVert), (void*)0);
        GL.GetError().LogE();
        GL.VertexAttribPointer((uint)_attribLocationVtxUV, 2, GLEnum.Float, false, (uint)sizeof(ImDrawVert), (void*)8);
        GL.GetError().LogE();
        GL.VertexAttribPointer((uint)_attribLocationVtxColor, 4, GLEnum.UnsignedByte, true, (uint)sizeof(ImDrawVert), (void*)16);
        GL.GetError().LogE();
    }


    public unsafe void RenderImDrawData(ImDrawDataPtr drawDataPtr)
    {
        var frameBufferSize = drawDataPtr.DisplaySize * drawDataPtr.FramebufferScale;
        var framebufferWidth = (int)frameBufferSize.X;
        var framebufferHeight = (int)frameBufferSize.Y;
        if (framebufferWidth <= 0 || framebufferHeight <= 0)
            return;
        
        // Backup GL state
        int lastActiveTexture = 0,
            lastProgram = 0,
            lastTexture = 0,
            lastSampler = 0,
            lastArrayBuffer = 0,
            lastVertexArrayObject = 0,
            lastBlendSrcRgb = 0,
            lastBlendDstRgb = 0,
            lastBlendSrcAlpha = 0,
            lastBlendDstAlpha = 0,
            lastBlendEquationRgb = 0,
            lastBlendEquationAlpha = 0;

        GL.GetInteger(GLEnum.ActiveTexture, &lastActiveTexture);
        GL.GetError().LogE();
        GL.ActiveTexture(GLEnum.Texture0);
        GL.GetError().LogE();

        GL.GetInteger(GLEnum.CurrentProgram, &lastProgram);
        GL.GetError().LogE();
        GL.GetInteger(GLEnum.TextureBinding2D, &lastTexture);
        GL.GetError().LogE();

        GL.GetInteger(GLEnum.SamplerBinding, &lastSampler);
        GL.GetError().LogE();

        GL.GetInteger(GLEnum.ArrayBufferBinding, &lastArrayBuffer);
        GL.GetError().LogE();
        GL.GetInteger(GLEnum.VertexArrayBinding, &lastVertexArrayObject);
        GL.GetError().LogE();

        Span<int> lastPolygonMode = stackalloc int[2];
        if (!_useGles)
        {
            GL.GetInteger(GLEnum.PolygonMode, lastPolygonMode);
            GL.GetError().LogE();
        }

        Span<int> lastScissorBox = stackalloc int[4];
        GL.GetInteger(GLEnum.ScissorBox, lastScissorBox);
        GL.GetError().LogE();

        GL.GetInteger(GLEnum.BlendSrcRgb, &lastBlendSrcRgb);
        GL.GetError().LogE();
        GL.GetInteger(GLEnum.BlendDstRgb, &lastBlendDstRgb);
        GL.GetError().LogE();

        GL.GetInteger(GLEnum.BlendSrcAlpha, &lastBlendSrcAlpha);
        GL.GetError().LogE();
        GL.GetInteger(GLEnum.BlendDstAlpha, &lastBlendDstAlpha);
        GL.GetError().LogE();

        GL.GetInteger(GLEnum.BlendEquationRgb, &lastBlendEquationRgb);
        GL.GetError().LogE();
        GL.GetInteger(GLEnum.BlendEquationAlpha, &lastBlendEquationAlpha);
        GL.GetError().LogE();

        bool lastEnableBlend = GL.IsEnabled(GLEnum.Blend);
        bool lastEnableCullFace = GL.IsEnabled(GLEnum.CullFace);
        bool lastEnableDepthTest = GL.IsEnabled(GLEnum.DepthTest);
        bool lastEnableStencilTest = GL.IsEnabled(GLEnum.StencilTest);
        bool lastEnableScissorTest = GL.IsEnabled(GLEnum.ScissorTest);

        bool lastEnablePrimitiveRestart = !_useGles ? GL.IsEnabled(GLEnum.PrimitiveRestart) : false;


        SetupRenderState(drawDataPtr, framebufferWidth, framebufferHeight);

        // Will project scissor/clipping rectangles into framebuffer space
        Vector2 clipOff = drawDataPtr.DisplayPos; // (0,0) unless using multi-viewports
        Vector2 clipScale = drawDataPtr.FramebufferScale; // (1,1) unless using retina display which are often (2,2)

        // Render command lists
        for (int n = 0; n < drawDataPtr.CmdListsCount; n++)
        {
            var cmdListPtr = drawDataPtr.CmdLists[n];

            // Upload vertex/index buffers

            GL.BufferData(GLEnum.ArrayBuffer, (nuint)(cmdListPtr.VtxBuffer.Size * sizeof(ImDrawVert)),
                (void*)cmdListPtr.VtxBuffer.Data, GLEnum.StreamDraw);
            
            GL.GetError().LogE();


            GL.BufferData(GLEnum.ElementArrayBuffer, (nuint)(cmdListPtr.IdxBuffer.Size * sizeof(ushort)),
                (void*)cmdListPtr.IdxBuffer.Data, GLEnum.StreamDraw);
            GL.GetError().LogE();

            for (int iCmd = 0; iCmd < cmdListPtr.CmdBuffer.Size; iCmd++)
            {
                ImDrawCmdPtr cmdPtr = cmdListPtr.CmdBuffer[iCmd];

                if (cmdPtr.UserCallback != IntPtr.Zero)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    Vector4 clipRect;
                    clipRect.X = (cmdPtr.ClipRect.X - clipOff.X) * clipScale.X;
                    clipRect.Y = (cmdPtr.ClipRect.Y - clipOff.Y) * clipScale.Y;
                    clipRect.Z = (cmdPtr.ClipRect.Z - clipOff.X) * clipScale.X;
                    clipRect.W = (cmdPtr.ClipRect.W - clipOff.Y) * clipScale.Y;

                    if (clipRect.X < framebufferWidth && clipRect.Y < framebufferHeight && clipRect.Z >= 0.0f &&
                        clipRect.W >= 0.0f)
                    {
                        // Apply scissor/clipping rectangle
                        GL.Scissor((int)clipRect.X, (int)(framebufferHeight - clipRect.W),
                            (uint)(clipRect.Z - clipRect.X), (uint)(clipRect.W - clipRect.Y));
                        GL.GetError().LogE();

                        // Bind texture, Draw
                        var textureId = (uint)cmdPtr.TextureId;
                        GL.BindTexture(GLEnum.Texture2D, textureId);
                        GL.GetError().LogE();

                        GL.DrawElementsBaseVertex(GLEnum.Triangles, cmdPtr.ElemCount, GLEnum.UnsignedShort,
                            (void*)(cmdPtr.IdxOffset * sizeof(ushort)), (int)cmdPtr.VtxOffset);
                        GL.GetError().LogE();
                    }
                }
            }
        }

        // Destroy the temporary VAO
        GL.DeleteVertexArray(_vertexArrayObject);
        GL.GetError().LogE();
        _vertexArrayObject = 0;

        // Restore modified GL state
        GL.UseProgram((uint)lastProgram);
        GL.GetError().LogE();
        GL.BindTexture(GLEnum.Texture2D, (uint)lastTexture);
        GL.GetError().LogE();

        GL.BindSampler(0, (uint)lastSampler);
        GL.GetError().LogE();

        GL.ActiveTexture((GLEnum)lastActiveTexture);
        GL.GetError().LogE();

        GL.BindVertexArray((uint)lastVertexArrayObject);
        GL.GetError().LogE();

        GL.BindBuffer(GLEnum.ArrayBuffer, (uint)lastArrayBuffer);
        GL.GetError().LogE();
        GL.BlendEquationSeparate((GLEnum)lastBlendEquationRgb, (GLEnum)lastBlendEquationAlpha);
        GL.GetError().LogE();
        GL.BlendFuncSeparate((GLEnum)lastBlendSrcRgb, (GLEnum)lastBlendDstRgb, (GLEnum)lastBlendSrcAlpha,
            (GLEnum)lastBlendDstAlpha);
        GL.GetError().LogE();

        if (lastEnableBlend)
        {
            GL.Enable(GLEnum.Blend);
            GL.GetError().LogE();
        }
        else
        {
            GL.Disable(GLEnum.Blend);
            GL.GetError().LogE();
        }

        if (lastEnableCullFace)
        {
            GL.Enable(GLEnum.CullFace);
            GL.GetError().LogE();
        }
        else
        {
            GL.Disable(GLEnum.CullFace);
            GL.GetError().LogE();
        }

        if (lastEnableDepthTest)
        {
            GL.Enable(GLEnum.DepthTest);
            GL.GetError().LogE();
        }
        else
        {
            GL.Disable(GLEnum.DepthTest);
            GL.GetError().LogE();
        }

        if (lastEnableStencilTest)
        {
            GL.Enable(GLEnum.StencilTest);
            GL.GetError().LogE();
        }
        else
        {
            GL.Disable(GLEnum.StencilTest);
            GL.GetError().LogE();
        }

        if (lastEnableScissorTest)
        {
            GL.Enable(GLEnum.ScissorTest);
            GL.GetError().LogE();
        }
        else
        {
            GL.Disable(GLEnum.ScissorTest);
            GL.GetError().LogE();
        }


        if (!_useGles)
        {
            if (lastEnablePrimitiveRestart)
            {
                GL.Enable(GLEnum.PrimitiveRestart);
                GL.GetError().LogE();
            }
            else
            {
                GL.Disable(GLEnum.PrimitiveRestart);
                GL.GetError().LogE();
            }

            GL.PolygonMode(GLEnum.FrontAndBack, (GLEnum)lastPolygonMode[0]);
            GL.GetError().LogE();
        }

        GL.Scissor(lastScissorBox[0], lastScissorBox[1], (uint)lastScissorBox[2], (uint)lastScissorBox[3]);
        GL.GetError().LogE();
    }

    /// <summary>
    /// Frees all graphics resources used by the renderer.
    /// </summary>
    public void Dispose() => Dispose(true);
    private void Dispose(bool disposing)
    {
        GL.DeleteBuffer(_vboHandle);
        GL.DeleteBuffer(_elementsHandle);
        GL.DeleteVertexArray(_vertexArrayObject);
        GL.DeleteTexture(_fontTexture);


        if (disposing)
        {
            try
            {
                Disposed?.Invoke(this);
            }
            catch (Exception e)
            {
                ImGuiLog.Error(e.ToString());
            }
        }
        else
        {
            ImGuiLog.DisposalWarning();
        }

        Disposed = null;
    }
    
    ~OpenGLImGuiController()
    {
        Dispose(false);
    }

    public event Action<object>? Disposed;

}