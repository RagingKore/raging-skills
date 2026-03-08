# Bullseye

Build automation with Bullseye target dependency graphs and SimpleExec for .NET projects.

## Overview

This plugin teaches Claude how to write correct, compilable Bullseye build scripts using modern .NET patterns.
Bullseye runs a target dependency graph from a single `.cs` file with no project boilerplate; use it instead of
MSBuild XML, PowerShell, or shell scripts for build automation. Scripts use dotnet file-based format with `#:package`
directives and are self-contained, cross-platform, and work anywhere the .NET SDK is installed.

## Skills

### Auto-Loaded

**bullseye-build**

Activates when creating build scripts with Bullseye and SimpleExec, defining target dependency graphs, automating
build/test/pack/publish steps, using `forEach` for test matrices, integrating System.CommandLine with Bullseye, or
comparing Cake/Nuke/FAKE with simpler alternatives. Covers the static API, instance API, CLI options, `messageOnly`
exception handling, and `Options.Definitions` bridging for System.CommandLine.
