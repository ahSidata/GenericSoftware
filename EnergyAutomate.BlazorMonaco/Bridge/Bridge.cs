using EnergyAutomate.BlazorMonaco.Helpers;
using Microsoft.JSInterop;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnergyAutomate.BlazorMonaco.Bridge
{
    public class Environment
    {
        public bool? GlobalAPI { get; set; }

        public string BaseUrl { get; set; }
    }

    public enum MarkerTag
    {
        Unnecessary = 1,
        Deprecated = 2
    }

    public enum MarkerSeverity
    {
        Hint = 1,
        Info = 2,
        Warning = 4,
        Error = 8
    }

    public class UriComponents
    {
        public string Scheme { get; set; }
        public string Authority { get; set; }
        public string Path { get; set; }
        public string Query { get; set; }
        public string Fragment { get; set; }
    }

    public enum KeyCode
    {
        DependsOnKbLayout = -1,

        Unknown = 0,
        Backspace = 1,
        Tab = 2,
        Enter = 3,
        Shift = 4,
        Ctrl = 5,
        Alt = 6,
        PauseBreak = 7,
        CapsLock = 8,
        Escape = 9,
        Space = 10,
        PageUp = 11,
        PageDown = 12,
        End = 13,
        Home = 14,
        LeftArrow = 15,
        UpArrow = 16,
        RightArrow = 17,
        DownArrow = 18,
        Insert = 19,
        Delete = 20,
        Digit0 = 21,
        Digit1 = 22,
        Digit2 = 23,
        Digit3 = 24,
        Digit4 = 25,
        Digit5 = 26,
        Digit6 = 27,
        Digit7 = 28,
        Digit8 = 29,
        Digit9 = 30,
        KeyA = 31,
        KeyB = 32,
        KeyC = 33,
        KeyD = 34,
        KeyE = 35,
        KeyF = 36,
        KeyG = 37,
        KeyH = 38,
        KeyI = 39,
        KeyJ = 40,
        KeyK = 41,
        KeyL = 42,
        KeyM = 43,
        KeyN = 44,
        KeyO = 45,
        KeyP = 46,
        KeyQ = 47,
        KeyR = 48,
        KeyS = 49,
        KeyT = 50,
        KeyU = 51,
        KeyV = 52,
        KeyW = 53,
        KeyX = 54,
        KeyY = 55,
        KeyZ = 56,
        Meta = 57,
        ContextMenu = 58,
        F1 = 59,
        F2 = 60,
        F3 = 61,
        F4 = 62,
        F5 = 63,
        F6 = 64,
        F7 = 65,
        F8 = 66,
        F9 = 67,
        F10 = 68,
        F11 = 69,
        F12 = 70,
        F13 = 71,
        F14 = 72,
        F15 = 73,
        F16 = 74,
        F17 = 75,
        F18 = 76,
        F19 = 77,
        F20 = 78,
        F21 = 79,
        F22 = 80,
        F23 = 81,
        F24 = 82,
        NumLock = 83,
        ScrollLock = 84,

        Semicolon = 85,

        Equal = 86,

        Comma = 87,

        Minus = 88,

        Period = 89,

        Slash = 90,

        Backquote = 91,

        BracketLeft = 92,

        Backslash = 93,

        BracketRight = 94,

        Quote = 95,

        OEM_8 = 96,

        IntlBackslash = 97,
        Numpad0 = 98,
        Numpad1 = 99,
        Numpad2 = 100,
        Numpad3 = 101,
        Numpad4 = 102,
        Numpad5 = 103,
        Numpad6 = 104,
        Numpad7 = 105,
        Numpad8 = 106,
        Numpad9 = 107,
        NumpadMultiply = 108,
        NumpadAdd = 109,
        NUMPAD_SEPARATOR = 110,
        NumpadSubtract = 111,
        NumpadDecimal = 112,
        NumpadDivide = 113,

        KEY_IN_COMPOSITION = 114,
        ABNT_C1 = 115,
        ABNT_C2 = 116,
        AudioVolumeMute = 117,
        AudioVolumeUp = 118,
        AudioVolumeDown = 119,
        BrowserSearch = 120,
        BrowserHome = 121,
        BrowserBack = 122,
        BrowserForward = 123,
        MediaTrackNext = 124,
        MediaTrackPrevious = 125,
        MediaStop = 126,
        MediaPlayPause = 127,
        LaunchMediaPlayer = 128,
        LaunchMail = 129,
        LaunchApp2 = 130,

        Clear = 131,

        MAX_VALUE = 132
    }

    public enum KeyMod
    {
        CtrlCmd = 2048,
        Shift = 1024,
        Alt = 512,
        WinCtrl = 256,
    }

    public class MarkdownString
    {
        public string Value { get; set; }
        public bool? IsTrusted { get; set; }
        public bool? SupportThemeIcons { get; set; }
        public bool? SupportHtml { get; set; }
        public UriComponents BaseUri { get; set; }
        public Dictionary<string, UriComponents> Uris { get; set; }
    }

    public class KeyboardEvent
    {
        public KeyboardEvent BrowserEvent { get; set; }
        public JsonElement? Target { get; set; }
        public bool CtrlKey { get; set; }
        public bool ShiftKey { get; set; }
        public bool AltKey { get; set; }
        public bool MetaKey { get; set; }
        public bool AltGraphKey { get; set; }
        public KeyCode KeyCode { get; set; }
        public string Code { get; set; }
    }

    public class MouseEvent
    {
        public MouseEvent BrowserEvent { get; set; }
        public bool LeftButton { get; set; }
        public bool MiddleButton { get; set; }
        public bool RightButton { get; set; }
        public int Buttons { get; set; }
        public JsonElement? Target { get; set; }
        public int Detail { get; set; }
        public double Posx { get; set; }
        public double Posy { get; set; }
        public bool CtrlKey { get; set; }
        public bool ShiftKey { get; set; }
        public bool AltKey { get; set; }
        public bool MetaKey { get; set; }
        public long Timestamp { get; set; }
        public bool DefaultPrevented { get; set; }
    }

    public class ScrollEvent
    {
        public double ScrollTop { get; set; }
        public double ScrollLeft { get; set; }
        public double ScrollWidth { get; set; }
        public double ScrollHeight { get; set; }
        public bool ScrollTopChanged { get; set; }
        public bool ScrollLeftChanged { get; set; }
        public bool ScrollWidthChanged { get; set; }
        public bool ScrollHeightChanged { get; set; }
    }

    public class ScrolledVisiblePosition
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Height { get; set; }
    }

    public class Position
    {
        public int LineNumber { get; set; }

        public int Column { get; set; }
    }

    public class EditorRange
    {
        public int StartLineNumber { get; set; }

        public int StartColumn { get; set; }

        public int EndLineNumber { get; set; }

        public int EndColumn { get; set; }

        public EditorRange()
        { }

        public EditorRange(int startLineNumber, int startColumn, int endLineNumber, int endColumn)
        {
            StartLineNumber = startLineNumber;
            StartColumn = startColumn;
            EndLineNumber = endLineNumber;
            EndColumn = endColumn;
        }
    }

    public class Selection : EditorRange
    {
        public int SelectionStartLineNumber { get; set; }

        public int SelectionStartColumn { get; set; }

        public int PositionLineNumber { get; set; }

        public int PositionColumn { get; set; }
    }

    public enum SelectionDirection
    {
        LTR = 0,

        RTL = 1
    }

    public class Token
    {
        public int Offset { get; set; }
        public string Type { get; set; }
        public string Language { get; set; }
    }

    public partial class Global
    {
        internal static async Task Create(
            IJSRuntime jsRuntime,
            string domElementId,
            StandaloneEditorConstructionOptions options,
            EditorOverrideServices overrideServices,
            DotNetObjectReference<EnergyAutomate.BlazorMonaco.Bridge.Editor> dotnetObjectRef)
        {
            options = options ?? new StandaloneEditorConstructionOptions();

            var optionsJson = JsonSerializer.Serialize(options, JsonSerializerExt.DefaultOptions);
            var optionsDict = JsonSerializer.Deserialize<JsonElement>(optionsJson);

#if NET5_0_OR_GREATER
            await JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync("blazorMonaco.editor.setWasm", OperatingSystem.IsBrowser());
#else
            var isBrowser = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Create("BROWSER"));
            await JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync("blazorMonaco.editor.setWasm", isBrowser);
#endif
            // Call the JS create method and await a confirmation boolean. The JS implementation
            // returns a Promise that resolves when the editor is fully created and registered.
            var created = await JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync<bool>("blazorMonaco.editor.create", domElementId, optionsDict, overrideServices, dotnetObjectRef);
            if (!created)
            {
                // As a fallback, try to poll briefly for the editor registration before giving up.
                var runtime = JsRuntimeExt.UpdateRuntime(jsRuntime);
                const int maxAttempts = 40;
                const int delayMs = 50;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    try
                    {
                        var editorType = await runtime.SafeInvokeAsync<string>("blazorMonaco.editor.getEditorType", domElementId);
                        if (!string.IsNullOrWhiteSpace(editorType))
                            return;
                    }
                    catch (JSException)
                    {
                        // ignore and retry
                    }

                    await Task.Delay(delayMs);
                }

                throw new InvalidOperationException($"Failed to create Monaco editor with id '{domElementId}'");
            }
        }

        internal static async Task CreateDiffEditor(
            IJSRuntime jsRuntime,
            string domElementId,
            StandaloneDiffEditorConstructionOptions options,
            EditorOverrideServices overrideServices,
            DotNetObjectReference<EnergyAutomate.BlazorMonaco.Bridge.Editor> dotnetObjectRef,
            DotNetObjectReference<EnergyAutomate.BlazorMonaco.Bridge.Editor> dotnetObjectRefOriginal,
            DotNetObjectReference<EnergyAutomate.BlazorMonaco.Bridge.Editor> dotnetObjectRefModified)
        {
            options = options ?? new StandaloneDiffEditorConstructionOptions();

            var optionsJson = JsonSerializer.Serialize(options, JsonSerializerExt.DefaultOptions);
            var optionsDict = JsonSerializer.Deserialize<JsonElement>(optionsJson);

#if NET5_0_OR_GREATER
            await JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync("blazorMonaco.editor.setWasm", OperatingSystem.IsBrowser());
#else
            var isBrowser = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Create("BROWSER"));
            await JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync("blazorMonaco.editor.setWasm", isBrowser);
#endif
            var created = await JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync<bool>("blazorMonaco.editor.createDiffEditor", domElementId, optionsDict, overrideServices, dotnetObjectRef, dotnetObjectRefOriginal, dotnetObjectRefModified);
            if (!created)
            {
                // Brief fallback polling
                var runtime = JsRuntimeExt.UpdateRuntime(jsRuntime);
                const int maxAttempts = 40;
                const int delayMs = 50;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    try
                    {
                        var editorType = await runtime.SafeInvokeAsync<string>("blazorMonaco.editor.getEditorType", domElementId);
                        if (!string.IsNullOrWhiteSpace(editorType))
                            return;
                    }
                    catch (JSException)
                    {
                        // ignore and retry
                    }

                    await Task.Delay(delayMs);
                }

                throw new InvalidOperationException($"Failed to create Monaco diff editor with id '{domElementId}'");
            }
        }

        [Obsolete("This method is deprecated as it's WASM only. Use the overload that takes an IJSRuntime parameter.")]
        public static Task<TextModel> CreateModel(string value, string language = null, string uri = null)
            => CreateModel(null, value, language, uri);

        public static async Task<TextModel> CreateModel(IJSRuntime jsRuntime, string value, string language = null, string uri = null)
        {
            var textModel = await JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync<TextModel>("blazorMonaco.editor.createModel", value, language, uri);
            if (textModel != null)
                textModel.JsRuntime = JsRuntimeExt.UpdateRuntime(jsRuntime);
            return textModel;
        }

        [Obsolete("This method is deprecated as it's WASM only. Use the overload that takes an IJSRuntime parameter.")]
        public static Task SetModelLanguage(TextModel model, string languageId)
            => SetModelLanguage(null, model, languageId);

        public static Task SetModelLanguage(IJSRuntime jsRuntime, TextModel model, string mimeTypeOrLanguageId)
            => JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync("blazorMonaco.editor.setModelLanguage", model.Uri, mimeTypeOrLanguageId);

        public static Task SetModelMarkers(IJSRuntime jsRuntime, TextModel model, string owner, List<MarkerData> markers)
            => JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync("blazorMonaco.editor.setModelMarkers", model.Uri, owner, markers);

        public static Task RemoveAllMarkers(IJSRuntime jsRuntime, string owner)
            => JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync("blazorMonaco.editor.removeAllMarkers", owner);

        public static Task<List<Marker>> GetModelMarkers(IJSRuntime jsRuntime, GetModelMarkersFilter filter)
            => JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync<List<Marker>>("blazorMonaco.editor.getModelMarkers", filter);

        public class GetModelMarkersFilter
        {
            public string Owner { get; set; }
            public string Resource { get; set; }
            public int? Take { get; set; }
        }

        [Obsolete("This method is deprecated as it's WASM only. Use the overload that takes an IJSRuntime parameter.")]
        public static Task<TextModel> GetModel(string uri)
            => GetModel(null, uri);

        public static async Task<TextModel> GetModel(IJSRuntime jsRuntime, string uri)
        {
            var textModel = await JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync<TextModel>("blazorMonaco.editor.getModel", uri);
            if (textModel != null)
                textModel.JsRuntime = JsRuntimeExt.UpdateRuntime(jsRuntime);
            return textModel;
        }

        [Obsolete("This method is deprecated as it's WASM only. Use the overload that takes an IJSRuntime parameter.")]
        public static Task<List<TextModel>> GetModels()
            => GetModels(null);

        public static async Task<List<TextModel>> GetModels(IJSRuntime jsRuntime)
        {
            var result = await JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync<List<TextModel>>("blazorMonaco.editor.getModels");
            result.ForEach(t =>
            {
                if (t != null)
                    t.JsRuntime = JsRuntimeExt.UpdateRuntime(jsRuntime);
            });
            return result;
        }

        [Obsolete("This method is deprecated as it's WASM only. Use the overload that takes an IJSRuntime parameter.")]
        public static Task ColorizeElement(string domNodeId, ColorizerElementOptions options)
            => ColorizeElement(null, domNodeId, options);

        public static Task ColorizeElement(IJSRuntime jsRuntime, string domNodeId, ColorizerElementOptions options)
            => JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync("blazorMonaco.editor.colorizeElement", domNodeId, options);

        [Obsolete("This method is deprecated as it's WASM only. Use the overload that takes an IJSRuntime parameter.")]
        public static Task<string> Colorize(string text, string languageId, ColorizerOptions options)
            => Colorize(null, text, languageId, options);

        public static Task<string> Colorize(IJSRuntime jsRuntime, string text, string languageId, ColorizerOptions options)
            => JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync<string>("blazorMonaco.editor.colorize", text, languageId, options);

        [Obsolete("This method is deprecated as it's WASM only. Use the overload that takes an IJSRuntime parameter.")]
        public static Task<string> ColorizeModelLine(TextModel model, int lineNumber, int? tabSize = null)
            => ColorizeModelLine(null, model, lineNumber, tabSize);

        public static Task<string> ColorizeModelLine(IJSRuntime jsRuntime, TextModel model, int lineNumber, int? tabSize = null)
            => JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync<string>("blazorMonaco.editor.colorizeModelLine", model.Uri, lineNumber, tabSize);

        [Obsolete("This method is deprecated as it's WASM only. Use the overload that takes an IJSRuntime parameter.")]
        public static Task DefineTheme(string themeName, StandaloneThemeData themeData)
            => DefineTheme(null, themeName, themeData);

        public static Task DefineTheme(IJSRuntime jsRuntime, string themeName, StandaloneThemeData themeData)
            => JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync("blazorMonaco.editor.defineTheme", themeName, themeData);

        [Obsolete("This method is deprecated as it's WASM only. Use the overload that takes an IJSRuntime parameter.")]
        public static Task SetTheme(string themeName)
            => SetTheme(null, themeName);

        public static Task SetTheme(IJSRuntime jsRuntime, string themeName)
            => JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync("blazorMonaco.editor.setTheme", themeName);

        [Obsolete("This method is deprecated as it's WASM only. Use the overload that takes an IJSRuntime parameter.")]
        public static Task RemeasureFonts()
            => RemeasureFonts(null);

        public static Task RemeasureFonts(IJSRuntime jsRuntime)
            => JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync("blazorMonaco.editor.remeasureFonts");
    }

    public class BuiltinTheme
    {
        public const string Vs = "vs";
        public const string VsDark = "vs-dark";
        public const string HcBlack = "hc-black";
        public const string HcLight = "hc-light";
    }

    public class StandaloneThemeData
    {
        public string Base { get; set; }
        public bool Inherit { get; set; }
        public List<TokenThemeRule> Rules { get; set; }
        public List<string> EncodedTokensColors { get; set; }
        public Dictionary<string, string> Colors { get; set; }
    }

    public class TokenThemeRule
    {
        public string Token { get; set; }
        public string Foreground { get; set; }
        public string Background { get; set; }
        public string FontStyle { get; set; }
    }

    public class ActionDescriptor
    {
        public string Id { get; set; }

        public string Label { get; set; }

        public string Precondition { get; set; }

        public int[] Keybindings { get; set; }

        public string KeybindingContext { get; set; }

        public string ContextMenuGroupId { get; set; }

        public float ContextMenuOrder { get; set; }

        [JsonIgnore]
        public Action<CodeEditor> Run { get; set; }
    }

    public interface IGlobalEditorOptions
    {
        int? TabSize { get; set; }

        bool? InsertSpaces { get; set; }

        bool? DetectIndentation { get; set; }

        bool? TrimAutoWhitespace { get; set; }

        bool? LargeFileOptimizations { get; set; }

        bool? WordBasedSuggestions { get; set; }

        bool? WordBasedSuggestionsOnlySameLanguage { get; set; }

        [JsonPropertyName("semanticHighlighting.enabled")]
        bool? SemanticHighlightingEnabled { get; set; }

        bool? StablePeek { get; set; }

        int? MaxTokenizationLineLength { get; set; }

        string Theme { get; set; }

        bool? AutoDetectHighContrast { get; set; }
    }

    public class EditorUpdateOptions : EditorOptions, IGlobalEditorOptions
    {
        public int? TabSize { get; set; }
        public bool? InsertSpaces { get; set; }
        public bool? DetectIndentation { get; set; }
        public bool? TrimAutoWhitespace { get; set; }
        public bool? LargeFileOptimizations { get; set; }
        public bool? WordBasedSuggestions { get; set; }
        public bool? WordBasedSuggestionsOnlySameLanguage { get; set; }
        [JsonPropertyName("semanticHighlighting.enabled")]
        public bool? SemanticHighlightingEnabled { get; set; }
        public bool? StablePeek { get; set; }
        public int? MaxTokenizationLineLength { get; set; }
        public string Theme { get; set; }
        public bool? AutoDetectHighContrast { get; set; }
    }

    public class StandaloneEditorConstructionOptions : EditorConstructionOptions, IGlobalEditorOptions
    {
        public TextModel Model { get; set; }

        public string Value { get; set; }

        public string Language { get; set; }

        public string Theme { get; set; }

        public bool? AutoDetectHighContrast { get; set; }

        public string AccessibilityHelpUrl { get; set; }

        public int? TabSize { get; set; }
        public bool? InsertSpaces { get; set; }
        public bool? DetectIndentation { get; set; }
        public bool? TrimAutoWhitespace { get; set; }
        public bool? LargeFileOptimizations { get; set; }
        public bool? WordBasedSuggestions { get; set; }
        public bool? WordBasedSuggestionsOnlySameLanguage { get; set; }
        [JsonPropertyName("semanticHighlighting.enabled")]
        public bool? SemanticHighlightingEnabled { get; set; }
        public bool? StablePeek { get; set; }
        public int? MaxTokenizationLineLength { get; set; }
    }

    public class StandaloneDiffEditorConstructionOptions : DiffEditorConstructionOptions
    {
        public string Theme { get; set; }

        public bool? AutoDetectHighContrast { get; set; }
    }

    public delegate void CommandHandler(params object[] args);

    public class EditorOverrideServices : Dictionary<string, object>
    { }

    public class Marker
    {
        public string Owner { get; set; }
        [JsonPropertyName("resource")]
        public string ResourceUri { get; set; }
        public MarkerSeverity Severity { get; set; }
        public JsonElement? Code { get; set; }
        [JsonIgnore]
        public string CodeAsString
        {
            get => Code?.AsString();
            set => Code = JsonElementExt.FromObject(value);
        }
        [JsonIgnore]
        public MarkerCode CodeAsObject
        {
            get => Code?.AsObject<MarkerCode>();
            set => Code = JsonElementExt.FromObject(value);
        }
        public string Message { get; set; }
        public string Source { get; set; }
        public int StartLineNumber { get; set; }
        public int StartColumn { get; set; }
        public int EndLineNumber { get; set; }
        public int EndColumn { get; set; }
        public int ModelVersionId { get; set; }
        public List<RelatedInformation> RelatedInformation { get; set; }
        public List<MarkerTag> Tags { get; set; }
        public string Origin { get; set; }
    }

    public class MarkerCode
    {
        public string Value { get; set; }
        [JsonPropertyName("target")]
        public string TargetUri { get; set; }
    }

    public class MarkerData
    {
        public JsonElement? Code { get; set; }
        [JsonIgnore]
        public string CodeAsString
        {
            get => Code?.AsString();
            set => Code = JsonElementExt.FromObject(value);
        }
        [JsonIgnore]
        public MarkerCode CodeAsObject
        {
            get => Code?.AsObject<MarkerCode>();
            set => Code = JsonElementExt.FromObject(value);
        }
        public MarkerSeverity Severity { get; set; }
        public string Message { get; set; }
        public string Source { get; set; }
        public int StartLineNumber { get; set; }
        public int StartColumn { get; set; }
        public int EndLineNumber { get; set; }
        public int EndColumn { get; set; }
        public int ModelVersionId { get; set; }
        public List<RelatedInformation> RelatedInformation { get; set; }
        public List<MarkerTag> Tags { get; set; }
        public string Origin { get; set; }
    }

    public class RelatedInformation
    {
        [JsonPropertyName("resource")]
        public string ResourceUri { get; set; }
        public string Message { get; set; }
        public int StartLineNumber { get; set; }
        public int StartColumn { get; set; }
        public int EndLineNumber { get; set; }
        public int EndColumn { get; set; }
    }

    public class ColorizerOptions
    {
        public int? TabSize { get; set; }
    }

    public class ColorizerElementOptions : ColorizerOptions
    {
        public string Theme { get; set; }
        public string MimeType { get; set; }
    }

    public enum ScrollbarVisibility
    {
        Auto = 1,
        Hidden = 2,
        Visible = 3
    }

    public class ThemeColor
    {
        public string Id { get; set; }
    }

    public class ThemeIcon
    {
        public string Id { get; set; }
        public ThemeColor Color { get; set; }
    }

    public class SingleEditOperation
    {
        public EnergyAutomate.BlazorMonaco.Bridge.EditorRange Range { get; set; }

        public string Text { get; set; }

        public bool? ForceMoveMarkers { get; set; }
    }

    public class WordAtPosition
    {
        public string Word { get; set; }

        public int StartColumn { get; set; }

        public int EndColumn { get; set; }
    }

    public enum OverviewRulerLane
    {
        Left = 1,
        Center = 2,
        Right = 4,
        Full = 7
    }

    public enum GlyphMarginLane
    {
        Left = 1,
        Center = 2,
        Right = 3
    }

    public enum MinimapPosition
    {
        Inline = 1,
        Gutter = 2
    }

    public enum MinimapSectionHeaderStyle
    {
        Normal = 1,
        Underlined = 2
    }

    public class DecorationOptions
    {
        public string Color { get; set; }

        public string DarkColor { get; set; }
    }

    public class ModelDecorationGlyphMarginOptions
    {
        public GlyphMarginLane Position { get; set; }

        public bool? PersistLane { get; set; }
    }

    public class ModelDecorationOverviewRulerOptions : DecorationOptions
    {
        public OverviewRulerLane Position { get; set; }
    }

    public class ModelDecorationMinimapOptions : DecorationOptions
    {
        public MinimapPosition Position { get; set; }

        public MinimapSectionHeaderStyle? SectionHeaderStyle { get; set; }

        public string SectionHeaderText { get; set; }
    }

    public class ModelDecorationOptions
    {
        public TrackedRangeStickiness? Stickiness { get; set; }

        public string ClassName { get; set; }

        public bool? ShouldFillLineOnLineBreak { get; set; }
        public string BlockClassName { get; set; }

        public bool? BlockIsAfterEnd { get; set; }
        public bool? BlockDoesNotCollapse { get; set; }

        public MarkdownString[] GlyphMarginHoverMessage { get; set; }

        public MarkdownString[] HoverMessage { get; set; }

        public MarkdownString[] LineNumberHoverMessage { get; set; }

        public bool? IsWholeLine { get; set; }

        public bool? ShowIfCollapsed { get; set; }

        public int? ZIndex { get; set; }

        public ModelDecorationOverviewRulerOptions OverviewRuler { get; set; }

        public ModelDecorationMinimapOptions Minimap { get; set; }

        public string GlyphMarginClassName { get; set; }

        public ModelDecorationGlyphMarginOptions GlyphMargin { get; set; }

        public int? LineHeight { get; set; }

        public string FontFamily { get; set; }

        public string FontSize { get; set; }

        public string FontWeight { get; set; }

        public string FontStyle { get; set; }

        public string LinesDecorationsClassName { get; set; }

        public string LinesDecorationsTooltip { get; set; }

        public string LineNumberClassName { get; set; }

        public string FirstLineDecorationClassName { get; set; }

        public string MarginClassName { get; set; }

        public string InlineClassName { get; set; }

        public bool? InlineClassNameAffectsLetterSpacing { get; set; }

        public string BeforeContentClassName { get; set; }

        public string AfterContentClassName { get; set; }

        public InjectedTextOptions After { get; set; }

        public InjectedTextOptions Before { get; set; }

        public TextDirection? TextDirection { get; set; }
    }

    public enum TextDirection
    {
        LTR = 0,
        RTL = 1
    }

    public class InjectedTextOptions
    {
        public string Content { get; set; }

        public string InlineClassName { get; set; }

        public bool? InlineClassNameAffectsLetterSpacing { get; set; }

        public InjectedTextCursorStops? CursorStops { get; set; }
    }

    public enum InjectedTextCursorStops
    {
        Both = 0,
        Right = 1,
        Left = 2,
        None = 3
    }

    public class ModelDeltaDecoration
    {
        public EnergyAutomate.BlazorMonaco.Bridge.EditorRange Range { get; set; }

        public ModelDecorationOptions Options { get; set; }
    }

    public class ModelDecoration
    {
        public string Id { get; set; }

        public int OwnerId { get; set; }

        public EnergyAutomate.BlazorMonaco.Bridge.EditorRange Range { get; set; }

        public ModelDecorationOptions Options { get; set; }
    }

    public enum EndOfLinePreference
    {
        TextDefined = 0,

        LF = 1,

        CRLF = 2
    }

    public enum DefaultEndOfLine
    {
        LF = 1,

        CRLF = 2
    }

    public enum EndOfLineSequence
    {
        LF = 0,

        CRLF = 1
    }

    public class IdentifiedSingleEditOperation : SingleEditOperation
    {
    }

    public class ValidEditOperation
    {
        public EnergyAutomate.BlazorMonaco.Bridge.EditorRange Range { get; set; }

        public string Text { get; set; }
    }

    public delegate List<Selection> CursorStateComputer(List<ValidEditOperation> inverseEditOperations);

    public class TextModelResolvedOptions
    {
        public int TabSize { get; set; }
        public int IndentSize { get; set; }
        public bool InsertSpaces { get; set; }
        public DefaultEndOfLine DefaultEOL { get; set; }
        public bool TrimAutoWhitespace { get; set; }
        public BracketPairColorizationOptions BracketPairColorizationOptions { get; set; }
    }

    public class TextModelUpdateOptions
    {
        public int? TabSize { get; set; }
        public int? IndentSize { get; set; }
        public bool? InsertSpaces { get; set; }
        public bool? TrimAutoWhitespace { get; set; }
        public BracketPairColorizationOptions BracketColorizationOptions { get; set; }
    }

    public class FindMatch
    {
        public EnergyAutomate.BlazorMonaco.Bridge.EditorRange Range { get; set; }
        public string[] Matches { get; set; }
    }

    public enum TrackedRangeStickiness
    {
        AlwaysGrowsWhenTypingAtEdges = 0,
        NeverGrowsWhenTypingAtEdges = 1,
        GrowsOnlyWhenTypingBefore = 2,
        GrowsOnlyWhenTypingAfter = 3
    }

    public enum PositionAffinity
    {
        Left = 0,

        Right = 1,

        None = 2,

        LeftOfInjectedText = 3,

        RightOfInjectedText = 4
    }

    public class Dimension
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class DiffEditorModel
    {
        public TextModel Original { get; set; }

        public TextModel Modified { get; set; }
    }

    public class ModelChangedEvent
    {
        public string OldModelUrl { get; set; }

        public string NewModelUrl { get; set; }
    }

    public class ContentSizeChangedEvent
    {
        public double ContentWidth { get; set; }
        public double ContentHeight { get; set; }
        public bool ContentWidthChanged { get; set; }
        public bool ContentHeightChanged { get; set; }
    }

    public class NewScrollPosition
    {
        public double ScrollLeft { get; set; }
        public double ScrollTop { get; set; }
    }

    public enum ScrollType
    {
        Smooth = 0,
        Immediate = 1
    }

    public class ModelLanguageChangedEvent
    {
        public string OldLanguage { get; set; }

        public string NewLanguage { get; set; }

        public string Source { get; set; }
    }

    public class ModelLanguageConfigurationChangedEvent
    {
    }

    public class ModelContentChangedEvent
    {
        public List<ModelContentChange> Changes { get; set; }

        public string Eol { get; set; }

        public int VersionId { get; set; }

        public bool IsUndoing { get; set; }

        public bool IsRedoing { get; set; }

        public bool IsFlush { get; set; }

        public bool IsEolChange { get; set; }

        public List<int> DetailedReasonsChangeLengths { get; set; }
    }

    public class SerializedModelContentChangedEvent
    {
        public List<ModelContentChange> Changes { get; set; }

        public string Eol { get; set; }

        public int VersionId { get; set; }

        public bool IsUndoing { get; set; }

        public bool IsRedoing { get; set; }

        public bool IsFlush { get; set; }

        public bool IsEolChange { get; set; }
    }

    public class ModelDecorationsChangedEvent
    {
        public bool AffectsMinimap { get; set; }
        public bool AffectsOverviewRuler { get; set; }
        public bool AffectsGlyphMargin { get; set; }
        public bool AffectsLineNumber { get; set; }
    }

    public class ModelOptionsChangedEvent
    {
        public bool TabSize { get; set; }
        public bool IndentSize { get; set; }
        public bool InsertSpaces { get; set; }
        public bool TrimAutoWhitespace { get; set; }
    }

    public class ModelContentChange
    {
        public EnergyAutomate.BlazorMonaco.Bridge.EditorRange Range { get; set; }

        public int RangeOffset { get; set; }

        public int RangeLength { get; set; }

        public string Text { get; set; }
    }

    public enum CursorChangeReason
    {
        NotSet = 0,

        ContentFlush = 1,

        RecoverFromMarkers = 2,

        Explicit = 3,

        Paste = 4,

        Undo = 5,

        Redo = 6
    }

    public class CursorPositionChangedEvent
    {
        public Position Position { get; set; }

        public List<Position> SecondaryPositions { get; set; }

        public CursorChangeReason Reason { get; set; }

        public string Source { get; set; }
    }

    public class CursorSelectionChangedEvent
    {
        public Selection Selection { get; set; }

        public List<Selection> SecondarySelections { get; set; }

        public int ModelVersionId { get; set; }

        public List<Selection> OldSelections { get; set; }

        public int OldModelVersionId { get; set; }

        public string Source { get; set; }

        public CursorChangeReason Reason { get; set; }
    }

    public enum AccessibilitySupport
    {
        Unknown = 0,
        Disabled = 1,
        Enabled = 2
    }

    public enum EditorAutoIndentStrategy
    {
        None = 0,
        Keep = 1,
        Brackets = 2,
        Advanced = 3,
        Full = 4
    }

    public class EditorOptions
    {
        public bool? InDiffEditor { get; set; }

        public bool? AllowVariableLineHeights { get; set; }

        public bool? AllowVariableFonts { get; set; }

        public bool? AllowVariableFontsInAccessibilityMode { get; set; }

        public string AriaLabel { get; set; }

        public bool? AriaRequired { get; set; }

        public bool? ScreenReaderAnnounceInlineSuggestion { get; set; }

        public int? TabIndex { get; set; }

        public int[] Rulers { get; set; }

        public List<string> WordSegmenterLocales { get; set; }

        public string WordSeparators { get; set; }

        public bool? SelectionClipboard { get; set; }

        public string LineNumbers { get; set; }
        public Func<int, string> LineNumbersLambda { get; set; }

        public int? CursorSurroundingLines { get; set; }

        public string CursorSurroundingLinesStyle { get; set; }

        public string RenderFinalNewline { get; set; }

        public string UnusualLineTerminators { get; set; }

        public bool? SelectOnLineNumbers { get; set; }

        public int? LineNumbersMinChars { get; set; }

        public bool? GlyphMargin { get; set; }

        public int? LineDecorationsWidth { get; set; }
        public string LineDecorationsWidthString { get; set; }

        public int? RevealHorizontalRightPadding { get; set; }

        public bool? RoundedSelection { get; set; }

        public string ExtraEditorClassName { get; set; }

        public bool? ReadOnly { get; set; }

        public MarkdownString ReadOnlyMessage { get; set; }

        public bool? DomReadOnly { get; set; }

        public bool? LinkedEditing { get; set; }

        public bool? RenameOnType { get; set; }

        public string RenderValidationDecorations { get; set; }

        public EditorScrollbarOptions Scrollbar { get; set; }

        public EditorStickyScrollOptions StickyScroll { get; set; }

        public EditorMinimapOptions Minimap { get; set; }

        public EditorFindOptions Find { get; set; }

        public bool? FixedOverflowWidgets { get; set; }

        public bool? AllowOverflow { get; set; }

        public int? OverviewRulerLanes { get; set; }

        public bool? OverviewRulerBorder { get; set; }

        public string CursorBlinking { get; set; }

        public bool? MouseWheelZoom { get; set; }

        public string MouseStyle { get; set; }

        public string CursorSmoothCaretAnimation { get; set; }

        public string CursorStyle { get; set; }

        public string OvertypeCursorStyle { get; set; }

        public bool? OvertypeOnPaste { get; set; }

        public int? CursorWidth { get; set; }

        public int? CursorHeight { get; set; }

        public bool? FontLigatures { get; set; }

        public bool? FontVariations { get; set; }

        public string DefaultColorDecorators { get; set; }

        public bool? DisableLayerHinting { get; set; }

        public bool? DisableMonospaceOptimizations { get; set; }

        public bool? HideCursorInOverviewRuler { get; set; }

        public bool? ScrollBeyondLastLine { get; set; }

        public bool? ScrollOnMiddleClick { get; set; }

        public int? ScrollBeyondLastColumn { get; set; }

        public bool? SmoothScrolling { get; set; }

        public bool? AutomaticLayout { get; set; }

        public string WordWrap { get; set; }

        public string WordWrapOverride1 { get; set; }

        public string WordWrapOverride2 { get; set; }

        public int? WordWrapColumn { get; set; }

        public string WrappingIndent { get; set; }

        public string WrappingStrategy { get; set; }

        public bool? WrapOnEscapedLineFeeds { get; set; }

        public string WordWrapBreakBeforeCharacters { get; set; }

        public string WordWrapBreakAfterCharacters { get; set; }

        public string WordBreak { get; set; }

        public int? StopRenderingLineAfter { get; set; }

        public EditorHoverOptions Hover { get; set; }

        public bool? Links { get; set; }

        public bool? ColorDecorators { get; set; }

        public string ColorDecoratorsActivatedOn { get; set; }

        public int? ColorDecoratorsLimit { get; set; }

        public EditorCommentsOptions Comments { get; set; }

        public bool? Contextmenu { get; set; }

        public int? MouseWheelScrollSensitivity { get; set; }

        public int? FastScrollSensitivity { get; set; }

        public bool? ScrollPredominantAxis { get; set; }

        public bool? InertialScroll { get; set; }

        public bool? ColumnSelection { get; set; }

        public string MultiCursorModifier { get; set; }

        public bool? MultiCursorMergeOverlapping { get; set; }

        public string MultiCursorPaste { get; set; }

        public int? MultiCursorLimit { get; set; }

        public string MouseMiddleClickAction { get; set; }

        public string AccessibilitySupport { get; set; }

        public int? AccessibilityPageSize { get; set; }

        public SuggestOptions Suggest { get; set; }
        public InlineSuggestOptions InlineSuggest { get; set; }

        public SmartSelectOptions SmartSelect { get; set; }

        public GotoLocationOptions GotoLocation { get; set; }

        public QuickSuggestionsOptions QuickSuggestions { get; set; }

        public int? QuickSuggestionsDelay { get; set; }

        public EditorPaddingOptions Padding { get; set; }

        public EditorParameterHintOptions ParameterHints { get; set; }

        public string AutoClosingBrackets { get; set; }

        public string AutoClosingComments { get; set; }

        public string AutoClosingQuotes { get; set; }

        public string AutoClosingDelete { get; set; }

        public string AutoClosingOvertype { get; set; }

        public string AutoSurround { get; set; }

        public string AutoIndent { get; set; }

        public bool? AutoIndentOnPaste { get; set; }

        public bool? AutoIndentOnPasteWithinString { get; set; }

        public bool? StickyTabStops { get; set; }

        public bool? FormatOnType { get; set; }

        public bool? FormatOnPaste { get; set; }

        public bool? DragAndDrop { get; set; }

        public bool? SuggestOnTriggerCharacters { get; set; }

        public string AcceptSuggestionOnEnter { get; set; }

        public bool? AcceptSuggestionOnCommitCharacter { get; set; }

        public string SnippetSuggestions { get; set; }

        public bool? EmptySelectionClipboard { get; set; }

        public bool? CopyWithSyntaxHighlighting { get; set; }

        public string SuggestSelection { get; set; }

        public int? SuggestFontSize { get; set; }

        public int? SuggestLineHeight { get; set; }

        public string TabCompletion { get; set; }

        public bool? SelectionHighlight { get; set; }

        public bool? SelectionHighlightMultiline { get; set; }

        public int? SelectionHighlightMaxLength { get; set; }

        public string OccurrencesHighlight { get; set; }

        public int? OccurrencesHighlightDelay { get; set; }

        public bool? CodeLens { get; set; }

        public string CodeLensFontFamily { get; set; }

        public int? CodeLensFontSize { get; set; }

        public EditorLightbulbOptions Lightbulb { get; set; }

        public int? CodeActionsOnSaveTimeout { get; set; }

        public bool? Folding { get; set; }

        public string FoldingStrategy { get; set; }

        public bool? FoldingHighlight { get; set; }

        public bool? FoldingImportsByDefault { get; set; }

        public int? FoldingMaximumRegions { get; set; }

        public string ShowFoldingControls { get; set; }

        public bool? UnfoldOnClickAfterEndOfLine { get; set; }

        public string MatchBrackets { get; set; }

        public string ExperimentalGpuAcceleration { get; set; }

        public string ExperimentalWhitespaceRendering { get; set; }

        public string RenderWhitespace { get; set; }

        public bool? RenderControlCharacters { get; set; }

        public string RenderLineHighlight { get; set; }

        public bool? RenderLineHighlightOnlyWhenFocus { get; set; }

        public bool? UseTabStops { get; set; }

        public bool? TrimWhitespaceOnDelete { get; set; }

        public string FontFamily { get; set; }

        public string FontWeight { get; set; }

        public int? FontSize { get; set; }

        public int? LineHeight { get; set; }

        public int? LetterSpacing { get; set; }

        public bool? ShowUnused { get; set; }

        public string PeekWidgetDefaultFocus { get; set; }

        public string Placeholder { get; set; }

        public bool? DefinitionLinkOpensInPeek { get; set; }

        public bool? ShowDeprecated { get; set; }

        public bool? MatchOnWordStartOnly { get; set; }

        public EditorInlayHintsOptions InlayHints { get; set; }

        public bool? UseShadowDOM { get; set; }

        public GuidesOptions Guides { get; set; }

        public UnicodeHighlightOptions UnicodeHighlight { get; set; }

        public BracketPairColorizationOptions BracketPairColorization { get; set; }

        public DropIntoEditorOptions DropIntoEditor { get; set; }

        public bool? EditContext { get; set; }

        public bool? RenderRichScreenReaderContent { get; set; }

        public PasteAsOptions PasteAs { get; set; }

        public bool? TabFocusMode { get; set; }

        public bool? InlineCompletionsAccessibilityVerbose { get; set; }
    }

    public interface IDiffEditorBaseOptions
    {
        bool? EnableSplitViewResizing { get; set; }

        int? SplitViewDefaultRatio { get; set; }

        bool? RenderSideBySide { get; set; }

        int? RenderSideBySideInlineBreakpoint { get; set; }

        bool? UseInlineViewWhenSpaceIsLimited { get; set; }

        bool? CompactMode { get; set; }

        int? MaxComputationTime { get; set; }

        int? MaxFileSize { get; set; }

        bool? IgnoreTrimWhitespace { get; set; }

        bool? RenderIndicators { get; set; }

        bool? RenderMarginRevertIcon { get; set; }

        bool? RenderGutterMenu { get; set; }

        bool? OriginalEditable { get; set; }

        bool? DiffCodeLens { get; set; }

        bool? RenderOverviewRuler { get; set; }

        string DiffWordWrap { get; set; }

        string DiffAlgorithm { get; set; }

        bool? AccessibilityVerbose { get; set; }

        bool? IsInEmbeddedEditor { get; set; }

        bool? OnlyShowAccessibleDiffViewer { get; set; }
    }

    public class DiffEditorOptions : EditorOptions, IDiffEditorBaseOptions
    {
        public bool? EnableSplitViewResizing { get; set; }
        public int? SplitViewDefaultRatio { get; set; }
        public bool? RenderSideBySide { get; set; }
        public int? RenderSideBySideInlineBreakpoint { get; set; }
        public bool? UseInlineViewWhenSpaceIsLimited { get; set; }
        public bool? CompactMode { get; set; }
        public int? MaxComputationTime { get; set; }
        public int? MaxFileSize { get; set; }
        public bool? IgnoreTrimWhitespace { get; set; }
        public bool? RenderIndicators { get; set; }
        public bool? RenderMarginRevertIcon { get; set; }
        public bool? RenderGutterMenu { get; set; }
        public bool? OriginalEditable { get; set; }
        public bool? DiffCodeLens { get; set; }
        public bool? RenderOverviewRuler { get; set; }
        public string DiffWordWrap { get; set; }
        public string DiffAlgorithm { get; set; }
        public bool? AccessibilityVerbose { get; set; }
        public bool? IsInEmbeddedEditor { get; set; }
        public bool? OnlyShowAccessibleDiffViewer { get; set; }
    }

    public class ConfigurationChangedEvent
    {
        private readonly List<bool> _options;

        public ConfigurationChangedEvent(List<bool> options)
        {
            _options = options;
        }

        public bool HasChanged(EditorOption id)
        {
            return _options[(int)id];
        }
    }

    public class ComputedEditorOptions
    {
        private readonly List<string> _options;

        public ComputedEditorOptions(List<string> options)
        {
            _options = options;
        }

        public T Get<T>(EditorOption id)
        {
            return JsonSerializer.Deserialize<T>(_options[(int)id]);
        }
    }

    public class IEditorOption<V>
    {
        public EditorOption Id { get; set; }
        public string Name { get; set; }
        public V DefaultValue { get; set; }
    }

    public class EditorCommentsOptions
    {
        public bool? InsertSpace { get; set; }

        public bool? IgnoreEmptyLines { get; set; }
    }

    public enum TextEditorCursorBlinkingStyle
    {
        Hidden = 0,

        Blink = 1,

        Smooth = 2,

        Phase = 3,

        Expand = 4,

        Solid = 5
    }

    public enum TextEditorCursorStyle
    {
        Line = 1,

        Block = 2,

        Underline = 3,

        LineThin = 4,

        BlockOutline = 5,

        UnderlineThin = 6
    }

    public class EditorFindOptions
    {
        public bool? CursorMoveOnType { get; set; }

        public bool? FindOnType { get; set; }

        public string SeedSearchStringFromSelection { get; set; }

        public bool? AutoFindInSelection { get; set; }
        public bool? AddExtraSpaceOnTop { get; set; }

        public bool? Loop { get; set; }
    }

    public class GotoLocationOptions
    {
        public string Multiple { get; set; }
        public string MultipleDefinitions { get; set; }
        public string MultipleTypeDefinitions { get; set; }
        public string MultipleDeclarations { get; set; }
        public string MultipleImplementations { get; set; }
        public string MultipleReferences { get; set; }
        public string MultipleTests { get; set; }
        public string AlternativeDefinitionCommand { get; set; }
        public string AlternativeTypeDefinitionCommand { get; set; }
        public string AlternativeDeclarationCommand { get; set; }
        public string AlternativeImplementationCommand { get; set; }
        public string AlternativeReferenceCommand { get; set; }
        public string AlternativeTestsCommand { get; set; }
    }

    public class EditorHoverOptions
    {
        public bool? Enabled { get; set; }

        public int? Delay { get; set; }

        public bool? Sticky { get; set; }

        public int? HidingDelay { get; set; }

        public bool? Above { get; set; }
    }

    public class OverviewRulerPosition
    {
        public double Width { get; set; }

        public double Height { get; set; }

        public double Top { get; set; }

        public double Right { get; set; }
    }

    public enum RenderMinimap
    {
        None = 0,
        Text = 1,
        Blocks = 2
    }

    public class EditorLayoutInfo
    {
        public float Width { get; set; }

        public float Height { get; set; }

        public float GlyphMarginLeft { get; set; }

        public float GlyphMarginWidth { get; set; }

        public int GlyphMarginDecorationLaneCount { get; set; }

        public float LineNumbersLeft { get; set; }

        public float LineNumbersWidth { get; set; }

        public float DecorationsLeft { get; set; }

        public float DecorationsWidth { get; set; }

        public float ContentLeft { get; set; }

        public float ContentWidth { get; set; }

        public EditorMinimapLayoutInfo Minimap { get; set; }

        public float ViewportColumn { get; set; }
        public bool IsWordWrapMinified { get; set; }
        public bool IsViewportWrapping { get; set; }
        public float WrappingColumn { get; set; }

        public float VerticalScrollbarWidth { get; set; }

        public float HorizontalScrollbarHeight { get; set; }

        public OverviewRulerPosition OverviewRuler { get; set; }
    }

    public class EditorMinimapLayoutInfo
    {
        public RenderMinimap RenderMinimap { get; set; }
        public float MinimapLeft { get; set; }
        public float MinimapWidth { get; set; }
        public bool MinimapHeightIsEditorHeight { get; set; }
        public bool MinimapIsSampling { get; set; }
        public float MinimapScale { get; set; }
        public float MinimapLineHeight { get; set; }
        public float MinimapCanvasInnerWidth { get; set; }
        public float MinimapCanvasInnerHeight { get; set; }
        public float MinimapCanvasOuterWidth { get; set; }
        public float MinimapCanvasOuterHeight { get; set; }
    }

    public static class ShowLightbulbIconMode
    {
        public const string Off = "off";
        public const string OnCode = "onCode";
        public const string On = "on";
    }

    public class EditorLightbulbOptions
    {
        public string Enabled { get; set; }
    }

    public class EditorStickyScrollOptions
    {
        public bool? Enabled { get; set; }

        public int? MaxLineCount { get; set; }

        public string DefaultModel { get; set; }

        public bool? ScrollWithEditor { get; set; }
    }

    public class EditorInlayHintsOptions
    {
        public string Enabled { get; set; }

        public float? FontSize { get; set; }

        public string FontFamily { get; set; }

        public bool? Padding { get; set; }

        public int? MaximumLength { get; set; }
    }

    public class EditorMinimapOptions
    {
        public bool? Enabled { get; set; }

        public string Autohide { get; set; }

        public string Side { get; set; }

        public string Size { get; set; }

        public string ShowSlider { get; set; }

        public bool? RenderCharacters { get; set; }

        public int? MaxColumn { get; set; }

        public float? Scale { get; set; }

        public bool? ShowRegionSectionHeaders { get; set; }

        public bool? ShowMarkSectionHeaders { get; set; }

        public string MarkSectionHeaderRegex { get; set; }

        public int? SectionHeaderFontSize { get; set; }

        public int? SectionHeaderLetterSpacing { get; set; }
    }

    public class EditorPaddingOptions
    {
        public float? Top { get; set; }

        public float? Bottom { get; set; }
    }

    public class EditorParameterHintOptions
    {
        public bool? Enabled { get; set; }

        public bool? Cycle { get; set; }
    }

    public static class QuickSuggestionsValue
    {
        public const string On = "on";
        public const string Inline = "inline";
        public const string Off = "off";
    }

    public class QuickSuggestionsOptions
    {
        public string Other { get; set; }
        public string Comments { get; set; }
        public string Strings { get; set; }
    }

    public enum RenderLineNumbersType
    {
        Off = 0,
        On = 1,
        Relative = 2,
        Interval = 3,
        Custom = 4
    }

    public class EditorScrollbarOptions
    {
        public int? ArrowSize { get; set; }

        public string Vertical { get; set; }

        public string Horizontal { get; set; }

        public bool? UseShadows { get; set; }

        public bool? VerticalHasArrows { get; set; }

        public bool? HorizontalHasArrows { get; set; }

        public bool? HandleMouseWheel { get; set; }

        public bool? AlwaysConsumeMouseWheel { get; set; }

        public int? HorizontalScrollbarSize { get; set; }

        public int? VerticalScrollbarSize { get; set; }

        public int? VerticalSliderSize { get; set; }

        public int? HorizontalSliderSize { get; set; }

        public bool? ScrollByPage { get; set; }

        public bool? IgnoreHorizontalScrollbarInContentHeight { get; set; }
    }

    public class UnicodeHighlightOptions
    {
        public bool? NonBasicASCII { get; set; }

        public bool? InvisibleCharacters { get; set; }

        public bool? AmbiguousCharacters { get; set; }

        public bool? IncludeComments { get; set; }

        public bool? IncludeStrings { get; set; }
    }

    public class InlineSuggestOptions
    {
        public bool? Enabled { get; set; }

        public string Mode { get; set; }
        public string ShowToolbar { get; set; }
        public bool? SyntaxHighlightingEnabled { get; set; }
        public bool? SuppressSuggestions { get; set; }
        public int? MinShowDelay { get; set; }
        public bool? SuppressInSnippetMode { get; set; }

        public bool? KeepOnBlur { get; set; }

        public string FontFamily { get; set; }
    }

    public class BracketPairColorizationOptions
    {
        public bool? Enabled { get; set; }

        public bool? IndependentColorPoolPerBracketType { get; set; }
    }

    public class GuidesOptions
    {
        public string BracketPairs { get; set; }

        public string BracketPairsHorizontal { get; set; }

        public bool? HighlightActiveBracketPair { get; set; }

        public bool? Indentation { get; set; }

        public bool? HighlightActiveIndentation { get; set; }
    }

    public class SuggestOptions
    {
        public string InsertMode { get; set; }

        public bool? FilterGraceful { get; set; }

        public bool? SnippetsPreventQuickSuggestions { get; set; }

        public bool? LocalityBonus { get; set; }

        public bool? ShareSuggestSelections { get; set; }

        public string SelectionMode { get; set; }

        public bool? ShowIcons { get; set; }

        public bool? ShowStatusBar { get; set; }

        public bool? Preview { get; set; }

        public string PreviewModel { get; set; }

        public bool? ShowInlineDetails { get; set; }

        public bool? ShowMethods { get; set; }

        public bool? ShowFunctions { get; set; }

        public bool? ShowConstructors { get; set; }

        public bool? ShowDeprecated { get; set; }

        public bool? MatchOnWordStartOnly { get; set; }

        public bool? ShowFields { get; set; }

        public bool? ShowVariables { get; set; }

        public bool? ShowClasses { get; set; }

        public bool? ShowStructs { get; set; }

        public bool? ShowInterfaces { get; set; }

        public bool? ShowModules { get; set; }

        public bool? ShowProperties { get; set; }

        public bool? ShowEvents { get; set; }

        public bool? ShowOperators { get; set; }

        public bool? ShowUnits { get; set; }

        public bool? ShowValues { get; set; }

        public bool? ShowConstants { get; set; }

        public bool? ShowEnums { get; set; }

        public bool? ShowEnumMembers { get; set; }

        public bool? ShowKeywords { get; set; }

        public bool? ShowWords { get; set; }

        public bool? ShowColors { get; set; }

        public bool? ShowFiles { get; set; }

        public bool? ShowReferences { get; set; }

        public bool? ShowFolders { get; set; }

        public bool? ShowTypeParameters { get; set; }

        public bool? ShowIssues { get; set; }

        public bool? ShowUsers { get; set; }

        public bool? ShowSnippets { get; set; }
    }

    public class SmartSelectOptions
    {
        public bool? SelectLeadingAndTrailingWhitespace { get; set; }
        public bool? SelectSubwords { get; set; }
    }

    public enum WrappingIndent
    {
        None = 0,

        Same = 1,

        Indent = 2,

        DeepIndent = 3
    }

    public class DropIntoEditorOptions
    {
        public bool? Enabled { get; set; }

        public string ShowDropSelector { get; set; }
    }

    public class PasteAsOptions
    {
        public bool? Enabled { get; set; }

        public string ShowPasteSelector { get; set; }
    }

    public enum EditorOption
    {
        acceptSuggestionOnCommitCharacter = 0,
        acceptSuggestionOnEnter = 1,
        accessibilitySupport = 2,
        accessibilityPageSize = 3,
        allowOverflow = 4,
        allowVariableLineHeights = 5,
        allowVariableFonts = 6,
        allowVariableFontsInAccessibilityMode = 7,
        ariaLabel = 8,
        ariaRequired = 9,
        autoClosingBrackets = 10,
        autoClosingComments = 11,
        screenReaderAnnounceInlineSuggestion = 12,
        autoClosingDelete = 13,
        autoClosingOvertype = 14,
        autoClosingQuotes = 15,
        autoIndent = 16,
        autoIndentOnPaste = 17,
        autoIndentOnPasteWithinString = 18,
        automaticLayout = 19,
        autoSurround = 20,
        bracketPairColorization = 21,
        guides = 22,
        codeLens = 23,
        codeLensFontFamily = 24,
        codeLensFontSize = 25,
        colorDecorators = 26,
        colorDecoratorsLimit = 27,
        columnSelection = 28,
        comments = 29,
        contextmenu = 30,
        copyWithSyntaxHighlighting = 31,
        cursorBlinking = 32,
        cursorSmoothCaretAnimation = 33,
        cursorStyle = 34,
        cursorSurroundingLines = 35,
        cursorSurroundingLinesStyle = 36,
        cursorWidth = 37,
        cursorHeight = 38,
        disableLayerHinting = 39,
        disableMonospaceOptimizations = 40,
        domReadOnly = 41,
        dragAndDrop = 42,
        dropIntoEditor = 43,
        editContext = 44,
        emptySelectionClipboard = 45,
        experimentalGpuAcceleration = 46,
        experimentalWhitespaceRendering = 47,
        extraEditorClassName = 48,
        fastScrollSensitivity = 49,
        find = 50,
        fixedOverflowWidgets = 51,
        folding = 52,
        foldingStrategy = 53,
        foldingHighlight = 54,
        foldingImportsByDefault = 55,
        foldingMaximumRegions = 56,
        unfoldOnClickAfterEndOfLine = 57,
        fontFamily = 58,
        fontInfo = 59,
        fontLigatures = 60,
        fontSize = 61,
        fontWeight = 62,
        fontVariations = 63,
        formatOnPaste = 64,
        formatOnType = 65,
        glyphMargin = 66,
        gotoLocation = 67,
        hideCursorInOverviewRuler = 68,
        hover = 69,
        inDiffEditor = 70,
        inlineSuggest = 71,
        letterSpacing = 72,
        lightbulb = 73,
        lineDecorationsWidth = 74,
        lineHeight = 75,
        lineNumbers = 76,
        lineNumbersMinChars = 77,
        linkedEditing = 78,
        links = 79,
        matchBrackets = 80,
        minimap = 81,
        mouseStyle = 82,
        mouseWheelScrollSensitivity = 83,
        mouseWheelZoom = 84,
        multiCursorMergeOverlapping = 85,
        multiCursorModifier = 86,
        mouseMiddleClickAction = 87,
        multiCursorPaste = 88,
        multiCursorLimit = 89,
        occurrencesHighlight = 90,
        occurrencesHighlightDelay = 91,
        overtypeCursorStyle = 92,
        overtypeOnPaste = 93,
        overviewRulerBorder = 94,
        overviewRulerLanes = 95,
        padding = 96,
        pasteAs = 97,
        parameterHints = 98,
        peekWidgetDefaultFocus = 99,
        placeholder = 100,
        definitionLinkOpensInPeek = 101,
        quickSuggestions = 102,
        quickSuggestionsDelay = 103,
        readOnly = 104,
        readOnlyMessage = 105,
        renameOnType = 106,
        renderRichScreenReaderContent = 107,
        renderControlCharacters = 108,
        renderFinalNewline = 109,
        renderLineHighlight = 110,
        renderLineHighlightOnlyWhenFocus = 111,
        renderValidationDecorations = 112,
        renderWhitespace = 113,
        revealHorizontalRightPadding = 114,
        roundedSelection = 115,
        rulers = 116,
        scrollbar = 117,
        scrollBeyondLastColumn = 118,
        scrollBeyondLastLine = 119,
        scrollPredominantAxis = 120,
        selectionClipboard = 121,
        selectionHighlight = 122,
        selectionHighlightMaxLength = 123,
        selectionHighlightMultiline = 124,
        selectOnLineNumbers = 125,
        showFoldingControls = 126,
        showUnused = 127,
        snippetSuggestions = 128,
        smartSelect = 129,
        smoothScrolling = 130,
        stickyScroll = 131,
        stickyTabStops = 132,
        stopRenderingLineAfter = 133,
        suggest = 134,
        suggestFontSize = 135,
        suggestLineHeight = 136,
        suggestOnTriggerCharacters = 137,
        suggestSelection = 138,
        tabCompletion = 139,
        tabIndex = 140,
        trimWhitespaceOnDelete = 141,
        unicodeHighlighting = 142,
        unusualLineTerminators = 143,
        useShadowDOM = 144,
        useTabStops = 145,
        wordBreak = 146,
        wordSegmenterLocales = 147,
        wordSeparators = 148,
        wordWrap = 149,
        wordWrapBreakAfterCharacters = 150,
        wordWrapBreakBeforeCharacters = 151,
        wordWrapColumn = 152,
        wordWrapOverride1 = 153,
        wordWrapOverride2 = 154,
        wrappingIndent = 155,
        wrappingStrategy = 156,
        showDeprecated = 157,
        inertialScroll = 158,
        inlayHints = 159,
        wrapOnEscapedLineFeeds = 160,
        effectiveCursorStyle = 161,
        editorClassName = 162,
        pixelRatio = 163,
        tabFocusMode = 164,
        layoutInfo = 165,
        wrappingInfo = 166,
        defaultColorDecorators = 167,
        colorDecoratorsActivatedOn = 168,
        inlineCompletionsAccessibilityVerbose = 169,
        effectiveEditContext = 170,
        scrollOnMiddleClick = 171,
        effectiveAllowVariableFonts = 172
    }

    public class EditorConstructionOptions : EditorOptions
    {
        public Dimension Dimension { get; set; }
    }

    public enum ContentWidgetPositionPreference
    {
        EXACT = 0,

        ABOVE = 1,

        BELOW = 2
    }

    public enum OverlayWidgetPositionPreference
    {
        TOP_RIGHT_CORNER = 0,

        BOTTOM_RIGHT_CORNER = 1,

        TOP_CENTER = 2
    }

    public enum MouseTargetType
    {
        UNKNOWN = 0,

        TEXTAREA = 1,

        GUTTER_GLYPH_MARGIN = 2,

        GUTTER_LINE_NUMBERS = 3,

        GUTTER_LINE_DECORATIONS = 4,

        GUTTER_VIEW_ZONE = 5,

        CONTENT_TEXT = 6,

        CONTENT_EMPTY = 7,

        CONTENT_VIEW_ZONE = 8,

        CONTENT_WIDGET = 9,

        OVERVIEW_RULER = 10,

        SCROLLBAR = 11,

        OVERLAY_WIDGET = 12,

        OUTSIDE_EDITOR = 13
    }

    public class BaseMouseTarget
    {
        public JsonElement? Element { get; set; }

        public Position Position { get; set; }

        public int MouseColumn { get; set; }

        public EnergyAutomate.BlazorMonaco.Bridge.EditorRange Range { get; set; }
        public MouseTargetType Type { get; set; }
        public object Detail { get; set; }
    }

    public class MouseTargetUnknown : BaseMouseTarget
    {
    }

    public class MouseTargetTextarea : BaseMouseTarget
    {
    }

    public class MouseTargetMarginData
    {
        public bool IsAfterLines { get; set; }
        public float GlyphMarginLeft { get; set; }
        public float GlyphMarginWidth { get; set; }
        public GlyphMarginLane GlyphMarginLane { get; set; }
        public float LineNumbersWidth { get; set; }
        public float OffsetX { get; set; }
    }

    public class MouseTargetMargin : BaseMouseTarget
    {
        public new MouseTargetMarginData Detail { get; set; }
    }

    public class MouseTargetViewZoneData
    {
        public string ViewZoneId { get; set; }
        public Position PositionBefore { get; set; }
        public Position PositionAfter { get; set; }
        public Position Position { get; set; }
        public int AfterLineNumber { get; set; }
    }

    public class MouseTargetViewZone : BaseMouseTarget
    {
        public new MouseTargetViewZoneData Detail { get; set; }
    }

    public class MouseTargetContentTextData
    {
        public string MightBeForeignElement { get; set; }
    }

    public class MouseTargetContentText : BaseMouseTarget
    {
        public new MouseTargetContentTextData Detail { get; set; }
    }

    public class MouseTargetContentEmptyData
    {
        public bool IsAfterLines { get; set; }
        public float? HorizontalDistanceToText { get; set; }
    }

    public class MouseTargetContentEmpty : BaseMouseTarget
    {
        public new MouseTargetContentEmptyData Detail { get; set; }
    }

    public class MouseTargetContentWidget : BaseMouseTarget
    {
        public new string Detail { get; set; }
    }

    public class MouseTargetOverlayWidget : BaseMouseTarget
    {
        public new string Detail { get; set; }
    }

    public class MouseTargetScrollbar : BaseMouseTarget
    {
    }

    public class MouseTargetOverviewRuler : BaseMouseTarget
    {
    }

    public class MouseTargetOutsideEditor : BaseMouseTarget
    {
    }

    public class EditorMouseEvent
    {
        public MouseEvent Event { get; set; }
        public BaseMouseTarget Target { get; set; }
    }

    public class PartialEditorMouseEvent
    {
        public MouseEvent Event { get; set; }
        public BaseMouseTarget Target { get; set; }
    }

    public class PasteEvent
    {
        public EnergyAutomate.BlazorMonaco.Bridge.EditorRange Range { get; set; }
        public string LanguageId { get; set; }
    }

    public class DiffEditorConstructionOptions : DiffEditorOptions
    {
        public Dimension Dimension { get; set; }

        public string OriginalAriaLabel { get; set; }

        public string ModifiedAriaLabel { get; set; }
    }


    public class RelativePattern
    {
        public string Base { get; set; }

        public string Pattern { get; set; }

        public RelativePattern()
        { }

        public RelativePattern(string pattern)
            => Pattern = pattern;

        public static implicit operator RelativePattern(string pattern) => new RelativePattern(pattern);
    }

    public class LanguageSelector : List<LanguageFilter>
    {
        public LanguageSelector()
        { }

        public LanguageSelector(string language)
            => Add(language);

        public LanguageSelector(List<string> languages)
            => AddRange(languages.Select(l => new LanguageFilter(l)));

        public LanguageSelector(LanguageFilter languageFilter)
            => Add(languageFilter);

        public LanguageSelector(List<LanguageFilter> languageFilters)
            => AddRange(languageFilters);

        public static implicit operator LanguageSelector(string language) => new LanguageSelector(language);

        public static implicit operator LanguageSelector(List<string> languages) => new LanguageSelector(languages);

        public static implicit operator LanguageSelector(LanguageFilter language) => new LanguageSelector(language);
    }

    public class LanguageFilter
    {
        public string Language { get; set; }
        public string Scheme { get; set; }
        public RelativePattern Pattern { get; set; }
        public string NotebookType { get; set; }

        public bool? HasAccessToAllModels { get; set; }
        public bool? Exclusive { get; set; }

        public bool? IsBuiltin { get; set; }

        public LanguageFilter()
        { }

        public LanguageFilter(string language)
            => Language = language;

        public static implicit operator LanguageFilter(string language) => new LanguageFilter(language);
    }

    public partial class Global
    {
    }

    public partial class Global
    {
        public static Task RegisterHoverProviderAsync(IJSRuntime jsRuntime, LanguageSelector language, HoverProvider.ProvideDelegate provideHover)
            => RegisterHoverProviderAsync(jsRuntime, language, new HoverProvider(provideHover));

        public static Task RegisterHoverProviderAsync(IJSRuntime jsRuntime, LanguageSelector language, HoverProvider hoverProvider)
            => JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync("blazorMonaco.languages.registerHoverProvider", language, DotNetObjectReference.Create(hoverProvider));

        [Obsolete("Please use the new RegisterCodeActionProvider method with async parameters instead.")]
        public static Task RegisterCodeActionProvider(IJSRuntime jsRuntime, LanguageSelector language, CodeActionProvider.ProvideCodeActionsDelegate provideCodeActions, CodeActionProvider.ResolveCodeActionDelegate resolveCodeAction = null, CodeActionProviderMetadata metadata = null)
            => RegisterCodeActionProvider(jsRuntime, language, new CodeActionProvider(provideCodeActions, resolveCodeAction), metadata);

        public static Task RegisterCodeActionProvider(IJSRuntime jsRuntime, LanguageSelector language, CodeActionProvider.ProvideDelegate provideCodeActions, CodeActionProvider.ResolveDelegate resolveCodeAction = null, CodeActionProviderMetadata metadata = null)
            => RegisterCodeActionProvider(jsRuntime, language, new CodeActionProvider(provideCodeActions, resolveCodeAction), metadata);

        public static Task RegisterCodeActionProvider(IJSRuntime jsRuntime, LanguageSelector language, CodeActionProvider codeActionProvider, CodeActionProviderMetadata metadata = null)
            => JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync("blazorMonaco.languages.registerCodeActionProvider", language, DotNetObjectReference.Create(codeActionProvider), metadata);

        public static Task RegisterDocumentFormattingEditProvider(IJSRuntime jsRuntime, LanguageSelector language, DocumentFormattingEditProvider.ProvideDelegate provideDocumentFormattingEdits)
            => RegisterDocumentFormattingEditProvider(jsRuntime, language, new DocumentFormattingEditProvider(null, provideDocumentFormattingEdits));

        public static Task RegisterDocumentFormattingEditProvider(IJSRuntime jsRuntime, LanguageSelector language, DocumentFormattingEditProvider documentFormattingEditProvider)
            => JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync("blazorMonaco.languages.registerDocumentFormattingEditProvider", language, documentFormattingEditProvider.DisplayName, DotNetObjectReference.Create(documentFormattingEditProvider));

        [Obsolete("Please use the new RegisterCompletionItemProvider method with async parameters instead.")]
        public static Task RegisterCompletionItemProvider(IJSRuntime jsRuntime, LanguageSelector language, CompletionItemProvider.ProvideCompletionItemsDelegate provideCompletionItems, CompletionItemProvider.ResolveCompletionItemDelegate resolveCompletionItem = null)
            => RegisterCompletionItemProvider(jsRuntime, language, new CompletionItemProvider(provideCompletionItems, resolveCompletionItem));

        public static Task RegisterCompletionItemProvider(IJSRuntime jsRuntime, LanguageSelector language, CompletionItemProvider.ProvideDelegate provideCompletionItems, CompletionItemProvider.ResolveDelegate resolveCompletionItem = null)
            => RegisterCompletionItemProvider(jsRuntime, language, new CompletionItemProvider(null, provideCompletionItems, resolveCompletionItem));

        public static Task RegisterCompletionItemProvider(IJSRuntime jsRuntime, LanguageSelector language, CompletionItemProvider completionItemProvider)
            => JsRuntimeExt.UpdateRuntime(jsRuntime).SafeInvokeAsync("blazorMonaco.languages.registerCompletionItemProvider", language, completionItemProvider.TriggerCharacters, DotNetObjectReference.Create(completionItemProvider));
    }

    public class CodeActionContext
    {
        public List<MarkerData> Markers { get; set; }

        public string Only { get; set; }

        public CodeActionTriggerType Trigger { get; set; }
    }

    public class CodeActionProvider
    {
        [Obsolete("Please use the new async ProvideDelegate instead.")]
        public delegate CodeActionList ProvideCodeActionsDelegate(string modelUri, global::EnergyAutomate.BlazorMonaco.Bridge.EditorRange range, CodeActionContext context);

        [Obsolete("Please use the new async ProvideMethod instead.")]
        public ProvideCodeActionsDelegate ProvideCodeActionsFunc { get; set; }

        public delegate Task<CodeActionList> ProvideDelegate(string modelUri, global::EnergyAutomate.BlazorMonaco.Bridge.EditorRange range, CodeActionContext context);

        public ProvideDelegate ProvideMethod { get; set; }

#if NET5_0_OR_GREATER

        [DynamicDependency(nameof(ProvideCodeActions))]
#endif
        [JSInvokable]
        public Task<CodeActionList> ProvideCodeActions(string modelUri, global::EnergyAutomate.BlazorMonaco.Bridge.EditorRange range, CodeActionContext context)
#pragma warning disable CS0618
            => ProvideMethod?.Invoke(modelUri, range, context)
                ?? Task.FromResult(ProvideCodeActionsFunc?.Invoke(modelUri, range, context));

#pragma warning restore CS0618

        [Obsolete("Please use the new async ResolveDelegate instead.")]
        public delegate CodeAction ResolveCodeActionDelegate(CodeAction codeAction);

        [Obsolete("Please use the new async ResolveMethod instead.")]
        public ResolveCodeActionDelegate ResolveCodeActionFunc { get; set; }

        public delegate Task<CodeAction> ResolveDelegate(CodeAction codeAction);

        public ResolveDelegate ResolveMethod { get; set; }

#if NET5_0_OR_GREATER

        [DynamicDependency(nameof(ResolveCodeAction))]
#endif
        [JSInvokable]
        public Task<CodeAction> ResolveCodeAction(CodeAction codeAction)
#pragma warning disable CS0618
            => ResolveMethod?.Invoke(codeAction)
                ?? Task.FromResult(ResolveCodeActionFunc?.Invoke(codeAction));

#pragma warning restore CS0618

        [Obsolete("Please use the new constructor with async parameters instead.")]
        public CodeActionProvider(ProvideCodeActionsDelegate provideCodeActions, ResolveCodeActionDelegate resolveCodeAction = null)
        {
            ProvideCodeActionsFunc = provideCodeActions;
            ResolveCodeActionFunc = resolveCodeAction;
        }

        public CodeActionProvider(ProvideDelegate provideCodeActions, ResolveDelegate resolveCodeAction = null)
        {
            ProvideMethod = provideCodeActions;
            ResolveMethod = resolveCodeAction;
        }
    }

    public class CodeActionProviderMetadata
    {
        public List<string> ProvidedCodeActionKinds { get; set; }
        public List<CodeActionProviderMetadataDocumentation> Documentation { get; set; }
    }

    public class CodeActionProviderMetadataDocumentation
    {
        public string Kind { get; set; }
        public Command Command { get; set; }
    }

    public enum IndentAction
    {
        None = 0,

        Indent = 1,

        IndentOutdent = 2,

        Outdent = 3
    }

    public class Hover
    {
        public MarkdownString[] Contents { get; set; }

        public EnergyAutomate.BlazorMonaco.Bridge.EditorRange Range { get; set; }

        public bool CanIncreaseVerbosity { get; set; }

        public bool CanDecreaseVerbosity { get; set; }
    }

    public class HoverProvider
    {
        public delegate Task<Hover> ProvideDelegate(string modelUri, Position position, HoverContext context);

        public ProvideDelegate ProvideMethod { get; set; }

#if NET5_0_OR_GREATER

        [DynamicDependency(nameof(ProvideHover))]
#endif
        [JSInvokable]
        public Task<Hover> ProvideHover(string modelUri, Position position, HoverContext context)
            => ProvideMethod?.Invoke(modelUri, position, context)
               ?? Task.FromResult<Hover>(null);

        public HoverProvider(ProvideDelegate provideHover)
        {
            ProvideMethod = provideHover;
        }
    }

    public class HoverContext
    {
        public HoverVerbosityRequest VerbosityRequest { get; set; }
    }

    public class HoverVerbosityRequest
    {
        public float VerbosityDelta { get; set; }

        public Hover PreviousHover { get; set; }
    }

    public enum HoverVerbosityAction
    {
        Increase = 0,

        Decrease = 1
    }

    public enum CompletionItemKind
    {
        Method = 0,
        Function = 1,
        Constructor = 2,
        Field = 3,
        Variable = 4,
        Class = 5,
        Struct = 6,
        Interface = 7,
        Module = 8,
        Property = 9,
        Event = 10,
        Operator = 11,
        Unit = 12,
        Value = 13,
        Constant = 14,
        Enum = 15,
        EnumMember = 16,
        Keyword = 17,
        Text = 18,
        Color = 19,
        File = 20,
        Reference = 21,
        Customcolor = 22,
        Folder = 23,
        TypeParameter = 24,
        User = 25,
        Issue = 26,
        Tool = 27,
        Snippet = 28
    }

    public class CompletionItemLabel
    {
        public string Label { get; set; }
        public string Detail { get; set; }
        public string Description { get; set; }
    }

    public enum CompletionItemTag
    {
        Deprecated = 1
    }

    public enum CompletionItemInsertTextRule
    {
        None = 0,

        KeepWhitespace = 1,

        InsertAsSnippet = 4
    }

    public class CompletionItemRanges
    {
        public global::EnergyAutomate.BlazorMonaco.Bridge.EditorRange Insert { get; set; }
        public global::EnergyAutomate.BlazorMonaco.Bridge.EditorRange Replace { get; set; }
    }

    public class CompletionItem
    {
        public JsonElement? Label { get; set; }
        [JsonIgnore]
        public string LabelAsString
        {
            get => Label?.AsString();
            set => Label = JsonElementExt.FromObject(value);
        }
        [JsonIgnore]
        public CompletionItemLabel LabelAsObject
        {
            get => Label?.AsObject<CompletionItemLabel>();
            set => Label = JsonElementExt.FromObject(value);
        }

        public CompletionItemKind Kind { get; set; }

        public List<CompletionItemTag> Tags { get; set; }

        public string Detail { get; set; }

        public JsonElement? Documentation { get; set; }
        [JsonIgnore]
        public string DocumentationAsString
        {
            get => Documentation?.AsString();
            set => Documentation = JsonElementExt.FromObject(value);
        }
        [JsonIgnore]
        public MarkdownString DocumentationAsObject
        {
            get => Documentation?.AsObject<MarkdownString>();
            set => Documentation = JsonElementExt.FromObject(value);
        }

        public string SortText { get; set; }

        public string FilterText { get; set; }

        public bool? Preselect { get; set; }

        public string InsertText { get; set; }

        public CompletionItemInsertTextRule? InsertTextRules { get; set; }

        public JsonElement? Range { get; set; }
        [JsonIgnore]
        public EnergyAutomate.BlazorMonaco.Bridge.EditorRange RangeAsObject
        {
            get => Range?.AsObject<EnergyAutomate.BlazorMonaco.Bridge.EditorRange>();
            set => Range = JsonElementExt.FromObject(value);
        }
        [JsonIgnore]
        public CompletionItemRanges RangeAsList
        {
            get => Range?.AsObject<CompletionItemRanges>();
            set => Range = JsonElementExt.FromObject(value);
        }

        public List<string> CommitCharacters { get; set; }

        public List<SingleEditOperation> AdditionalTextEdits { get; set; }

        public Command Command { get; set; }

        public Command Action { get; set; }
    }

    public class CompletionList
    {
        public List<CompletionItem> Suggestions { get; set; }
        public bool? Incomplete { get; set; }
    }

    public class PartialAcceptInfo
    {
        public PartialAcceptTriggerKind Kind { get; set; }
        public int AcceptedLength { get; set; }
    }

    public enum PartialAcceptTriggerKind
    {
        Word = 0,
        Line = 1,
        Suggest = 2
    }

    public enum CompletionTriggerKind
    {
        Invoke = 0,
        TriggerCharacter = 1,
        TriggerForIncompleteCompletions = 2
    }

    public class CompletionContext
    {
        public CompletionTriggerKind? TriggerKind { get; set; }

        public string TriggerCharacter { get; set; }
    }

    public class CompletionItemProvider
    {
        public List<string> TriggerCharacters { get; set; }

        [Obsolete("Please use the new async ProvideDelegate instead.")]
        public delegate CompletionList ProvideCompletionItemsDelegate(string modelUri, Position position, CompletionContext context);

        [Obsolete("Please use the new async ProvideMethod instead.")]
        public ProvideCompletionItemsDelegate ProvideCompletionItemsFunc { get; set; }

        public delegate Task<CompletionList> ProvideDelegate(string modelUri, Position position, CompletionContext context);

        public ProvideDelegate ProvideMethod { get; set; }

#if NET5_0_OR_GREATER

        [DynamicDependency(nameof(ProvideCompletionItems))]
#endif
        [JSInvokable]
        public Task<CompletionList> ProvideCompletionItems(string modelUri, Position position, CompletionContext context)
#pragma warning disable CS0618
            => ProvideMethod?.Invoke(modelUri, position, context)
                ?? Task.FromResult(ProvideCompletionItemsFunc?.Invoke(modelUri, position, context));

#pragma warning restore CS0618

        [Obsolete("Please use the new async ResolveDelegate instead.")]
        public delegate CompletionItem ResolveCompletionItemDelegate(CompletionItem completionItem);

        [Obsolete("Please use the new async ResolveMethod instead.")]
        public ResolveCompletionItemDelegate ResolveCompletionItemFunc { get; set; }

        public delegate Task<CompletionItem> ResolveDelegate(CompletionItem completionItem);

        public ResolveDelegate ResolveMethod { get; set; }

#if NET5_0_OR_GREATER

        [DynamicDependency(nameof(ResolveCompletionItem))]
#endif
        [JSInvokable]
        public Task<CompletionItem> ResolveCompletionItem(CompletionItem completionItem)
#pragma warning disable CS0618
            => ResolveMethod?.Invoke(completionItem)
                ?? Task.FromResult(ResolveCompletionItemFunc?.Invoke(completionItem));

#pragma warning restore CS0618

        [Obsolete("Please use the new constructor with async parameters instead.")]
        public CompletionItemProvider(ProvideCompletionItemsDelegate provideCompletionItems, ResolveCompletionItemDelegate resolveCompletionItem = null)
        {
            ProvideCompletionItemsFunc = provideCompletionItems;
            ResolveCompletionItemFunc = resolveCompletionItem;
        }

        public CompletionItemProvider(List<string> triggerCharacters, ProvideDelegate provideCompletionItems, ResolveDelegate resolveCompletionItem = null)
        {
            TriggerCharacters = triggerCharacters;
            ProvideMethod = provideCompletionItems;
            ResolveMethod = resolveCompletionItem;
        }
    }

    public enum InlineCompletionTriggerKind
    {
        Automatic = 0,

        Explicit = 1
    }

    public class InlineCompletionContext
    {
        public InlineCompletionTriggerKind TriggerKind { get; set; }
        public SelectedSuggestionInfo SelectedSuggestionInfo { get; set; }
        public bool IncludeInlineEdits { get; set; }
        public bool IncludeInlineCompletions { get; set; }
        public long RequestIssuedDateTime { get; set; }
        public long EarliestShownDateTime { get; set; }
    }

    public class SelectedSuggestionInfo
    {
        public EnergyAutomate.BlazorMonaco.Bridge.EditorRange Range { get; set; }
        public string Text { get; set; }
        public CompletionItemKind CompletionKind { get; set; }
        public bool IsSnippetText { get; set; }
    }

    public class CodeAction
    {
        public string Title { get; set; }
        public Command Command { get; set; }
        public WorkspaceEdit Edit { get; set; }
        public List<MarkerData> Diagnostics { get; set; }
        public string Kind { get; set; }
        public bool? IsPreferred { get; set; }
        public bool? IsAI { get; set; }
        public string Disabled { get; set; }
        public List<EnergyAutomate.BlazorMonaco.Bridge.EditorRange> Ranges { get; set; }
    }

    public enum CodeActionTriggerType
    {
        Invoke = 1,
        Auto = 2
    }

    public class CodeActionList
    {
        public List<CodeAction> Actions { get; set; }
    }

    public enum SignatureHelpTriggerKind
    {
        Invoke = 1,
        TriggerCharacter = 2,
        ContentChange = 3
    }

    public enum DocumentHighlightKind
    {
        Text = 0,

        Read = 1,

        Write = 2
    }

    public enum SymbolKind
    {
        File = 0,
        Module = 1,
        Namespace = 2,
        Package = 3,
        Class = 4,
        Method = 5,
        Property = 6,
        Field = 7,
        Constructor = 8,
        Enum = 9,
        Interface = 10,
        Function = 11,
        Variable = 12,
        Constant = 13,
        String = 14,
        Number = 15,
        Boolean = 16,
        Array = 17,
        Object = 18,
        Key = 19,
        Null = 20,
        EnumMember = 21,
        Struct = 22,
        Event = 23,
        Operator = 24,
        TypeParameter = 25
    }

    public enum SymbolTag
    {
        Deprecated = 1
    }

    public class TextEdit
    {
        public EnergyAutomate.BlazorMonaco.Bridge.EditorRange Range { get; set; }
        public string Text { get; set; }
        public EndOfLineSequence? Eol { get; set; }
    }

    public class FormattingOptions
    {
        public int TabSize { get; set; }
        public bool InsertSpaces { get; set; }
    }

    public class DocumentFormattingEditProvider
    {
        public string DisplayName { get; set; }

        public delegate Task<TextEdit[]> ProvideDelegate(string modelUri, FormattingOptions options);

        public ProvideDelegate ProvideMethod { get; set; }

#if NET5_0_OR_GREATER

        [DynamicDependency(nameof(ProvideDocumentFormattingEdits))]
#endif
        [JSInvokable]
        public Task<TextEdit[]> ProvideDocumentFormattingEdits(string modelUri, FormattingOptions options)
            => ProvideMethod?.Invoke(modelUri, options)
                ?? Task.FromResult<TextEdit[]>(null);

        public DocumentFormattingEditProvider(string displayName, ProvideDelegate provideDocumentFormattingEditsDelegate)
        {
            DisplayName = displayName;
            ProvideMethod = provideDocumentFormattingEditsDelegate;
        }
    }

    public class WorkspaceEditMetadata
    {
        public bool? NeedsConfirmation { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
    }

    public class WorkspaceFileEditOptions
    {
        public bool? Overwrite { get; set; }
        public bool? IgnoreIfNotExists { get; set; }
        public bool? IgnoreIfExists { get; set; }
        public bool? Recursive { get; set; }
        public bool? Copy { get; set; }
        public bool? Folder { get; set; }
        public bool? SkipTrashBin { get; set; }
        public int? MaxSize { get; set; }
    }

    public class WorkspaceFileEdit : IWorkspaceEdit
    {
        [JsonPropertyName("oldResource")]
        public string OldResourceUri { get; set; }
        [JsonPropertyName("newResource")]
        public string NewResourceUri { get; set; }
        public WorkspaceFileEditOptions Options { get; set; }
        public WorkspaceEditMetadata Metadata { get; set; }
    }

    public class WorkspaceTextEdit : IWorkspaceEdit
    {
        [JsonPropertyName("resource")]
        public string ResourceUri { get; set; }
        public TextEditWithOptions TextEdit { get; set; }
        public int? VersionId { get; set; }
        public WorkspaceEditMetadata Metadata { get; set; }
    }

    public class TextEditWithOptions : TextEdit
    {
        public bool? InsertAsSnippet { get; set; }
        public bool? KeepWhitespace { get; set; }
    }

    public interface IWorkspaceEdit
    {
        WorkspaceEditMetadata Metadata { get; set; }
    }

    public class WorkspaceEdit
    {
        [JsonConverter(typeof(ListJsonConverter<IWorkspaceEdit, WorkspaceEditJsonConverter>))]
        public List<IWorkspaceEdit> Edits { get; set; }
    }

    public class CustomEdit : IWorkspaceEdit
    {
        [JsonPropertyName("resource")]
        public string ResourceUri { get; set; }
        public WorkspaceEditMetadata Metadata { get; set; }
    }

    public enum NewSymbolNameTag
    {
        AIGenerated = 1
    }

    public enum NewSymbolNameTriggerKind
    {
        Invoke = 0,
        Automatic = 1
    }

    public class Command
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Tooltip { get; set; }
        public List<object> Arguments { get; set; }
    }

    public enum InlayHintKind
    {
        Type = 1,
        Parameter = 2
    }

    public enum ModuleKind
    {
        None = 0,
        CommonJS = 1,
        AMD = 2,
        UMD = 3,
        System = 4,
        ES2015 = 5,
        ESNext = 99
    }

    public enum JsxEmit
    {
        None = 0,
        Preserve = 1,
        React = 2,
        ReactNative = 3,
        ReactJSX = 4,
        ReactJSXDev = 5
    }

    public enum NewLineKind
    {
        CarriageReturnLineFeed = 0,
        LineFeed = 1
    }

    public enum ScriptTarget
    {
        ES3 = 0,
        ES5 = 1,
        ES2015 = 2,
        ES2016 = 3,
        ES2017 = 4,
        ES2018 = 5,
        ES2019 = 6,
        ES2020 = 7,
        ESNext = 99,
        JSON = 100,
        Latest = 99
    }

    public enum ModuleResolutionKind
    {
        Classic = 1,
        NodeJs = 2
    }
}