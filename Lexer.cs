// -----------------------------------------------------------------------------
// Re:Mind Language Lexer
// -----------------------------------------------------------------------------
// このファイルは Re:Mind 言語処理系の Phase 1（Lexer）に対応する実装です。
// ReMindSourceCode をトークン列へ変換する役割を持ち、
// 後続フェーズ（Parser → AST → IR → CodeGen）の基盤となります。
// 
// 主な構成:
//   - TokenType : Re:Mind の構文要素を表すトークン種別
//   - Token     : トークン本体（種別・文字列・行番号・列番号）
//   - Lexer     : 入力文字列を走査し、Token の列へ変換する字句解析器
//
// 特徴:
//   - 日本語識別子（例: コンソール, 一行表示する）をサポート
//   - Re:Mind 特有記号（□, ◇, 〇）を専用トークンとして扱う
//   - コメント（//, /* */）のスキップ処理
//   - 行番号・列番号を保持し、構文解析時のエラー報告に利用可能
//
// Phase 1 完了条件:
//   - ReMindSourceCode を Tokenize() に渡すと、正しいトークン列が得られる
//   - □コンソール.一行表示する(引数2) が適切なトークン列に分解される
//
// この Lexer は Phase 2（Parser）の入力として使用されます。
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace ReMindParser
{
    public enum TokenType
{
    Identifier,
    Number,
    String,
    DocumentComment,
    Keyword,
    Symbol,
    Operator,
    DeclarationStart, // ▽
    DeclarationEnd,   // △
    DeclarationBullet, // ・
    ImportStart,      // ■
    AliasStart,       // ▼
    AliasEnd,         // ▲
    Box,        // □
    Diamond,    // ◇
    Circle,     // 〇
    Comma,
    Dot,
    LParen,
    RParen,
    LBrace,
    RBrace,
    LBracket,
    RBracket,
    Assign,     // =
    Plus,
    Minus,
    Asterisk,
    Slash,
    Percent,
    Eof
}
public class Token
{
    public TokenType Type { get; }
    public string Text { get; }
    public int Line { get; }
    public int Column { get; }

    public Token(TokenType type, string text, int line, int column)
    {
        Type = type;
        Text = text;
        Line = line;
        Column = column;
    }
}

public class Lexer
{
    private readonly string _source;
    private int _index;
    private int _line = 1;
    private int _column = 1;

    public Lexer(string source)
    {
        _source = source;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (!IsEnd())
        {
            var token = NextToken();
            tokens.Add(token);
        }

        tokens.Add(new Token(TokenType.Eof, "", _line, _column));
        return tokens;
    }

    public Token NextToken()
    {
        while (!IsEnd())
        {
            var ch = Peek();

            if (ch == '/' && _index + 1 < _source.Length && _source[_index + 1] == '/')
            {
                _index += 2;
                _column += 2;
                while (!IsEnd() && Peek() != '\n')
                {
                    Advance();
                }
                continue;
            }

            if (ch == '/' && _index + 1 < _source.Length && _source[_index + 1] == '*')
            {
                var isDocumentComment = _index + 2 < _source.Length && _source[_index + 2] == '*';
                var startLine = _line;
                var startColumn = _column;
                _index += 2;
                _column += 2;
                var commentText = string.Empty;
                while (!IsEnd())
                {
                    if (Peek() == '*' && _index + 1 < _source.Length && _source[_index + 1] == '/')
                    {
                        _index += 2;
                        _column += 2;
                        break;
                    }

                    if (isDocumentComment)
                    {
                        commentText += Peek();
                    }
                    Advance();
                }

                if (isDocumentComment)
                {
                    return new Token(TokenType.DocumentComment, commentText, startLine, startColumn);
                }
                continue;
            }

            if (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n' || ch == '　')
            {
                Advance();
                continue;
            }

            break;
        }

        if (IsEnd())
        {
            return new Token(TokenType.Eof, "", _line, _column);
        }

        var currentChar = Peek();
        if (currentChar == '▽')
        {
            Advance();
            return new Token(TokenType.DeclarationStart, "▽", _line, _column);
        }

        if (currentChar == '△')
        {
            Advance();
            return new Token(TokenType.DeclarationEnd, "△", _line, _column);
        }

        if (currentChar == '・')
        {
            Advance();
            return new Token(TokenType.DeclarationBullet, "・", _line, _column);
        }

        if (currentChar == '■')
        {
            Advance();
            return new Token(TokenType.ImportStart, "■", _line, _column);
        }

        if (currentChar == '▼')
        {
            Advance();
            return new Token(TokenType.AliasStart, "▼", _line, _column);
        }

        if (currentChar == '▲')
        {
            Advance();
            return new Token(TokenType.AliasEnd, "▲", _line, _column);
        }

        if (currentChar == '□')
        {
            Advance();
            return new Token(TokenType.Box, "□", _line, _column);
        }

        if (currentChar == '◇')
        {
            Advance();
            return new Token(TokenType.Diamond, "◇", _line, _column);
        }

        if (currentChar == '〇')
        {
            Advance();
            return new Token(TokenType.Circle, "〇", _line, _column);
        }

        if (currentChar == '(')
        {
            Advance();
            return new Token(TokenType.LParen, "(", _line, _column);
        }

        if (currentChar == ')')
        {
            Advance();
            return new Token(TokenType.RParen, ")", _line, _column);
        }

        if (currentChar == '{')
        {
            Advance();
            return new Token(TokenType.LBrace, "{", _line, _column);
        }

        if (currentChar == '}')
        {
            Advance();
            return new Token(TokenType.RBrace, "}", _line, _column);
        }

        if (currentChar == '[')
        {
            Advance();
            return new Token(TokenType.LBracket, "[", _line, _column);
        }

        if (currentChar == ']')
        {
            Advance();
            return new Token(TokenType.RBracket, "]", _line, _column);
        }

        if (currentChar == '.')
        {
            Advance();
            return new Token(TokenType.Dot, ".", _line, _column);
        }

        if (currentChar == ',')
        {
            Advance();
            return new Token(TokenType.Comma, ",", _line, _column);
        }

        if (currentChar == '=')
        {
            var startLine = _line;
            var startColumn = _column;
            Advance();
            if (!IsEnd() && Peek() == '=')
            {
                Advance();
                return new Token(TokenType.Operator, "==", startLine, startColumn);
            }
            return new Token(TokenType.Assign, "=", startLine, startColumn);
        }

        if (currentChar == '!')
        {
            var startLine = _line;
            var startColumn = _column;
            Advance();
            if (!IsEnd() && Peek() == '=')
            {
                Advance();
                return new Token(TokenType.Operator, "!=", startLine, startColumn);
            }
            return new Token(TokenType.Operator, "!", startLine, startColumn);
        }

        if (currentChar == '&' || currentChar == '|')
        {
            var startLine = _line;
            var startColumn = _column;
            var expected = currentChar;
            Advance();
            if (!IsEnd() && Peek() == expected)
            {
                Advance();
                return new Token(TokenType.Operator, expected == '&' ? "&&" : "||", startLine, startColumn);
            }

            throw new InvalidOperationException($"Expected '{expected}{expected}' at line {startLine}, column {startColumn}.");
        }

        if (currentChar == '+')
        {
            Advance();
            return new Token(TokenType.Plus, "+", _line, _column);
        }

        if (currentChar == '-')
        {
            Advance();
            return new Token(TokenType.Minus, "-", _line, _column);
        }

        if (currentChar == '*')
        {
            Advance();
            return new Token(TokenType.Asterisk, "*", _line, _column);
        }

        if (currentChar == '/')
        {
            Advance();
            return new Token(TokenType.Slash, "/", _line, _column);
        }

        if (currentChar == '%')
        {
            Advance();
            return new Token(TokenType.Percent, "%", _line, _column);
        }

        if (currentChar == '<' || currentChar == '>')
        {
            var startLine = _line;
            var startColumn = _column;
            Advance();
            if (!IsEnd() && Peek() == '=')
            {
                Advance();
                return new Token(TokenType.Operator, $"{currentChar}=", startLine, startColumn);
            }
            return new Token(TokenType.Operator, currentChar.ToString(), startLine, startColumn);
        }

        if (currentChar == ':' || currentChar == '?' || currentChar == ';')
        {
            Advance();
            return new Token(TokenType.Symbol, currentChar.ToString(), _line, _column);
        }

        if (char.IsDigit(currentChar))
        {
            var startLine = _line;
            var startColumn = _column;
            var numberText = string.Empty;

            while (!IsEnd() && char.IsDigit(Peek()))
            {
                numberText += Peek();
                Advance();
            }

            return new Token(TokenType.Number, numberText, startLine, startColumn);
        }

        if (currentChar == '"')
        {
            var startLine = _line;
            var startColumn = _column;
            var stringText = string.Empty;

            Advance();
            while (!IsEnd() && Peek() != '"')
            {
                stringText += Peek();
                Advance();
            }

            if (!IsEnd())
            {
                Advance();
            }

            return new Token(TokenType.String, stringText, startLine, startColumn);
        }

        if (char.IsLetter(currentChar))
        {
            var startLine = _line;
            var startColumn = _column;
            var identifierText = string.Empty;

            while (!IsEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
            {
                identifierText += Peek();
                Advance();
            }

            return new Token(TokenType.Identifier, identifierText, startLine, startColumn);
        }

        throw new InvalidOperationException($"Unexpected character '{currentChar}' at line {_line}, column {_column}.");
    }

    private bool IsEnd()
    {
        return _index >= _source.Length;
    }

    private char Peek()
    {
        return _source[_index];
    }

    private void Advance()
    {
        if (IsEnd())
        {
            return;
        }

        if (_source[_index] == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }

        _index++;
    }
}

}
