namespace EnergyAutomate.Services.CodeFactory;

public sealed record CodeTemplateDefinition(
    string Topic,
    string Key,
    string DisplayName,
    string Description,
    string Language,
    string DefaultCode);
