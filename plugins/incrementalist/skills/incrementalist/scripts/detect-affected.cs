#!/usr/bin/env dotnet
// detect-affected.cs — Detect affected projects using Incrementalist.
//
// Usage:
//   dotnet detect-affected.cs <config> <output-file>
//   dotnet detect-affected.cs .incrementalist/testsOnly.json affected-unit-tests.txt
//
// Writes affected project paths (one per line) to <output-file>.
// Exits 0 with an empty file when nothing is affected.

using System.Diagnostics;

var config = args.Length > 0 ? args[0] : ".incrementalist/incrementalist.json";
var output = args.Length > 1 ? args[1] : "affected-projects.txt";

File.WriteAllText(output, "");

GhaGroup($"Detecting affected projects (config: {config})");

var exitCode = Run("dotnet", $"incrementalist --config \"{config}\" --verbose -f \"{output}\"");

if (exitCode != 0) {
    GhaEndGroup();
    Environment.Exit(exitCode);
}

if (!File.Exists(output) || new FileInfo(output).Length == 0) {
    Console.WriteLine("No affected projects detected.");
    GhaEndGroup();
    return;
}

var lines = File.ReadAllLines(output).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
Console.WriteLine($"Affected projects ({lines.Length}):");

foreach (var line in lines)
    Console.WriteLine(line);

GhaEndGroup();

static void GhaGroup(string title) => Console.WriteLine($"::group::{title}");
static void GhaEndGroup()          => Console.WriteLine("::endgroup::");

static int Run(string fileName, string arguments) {
    var psi = new ProcessStartInfo(fileName, arguments) {
        UseShellExecute = false
    };

    using var process = Process.Start(psi)!;
    process.WaitForExit();
    return process.ExitCode;
}
