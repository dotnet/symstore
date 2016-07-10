#r "System.Xml.Linq"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;

var errors = new List<string>();

void ReportError(string message)
{
    var color = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(message);
    Console.ForegroundColor = color;
}

void ReportSuccess(string message)
{
    var color = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine(message);
    Console.ForegroundColor = color;
}

string usage = @"usage: pack.csx <version> <nuspec-dir> <binaries-dir> <output-directory>";

if (Args.Count() != 4)
{
    Console.WriteLine(usage);
    Environment.Exit(1);
}

var SolutionRoot = Path.GetFullPath(Path.Combine(ScriptRoot(), "../../"));
string ScriptRoot([CallerFilePath]string path = "") => Path.GetDirectoryName(path);

// Strip trailing '\' characters because if the path is later passed on the
// command line when surrounded by quotes (in case the path has spaces) some
// utilities will consider the '\"' as an escape sequence for the end quote
var BuildVersion = Args[0].Trim();
var NuSpecDir = Path.GetFullPath(Args[1].Trim());
var BinDir = Path.GetFullPath(Path.GetFullPath(Args[2]).Trim().TrimEnd('\\'));
var OutDir = Path.GetFullPath(Path.GetFullPath(Args[3]).Trim().TrimEnd('\\'));

var doc = XDocument.Load(Path.Combine(SolutionRoot, "build/Targets/Dependencies.props"));
XNamespace ns = @"http://schemas.microsoft.com/developer/msbuild/2003";

var dependencyVersions = from e in doc.Root.Descendants()
                         where e.Name.LocalName.EndsWith("Version")
                         select new { VariableName = e.Name.LocalName, Value = e.Value };

var commonArgs =
    $"-OutputDirectory \"{OutDir}\" " +
    $"-prop version=\"{BuildVersion}\" " +
    string.Join(" ", dependencyVersions.Select(d => $"-prop {d.VariableName}=\"{d.Value}\""));

Directory.CreateDirectory(OutDir);

int exitCode = 0;

foreach (var nuspec in Directory.EnumerateFiles(NuSpecDir, "*.nuspec", SearchOption.TopDirectoryOnly))
{
    var libraryName = Path.GetFileNameWithoutExtension(nuspec);

    var nugetArgs =
        $"pack \"{nuspec}\" " +
        $"-BasePath \"{Path.Combine(BinDir, libraryName)}\" " +
        commonArgs;

    var nugetExePath = Path.GetFullPath(Path.Combine(SolutionRoot, "nuget.exe"));
    var p = new Process();
    p.StartInfo.FileName = nugetExePath;
    p.StartInfo.Arguments = nugetArgs;
    p.StartInfo.UseShellExecute = false;
    p.StartInfo.RedirectStandardError = true;
    p.StartInfo.RedirectStandardOutput = true;

    Console.WriteLine($"{Environment.NewLine}Running: nuget.exe {nugetArgs}");

    p.Start();
    p.WaitForExit();

    if (p.ExitCode != 0)
    {
        exitCode = p.ExitCode;

        var message = $"{nuspec}: error: {p.StandardError.ReadToEnd()}";
        errors.Add(message);
        ReportError(message);
    }
    else
    {
        ReportSuccess(p.StandardOutput.ReadToEnd());
    }
}

foreach (var error in errors)
{
    ReportError(error);
}

return exitCode;
