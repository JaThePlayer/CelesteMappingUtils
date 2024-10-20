using System.Collections.Generic;
using ImGuiColorTextEditNet;

namespace FosterTest;

public class CSharpStyleHighlighter : ISyntaxHighlighter
{
    static readonly object DefaultState = new();
    static readonly object MultiLineCommentState = new();
    
    readonly Dictionary<string, Identifier> _identifiers;
    
    record Identifier(PaletteIndex Color)
    {
        public Coordinates Location = Coordinates.Invalid;
        public string Declaration = "";
    }

    private static LanguageDefinition CSharp() => new("CSharp")
    {
        Keywords = [
            "var", "break", "case", "return", "default", "do", "continue", "while", "new", "await",
            "const", "else", "extern", "for", "foreach", "goto", "if", "using", "global",
            "struct", "class", "interface", "enum", "namespace",
            "bool", "ushort", "short", "char", "double", "float", "int", "long", "string", "nint", "nuint", "byte", "sbyte", "decimal", "object", "dynamic",
            "sizeof", "static", "switch", "void", "volatile",
            "public", "internal", "private", "protected", "file",
            "abstract", "virtual", "sealed", "override", "base",
            "ref", "readonly", "stackalloc", "lock",
            "delegate", "unmanaged",
            "where", "allows",
            "true", "false",
            "from", "in", "select",
            "is", "and", "or", "not"
        ],
        Identifiers = []
    };
    
    public CSharpStyleHighlighter()
    {
        var language = CSharp();

        _identifiers = new();
        if (language.Keywords != null)
            foreach (var keyword in language.Keywords)
                _identifiers.Add(keyword, new Identifier(PaletteIndex.Keyword));

        if (language.Identifiers != null)
        {
            foreach (var name in language.Identifiers)
            {
                var identifier = new Identifier(PaletteIndex.KnownIdentifier)
                {
                    Declaration = "Built-in function"
                };
                _identifiers.Add(name, identifier);
            }
        }
    }
    
    public string? GetTooltip(string id)
    {
        if (!_identifiers.TryGetValue(id, out var info))
            return null;
        
        return info?.Declaration;
    }

    public object Colorize(Span<Glyph> line, object? state)
    {
      for (int i = 0; i < line.Length;)
      {
        int result = Tokenize(line[i..], ref state);
        Util.Assert(result != 0);

        if (result == -1)
        {
          line[i] = new Glyph(line[i].Char, PaletteIndex.Default);
          i++;
        }
        else i += result;
      }

      return state ?? DefaultState;
    }
    
    private int Tokenize(Span<Glyph> span, ref object? state)
    {
        int i = 0;

        // Skip leading whitespace
        while (i < span.Length && span[i].Char is ' ' or '\t')
          i++;

        if (i > 0)
          return i;

        int result;
        if ((result = TokenizeMultiLineComment(span, ref state)) != -1) return result;
        if ((result = TokenizeSingleLineComment(span))      != -1) return result;
        if ((result = TokenizePreprocessorDirective(span))  != -1) return result;
        if ((result = TokenizeCStyleString(span, ref state)) != -1) return result;
        if ((result = TokenizeCStyleCharacterLiteral(span)) != -1) return result;
        if ((result = TokenizeCStyleIdentifier(span))       != -1) return result;
        if ((result = TokenizeCStyleNumber(span))           != -1) return result;
        if ((result = TokenizeCStylePunctuation(span))      != -1) return result;
        return -1;
    }

    public bool AutoIndentation => true;

    public int MaxLinesPerFrame => 1000;
    
    int TokenizeMultiLineComment(Span<Glyph> span, ref object? state)
    {
      int i = 0;
      if (state != MultiLineCommentState && (span[i].Char != '/' || 1 >= span.Length || span[1].Char != '*'))
        return -1;

      state = MultiLineCommentState;
      for (; i < span.Length; i++)
      {
        span[i] = new Glyph(span[i].Char, PaletteIndex.MultiLineComment);
        if (span[i].Char == '*' && i + 1 < span.Length && span[i + 1].Char == '/')
        {
          i++;
          span[i] = new Glyph(span[i].Char, PaletteIndex.MultiLineComment);
          state = DefaultState;
          return i;
        }
      }

      return i;
    }

    int TokenizeSingleLineComment(Span<Glyph> span)
    {
      if (span[0].Char != '/' || 1 >= span.Length || span[1].Char != '/')
        return -1;

      for (int i = 0; i < span.Length; i++)
        span[i] = new Glyph(span[i].Char, PaletteIndex.Comment);

      return span.Length;
    }

    int TokenizePreprocessorDirective(Span<Glyph> span)
    {
      if (span[0].Char != '#')
        return -1;

      for (int i = 0; i < span.Length; i++)
        span[i] = new Glyph(span[i].Char, PaletteIndex.Preprocessor);

      return span.Length;
    }
    
    int TokenizeCStyleString(Span<Glyph> input, ref object? state)
    {
        bool interpolated = false;
        var i = 0;
        while (input[i].Char != '"')
        {
            switch (input[i].Char)
            {
                case '$':
                    interpolated = true;
                    break;
                default:
                    return -1; // No opening quotes
            }
            i++;
        }
        i++;

        var tokenized = 0;
        
        for (; i < input.Length; i++)
        {
            var c = input[i].Char;

            if (c == '{' && interpolated)
            {
                // interpolated string gap
                var end = i + 1;
                var balance = 1;
                while (end < input.Length && balance > 0)
                {
                    switch (input[end].Char)
                    {
                        case '{':
                            balance++;
                            break;
                        case '}':
                            balance--;
                            if (balance <= 0)
                                end--;
                            break;
                    }
                    end++;
                }

                if (end == input.Length)
                {
                    continue;
                }
                
                for (; tokenized < i; tokenized++)
                    input[tokenized] = new Glyph(input[tokenized].Char, PaletteIndex.String);
                input[tokenized] = new Glyph(input[tokenized++].Char, PaletteIndex.Keyword);
                
                Colorize(input[(i + 1)..end], state);
                i = end;
                tokenized = i;
                input[tokenized] = new Glyph(input[tokenized++].Char, PaletteIndex.Keyword);
                continue;
            }
            
            // handle end of string
            if (c == '"')
            {
                for (; tokenized <= i; tokenized++)
                    input[tokenized] = new Glyph(input[tokenized].Char, PaletteIndex.String);

                return i + 1;
            }

            // handle escape character for "
            if (c == '\\' && i + 1 < input.Length && input[i + 1].Char == '"')
                i++;
        }

        return -1; // No closing quotes
    }

    static int TokenizeCStyleCharacterLiteral(Span<Glyph> input)
    {
        int i = 0;

        if (input[i++].Char != '\'')
            return -1;

        if (i < input.Length && input[i].Char == '\\')
            i++; // handle escape characters

        i++; // Skip actual char

        // handle end of character literal
        if (i >= input.Length || input[i].Char != '\'')
            return -1;

        for (int j = 0; j < i; j++)
            input[j] = new Glyph(input[j].Char, PaletteIndex.CharLiteral);

        return i;
    }

    int TokenizeCStyleIdentifier(Span<Glyph> input)
    {
        int i = 0;

        var c = input[i].Char;
        if (!char.IsLetter(c) && c != '_')
            return -1;

        i++;

        for (; i < input.Length; i++)
        {
            c = input[i].Char;
            if (c != '_' && !char.IsLetterOrDigit(c))
                break;
        }

        Span<char> id = stackalloc char[i];
        for (int j = 0; j < i; j++)
        {
            id[j] = input[j].Char;
        }
        
        var info = _identifiers.GetValueOrDefault(id.ToString());

        for (int j = 0; j < i; j++)
            input[j] = new Glyph(input[j].Char, info?.Color ?? PaletteIndex.Identifier);

        return i;
    }

    static int TokenizeCStyleNumber(Span<Glyph> input)
    {
        int i = 0;
        char c = input[i].Char;

        bool startsWithNumber = char.IsNumber(c);

        if (c != '+' && c != '-' && !startsWithNumber)
            return -1;

        i++;

        bool hasNumber = startsWithNumber;
        while (i < input.Length && char.IsNumber(input[i].Char))
        {
            hasNumber = true;
            i++;
        }

        if (!hasNumber)
            return -1;

        bool isFloat = false;
        bool isHex = false;
        bool isBinary = false;

        if (i < input.Length)
        {
            if (input[i].Char == '.')
            {
                isFloat = true;

                i++;
                while (i < input.Length && char.IsNumber(input[i].Char))
                    i++;
            }
            else if (input[i].Char is 'x' or 'X' && i == 1 && input[i].Char == '0')
            {
                // hex formatted integer of the type 0xef80
                isHex = true;

                i++;
                for (; i < input.Length; i++)
                {
                    c = input[i].Char;
                    if (!char.IsNumber(c) && c is not (>= 'a' and <= 'f') && c is not (>= 'A' and <= 'F'))
                        break;
                }
            }
            else if (input[i].Char is 'b' or 'B' && i == 1 && input[i].Char == '0')
            {
                // binary formatted integer of the type 0b01011101

                isBinary = true;

                i++;
                for (; i < input.Length; i++)
                {
                    c = input[i].Char;
                    if (c != '0' && c != '1')
                        break;
                }
            }
        }

        if (!isHex && !isBinary)
        {
            // floating point exponent
            if (i < input.Length && input[i].Char is 'e' or 'E')
            {
                isFloat = true;

                i++;

                if (i < input.Length && input[i].Char is '+' or '-')
                    i++;

                bool hasDigits = false;
                while (i < input.Length && input[i].Char is >= '0' and <= '9')
                {
                    hasDigits = true;
                    i++;
                }

                if (!hasDigits)
                    return -1;
            }

            // single precision floating point type
            if (i < input.Length && input[i].Char == 'f')
                i++;
        }

        if (!isFloat)
        {
            // integer size type
            while (i < input.Length && input[i].Char is 'u' or 'U' or 'l' or 'L')
                i++;
        }

        return i;
    }

    static int TokenizeCStylePunctuation(Span<Glyph> input)
    {
        switch (input[0].Char)
        {
            case '[': case ']':
            case '{': case '}':
            case '(': case ')':
            case '-': case '+':
            case '<': case '>':
            case '?': case ':': case ';':
            case '!': case '%': case '^':
            case '&': case '|':
            case '*': case '/':
            case '=':
            case '~':
            case ',': case '.':
                input[0] = new Glyph(input[0].Char, PaletteIndex.Punctuation);
                return 1;
            default:
                return -1;
        }
    }

}