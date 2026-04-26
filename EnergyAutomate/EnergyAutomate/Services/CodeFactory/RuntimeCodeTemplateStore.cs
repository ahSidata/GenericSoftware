using System.Collections.Concurrent;

namespace EnergyAutomate.Services.CodeFactory;

public sealed class RuntimeCodeTemplateStore
{
    private readonly IReadOnlyList<CodeTemplateDefinition> _defaults;
    private readonly ConcurrentDictionary<string, string> _runtimeCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<RuntimeCodeTemplateStore> _logger;

    public RuntimeCodeTemplateStore(ICodeTemplateProvider templateProvider, ILogger<RuntimeCodeTemplateStore> logger)
    {
        ArgumentNullException.ThrowIfNull(templateProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _defaults = templateProvider.GetTemplates();
        _logger = logger;
    }

    public IReadOnlyList<CodeTemplateViewModel> GetTemplates()
    {
        return _defaults
            .Select(definition => CodeTemplateViewModel.FromDefinition(
                definition,
                _runtimeCode.TryGetValue(definition.Key, out var currentCode) ? currentCode : null))
            .OrderBy(template => template.Topic)
            .ThenBy(template => template.DisplayName)
            .ToList();
    }

    public CodeTemplateViewModel? GetTemplate(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var definition = _defaults.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
        {
            return null;
        }

        return CodeTemplateViewModel.FromDefinition(
            definition,
            _runtimeCode.TryGetValue(definition.Key, out var currentCode) ? currentCode : null);
    }

    public CodeTemplateViewModel SaveTemplate(string key, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(code);

        var definition = _defaults.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Template '{key}' was not found.");

        _runtimeCode[definition.Key] = code;
        _logger.LogTrace("Runtime code template {TemplateKey} saved", definition.Key);

        return CodeTemplateViewModel.FromDefinition(definition, code);
    }

    public CodeTemplateViewModel ResetTemplate(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var definition = _defaults.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Template '{key}' was not found.");

        _runtimeCode.TryRemove(definition.Key, out _);
        _logger.LogTrace("Runtime code template {TemplateKey} reset to default", definition.Key);

        return CodeTemplateViewModel.FromDefinition(definition);
    }
}
