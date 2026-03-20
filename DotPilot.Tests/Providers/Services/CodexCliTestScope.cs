using System.Globalization;
using System.Text.Json;

namespace DotPilot.Tests.Providers;

internal sealed class CodexCliTestScope : IDisposable
{
    private const int DeleteRetryCount = 20;
    private static readonly TimeSpan DeleteRetryDelay = TimeSpan.FromMilliseconds(250);
    private readonly string rootPath;
    private readonly string? originalPath;
    private readonly string? originalHome;
    private readonly string? originalUserProfile;
    private readonly string? originalDotPilotOnnxModelPath;
    private readonly string? originalOnnxModelPath;
    private readonly string? originalDotPilotLlamaSharpModelPath;
    private readonly string? originalLlamaSharpModelPath;
    private bool disposed;

    private CodexCliTestScope(
        string rootPath,
        string? originalPath,
        string? originalHome,
        string? originalUserProfile,
        string? originalDotPilotOnnxModelPath,
        string? originalOnnxModelPath,
        string? originalDotPilotLlamaSharpModelPath,
        string? originalLlamaSharpModelPath)
    {
        this.rootPath = rootPath;
        this.originalPath = originalPath;
        this.originalHome = originalHome;
        this.originalUserProfile = originalUserProfile;
        this.originalDotPilotOnnxModelPath = originalDotPilotOnnxModelPath;
        this.originalOnnxModelPath = originalOnnxModelPath;
        this.originalDotPilotLlamaSharpModelPath = originalDotPilotLlamaSharpModelPath;
        this.originalLlamaSharpModelPath = originalLlamaSharpModelPath;
    }

    public static CodexCliTestScope Create(string testName)
    {
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        var originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE");
        var originalDotPilotOnnxModelPath = Environment.GetEnvironmentVariable("DOTPILOT_ONNX_MODEL_PATH");
        var originalOnnxModelPath = Environment.GetEnvironmentVariable("ONNX_MODEL_PATH");
        var originalDotPilotLlamaSharpModelPath = Environment.GetEnvironmentVariable("DOTPILOT_LLAMASHARP_MODEL_PATH");
        var originalLlamaSharpModelPath = Environment.GetEnvironmentVariable("LLAMASHARP_MODEL_PATH");
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "DotPilot.Tests",
            testName,
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(rootPath);
        Environment.SetEnvironmentVariable("PATH", rootPath);

        var homePath = Path.Combine(rootPath, "home");
        Directory.CreateDirectory(homePath);
        Environment.SetEnvironmentVariable("HOME", homePath);
        Environment.SetEnvironmentVariable("USERPROFILE", homePath);

        return new CodexCliTestScope(
            rootPath,
            originalPath,
            originalHome,
            originalUserProfile,
            originalDotPilotOnnxModelPath,
            originalOnnxModelPath,
            originalDotPilotLlamaSharpModelPath,
            originalLlamaSharpModelPath);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Environment.SetEnvironmentVariable("PATH", originalPath);
        Environment.SetEnvironmentVariable("HOME", originalHome);
        Environment.SetEnvironmentVariable("USERPROFILE", originalUserProfile);
        Environment.SetEnvironmentVariable("DOTPILOT_ONNX_MODEL_PATH", originalDotPilotOnnxModelPath);
        Environment.SetEnvironmentVariable("ONNX_MODEL_PATH", originalOnnxModelPath);
        Environment.SetEnvironmentVariable("DOTPILOT_LLAMASHARP_MODEL_PATH", originalDotPilotLlamaSharpModelPath);
        Environment.SetEnvironmentVariable("LLAMASHARP_MODEL_PATH", originalLlamaSharpModelPath);
        DeleteDirectoryWithRetry(rootPath);

        disposed = true;
    }

    public void WriteVersionCommand(string commandName, string output)
    {
        WriteCommand(
            commandName,
            OperatingSystem.IsWindows()
                ? $"@echo off{Environment.NewLine}echo {output}{Environment.NewLine}"
                : $"#!/bin/sh{Environment.NewLine}echo \"{output}\"{Environment.NewLine}");
    }

    public void WriteCountingVersionCommand(string commandName, string output, int delayMilliseconds)
    {
        var counterPath = GetCounterFilePath(commandName);
        if (OperatingSystem.IsWindows())
        {
            WriteCommand(
                commandName,
                string.Join(
                    Environment.NewLine,
                    "@echo off",
                    $"set \"COUNTER_FILE={counterPath}\"",
                    "set /a COUNT=0",
                    "if exist \"%COUNTER_FILE%\" set /p COUNT=<\"%COUNTER_FILE%\"",
                    "set /a COUNT=%COUNT%+1",
                    ">\"%COUNTER_FILE%\" echo %COUNT%",
                    $"if not \"{delayMilliseconds}\"==\"0\" powershell -NoProfile -Command \"Start-Sleep -Milliseconds {delayMilliseconds}\"",
                    $"echo {output}",
                    string.Empty));
            return;
        }

        WriteCommand(
            commandName,
            string.Join(
                Environment.NewLine,
                "#!/bin/sh",
                $"counter_file='{counterPath.Replace("'", "'\\''", StringComparison.Ordinal)}'",
                "count=0",
                "if [ -f \"$counter_file\" ]; then count=$(cat \"$counter_file\"); fi",
                "count=$((count + 1))",
                "printf '%s' \"$count\" > \"$counter_file\"",
                delayMilliseconds > 0
                    ? $"sleep {(delayMilliseconds / 1000d).ToString(CultureInfo.InvariantCulture)}"
                    : string.Empty,
                $"echo \"{output}\"",
                string.Empty));
    }

    public int ReadInvocationCount(string commandName)
    {
        var counterPath = GetCounterFilePath(commandName);
        if (!File.Exists(counterPath))
        {
            return 0;
        }

        var content = File.ReadAllText(counterPath).Trim();
        return int.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? count
            : 0;
    }

    public void WriteCodexMetadata(
        string defaultModel,
        params string[] models)
    {
        var configDirectory = Path.Combine(
            Environment.GetEnvironmentVariable("HOME") ?? rootPath,
            ".codex");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(
            Path.Combine(configDirectory, "config.toml"),
            string.Join(
                Environment.NewLine,
                $"model = \"{defaultModel}\"",
                "model_reasoning_effort = \"medium\"",
                string.Empty));

        var payload = new
        {
            fetched_at = "2026-03-15T15:52:07.329647Z",
            etag = "test",
            client_version = "1.0.0",
            models = models.Select(model => new
            {
                slug = model,
                display_name = model,
                description = $"Test model {model}",
                default_reasoning_level = "medium",
                supported_reasoning_levels = new[]
                {
                    new
                    {
                        effort = "low",
                        description = "Fast responses with lighter reasoning",
                    },
                    new
                    {
                        effort = "medium",
                        description = "Balances speed and reasoning depth for everyday tasks",
                    },
                },
                shell_type = "shell_command",
                visibility = "list",
                supported_in_api = true,
                priority = 0,
                availability_nux = (string?)null,
                upgrade = (string?)null,
            }),
        };

        File.WriteAllText(
            Path.Combine(configDirectory, "models_cache.json"),
            JsonSerializer.Serialize(payload));
    }

    public void WriteClaudeSettings(string model)
    {
        var configDirectory = Path.Combine(
            Environment.GetEnvironmentVariable("HOME") ?? rootPath,
            ".claude");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(
            Path.Combine(configDirectory, "settings.json"),
            JsonSerializer.Serialize(new
            {
                model,
            }));
    }

    public void WriteCopilotConfig(string model)
    {
        var configDirectory = Path.Combine(
            Environment.GetEnvironmentVariable("HOME") ?? rootPath,
            ".copilot");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(
            Path.Combine(configDirectory, "config.json"),
            JsonSerializer.Serialize(new
            {
                model,
            }));
    }

    public void WriteGeminiMetadata(
        string defaultModel,
        params string[] models)
    {
        var configDirectory = Path.Combine(
            Environment.GetEnvironmentVariable("HOME") ?? rootPath,
            ".gemini");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(
            Path.Combine(configDirectory, "config.toml"),
            string.Join(
                Environment.NewLine,
                $"model = \"{defaultModel}\"",
                string.Empty));

        var payload = new
        {
            models = models.Select(model => new
            {
                slug = model,
                display_name = model,
                description = $"Test model {model}",
                visibility = "list",
                supported_in_api = true,
                supported_reasoning_levels = new[]
                {
                    new
                    {
                        effort = "low",
                    },
                    new
                    {
                        effort = "high",
                    },
                },
            }),
        };

        File.WriteAllText(
            Path.Combine(configDirectory, "models_cache.json"),
            JsonSerializer.Serialize(payload));
    }

    public string WriteOnnxModelDirectory(string directoryName = "phi-4-mini-instruct-onnx")
    {
        var directoryPath = Path.Combine(rootPath, directoryName);
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(Path.Combine(directoryPath, "genai_config.json"), "{}");
        Environment.SetEnvironmentVariable("DOTPILOT_ONNX_MODEL_PATH", directoryPath);
        return directoryPath;
    }

    public string WriteLlamaSharpModelFile(string fileName = "llama-3.2-3b-instruct.gguf")
    {
        var filePath = Path.Combine(rootPath, fileName);
        File.WriteAllText(filePath, "placeholder model");
        Environment.SetEnvironmentVariable("DOTPILOT_LLAMASHARP_MODEL_PATH", filePath);
        return filePath;
    }

    private string GetCounterFilePath(string commandName)
    {
        return Path.Combine(rootPath, commandName + ".count");
    }

    private void WriteCommand(string commandName, string commandBody)
    {
        var commandPath = OperatingSystem.IsWindows()
            ? Path.Combine(rootPath, commandName + ".cmd")
            : Path.Combine(rootPath, commandName);
        File.WriteAllText(commandPath, commandBody);

        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(
            commandPath,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupRead |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherExecute);
    }

    private static void DeleteDirectoryWithRetry(string path)
    {
        for (var attempt = 0; attempt < DeleteRetryCount; attempt++)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < DeleteRetryCount - 1)
            {
                System.Threading.Thread.Sleep(DeleteRetryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < DeleteRetryCount - 1)
            {
                System.Threading.Thread.Sleep(DeleteRetryDelay);
            }
        }
    }
}
