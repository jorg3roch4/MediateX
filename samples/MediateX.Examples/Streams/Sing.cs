using MediateX;

namespace MediateX.Examples;

public class Sing : IStreamRequest<Song>
{
    public string Message { get; set; }
}