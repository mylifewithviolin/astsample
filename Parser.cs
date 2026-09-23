using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ReMindAst;

namespace ReMindParser
{
    public class Parser
    {
        private readonly List<Token> _tokens;
        private int _index;

        public Parser(List<Token> tokens)
        {
            _tokens = tokens ?? new List<Token>();
            _index = 0;
        }

        private string ParseIdentifier()
        {
            if (_index >= _tokens.Count)
            {
                throw new InvalidOperationException("Unexpected end of input while parsing identifier.");
            }

            var current = _tokens[_index];

            if (current.Type != TokenType.Identifier)
            {
                throw new InvalidOperationException($"Expected identifier, got '{current.Text}'.");
            }

            _index++;
            return current.Text;
        }

        private string ParsePrimary()
        {
            if (_index >= _tokens.Count)
            {
                throw new InvalidOperationException("Unexpected end of input while parsing primary expression.");
            }

            var current = _tokens[_index];

            if (current.Type == TokenType.Identifier)
            {
                return ParseIdentifier();
            }

            if (current.Type == TokenType.Number)
            {
                _index++;
                return current.Text;
            }

            if (current.Type == TokenType.String)
            {
                _index++;
                return current.Text;
            }

            throw new InvalidOperationException($"Expected primary expression, got '{current.Text}'.");
        }

        private Expression ParsePostfixExpression()
        {
            Expression expression;
            if (_tokens[_index].Type == TokenType.LParen)
            {
                _index++;
                expression = ParseExpression();
                if (_index >= _tokens.Count || _tokens[_index].Type != TokenType.RParen)
                {
                    throw new InvalidOperationException("Expected ')' after grouped expression.");
                }
                _index++;
            }
            else if (_tokens[_index].Type == TokenType.Minus && _index + 1 < _tokens.Count && _tokens[_index + 1].Type == TokenType.Number)
            {
                _index++;
                expression = new LiteralExpression { Value = -int.Parse(_tokens[_index++].Text) };
            }
            else if (_tokens[_index].Type == TokenType.Minus)
            {
                _index++;
                expression = new UnaryExpression { Operator = "-", Operand = ParsePostfixExpression() };
            }
                else if (_tokens[_index].Type == TokenType.Operator && _tokens[_index].Text == "!")
                {
                    _index++;
                    expression = new UnaryExpression { Operator = "!", Operand = ParsePostfixExpression() };
                }
            else if (_tokens[_index].Type == TokenType.String)
            {
                expression = new LiteralExpression { Value = _tokens[_index++].Text };
            }
            else if (_tokens[_index].Type == TokenType.Number)
            {
                expression = new LiteralExpression { Value = int.Parse(_tokens[_index++].Text) };
            }
            else
            {
                var nameJa = ParsePrimary();
                    expression = nameJa switch
                    {
                        "true" => new LiteralExpression { Value = true },
                        "false" => new LiteralExpression { Value = false },
                        _ => new IdentifierExpression { NameJa = nameJa, NameEn = ResolveTargetName(nameJa) }
                    };
            }

            while (_index < _tokens.Count)
            {
                if (_tokens[_index].Type == TokenType.Dot)
                {
                    _index++;
                    expression = new MemberAccessExpression { Expression = expression, MemberName = ResolveTargetName(ParseIdentifier()) };
                }
                else if (_tokens[_index].Type == TokenType.LBracket)
                {
                    _index++;
                    var index = ParseExpression();
                    if (_index >= _tokens.Count || _tokens[_index].Type != TokenType.RBracket)
                    {
                        throw new InvalidOperationException("Expected ']' after array index.");
                    }
                    _index++;
                    expression = new ElementAccessExpression { ArrayExpression = expression, IndexExpression = index };
                }
                else if (_tokens[_index].Type == TokenType.LParen)
                {
                    _index++;
                    var invocation = new InvocationExpression { Target = expression };
                    while (_index < _tokens.Count && _tokens[_index].Type != TokenType.RParen)
                    {
                        invocation.Arguments.Add(ParseExpression());
                        if (_index < _tokens.Count && _tokens[_index].Type == TokenType.Comma)
                        {
                            _index++;
                        }
                    }
                    if (_index >= _tokens.Count || _tokens[_index].Type != TokenType.RParen)
                    {
                        throw new InvalidOperationException("Unterminated invocation.");
                    }
                    _index++;
                    expression = invocation;
                }
                else if (_tokens[_index].Type == TokenType.Plus && _index + 1 < _tokens.Count && _tokens[_index + 1].Type == TokenType.Plus)
                {
                    _index += 2;
                    expression = new UnaryExpression { Operator = "++", Operand = expression };
                }
                else
                {
                    break;
                }
            }
            return expression;
        }

        private Expression ParseExpression(int minimumPrecedence = 0)
        {
            var left = ParsePostfixExpression();
            while (_index < _tokens.Count && TryGetBinaryPrecedence(_tokens[_index], out var precedence) && precedence >= minimumPrecedence)
            {
                var operation = _tokens[_index++].Text;
                left = new BinaryExpression { Left = left, Operator = operation, Right = ParseExpression(precedence + 1) };
            }
            return left;
        }

        private static bool TryGetBinaryPrecedence(Token token, out int precedence)
        {
            precedence = token.Type switch
            {
                    TokenType.Operator when token.Text == "||" => 1,
                    TokenType.Operator when token.Text == "&&" => 2,
                    TokenType.Operator => 3,
                    TokenType.Plus or TokenType.Minus => 4,
                    TokenType.Asterisk or TokenType.Slash or TokenType.Percent => 5,
                _ => 0
            };
            return precedence > 0;
        }

        private Statement ParseStatement()
        {
            if (_index >= _tokens.Count)
            {
                throw new InvalidOperationException("Unexpected end of input while parsing statement.");
            }

            if (_tokens[_index].Type == TokenType.Box)
            {
                _index++;
                var expr = ParseExpression();

                if (TryConsumeReturnMarker(expr))
                {
                    return new ReturnStatement { Value = expr };
                }

                if (_index < _tokens.Count && _tokens[_index].Type == TokenType.Assign)
                {
                    _index++;
                    return new AssignmentStatement
                    {
                        Left = expr,
                        Right = ParseExpression()
                    };
                }

                return new ExpressionStatement
                {
                    Expression = expr
                };
            }

            if (_tokens[_index].Type == TokenType.DeclarationBullet)
            {
                return ParseLocalVariable(null);
            }

            if (_tokens[_index].Type == TokenType.Diamond)
            {
                _index++;
                var condition = ParseExpression();

                if (_index >= _tokens.Count || _tokens[_index].Text != "の場合")
                {
                    throw new InvalidOperationException("Expected 'の場合' after if condition.");
                }

                _index++;

                var thenBody = new List<Statement>();
                JavadocComment? pendingDocumentation = null;
                while (_index < _tokens.Count && !IsBranchTerminator())
                {
                    pendingDocumentation ??= TakeDocumentation();
                    if (pendingDocumentation == null)
                    {
                        thenBody.Add(ParseStatement());
                    }
                    else if (_index < _tokens.Count && _tokens[_index].Type == TokenType.DeclarationBullet)
                    {
                        thenBody.Add(ParseLocalVariable(pendingDocumentation));
                        pendingDocumentation = null;
                    }
                }

                var elseBody = new List<Statement>();
                if (_index + 1 < _tokens.Count && _tokens[_index].Type == TokenType.Diamond && _tokens[_index + 1].Text == "他に")
                {
                    _index += 2;
                    pendingDocumentation = null;
                    while (_index < _tokens.Count && !IsBranchEnd())
                    {
                        pendingDocumentation ??= TakeDocumentation();
                        if (pendingDocumentation == null)
                        {
                            elseBody.Add(ParseStatement());
                        }
                        else if (_index < _tokens.Count && _tokens[_index].Type == TokenType.DeclarationBullet)
                        {
                            elseBody.Add(ParseLocalVariable(pendingDocumentation));
                            pendingDocumentation = null;
                        }
                    }
                }

                if (IsBranchEnd())
                {
                    _index += 2;
                }

                var ifStatement = new IfStatement
                {
                    Condition = condition
                };
                ifStatement.ThenBody.AddRange(thenBody);
                ifStatement.ElseBody.AddRange(elseBody);
                return ifStatement;
            }

            if (_tokens[_index].Type == TokenType.Circle)
            {
                _index++;
                if (_index + 2 < _tokens.Count && _tokens[_index].Type == TokenType.Identifier && _tokens[_index + 1].Type == TokenType.Identifier && _tokens[_index + 2].Type == TokenType.Assign)
                {
                    var type = TakeText("for initializer type");
                    var nameJa = TakeText("for initializer name");
                    _index++;
                    var initializer = new LocalVariableDeclaration { Type = type, NameJa = nameJa, NameEn = ResolveTargetName(nameJa), Initializer = ParseExpression() };
                    if (_index >= _tokens.Count || _tokens[_index++].Type != TokenType.Comma)
                    {
                        throw new InvalidOperationException("Expected ',' after for initializer.");
                    }
                    var forStatement = new ForStatement { Initializer = initializer, Condition = ParseExpression() };
                    if (_index >= _tokens.Count || _tokens[_index++].Type != TokenType.Comma)
                    {
                        throw new InvalidOperationException("Expected ',' after for condition.");
                    }
                    forStatement.Iterator = ParseExpression();
                    if (MatchText("繰り返す")) _index++;
                    ParseLoopBody(forStatement.Body);
                    return forStatement;
                }

                var whileStatement = new WhileStatement { Condition = ParseExpression() };
                if (_index < _tokens.Count && _tokens[_index].Text.StartsWith("の間は", StringComparison.Ordinal)) _index++;
                else if (MatchText("繰り返す")) _index++;
                ParseLoopBody(whileStatement.Body);
                return whileStatement;
            }

            if (_tokens[_index].Type == TokenType.LBrace)
            {
                _index++;
                var statements = new List<Statement>();

                while (_index < _tokens.Count && _tokens[_index].Type != TokenType.RBrace)
                {
                    statements.Add(ParseStatement());
                }

                if (_index >= _tokens.Count)
                {
                    throw new InvalidOperationException("Unterminated block.");
                }

                _index++; // consume }
                var blockStatement = new BlockStatement();
                blockStatement.Statements.AddRange(statements);
                return blockStatement;
            }

            throw new InvalidOperationException($"Unsupported statement start: '{_tokens[_index].Text}'.");
        }

        private LocalVariableDeclaration ParseLocalVariable(JavadocComment? documentation)
        {
            _index++;
            var type = ParseTypeName();
            var nameJa = TakeText("local variable name");
            Expression? initializer = null;
            if (_index < _tokens.Count && _tokens[_index].Type == TokenType.Assign)
            {
                _index++;
                if (type.EndsWith("[]", StringComparison.Ordinal))
                {
                    var array = new ArrayLiteralExpression { ElementType = type[..^2] };
                    var declarationLine = _tokens[_index].Line;
                    do
                    {
                        array.Elements.Add(ParseExpression());
                        if (_index >= _tokens.Count || _tokens[_index].Type != TokenType.Comma)
                        {
                            break;
                        }
                        _index++;
                    } while (_index < _tokens.Count && _tokens[_index].Line == declarationLine);
                    initializer = array;
                }
                else
                {
                    initializer = ParseExpression();
                }
            }
            return new LocalVariableDeclaration { Javadoc = documentation, Type = type, NameJa = nameJa, NameEn = ResolveTargetName(nameJa), Initializer = initializer };
        }

        // 「値を　返す」構文を検出する。「を」が直前の識別子に結合している場合と、独立トークンの場合の両方に対応する。
        private bool TryConsumeReturnMarker(Expression expr)
        {
            if (expr is IdentifierExpression identifierExpr &&
                identifierExpr.NameJa.Length > 1 &&
                identifierExpr.NameJa.EndsWith("を", StringComparison.Ordinal) &&
                _index < _tokens.Count && _tokens[_index].Text == "返す")
            {
                var trimmed = identifierExpr.NameJa[..^1];
                identifierExpr.NameJa = trimmed;
                identifierExpr.NameEn = ResolveTargetName(trimmed);
                _index++;
                return true;
            }

            if (_index + 1 < _tokens.Count && _tokens[_index].Text == "を" && _tokens[_index + 1].Text == "返す")
            {
                _index += 2;
                return true;
            }

            return false;
        }

        private void ParseLoopBody(List<Statement> body)
        {
            JavadocComment? pendingDocumentation = null;
            while (_index < _tokens.Count && !IsLoopEnd())
            {
                pendingDocumentation ??= TakeDocumentation();
                if (pendingDocumentation == null)
                {
                    body.Add(ParseStatement());
                }
                else if (_index < _tokens.Count && _tokens[_index].Type == TokenType.DeclarationBullet)
                {
                    body.Add(ParseLocalVariable(pendingDocumentation));
                    pendingDocumentation = null;
                }
            }
            if (IsLoopEnd()) _index += 2;
        }

        private bool IsLoopEnd() => _index + 1 < _tokens.Count && _tokens[_index].Type == TokenType.Circle && _tokens[_index + 1].Text == "ここまで";
        private bool IsBranchEnd() => _index + 1 < _tokens.Count && _tokens[_index].Type == TokenType.Diamond && _tokens[_index + 1].Text == "ここまで";
        private bool IsBranchTerminator() => IsBranchEnd() || (_index + 1 < _tokens.Count && _tokens[_index].Type == TokenType.Diamond && _tokens[_index + 1].Text == "他に");

        private bool MatchText(string text)
        {
            return _index < _tokens.Count && _tokens[_index].Text == text;
        }

        private string TakeText(string description)
        {
            if (_index >= _tokens.Count)
            {
                throw new InvalidOperationException($"Unexpected end of input while parsing {description}.");
            }

            return _tokens[_index++].Text;
        }

        private JavadocComment ParseDocumentation(string text)
        {
            var documentation = new JavadocComment();
            foreach (var rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim().TrimStart('*').Trim();
                if (line.StartsWith("@param", StringComparison.Ordinal))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        documentation.Params.Add(new JavadocParam
                        {
                            NameJa = parts[1],
                            NameEn = parts[2]
                        });
                    }
                }
                else if (!string.IsNullOrEmpty(line))
                {
                    documentation.Summary = line;
                }
            }

            return documentation;
        }

        private JavadocComment? TakeDocumentation()
        {
            if (_index < _tokens.Count && _tokens[_index].Type == TokenType.DocumentComment)
            {
                return ParseDocumentation(_tokens[_index++].Text);
            }

            return null;
        }

        private string ParseTypeName()
        {
            var typeName = TakeText("type");
            if (_index + 1 < _tokens.Count &&
                _tokens[_index].Type == TokenType.LBracket &&
                _tokens[_index + 1].Type == TokenType.RBracket)
            {
                _index += 2;
                typeName += "[]";
            }

            if (MatchText("?"))
            {
                typeName += "?";
                _index++;
            }

            return typeName;
        }

        private static readonly Dictionary<string, string> AliasTargetNames = new(StringComparer.Ordinal)
        {
            ["コンソール"] = "Console",
            ["一行表示する"] = "WriteLine",
        };

        private readonly Dictionary<string, string> _javadocTargetNames = new(StringComparer.Ordinal);

        // ソース上のJavadoc（要約行・@param）から収集した対応表を優先し、無ければ日本語名をそのまま使う（暫定対応、正式仕様は仕様書.mdに追記予定）。
        private string ResolveTargetName(string name)
        {
            if (AliasTargetNames.TryGetValue(name, out var alias))
            {
                return alias;
            }
            return _javadocTargetNames.TryGetValue(name, out var resolved) ? resolved : name;
        }

        private void BuildJavadocTargetNameTable()
        {
            string? pendingSummary = null;
            List<JavadocParam>? pendingParams = null;

            for (var i = 0; i < _tokens.Count; i++)
            {
                var token = _tokens[i];

                if (token.Type == TokenType.DocumentComment)
                {
                    var documentation = ParseDocumentation(token.Text);
                    pendingSummary = documentation.Summary;
                    pendingParams = documentation.Params;
                    continue;
                }

                if (token.Type == TokenType.DeclarationStart)
                {
                    var declaredName = FindDeclarationName(i);
                    if (declaredName != null && !string.IsNullOrEmpty(pendingSummary))
                    {
                        _javadocTargetNames[declaredName] = pendingSummary!;
                    }
                    if (pendingParams != null)
                    {
                        foreach (var parameter in pendingParams)
                        {
                            _javadocTargetNames[parameter.NameJa] = parameter.NameEn;
                        }
                    }
                    pendingSummary = null;
                    pendingParams = null;
                    continue;
                }

                if (token.Type == TokenType.DeclarationBullet)
                {
                    var localName = FindLocalVariableName(i);
                    if (localName != null && !string.IsNullOrEmpty(pendingSummary))
                    {
                        _javadocTargetNames[localName] = pendingSummary!;
                    }
                    pendingSummary = null;
                    pendingParams = null;
                }
            }
        }

        private string? FindDeclarationName(int declarationStartIndex)
        {
            // ▽public クラス プログラム型 / ▽static void メイン(...)
            var i = declarationStartIndex + 1;
            string? lastIdentifier = null;
            while (i < _tokens.Count &&
                   _tokens[i].Type != TokenType.LParen &&
                   _tokens[i].Type != TokenType.DeclarationEnd &&
                   _tokens[i].Type != TokenType.DeclarationStart)
            {
                if (_tokens[i].Type == TokenType.Identifier)
                {
                    if (_tokens[i].Text == "クラス")
                    {
                        return i + 1 < _tokens.Count ? _tokens[i + 1].Text : null;
                    }
                    lastIdentifier = _tokens[i].Text;
                }
                i++;
            }
            return lastIdentifier;
        }

        private static readonly HashSet<string> FieldModifierKeywords = new(StringComparer.Ordinal)
        {
            "public", "private", "protected", "internal", "static"
        };

        private string? FindLocalVariableName(int bulletIndex)
        {
            // ・int[] 配列 = ... / ・int 探す値 = ... / ・private static string? 挨拶１
            var i = bulletIndex + 1;
            while (i < _tokens.Count && _tokens[i].Type == TokenType.Identifier && FieldModifierKeywords.Contains(_tokens[i].Text))
            {
                i++;
            }
            if (i >= _tokens.Count || _tokens[i].Type != TokenType.Identifier)
            {
                return null;
            }
            i++;
            if (i + 1 < _tokens.Count && _tokens[i].Type == TokenType.LBracket && _tokens[i + 1].Type == TokenType.RBracket)
            {
                i += 2;
            }
            if (i < _tokens.Count && _tokens[i].Type == TokenType.Symbol && _tokens[i].Text == "?")
            {
                i++;
            }
            return i < _tokens.Count && _tokens[i].Type == TokenType.Identifier ? _tokens[i].Text : null;
        }

        private Parameter ParseTokenParameter()
        {
            var type = ParseTypeName();
            var nameJa = TakeText("parameter name");
            return new Parameter
            {
                Type = type,
                NameJa = nameJa,
                NameEn = ResolveTargetName(nameJa)
            };
        }

        private MethodDeclaration ParseTokenMethod(JavadocComment? documentation = null)
        {
            var methodModifier = TakeText("method modifier");
            var returnType = ParseTypeName();
            var nameJa = TakeText("method name");
            if (_index >= _tokens.Count || _tokens[_index].Type != TokenType.LParen)
            {
                throw new InvalidOperationException("Expected '(' after method name.");
            }

            _index++;
            var method = new MethodDeclaration
            {
                NameJa = nameJa,
                NameEn = ResolveTargetName(nameJa),
                ReturnType = returnType,
                Javadoc = documentation
            };
            method.Modifiers.Add(methodModifier);

            while (_index < _tokens.Count && _tokens[_index].Type != TokenType.RParen)
            {
                method.Parameters.Add(ParseTokenParameter());
                if (_index < _tokens.Count && _tokens[_index].Type == TokenType.Comma)
                {
                    _index++;
                }
            }

            if (_index >= _tokens.Count || _tokens[_index].Type != TokenType.RParen)
            {
                throw new InvalidOperationException("Expected ')' after method parameters.");
            }

            _index++;
            while (_index < _tokens.Count && _tokens[_index].Type != TokenType.DeclarationEnd)
            {
                var pendingDocumentation = TakeDocumentation();
                if (pendingDocumentation != null)
                {
                    if (_index < _tokens.Count && _tokens[_index].Type == TokenType.DeclarationBullet)
                    {
                        method.Body.Add(ParseLocalVariable(pendingDocumentation));
                    }
                    continue;
                }
                method.Body.Add(ParseStatement());
            }

            if (_index < _tokens.Count)
            {
                _index++;
            }

            return method;
        }

        private ClassDeclaration ParseTokenClass(JavadocComment? documentation = null)
        {
            TakeText("class modifier");
            TakeText("class keyword");
            var nameJa = TakeText("class name");
            var cls = new ClassDeclaration
            {
                NameJa = nameJa,
                NameEn = ResolveTargetName(nameJa),
                Javadoc = documentation
            };
            cls.Modifiers.Add("public");

            while (_index < _tokens.Count && _tokens[_index].Type != TokenType.DeclarationEnd)
            {
                var pendingDocumentation = TakeDocumentation();
                if (_tokens[_index].Type == TokenType.DeclarationStart)
                {
                    _index++;
                    cls.Methods.Add(ParseTokenMethod(pendingDocumentation));
                }
                else if (_tokens[_index].Type == TokenType.DeclarationBullet)
                {
                    var fieldLine = _tokens[_index].Line;
                    _index++;
                    var fieldTokens = new List<string>();
                    while (_index < _tokens.Count && _tokens[_index].Line == fieldLine)
                    {
                        fieldTokens.Add(_tokens[_index++].Text);
                    }

                    if ((fieldTokens.Count >= 4 && fieldTokens[0].StartsWith("定数", StringComparison.Ordinal)) ||
                        (fieldTokens.Count >= 5 && fieldTokens[0] == "定数"))
                    {
                        var constantType = fieldTokens[0] == "定数" ? fieldTokens[1] : fieldTokens[0][2..];
                        var constantNameIndex = fieldTokens[0] == "定数" ? 2 : 1;
                        var constantValueIndex = constantNameIndex + 2;
                        cls.Fields.Add(new FieldDeclaration
                        {
                            Type = constantType,
                            NameJa = fieldTokens[constantNameIndex],
                            NameEn = ResolveTargetName(fieldTokens[constantNameIndex]),
                            Initializer = ParseSimpleLiteral(fieldTokens[constantValueIndex]),
                            Javadoc = pendingDocumentation,
                            Modifiers = { "const" }
                        });
                    }
                    else if (fieldTokens.Count >= 4)
                    {
                        var fieldName = fieldTokens[^1];
                        var fieldType = string.Concat(fieldTokens.GetRange(2, fieldTokens.Count - 3));
                        cls.Fields.Add(new FieldDeclaration
                        {
                            Type = fieldType,
                            NameJa = fieldName,
                            NameEn = ResolveTargetName(fieldName),
                            Javadoc = pendingDocumentation,
                            Modifiers = { fieldTokens[0], fieldTokens[1] }
                        });
                    }
                }
                else
                {
                    _index++;
                }
            }

            if (_index < _tokens.Count)
            {
                _index++;
            }

            return cls;
        }

        private static Expression ParseSimpleLiteral(string value)
        {
            if (value == "true" || value == "false")
            {
                return new LiteralExpression { Value = value == "true" };
            }

            if (int.TryParse(value, out var integerValue))
            {
                return new LiteralExpression { Value = integerValue };
            }

            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                return new LiteralExpression { Value = value[1..^1] };
            }

            return new IdentifierExpression { NameJa = value, NameEn = value };
        }

        private ImportDeclaration BuildSystemImport()
        {
            var isJavaImport = _tokens.Any(token =>
                token.Text == "java.lang.System" ||
                token.Text == "PrintStream" ||
                token.Text == "println");
            var import = new ImportDeclaration
            {
                Name = isJavaImport ? "java.lang.System" : "System"
            };
            var aliasClass = new AliasClass
            {
                OriginalName = "コンソール",
                TranspiledName = isJavaImport ? "System.out" : "Console"
            };
            aliasClass.Members.Add(new AliasMember
            {
                OriginalName = "一行表示する",
                TranspiledName = isJavaImport ? "println" : "WriteLine"
            });
            import.AliasClasses.Add(aliasClass);
            return import;
        }

        private CompilationUnit ParseTokenCompilationUnit()
        {
            var compilationUnit = new CompilationUnit();
            while (_index < _tokens.Count &&
                   _tokens[_index].Type != TokenType.DeclarationStart &&
                   _tokens[_index].Type != TokenType.DocumentComment)
            {
                _index++;
            }

            if (_index >= _tokens.Count)
            {
                return compilationUnit;
            }

            _index++;
            TakeText("namespace keyword");
            var ns = new NamespaceDeclaration
            {
                Name = TakeText("namespace name")
            };

            while (_index < _tokens.Count &&
                   _tokens[_index].Type != TokenType.DeclarationStart &&
                   _tokens[_index].Type != TokenType.DocumentComment)
            {
                _index++;
            }

            var classDocumentation = TakeDocumentation();
            if (_index < _tokens.Count && _tokens[_index].Type == TokenType.DeclarationStart)
            {
                _index++;
                ns.Classes.Add(ParseTokenClass(classDocumentation));
            }

            compilationUnit.Namespaces.Add(ns);
            compilationUnit.Imports.Add(BuildSystemImport());
            return compilationUnit;
        }

        public CompilationUnit ParseCompilationUnit()
        {
            if (_tokens.Exists(token => token.Type == TokenType.DeclarationStart))
            {
                BuildJavadocTargetNameTable();
                return ParseTokenCompilationUnit();
            }

            var compilationUnit = new CompilationUnit();

            while (_index < _tokens.Count)
            {
                if (_tokens[_index].Type == TokenType.Eof)
                {
                    _index++;
                    continue;
                }

                var statement = ParseStatement();
                compilationUnit.Statements.Add(statement);
            }

            return compilationUnit;
        }
    }


    public class ReMindBubbleSortAstGenerator
    {
        private readonly string[] _lines;
        private int _index;

        public ReMindBubbleSortAstGenerator(string source)
        {
            _lines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            _index = 0;
        }

        public CompilationUnit Parse()
        {
            var cu = new CompilationUnit();

            // 名前空間
            NamespaceDeclaration ns = ParseNamespace();
            cu.Namespaces.Add(ns);

            // System import は固定で追加（今回のソース前提）
            cu.Imports.Add(BuildSystemImport());

            return cu;
        }

        private NamespaceDeclaration ParseNamespace()
        {
            // ▽名前空間 BubbleSort
            var ns = new NamespaceDeclaration();

            while (_index < _lines.Length)
            {
                var line = _lines[_index].Trim();

                if (line.StartsWith("▽名前空間"))
                {
                    var name = line.Replace("▽名前空間", "").Trim();
                    ns.Name = name;

                    _index++;
                    break;
                }
                _index++;
            }

            // クラスを一つだけパース（今回の前提）
            ns.Classes.Add(ParseClass());

            return ns;
        }

        private ClassDeclaration ParseClass()
        {
            var cls = new ClassDeclaration();

            JavadocComment? pendingJavadoc = null;

            while (_index < _lines.Length)
            {
                var line = _lines[_index].Trim();

                if (line.StartsWith("/**"))
                {
                    pendingJavadoc = ParseJavadoc();
                    continue;
                }

                if (line.StartsWith("▽public クラス"))
                {
                    // ▽public クラス プログラム型
                    var m = Regex.Match(line, @"▽public\s+クラス\s+(\S+)");
                    if (m.Success)
                    {
                        cls.NameJa = m.Groups[1].Value;
                        cls.NameEn = "Program";
                        cls.Modifiers.Add("public");
                        cls.Javadoc = pendingJavadoc;
                        pendingJavadoc = null;
                    }
                    _index++;
                    break;
                }

                _index++;
            }

            // クラス本体のメソッドたち
            while (_index < _lines.Length)
            {
                var line = _lines[_index].Trim();

                if (line == "△") // クラス終端（今回前提）
                {
                    _index++;
                    break;
                }

                if (line.StartsWith("/**"))
                {
                    var j = ParseJavadoc();
                    // 直後に来るメソッドに付与
                    var method = ParseMethod(j);
                    if (method != null)
                    {
                        cls.Methods.Add(method);
                        continue;
                    }
                }
                else if (line.StartsWith("▽static") || line.StartsWith("▽public"))
                {
                    // Javadoc なしメソッド（例: ほぼ無い想定だが一応）
                    var method = ParseMethod(null);
                    if (method != null)
                    {
                        cls.Methods.Add(method);
                        continue;
                    }
                }
                else
                {
                    _index++;
                }
            }

            return cls;
        }

        private JavadocComment ParseJavadoc()
        {
            var j = new JavadocComment();
            if (_index < _lines.Length)
            {
                var singleLine = _lines[_index].Trim();
                if (singleLine.StartsWith("/**") && singleLine.Contains("*/"))
                {
                    var content = singleLine
                        .Substring(3, singleLine.IndexOf("*/", StringComparison.Ordinal) - 3)
                        .Trim()
                        .TrimStart('*')
                        .Trim();
                    if (content.StartsWith("@param"))
                    {
                        var parts = content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3)
                        {
                            j.Params.Add(new JavadocParam
                            {
                                NameJa = parts[1],
                                NameEn = parts[2]
                            });
                        }
                    }
                    else if (!string.IsNullOrEmpty(content))
                    {
                        j.Summary = content;
                    }
                    _index++;
                    return j;
                }
            }

            _index++; // /** 行を飛ばす

            while (_index < _lines.Length)
            {
                var line = _lines[_index].Trim();

                if (line.StartsWith("*/"))
                {
                    _index++;
                    break;
                }

                // * Program
                if (line.StartsWith("*"))
                {
                    var content = line.TrimStart('*').Trim();

                    if (content.StartsWith("@param"))
                    {
                        // @param 引数 args
                        var parts = content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3)
                        {
                            j.Params.Add(new JavadocParam
                            {
                                NameJa = parts[1],
                                NameEn = parts[2]
                            });
                        }
                    }
                    else if (!string.IsNullOrEmpty(content))
                    {
                        j.Summary = content;
                    }
                }

                _index++;
            }

            return j;
        }

        private MethodDeclaration? ParseMethod(JavadocComment? javadoc)
        {
            var line = _lines[_index].Trim();

            // 例:
            // ▽static void メイン(string[] 引数)
            // ▽public void バブルソートする(int[] 配列)
            // ▽static void コンソール表示する(string 引数2)

            var m = Regex.Match(line, @"▽(?<mod>static|public)\s+void\s+(?<nameJa>\S+)\((?<paramStr>.*)\)");
            if (!m.Success)
            {
                return null;
            }

            var method = new MethodDeclaration
            {
                ReturnType = "void",
                Javadoc = javadoc
            };

            var mod = m.Groups["mod"].Value;
            method.Modifiers.Add(mod);

            var nameJa = m.Groups["nameJa"].Value;
            method.NameJa = nameJa;
            method.NameEn = MapMethodName(nameJa);

            var paramStr = m.Groups["paramStr"].Value.Trim();
            if (!string.IsNullOrEmpty(paramStr))
            {
                var p = ParseParameter(paramStr);
                if (p != null)
                {
                    method.Parameters.Add(p);
                }
            }

            _index++; // メソッドヘッダを進める

            // 本体をパース（今回：かなり前提に依存）
            method.Body.AddRange(ParseMethodBody(nameJa));

            return method;
        }

        private Parameter? ParseParameter(string paramStr)
        {
            // 例: "string[] 引数" / "int[] 配列" / "string 引数2"
            var parts = paramStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return null;

            var type = parts[0];
            var nameJa = parts[1];

            return new Parameter
            {
                Type = type,
                NameJa = nameJa,
                        NameEn = MapIdentifierName(nameJa)
            };
        }

        private IEnumerable<Statement> ParseMethodBody(string methodNameJa)
        {
            var stmts = new List<Statement>();

            while (_index < _lines.Length)
            {
                var line = _lines[_index].Trim();

                if (line == "△") // メソッド終了
                {
                    _index++;
                    break;
                }

                // ローカル変数のJavadoc
                if (line.StartsWith("/**"))
                {
                    var javadoc = ParseJavadoc();
                    // 直後の行がローカル変数になる前提
                    var varStmt = ParseLocalVariableDeclaration(javadoc);
                    if (varStmt != null)
                    {
                        stmts.Add(varStmt);
                        continue;
                    }
                    else
                    {
                        continue;
                    }
                }

                // 〇外側 < 配列.Length の間は繰り返す → while
                if (line.StartsWith("〇") && line.Contains("の間は繰り返す"))
                {
                    var whileStmt = ParseWhileStatement(line);
                    stmts.Add(whileStmt);
                    continue;
                }

                // 〇int i=0,i<配列.Length,i++ 繰り返す → for
                if (line.StartsWith("〇int") && line.Contains("繰り返す"))
                {
                    var forStmt = ParseForStatement(line);
                    stmts.Add(forStmt);
                    _index++;
                    // for の中身1行（コンソール表示する）のみ、今回前提
                    var bodyLine = _lines[_index].Trim();
                    var expr = ParseConsoleWriteLineElement(bodyLine);
                    if (expr != null)
                    {
                        forStmt.Body.Add(new ExpressionStatement { Expression = expr });
                    }
                    // 〇ここまで
                    while (_index < _lines.Length && !_lines[_index].Trim().StartsWith("〇ここまで"))
                    {
                        _index++;
                    }
                    _index++; // 「〇ここまで」超え
                    continue;
                }

                // 呼び出し1行: □バブルソートする(配列)
                if (line.StartsWith("□バブルソートする"))
                {
                    var expr = new InvocationExpression
                    {
                        Target = new IdentifierExpression
                        {
                            NameJa = "バブルソートする",
                            NameEn = "BubbleSort"
                        }
                    };
                    expr.Arguments.Add(new IdentifierExpression
                    {
                        NameJa = "配列",
                        NameEn = "array"
                    });
                    stmts.Add(new ExpressionStatement { Expression = expr });
                    _index++;
                    continue;
                }

                // □コンソール表示する(配列[i]) のような行（他メソッドでもありうる）
                if (line.StartsWith("□コンソール表示する"))
                {
                    var expr = ParseConsoleWriteLineElement(line);
                    if (expr != null)
                    {
                        stmts.Add(new ExpressionStatement { Expression = expr });
                    }
                    _index++;
                    continue;
                }

                _index++;
            }

            return stmts;
        }

        private LocalVariableDeclaration? ParseLocalVariableDeclaration(JavadocComment javadoc)
        {
            if (_index >= _lines.Length) return null;

            var line = _lines[_index].Trim();
            // 例: ・int[] 配列 = 15,13,9,6,4,1
            //     ・int 外側 = 0
            var m = Regex.Match(line, @"・(?<type>[a-zA-Z\[\]]+)\s+(?<nameJa>\S+)\s*=\s*(?<rhs>.+)");
            if (!m.Success)
            {
                _index++;
                return null;
            }

            var type = m.Groups["type"].Value;
            var nameJa = m.Groups["nameJa"].Value;
            var rhs = m.Groups["rhs"].Value.Trim();

            Expression initializer;

            if (rhs.Contains(","))
            {
                // 配列のようなリテラル（今回: int[] 配列）
                var arr = new ArrayLiteralExpression
                {
                    ElementType = "int"
                };
                var nums = rhs.Split(',').Select(s => s.Trim());
                foreach (var n in nums)
                {
                    if (int.TryParse(n, out int v))
                    {
                        arr.Elements.Add(new LiteralExpression { Value = v });
                    }
                }
                initializer = arr;
            }
            else if (int.TryParse(rhs, out int v))
            {
                initializer = new LiteralExpression { Value = v };
            }
            else
            {
                // 簡易対応
                initializer = new IdentifierExpression { NameJa = rhs, NameEn = MapIdentifierName(rhs) };
            }

            var decl = new LocalVariableDeclaration
            {
                Javadoc = javadoc,
                Type = type,
                NameJa = nameJa,
                NameEn = MapIdentifierName(nameJa),
                Initializer = initializer
            };

            _index++;
            return decl;
        }

        private WhileStatement ParseWhileStatement(string line)
        {
            // 〇外側 < 配列.Length の間は繰り返す
            var condPart = line.TrimStart('〇').Replace("の間は繰り返す", "").Trim();
            var cond = ParseBinaryCondition(condPart);

            var ws = new WhileStatement
            {
                Condition = cond
            };

            _index++;

            // 本体を読み取る（〇ここまでまで）
            while (_index < _lines.Length)
            {
                var cur = _lines[_index].Trim();
                if (cur.StartsWith("〇ここまで"))
                {
                    _index++;
                    break;
                }

                if (cur.StartsWith("/**"))
                {
                    var j = ParseJavadoc();
                    var decl = ParseLocalVariableDeclaration(j);
                    if (decl != null)
                    {
                        ws.Body.Add(decl);
                        continue;
                    }
                }
                else if (cur.StartsWith("〇") && cur.Contains("の間は繰り返す"))
                {
                    // 内側 while
                    var innerWhile = ParseWhileStatement(cur);
                    ws.Body.Add(innerWhile);
                    continue;
                }
                else if (cur.StartsWith("◇配列[内側] > 配列[内側 + 1] の場合"))
                {
                    var ifStmt = ParseIfSwapBlock();
                    ws.Body.Add(ifStmt);
                    continue;
                }
                else if (cur.StartsWith("□外側++"))
                {
                    var expr = new UnaryExpression
                    {
                        Operator = "++",
                        Operand = new IdentifierExpression
                        {
                            NameJa = "外側",
                            NameEn = "outer"
                        }
                    };
                    ws.Body.Add(new ExpressionStatement { Expression = expr });
                    _index++;
                    continue;
                }
                else if (cur.StartsWith("□内側++"))
                {
                    var expr = new UnaryExpression
                    {
                        Operator = "++",
                        Operand = new IdentifierExpression
                        {
                            NameJa = "内側",
                            NameEn = "inner"
                        }
                    };
                    ws.Body.Add(new ExpressionStatement { Expression = expr });
                    _index++;
                    continue;
                }
                else
                {
                    _index++;
                }
            }

            return ws;
        }

        private ForStatement ParseForStatement(string line)
        {
            // 〇int i=0,i<配列.Length,i++ 繰り返す
            var body = line.TrimStart('〇').Replace("繰り返す", "").Trim();
            // int i=0,i<配列.Length,i++
            var parts = body.Split(',');

            // 初期化: int i=0
            var initPart = parts[0].Trim();
            var mInit = Regex.Match(initPart, @"int\s+(?<name>\w+)\s*=\s*(?<value>\d+)");
            var initializer = new LocalVariableDeclaration
            {
                Type = "int",
                NameJa = mInit.Groups["name"].Value,
                NameEn = mInit.Groups["name"].Value,
                Initializer = new LiteralExpression { Value = int.Parse(mInit.Groups["value"].Value) }
            };

            // 条件: i<配列.Length
            var condPart = parts[1].Trim();
            var condition = ParseBinaryCondition(condPart);

            // 反復: i++
            var iterPart = parts[2].Trim();
            var iterExpr = new UnaryExpression
            {
                Operator = "++",
                Operand = new IdentifierExpression
                {
                    NameJa = "i",
                    NameEn = "i"
                }
            };

            return new ForStatement
            {
                Initializer = initializer,
                Condition = condition,
                Iterator = iterExpr
            };
        }

        private Expression ParseBinaryCondition(string condPart)
        {
            // かなり固定フォーマットに依存: 例 「外側 < 配列.Length」/「内側 < 配列.Length - 外側 - 1」/「i<配列.Length」
            condPart = condPart.Replace(" ", "");

            // 不等号で切る簡易版
            if (condPart.Contains("<="))
            {
                var parts = condPart.Split(new[] { "<=" }, StringSplitOptions.None);
                return new BinaryExpression
                {
                    Operator = "<=",
                    Left = ParseSimpleExpression(parts[0]),
                    Right = ParseSimpleExpression(parts[1])
                };
            }
            if (condPart.Contains("<"))
            {
                var parts = condPart.Split('<');
                return new BinaryExpression
                {
                    Operator = "<",
                    Left = ParseSimpleExpression(parts[0]),
                    Right = ParseSimpleExpression(parts[1])
                };
            }

            throw new NotSupportedException("この条件は簡易パーサでは未対応です: " + condPart);
        }

        private Expression ParseSimpleExpression(string text)
        {
            // 配列.Length - 外側 - 1 のようなものも来るので、ここも簡易。
            if (int.TryParse(text, out var n))
            {
                return new LiteralExpression { Value = n };
            }

            if (text.Contains("配列.Length"))
            {
                // 配列.Length - 外側 - 1 など
                // ここでは BinaryExpression ネストを手書き
                // 今回用途に特化
                if (text == "配列.Length")
                {
                    return new MemberAccessExpression
                    {
                        Expression = new IdentifierExpression
                        {
                            NameJa = "配列",
                            NameEn = "array"
                        },
                        MemberName = "Length"
                    };
                }

                // 配列.Length-外側-1
                var replaced = text.Replace("配列.Length", "L");
                var tokens = replaced.Split('-'); // L,外側,1
                Expression expr = new MemberAccessExpression
                {
                    Expression = new IdentifierExpression
                    {
                        NameJa = "配列",
                        NameEn = "array"
                    },
                    MemberName = "Length"
                };
                for (int i = 1; i < tokens.Length; i++)
                {
                    Expression right;
                    if (int.TryParse(tokens[i], out var vi))
                    {
                        right = new LiteralExpression { Value = vi };
                    }
                    else
                    {
                        right = new IdentifierExpression
                        {
                            NameJa = tokens[i],
                            NameEn = MapIdentifierName(tokens[i])
                        };
                    }
                    expr = new BinaryExpression
                    {
                        Operator = "-",
                        Left = expr,
                        Right = right
                    };
                }

                return expr;
            }

            // 単純識別子
            return new IdentifierExpression
            {
                NameJa = text,
                NameEn = MapIdentifierName(text)
            };
        }

        private IfStatement ParseIfSwapBlock()
        {
            // ◇配列[内側] > 配列[内側 + 1] の場合
            var ifStmt = new IfStatement();
            ifStmt.Condition = BuildSwapCondition();
            _index++;

            // /** temp */ ・int 一時 = 配列[内側] 等を順に読む
            // 今回は固定パターン前提
            // 1. Javadoc + ローカル変数宣言
            if (_lines[_index].Trim().StartsWith("/**"))
            {
                var j = ParseJavadoc();
                var decl = ParseLocalVariableDeclaration(j);
                if (decl != null)
                {
                    ifStmt.ThenBody.Add(decl);
                }
            }

            // 2. □配列[内側] = 配列[内側 + 1]
            if (_lines[_index].Trim().StartsWith("□配列[内側] = 配列[内側 + 1]"))
            {
                var assign1 = new AssignmentStatement
                {
                    Left = new ElementAccessExpression
                    {
                        ArrayExpression = new IdentifierExpression
                        {
                            NameJa = "配列",
                            NameEn = "array"
                        },
                        IndexExpression = new IdentifierExpression
                        {
                            NameJa = "内側",
                            NameEn = "inner"
                        }
                    },
                    Right = new ElementAccessExpression
                    {
                        ArrayExpression = new IdentifierExpression
                        {
                            NameJa = "配列",
                            NameEn = "array"
                        },
                        IndexExpression = new BinaryExpression
                        {
                            Operator = "+",
                            Left = new IdentifierExpression
                            {
                                NameJa = "内側",
                                NameEn = "inner"
                            },
                            Right = new LiteralExpression { Value = 1 }
                        }
                    }
                };
                ifStmt.ThenBody.Add(assign1);
                _index++;
            }

            // 3. □配列[内側 + 1] = 一時
            if (_lines[_index].Trim().StartsWith("□配列[内側 + 1] = 一時"))
            {
                var assign2 = new AssignmentStatement
                {
                    Left = new ElementAccessExpression
                    {
                        ArrayExpression = new IdentifierExpression
                        {
                            NameJa = "配列",
                            NameEn = "array"
                        },
                        IndexExpression = new BinaryExpression
                        {
                            Operator = "+",
                            Left = new IdentifierExpression
                            {
                                NameJa = "内側",
                                NameEn = "inner"
                            },
                            Right = new LiteralExpression { Value = 1 }
                        }
                    },
                    Right = new IdentifierExpression
                    {
                        NameJa = "一時",
                        NameEn = "temp"
                    }
                };
                ifStmt.ThenBody.Add(assign2);
                _index++;
            }

            // ◇ここまで
            while (_index < _lines.Length && !_lines[_index].Trim().StartsWith("◇ここまで"))
            {
                _index++;
            }
            if (_index < _lines.Length) _index++;

            return ifStmt;
        }

        private Expression BuildSwapCondition()
        {
            // 配列[内側] > 配列[内側 + 1]
            return new BinaryExpression
            {
                Operator = ">",
                Left = new ElementAccessExpression
                {
                    ArrayExpression = new IdentifierExpression
                    {
                        NameJa = "配列",
                        NameEn = "array"
                    },
                    IndexExpression = new IdentifierExpression
                    {
                        NameJa = "内側",
                        NameEn = "inner"
                    }
                },
                Right = new ElementAccessExpression
                {
                    ArrayExpression = new IdentifierExpression
                    {
                        NameJa = "配列",
                        NameEn = "array"
                    },
                    IndexExpression = new BinaryExpression
                    {
                        Operator = "+",
                        Left = new IdentifierExpression
                        {
                            NameJa = "内側",
                            NameEn = "inner"
                        },
                        Right = new LiteralExpression { Value = 1 }
                    }
                }
            };
        }

        private Expression? ParseConsoleWriteLineElement(string line)
        {
            // □コンソール表示する(配列[i]) / □コンソール表示する(配列[内側]) 等を想定
            var m = Regex.Match(line, @"□コンソール表示する\((?<content>.+)\)");
            if (!m.Success) return null;

            var content = m.Groups["content"].Value.Trim();

            Expression argExpr;

            // 配列[i] 形式
            var mArr = Regex.Match(content, @"配列\[(?<idx>.+)\]");
            if (mArr.Success)
            {
                var idx = mArr.Groups["idx"].Value.Trim();
                argExpr = new ElementAccessExpression
                {
                    ArrayExpression = new IdentifierExpression
                    {
                        NameJa = "配列",
                        NameEn = "array"
                    },
                    IndexExpression = new IdentifierExpression
                    {
                        NameJa = idx,
                        NameEn = idx
                    }
                };
            }
            else
            {
                // 単純文字列 or 識別子（今回: "該当なし" 等もありえるが、バブルソートでは整数のみ）
                if (content.StartsWith("\"") && content.EndsWith("\""))
                {
                    argExpr = new LiteralExpression
                    {
                        Value = content.Trim('"')
                    };
                }
                else
                {
                    argExpr = new IdentifierExpression
                    {
                        NameJa = content,
                        NameEn = content
                    };
                }
            }

            var target = new IdentifierExpression
            {
                NameJa = "コンソール表示する",
                NameEn = "ConsoleOut"
            };

            var inv = new InvocationExpression
            {
                Target = target
            };
            inv.Arguments.Add(argExpr);

            return inv;
        }

        private ImportDeclaration BuildSystemImport()
        {
            var imp = new ImportDeclaration
            {
                Name = "System"
            };

            var alias = new AliasClass
            {
                OriginalName = "コンソール",
                TranspiledName = "Console"
            };

            alias.Members.Add(new AliasMember
            {
                OriginalName = "一行表示する",
                TranspiledName = "WriteLine"
            });

            imp.AliasClasses.Add(alias);
            return imp;
        }

        // --- 日本語名 → 英語名 簡易マップ（今回のバブルソート用） ---

        private string MapMethodName(string nameJa)
        {
            return nameJa switch
            {
                "メイン" => "Main",
                "バブルソートする" => "BubbleSort",
                "コンソール表示する" => "ConsoleOut",
                _ => nameJa
            };
        }

        private string MapIdentifierName(string nameJa)
        {
            return nameJa switch
            {
                "配列" => "array",
                "外側" => "outer",
                "内側" => "inner",
                "一時" => "temp",
                "引数" => "args",
                "引数2" => "args2",
                _ => nameJa
            };
        }

    }

}
