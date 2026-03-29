namespace ImGuiWindows.Contracts;

public interface ITemporaryObject
{
    event Action<object>? Disposed;
}