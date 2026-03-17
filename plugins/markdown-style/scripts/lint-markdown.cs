#!/usr/bin/env dotnet
// lint-markdown.cs - Async PostToolUse hook: runs markdownlint-cli2 --fix on edited .md files
// Receives PostToolUse JSON on stdin from Claude Code (matches Edit|Write)
// Returns JSON with hookSpecificOutput.additionalContext for async hook delivery
// Usage: echo '{"tool_name":"Write","tool_input":{"file_path":"README.md"}}' | dotnet lint-markdown.cs

#:package CliWrap@3.*

using System.Text.Json;
using System.Text.Json.Serialization;
using CliWrap;
using CliWrap.Buffered;

var json = await Console.In.ReadToEndAsync();

if (string.IsNullOrWhiteSpace(json))
    return 0;

var hookInput = JsonSerializer.Deserialize(json, HookJsonContext.Default.PostToolUseInput)!;
var filePath = hookInput.ToolInput.FilePath;

if (string.IsNullOrEmpty(filePath) || !filePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
    return 0;

if (!File.Exists(filePath))
    return 0;

// Auto-fix mechanical issues
await Cli.Wrap("npx")
    .WithArguments(["markdownlint-cli2", "--fix", filePath])
    .WithValidation(CommandResultValidation.None)
    .ExecuteAsync();

// Check for remaining violations
var result = await Cli.Wrap("npx")
    .WithArguments(["markdownlint-cli2", filePath])
    .WithValidation(CommandResultValidation.None)
    .ExecuteBufferedAsync();

if (result.ExitCode != 0) {
    var violations = string.IsNullOrEmpty(result.StandardOutput) ? result.StandardError : result.StandardOutput;
    var specific = new HookSpecificOutput("PostToolUse", $"markdownlint violations in {filePath}:\n{violations.Trim()}");
    var output = new AsyncHookOutput(specific);
    await Console.Out.WriteLineAsync(JsonSerializer.Serialize(output, HookJsonContext.Default.AsyncHookOutput));
}

return 0;

// PostToolUse input contract (only fields this hook needs; unknown fields are ignored)
record PostToolUseInput(
    [property: JsonPropertyName("tool_name")] string ToolName,
    [property: JsonPropertyName("tool_input")] PostToolUseToolInput ToolInput);

record PostToolUseToolInput(
    [property: JsonPropertyName("file_path")] string FilePath,
    [property: JsonPropertyName("content")] string Content);

// Async hook output contract
record AsyncHookOutput(
    [property: JsonPropertyName("hookSpecificOutput")] HookSpecificOutput HookSpecificOutput);

record HookSpecificOutput(
    [property: JsonPropertyName("hookEventName")] string HookEventName,
    [property: JsonPropertyName("additionalContext")] string AdditionalContext);

[JsonSerializable(typeof(PostToolUseInput))]
[JsonSerializable(typeof(AsyncHookOutput))]
partial class HookJsonContext : JsonSerializerContext;
