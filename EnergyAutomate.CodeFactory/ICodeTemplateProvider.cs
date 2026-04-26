namespace EnergyAutomate.Services.CodeFactory;

public interface ICodeTemplateProvider
{
    IReadOnlyList<CodeTemplateDefinition> GetTemplates();
}
