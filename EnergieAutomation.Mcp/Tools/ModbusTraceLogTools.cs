using ModelContextProtocol.Server;
using System.ComponentModel;

/// <summary>
/// MCP tools for reading Modbus trace logs from the local dump directory.
/// </summary>
[McpServerToolType]
internal class ModbusTraceLogTools
{
    [McpServerTool]
    [Description("Returns the latest Modbus trace log entries from the dump directory.")]
    public string GetLatestModbusTraceLog(
        [Description("Maximum number of log lines to return.")] int maxLines = 200)
    {
        if (maxLines <= 0)
        {
            return "maxLines must be greater than zero.";
        }

        var dumpDirectory = @"D:\dump";
        if (!Directory.Exists(dumpDirectory))
        {
            return $"No Modbus trace folder found at {dumpDirectory}.";
        }

        var files = Directory.GetFiles(dumpDirectory, "*_Messages.txt")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();

        if (files.Length == 0)
        {
            return $"No Modbus trace files found in {dumpDirectory}.";
        }

        var latestFile = files[0];
        var lines = File.ReadLines(latestFile).TakeLast(maxLines);

        return $"File: {Path.GetFileName(latestFile)}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
    }

    [McpServerTool]
    [Description("Returns a slice of a specific Modbus trace file from the dump directory.")]
    public string GetModbusTraceFile(
        [Description("The trace file name to read.")] string fileName,
        [Description("The number of lines to skip before reading.")] int skip = 0,
        [Description("The number of lines to return after skipping.")] int take = 200)
    {
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

        var dumpDirectory = @"D:\dump";
        if (!Directory.Exists(dumpDirectory))
        {
            return $"No Modbus trace folder found at {dumpDirectory}.";
        }

        var safeFileName = Path.GetFileName(fileName);
        var filePath = Path.Combine(dumpDirectory, safeFileName);
        if (!File.Exists(filePath))
        {
            return $"No Modbus trace file found at {filePath}.";
        }

        var lines = File.ReadLines(filePath).Skip(skip).Take(take);
        return $"File: {Path.GetFileName(filePath)}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
    }
}
