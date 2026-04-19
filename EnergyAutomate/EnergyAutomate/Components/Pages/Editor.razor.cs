using EnergyAutomate.BlazorMonaco;
using EnergyAutomate.BlazorMonaco.Bridge;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace EnergyAutomate.Components.Pages
{
    public partial class Editor
    {
        [AllowNull]
        private StandaloneCodeEditor _editor;

        private static StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor)
        {
            return new StandaloneEditorConstructionOptions
            {
                Language = "javascript",
                GlyphMargin = true,
                AutomaticLayout = true,
                Value = "\"use strict\";\n" +
                            "function Person(age) {\n" +
                            "	if (age) {\n" +
                            "		this.age = age;\n" +
                            "	}\n" +
                            "}\n" +
                            "Person.prototype.getAge = function () {\n" +
                            "	return this.age;\n" +
                            "};\n"
            };
        }

        private async Task EditorOnDidInit()
        {
            await _editor.AddCommand((int)KeyMod.CtrlCmd | (int)KeyCode.KeyH, (args) =>
            {
                Console.WriteLine("Ctrl+H : Initial editor command is triggered.");
            });

            var newDecorations = new ModelDeltaDecoration[]
            {
            new() {
                Range = new EditorRange(3,1,3,1),
                Options = new ModelDecorationOptions
                {
                    IsWholeLine = true,
                    ClassName = "decorationContentClass",
                    GlyphMarginClassName = "decorationGlyphMarginClass"
                }
            }
            };

            var decorationIds = await _editor.DeltaDecorations(null, newDecorations);
            // You can now use '_decorationIds' to change or remove the decorations
        }

        private void OnContextMenu(EditorMouseEvent eventArg)
        {
            Console.WriteLine("OnContextMenu : " + JsonSerializer.Serialize(eventArg));
        }
    }
}