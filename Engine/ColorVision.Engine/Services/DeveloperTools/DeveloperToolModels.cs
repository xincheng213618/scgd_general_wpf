using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.DeveloperTools
{
    public enum DeveloperToolKind { Python, NodeJs }
    public enum DeveloperToolDownloadSource { DomesticMirror, Official }

    public sealed record DeveloperToolInstallation(string Version, string ExecutablePath, string Source, string PackageManagerVersion);

    public sealed record DeveloperToolSnapshot(
        IReadOnlyList<DeveloperToolInstallation> Installations,
        string CurrentCommandPath,
        string RefreshedCommandPath,
        string PackageManagerPath,
        string Note);

    public sealed record DeveloperToolRelease(DeveloperToolKind Kind, Version Version, string NpmVersion = "")
    {
        public string DisplayName => Kind == DeveloperToolKind.Python
            ? $"Python {Version} · Windows x64"
            : $"Node.js {Version} LTS · npm {NpmVersion} · Windows x64";

        public string FileName => Kind == DeveloperToolKind.Python
            ? $"python-{Version}-amd64.exe"
            : $"node-v{Version}-x64.msi";

        public Uri OfficialUri => new(Kind == DeveloperToolKind.Python
            ? $"https://www.python.org/ftp/python/{Version}/{FileName}"
            : $"https://nodejs.org/dist/v{Version}/{FileName}");

        public Uri GetDownloadUri(DeveloperToolDownloadSource source) => source == DeveloperToolDownloadSource.Official
            ? OfficialUri
            : new Uri($"https://cdn.npmmirror.com/binaries/{(Kind == DeveloperToolKind.Python ? "python/" + Version : "node/v" + Version)}/{FileName}");
    }
}
