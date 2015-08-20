﻿#r "Packages\EasyHttp.1.6.58.0\lib\net40\EasyHttp.dll"
#r "Packages\JsonFx.2.0.1209.2802\lib\net40\JsonFx.dll"

#r "System.IO.Compression"
#r "System.IO.Compression.FileSystem"

using System.Diagnostics;
using System.Dynamic;
using System.IO.Compression;

using EasyHttp.Http;
using EasyHttp.Infrastructure;

using static Nake.FS;
using static Nake.Run;

var OutputPath = @"%NakeScriptDirectory%\Output";
var PackagePath = @"{OutputPath}\Package";

var DebugOutputPath = @"{PackagePath}\Debug";
var ReleaseOutputPath = @"{PackagePath}\Release";

Func<string> PackageFile = () => PackagePath + @"\Nake.{Version()}.nupkg";
Func<string> ArchiveFile = () => OutputPath + @"\{Version()}.zip";

/// Zips all binaries for standalone installation
[Step] void Zip()
{
    var files = new FileSet("{ReleaseOutputPath}")
    {
        "Nake.*",
        "Meta.*",
        "Utility.*",
        "GlobDir.dll",
        "Microsoft.CodeAnalysis.dll",
        "Microsoft.CodeAnalysis.CSharp.dll",
        "System.Collections.Immutable.dll",
        "System.Reflection.Metadata.dll",
        "-:*.Tests.*"
    };

    Delete(ArchiveFile());

    using (ZipArchive archive = ZipFile.Open(ArchiveFile(), ZipArchiveMode.Create))
    {
        foreach (var file in files)
            archive.CreateEntryFromFile(file, Path.GetFileName(file));
    }
}

/// Publishes package to NuGet gallery
[Step] void NuGet()
{
    Cmd(@"Tools\Nuget.exe push {PackageFile()} %NuGetApiKey%");
}

/// Publishes standalone version to GitHub releases
[Step] void Standalone(ReleaseType releaseType, string description = null)
{
    Zip();

    string release = CreateRelease(releaseType == ReleaseType.Beta, description);
    Upload(release, ArchiveFile(), "application/zip");
}

enum ReleaseType {Beta, Release}

string CreateRelease(bool beta, string description)
{
    dynamic data = new ExpandoObject();

    data.tag_name = data.name = Version();
    data.target_commitish = beta ? "dev" : "master";
    data.prerelease = beta;
    data.body = !string.IsNullOrEmpty(description) 
                ? description 
                : "Standalone release {Version()}";

    return GitHub().Post("https://api.github.com/repos/yevhen/nake/releases",
                          data, HttpContentTypes.ApplicationJson).Location;
}

void Upload(string release, string filePath, string contentType)
{
    GitHub().Post(GetUploadUri(release) + "?name=" + Path.GetFileName(filePath), null, new List<FileData>
    {
        new FileData()
        {
            ContentType = contentType,
            Filename = filePath
        }
    });
}

string GetUploadUri(string release)
{
    var body = GitHub().Get(release).DynamicBody;
    return ((string)body.upload_url).Replace("{{?name}}", "");
}

HttpClient GitHub()
{
    var client = new HttpClient();

    client.Request.Accept = "application/vnd.github.manifold-preview";
    client.Request.ContentType = "application/json";
    client.Request.AddExtraHeader("Authorization", "token %GitHubToken%");

    return client;
}

string Version()
{
    return FileVersionInfo
            .GetVersionInfo(@"{ReleaseOutputPath}\Nake.exe")
            .ProductVersion;
}