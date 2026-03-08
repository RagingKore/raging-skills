#!/usr/bin/env dotnet
#:package Microsoft.Extensions.FileSystemGlobbing@9.*
// run-affected-tests.cs — Run tests for projects listed in an affected-projects file.
//
// Supports both `dotnet test` and `dotnet run` (for TUnit and similar frameworks).
// If the affected-projects file does not exist (e.g., detection step was skipped in full mode),
// falls back to discovering all test projects matching --fallback-glob.
//
// Usage:
//   dotnet run-affected-tests.cs <affected-file> [options]
//
// Options:
//   --runner test|run        dotnet verb to use (default: test)
//   --frameworks f1,f2,...   comma-separated TFMs to iterate (default: single run, no --framework flag)
//   --configuration cfg      build configuration (default: Release)
//   --no-build               pass --no-build to dotnet
//   --results-dir path       test results directory (default: ./artifacts/test-results)
//   --extra-args "..."       additional arguments passed after -- (for TUnit etc.)
//   --fallback-glob "pat"    glob pattern to find all test projects when affected file is missing
//                            (default: **/*.Tests.csproj)
//
// Examples:
//   dotnet run-affected-tests.cs affected-unit-tests.txt \
//     --runner test --frameworks net8.0,net9.0 --no-build \
//     --fallback-glob "test/**/*.Tests.csproj"

using System.Diagnostics;

if (args.Length == 0) {
    Console.Error.WriteLine("Usage: run-affected-tests.cs <affected-file> [options]");
    return 1;
}

var affectedFile  = args[0];
var runner        = "test";
var frameworks    = "";
var configuration = "Release";
var noBuild       = false;
var resultsDir    = "./artifacts/test-results";
var extraArgs     = "";
var fallbackGlob  = "**/*.Tests.csproj";

for (var i = 1; i < args.Length; i++)
    switch (args[i]) {
        case "--runner": runner = args[++i]; break;
        case "--frameworks": frameworks = args[++i]; break;
        case "--configuration": configuration = args[++i]; break;
        case "--no-build": noBuild = true; break;
        case "--results-dir": resultsDir = args[++i]; break;
        case "--extra-args": extraArgs = args[++i]; break;
        case "--fallback-glob": fallbackGlob = args[++i]; break;

        default:
            Console.Error.WriteLine($"Unknown option: {args[i]}");
            return 1;
    }

// If the affected file does not exist (detection step was skipped in full mode),
// discover all test projects matching the fallback glob and run them all.
if (!File.Exists(affectedFile)) {
    Console.WriteLine($"Affected file not found (full mode). Discovering all test projects via: {fallbackGlob}");

    var matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher();
    matcher.AddInclude(fallbackGlob);
    var result = matcher.Execute(new Microsoft.Extensions.FileSystemGlobbing.Abstractions.DirectoryInfoWrapper(new DirectoryInfo(".")));

    var discovered = result.Files.Select(f => f.Path).ToArray();
    affectedFile = Path.GetTempFileName();
    File.WriteAllLines(affectedFile, discovered);
}

var projects = File.ReadAllLines(affectedFile)
    .Where(l => !string.IsNullOrWhiteSpace(l))
    .ToArray();

if (projects.Length == 0) {
    Console.WriteLine("No test projects found — skipping.");
    return 0;
}

Directory.CreateDirectory(resultsDir);
var failed = 0;

var tfmList = string.IsNullOrEmpty(frameworks)
    ? [""]
    : frameworks.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

foreach (var project in projects) {
    var projectName = Path.GetFileName(Path.GetDirectoryName(project)) ?? Path.GetFileNameWithoutExtension(project);

    foreach (var tfm in tfmList) {
        var label = string.IsNullOrEmpty(tfm) ? projectName : $"{projectName} ({tfm})";
        Console.WriteLine($"::group::Testing {label}");

        var tfmFlag     = string.IsNullOrEmpty(tfm) ? "" : $"--framework {tfm}";
        var noBuildFlag = noBuild ? "--no-build" : "";
        var trxName     = string.IsNullOrEmpty(tfm) ? projectName : $"{projectName}-{tfm}";

        string cmdArgs;

        if (runner == "test")
            cmdArgs = string.Join(
                ' ',
                new[] {
                        "test", $"--project \"{project}\"", $"--configuration {configuration}", noBuildFlag, tfmFlag,
                        $"--logger \"trx;LogFileName={trxName}.trx\"", $"--results-directory \"{resultsDir}\"", extraArgs
                    }
                    .Where(s => !string.IsNullOrEmpty(s))
            );
        else {
            var afterSeparator = string.Join(
                ' ',
                new[] {
                        $"--results-directory \"{resultsDir}\"",
                        $"--report-trx --report-trx-filename \"{trxName}.trx\"", extraArgs
                    }
                    .Where(s => !string.IsNullOrEmpty(s))
            );

            cmdArgs = string.Join(
                ' ',
                new[] {
                        "run", $"--project \"{project}\"", $"--configuration {configuration}", noBuildFlag, tfmFlag,
                        "--", afterSeparator
                    }
                    .Where(s => !string.IsNullOrEmpty(s))
            );
        }

        var exitCode = Run("dotnet", cmdArgs);

        if (exitCode != 0) {
            Console.WriteLine($"::error::Tests failed: {label}");
            failed++;
        }

        Console.WriteLine("::endgroup::");
    }
}

if (failed > 0) {
    Console.WriteLine($"::error::{failed} test run(s) failed.");
    return 1;
}

Console.WriteLine("All affected tests passed.");
return 0;

static int Run(string fileName, string arguments) {
    var psi = new ProcessStartInfo(fileName, arguments) {
        UseShellExecute = false
    };

    using var process = Process.Start(psi)!;
    process.WaitForExit();
    return process.ExitCode;
}
