using ICSharpCode.AvalonEdit.Document;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace ColorVision.Solution.Editor.AvalonEditor;

internal sealed record EditorSymbol(string Name, string Kind, int Offset, int Line, string Signature)
{
    public string Label => $"{Name}   ·   {Kind}   ·   {Line}";
}

internal sealed record EditorDiagnostic(int Offset, int Line, int Column, string Message);

internal sealed record EditorCodeModel(string[] Words, EditorSymbol[] Symbols, IReadOnlyDictionary<int, int> Brackets, EditorDiagnostic? Diagnostic)
{
    internal static EditorCodeModel Empty { get; } = new([], [], new Dictionary<int, int>(), null);
}

/// <summary>Collects document-local editing information from the same masked code lines used for folding.</summary>
internal sealed class EditorCodeAnalysis(string? language)
{
    private static readonly Regex Identifier = new(@"[\p{L}_][\p{L}\p{Nd}_]*", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex PythonSymbol = new(@"^\s*(?:async\s+)?(?<kind>def|class)\s+(?<name>[\p{L}_][\p{L}\p{Nd}_]*)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex TypeSymbol = new(@"\b(?<kind>class|struct|interface|enum|record|namespace)\s+(?<name>[\p{L}_][\p{L}\p{Nd}_.]*)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex FunctionSymbol = new(@"^\s*(?:export\s+)?(?:async\s+)?function\s+(?<name>[\p{L}_][\p{L}\p{Nd}_]*)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex MethodSymbol = new(@"^\s*(?:(?:public|private|protected|internal|static|virtual|override|abstract|async|sealed|new|partial|extern)\s+)*(?:[\w<>,\[\]?.]+\s+)+(?<name>[\p{L}_][\p{L}\p{Nd}_]*)\s*(?:<[^>]+>)?\s*\(", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private readonly HashSet<string> _words = new(StringComparer.Ordinal);
    private readonly List<EditorSymbol> _symbols = [];
    private readonly Stack<(char Character, int Offset)> _open = new();
    private readonly Dictionary<int, int> _brackets = [];

    internal void ReadLine(DocumentLine line, string code)
    {
        if (_words.Count < 20_000)
            foreach (Match match in Identifier.Matches(code))
            {
                if (match.Length is > 1 and <= 100) _words.Add(match.Value);
                if (_words.Count >= 20_000) break;
            }
        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];
            if (c is '(' or '[' or '{') _open.Push((c, line.Offset + i));
            else if (c is ')' or ']' or '}')
            {
                char expected = c switch { ')' => '(', ']' => '[', _ => '{' };
                if (_open.TryPeek(out var opening) && opening.Character == expected)
                {
                    _open.Pop();
                    _brackets[opening.Offset] = line.Offset + i;
                    _brackets[line.Offset + i] = opening.Offset;
                }
                else _open.Clear();
            }
        }
        if (_symbols.Count >= 4_000 || code.Length > 2_000) return;
        Match? declaration = language switch
        {
            "Python" => PythonSymbol.Match(code),
            "C#" or "C++" or "Java" => TypeSymbol.Match(code),
            "JavaScript" or "PHP" => FunctionSymbol.Match(code),
            _ => null
        };
        if (declaration is { Success: false } && language is "C#" or "C++" or "Java")
        {
            string firstWord = Identifier.Match(code).Value;
            if (firstWord is not ("return" or "throw" or "await" or "using" or "new" or "yield")) declaration = MethodSymbol.Match(code);
        }
        if (declaration is not { Success: true }) return;
        var name = declaration.Groups["name"];
        string kind = declaration.Groups["kind"].Value;
        _symbols.Add(new EditorSymbol(name.Value, kind is "class" or "struct" or "interface" or "record" or "enum" ? "类型" : kind == "namespace" ? "命名空间" : "函数",
            line.Offset + name.Index, line.LineNumber, code.Trim()[..Math.Min(code.Trim().Length, 160)]));
    }

    internal EditorCodeModel Complete(TextDocument document)
    {
        EditorDiagnostic? diagnostic = null;
        if (language == "Json" && document.TextLength != 0)
        {
            try { JToken.Parse(document.Text); }
            catch (JsonReaderException ex)
            {
                int line = Math.Clamp(ex.LineNumber, 1, document.LineCount);
                var sourceLine = document.GetLineByNumber(line);
                int column = Math.Clamp(ex.LinePosition, 1, sourceLine.Length + 1);
                diagnostic = new EditorDiagnostic(sourceLine.Offset + column - 1, line, column, ex.Message);
            }
        }
        return new EditorCodeModel(_words.Order(StringComparer.OrdinalIgnoreCase).ToArray(), _symbols.ToArray(), _brackets, diagnostic);
    }

    internal static bool IsIdentifier(char c) => char.IsLetterOrDigit(c) || c == '_';

    internal static (int Offset, string Word) WordAt(TextDocument document, int offset)
    {
        int start = offset, end = offset;
        while (start > 0 && IsIdentifier(document.GetCharAt(start - 1))) start--;
        while (end < document.TextLength && IsIdentifier(document.GetCharAt(end))) end++;
        return (start, document.GetText(start, end - start));
    }
}
