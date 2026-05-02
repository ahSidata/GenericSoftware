using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Linq;

/// <summary>
/// MCP tools for reading Modbus trace logs from the local dump directory.
/// </summary>
[McpServerToolType]
internal class ModbusTraceLogTools
{
    private const string DumpDirectoryEnvironmentVariable = "DUMP_DIR";

    [McpServerTool]
    [Description("Returns the latest Modbus trace files from the dump directory.")]
    public string GetLatestModbusTraceLogs(
        [Description("Maximum number of files to return.")] int maxFiles = 10)
    {
        if (maxFiles <= 0)
        {
            return "maxFiles must be greater than zero.";
        }

        var dumpDirectory = GetDumpRootDirectory();
        if (!Directory.Exists(dumpDirectory))
        {
            return $"No Modbus trace folder found at {dumpDirectory}.";
        }

        var files = Directory.GetFiles(dumpDirectory, "*_Messages.txt", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();

        if (files.Length == 0)
        {
            return $"No Modbus trace files found in {dumpDirectory}.";
        }

        var latestFiles = files.Take(maxFiles)
            .Select(filePath => GetDumpFileInfo(dumpDirectory, filePath))
            .ToArray();

        return string.Join(Environment.NewLine + Environment.NewLine, latestFiles.Select(fileInfo =>
            $"TypeFolder: {fileInfo.TypeFolder}{Environment.NewLine}TopicFolder: {fileInfo.TopicFolder}{Environment.NewLine}FileName: {fileInfo.FileName}{Environment.NewLine}RelativePath: {fileInfo.RelativePath}"));
    }

    [McpServerTool]
    [Description("Returns a slice of a specific Modbus trace file from the dump directory.")]
    public string GetModbusTraceFile(
        [Description("The type folder name under the dump directory.")] string typeFolder,
        [Description("The topic folder name under the type folder.")] string topicFolder,
        [Description("The trace file name to read.")] string fileName,
        [Description("The number of lines to skip before reading.")] int skip = 0,
        [Description("The number of lines to return after skipping.")] int take = 200)
    {
        if (string.IsNullOrWhiteSpace(typeFolder))
        {
            return "typeFolder must not be empty.";
        }

        if (string.IsNullOrWhiteSpace(topicFolder))
        {
            return "topicFolder must not be empty.";
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "fileName must not be empty.";
        }

        if (skip < 0)
        {
            return "skip must be greater than or equal to zero.";
        }

        if (take <= 0)
        {
            return "take must be greater than zero.";
        }

        var dumpDirectory = GetDumpRootDirectory();
        if (!Directory.Exists(dumpDirectory))
        {
            return $"No Modbus trace folder found at {dumpDirectory}.";
        }

        var safeTypeFolder = Path.GetFileName(typeFolder);
        var safeTopicFolder = Path.GetFileName(topicFolder);
        var safeFileName = Path.GetFileName(fileName);
        var filePath = Path.GetFullPath(Path.Combine(dumpDirectory, safeTypeFolder, safeTopicFolder, safeFileName));
        var dumpRoot = Path.GetFullPath(dumpDirectory) + Path.DirectorySeparatorChar;
        if (!filePath.StartsWith(dumpRoot, StringComparison.OrdinalIgnoreCase))
        {
            return "The requested file path is invalid.";
        }

        if (!File.Exists(filePath))
        {
            return $"No Modbus trace file found at {filePath}.";
        }

        var fileInfo = GetDumpFileInfo(dumpDirectory, filePath);
        var lines = File.ReadLines(filePath).Skip(skip).Take(take);
        return $"TypeFolder: {fileInfo.TypeFolder}{Environment.NewLine}TopicFolder: {fileInfo.TopicFolder}{Environment.NewLine}FileName: {fileInfo.FileName}{Environment.NewLine}RelativePath: {fileInfo.RelativePath}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
    }

    private static DumpFileInfo GetDumpFileInfo(string dumpDirectory, string filePath)
    {
        var dumpRoot = Path.GetFullPath(dumpDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(filePath);
        var relativePath = fullPath.StartsWith(dumpRoot, StringComparison.OrdinalIgnoreCase)
            ? fullPath[dumpRoot.Length..]
            : Path.GetFileName(fullPath);

        var relativeParts = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var fileName = relativeParts.Length > 0 ? relativeParts[^1] : Path.GetFileName(fullPath);
        var topicFolder = relativeParts.Length > 2 ? relativeParts[^2] : string.Empty;
        var typeFolder = relativeParts.Length > 2 ? relativeParts[^3] : string.Empty;

        return new DumpFileInfo(typeFolder, topicFolder, fileName, relativePath);
    }

    private static string GetDumpRootDirectory()
    {
        var configuredDirectory = Environment.GetEnvironmentVariable(DumpDirectoryEnvironmentVariable);
        return string.IsNullOrWhiteSpace(configuredDirectory) ? @"D:\Dump" : configuredDirectory;
    }

    private sealed record DumpFileInfo(string TypeFolder, string TopicFolder, string FileName, string RelativePath);
}
