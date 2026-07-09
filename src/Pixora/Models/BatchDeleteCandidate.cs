using System.IO;

namespace Pixora.Models;

public sealed record BatchDeleteCandidate(string Path, bool IsVideo)
{
    public string Kind => IsVideo ? "视频" : "图片/动图";

    public string FileName => System.IO.Path.GetFileName(Path);

    public string Extension => System.IO.Path.GetExtension(Path).ToLowerInvariant();

    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;
}
