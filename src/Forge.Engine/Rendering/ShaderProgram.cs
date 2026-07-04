using Silk.NET.OpenGL;

namespace Forge.Engine.Rendering;

/// <summary>
/// Compiles and manages an OpenGL shader program from vertex + fragment source.
/// </summary>
public sealed class ShaderProgram : IDisposable
{
    private readonly GL _gl;
    private uint _handle;
    private readonly Dictionary<string, int> _uniformCache = new();

    public uint Handle => _handle;

    public ShaderProgram(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;

        uint vert = CompileShader(ShaderType.VertexShader, vertexSource);
        uint frag = CompileShader(ShaderType.FragmentShader, fragmentSource);

        _handle = _gl.CreateProgram();
        _gl.AttachShader(_handle, vert);
        _gl.AttachShader(_handle, frag);
        _gl.LinkProgram(_handle);

        _gl.GetProgram(_handle, ProgramPropertyARB.LinkStatus, out int status);
        if (status == 0)
        {
            string log = _gl.GetProgramInfoLog(_handle);
            throw new InvalidOperationException($"Shader link error: {log}");
        }

        _gl.DeleteShader(vert);
        _gl.DeleteShader(frag);
    }

    private uint CompileShader(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0)
        {
            string log = _gl.GetShaderInfoLog(shader);
            throw new InvalidOperationException($"Shader compile error ({type}): {log}");
        }

        return shader;
    }

    public void Use() => _gl.UseProgram(_handle);

    public int GetUniformLocation(string name)
    {
        if (_uniformCache.TryGetValue(name, out int loc))
            return loc;

        loc = _gl.GetUniformLocation(_handle, name);
        _uniformCache[name] = loc;
        return loc;
    }

    public void SetUniform(string name, int value) =>
        _gl.Uniform1(GetUniformLocation(name), value);

    public void SetUniform(string name, float value) =>
        _gl.Uniform1(GetUniformLocation(name), value);

    public void SetUniform(string name, float x, float y) =>
        _gl.Uniform2(GetUniformLocation(name), x, y);

    public void SetUniform(string name, float x, float y, float z) =>
        _gl.Uniform3(GetUniformLocation(name), x, y, z);

    public void SetUniform(string name, float x, float y, float z, float w) =>
        _gl.Uniform4(GetUniformLocation(name), x, y, z, w);

    public unsafe void SetUniformMatrix4(string name, float[] matrix)
    {
        fixed (float* ptr = matrix)
        {
            _gl.UniformMatrix4(GetUniformLocation(name), 1, false, ptr);
        }
    }

    public void Dispose()
    {
        if (_handle != 0)
        {
            _gl.DeleteProgram(_handle);
            _handle = 0;
        }
    }
}
