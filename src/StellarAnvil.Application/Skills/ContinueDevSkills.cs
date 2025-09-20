using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace StellarAnvil.Application.Skills;

/// <summary>
/// Continue.dev compatible skills for AI agents
/// </summary>
public class ContinueDevSkills
{
    [KernelFunction, Description("Search the web for information")]
    public async Task<string> WebSearchAsync(
        [Description("The search query")] string query,
        [Description("Number of results to return")] int maxResults = 5)
    {
        // Mock implementation - replace with actual web search API
        await Task.Delay(100);
        
        return JsonSerializer.Serialize(new
        {
            query,
            results = new[]
            {
                new { title = $"Search result 1 for: {query}", url = "https://example.com/1", snippet = "Mock search result snippet 1" },
                new { title = $"Search result 2 for: {query}", url = "https://example.com/2", snippet = "Mock search result snippet 2" }
            }
        });
    }

    [KernelFunction, Description("Read a file from the filesystem")]
    public async Task<string> ReadFileAsync(
        [Description("The file path to read")] string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return $"Error: File not found at path: {filePath}";
            }

            var content = await File.ReadAllTextAsync(filePath);
            return $"File content from {filePath}:\n{content}";
        }
        catch (Exception ex)
        {
            return $"Error reading file {filePath}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Write content to a file")]
    public async Task<string> WriteFileAsync(
        [Description("The file path to write to")] string filePath,
        [Description("The content to write")] string content)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(filePath, content);
            return $"Successfully wrote content to {filePath}";
        }
        catch (Exception ex)
        {
            return $"Error writing file {filePath}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Execute code in a safe environment")]
    public async Task<string> ExecuteCodeAsync(
        [Description("The programming language (python, javascript, bash, etc.)")] string language,
        [Description("The code to execute")] string code)
    {
        try
        {
            // Mock implementation - in production, use a sandboxed environment
            await Task.Delay(500); // Simulate execution time

            return language.ToLower() switch
            {
                "python" => $"Python execution result:\n# Code executed:\n{code}\n# Output:\nMock Python output",
                "javascript" => $"JavaScript execution result:\n// Code executed:\n{code}\n// Output:\nMock JavaScript output",
                "bash" => $"Bash execution result:\n# Command executed:\n{code}\n# Output:\nMock bash output",
                _ => $"Language '{language}' not supported in mock environment"
            };
        }
        catch (Exception ex)
        {
            return $"Error executing {language} code: {ex.Message}";
        }
    }

    [KernelFunction, Description("Run a terminal command")]
    public async Task<string> RunTerminalCommandAsync(
        [Description("The command to run")] string command,
        [Description("Working directory (optional)")] string? workingDirectory = null)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
                Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory()
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return "Error: Failed to start process";
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var result = $"Command: {command}\nExit Code: {process.ExitCode}";
            if (!string.IsNullOrEmpty(output))
                result += $"\nOutput:\n{output}";
            if (!string.IsNullOrEmpty(error))
                result += $"\nError:\n{error}";

            return result;
        }
        catch (Exception ex)
        {
            return $"Error running command '{command}': {ex.Message}";
        }
    }

    [KernelFunction, Description("List files and directories")]
    public async Task<string> ListDirectoryAsync(
        [Description("The directory path to list")] string directoryPath,
        [Description("Include subdirectories")] bool recursive = false)
    {
        try
        {
            await Task.Delay(10); // Async operation

            if (!Directory.Exists(directoryPath))
            {
                return $"Error: Directory not found: {directoryPath}";
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(directoryPath, "*", searchOption);
            var directories = Directory.GetDirectories(directoryPath, "*", searchOption);

            var result = $"Directory listing for: {directoryPath}\n\nDirectories:\n";
            foreach (var dir in directories)
            {
                result += $"  📁 {Path.GetRelativePath(directoryPath, dir)}\n";
            }

            result += "\nFiles:\n";
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                result += $"  📄 {Path.GetRelativePath(directoryPath, file)} ({fileInfo.Length} bytes)\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error listing directory {directoryPath}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get current working directory")]
    public Task<string> GetCurrentDirectoryAsync()
    {
        return Task.FromResult($"Current working directory: {Directory.GetCurrentDirectory()}");
    }

    [KernelFunction, Description("Create a new directory")]
    public async Task<string> CreateDirectoryAsync(
        [Description("The directory path to create")] string directoryPath)
    {
        try
        {
            await Task.Delay(10); // Async operation
            
            if (Directory.Exists(directoryPath))
            {
                return $"Directory already exists: {directoryPath}";
            }

            Directory.CreateDirectory(directoryPath);
            return $"Successfully created directory: {directoryPath}";
        }
        catch (Exception ex)
        {
            return $"Error creating directory {directoryPath}: {ex.Message}";
        }
    }
}
