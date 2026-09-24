using System;
using System.Linq;
using ReMindAst;

namespace ReMindBackend
{
    public class CSharpCodeGenerator
    {
        private readonly IndentWriter _w = new();
        private readonly MappingTable _mappingTable;
        private Dictionary<string, AliasClass> _aliasMap;

        public CSharpCodeGenerator() : this(new MappingTable())
        {
        }

        public CSharpCodeGenerator(MappingTable mappingTable)
        {
            _mappingTable = mappingTable;
            _mappingTable.ConsoleWriteLine.TryAdd("コンソール.一行表示する", "Console.WriteLine");
        }

        public string Generate(CompilationUnit cu)
        {
            _aliasMap = BuildAliasMap(cu);

            // using
            foreach (var imp in cu.Imports)
            {
                _w.WriteLine($"using {imp.Name};");
            }
            _w.WriteLine();

            // namespace
            foreach (var ns in cu.Namespaces)
            {
                _w.WriteLine($"namespace {ns.Name}");
                _w.WriteLine("{");
                _w.Indent();

                foreach (var cls in ns.Classes)
                {
                    GenerateClass(cls);
                }

                _w.Unindent();
                _w.WriteLine("}");
            }

            return _w.ToString();
        }

        public string Generate(ProgramIR program)
        {
            foreach (var import in program.Imports)
            {
                _w.WriteLine($"using {import};");
            }
            _w.WriteLine();

            foreach (var namespaceName in program.Namespaces)
            {
                _w.WriteLine($"namespace {namespaceName}");
                _w.WriteLine("{");
                _w.Indent();

                foreach (var classIR in program.Classes)
                {
                    GenerateClass(classIR);
                }

                _w.Unindent();
                _w.WriteLine("}");
            }

            return _w.ToString();
        }

        private void GenerateClass(ClassIR classIR)
        {
            GenerateDocumentation(classIR.Documentation);
            _w.WriteLine($"public class {classIR.Name}");
            _w.WriteLine("{");
            _w.Indent();

            foreach (var field in classIR.Fields)
            {
                GenerateDocumentation(field.Documentation);
                var declaration = field.Modifiers.Contains("const", StringComparer.Ordinal)
                    ? $"private const {field.Type} {field.Name} = {GenerateExpression(field.Initializer!)};"
                    : $"{string.Join(" ", field.Modifiers)} {field.Type} {field.Name};";
                _w.WriteLine(declaration);
            }

            if (classIR.Fields.Count > 0 && classIR.Methods.Count > 0)
            {
                _w.WriteLine();
            }

            foreach (var method in classIR.Methods)
            {
                GenerateMethod(method);
                _w.WriteLine();
            }

            _w.Unindent();
            _w.WriteLine("}");
        }

        private void GenerateMethod(MethodIR methodIR)
        {
            GenerateDocumentation(methodIR.Documentation);
            _w.WriteLine($"static {methodIR.ReturnType} {methodIR.Name}({string.Join(", ", methodIR.Parameters)})");
            _w.WriteLine("{");
            _w.Indent();

            foreach (var statement in methodIR.Body.Statements)
            {
                GenerateStatement(statement);
            }

            _w.Unindent();
            _w.WriteLine("}");
        }

        private void GenerateStatement(StatementIR stmt)
        {
            _ = _mappingTable;
            ApplyFormattingInfo(stmt.FormattingInfo);

            if (stmt is VariableDeclarationIR variable)
            {
                GenerateDocumentation(variable.Documentation);
                var declarationPrefix = variable.IsConstant ? "const " : string.Empty;
                _w.WriteLine($"{declarationPrefix}{variable.Type} {variable.Name} = {GenerateExpression(variable.Initializer)};");
            }

            if (stmt is AssignmentIR assignment)
            {
                var target = assignment.TargetExpression == null
                    ? assignment.Target
                    : GenerateExpression(assignment.TargetExpression);
                _w.WriteLine($"{target} = {GenerateExpression(assignment.Value)};");
            }

            if (stmt is ForIR forStatement)
            {
                var initializer = GenerateInlineStatement(forStatement.Initializer);
                _w.WriteLine($"for ({initializer}; {GenerateExpression(forStatement.Condition)}; {GenerateExpression(forStatement.Iterator)})");
                _w.WriteLine("{");
                _w.Indent();
                foreach (var statement in forStatement.Body.Statements)
                {
                    GenerateStatement(statement);
                }
                _w.Unindent();
                _w.WriteLine("}");
            }

            if (stmt is CallIR call)
            {
                var callName = call.MethodName.Equals("ConsoleOut", StringComparison.OrdinalIgnoreCase)
                    ? "Console.WriteLine"
                    : call.MethodName;
                _w.WriteLine($"{callName}({string.Join(", ", call.Arguments.Select(argument => GenerateExpression(argument)))});");
            }

            if (stmt is ExpressionStatementIR expressionStatement)
            {
                _w.WriteLine($"{GenerateExpression(expressionStatement.Expression)};");
            }

            if (stmt is IfIR ifStatement)
            {
                _w.WriteLine($"if ({GenerateExpression(ifStatement.Condition)})");
                _w.WriteLine("{");
                _w.Indent();

                foreach (var statement in ifStatement.ThenBlock.Statements)
                {
                    GenerateStatement(statement);
                }

                _w.Unindent();
                _w.WriteLine("}");

                if (ifStatement.ElseBlock != null && ifStatement.ElseBlock.Statements.Count > 0)
                {
                    _w.WriteLine("else");
                    _w.WriteLine("{");
                    _w.Indent();

                    foreach (var statement in ifStatement.ElseBlock.Statements)
                    {
                        GenerateStatement(statement);
                    }

                    _w.Unindent();
                    _w.WriteLine("}");
                }
            }

            if (stmt is WhileIR whileStatement)
            {
                _w.WriteLine($"while ({GenerateExpression(whileStatement.Condition)})");
                _w.WriteLine("{");
                _w.Indent();

                foreach (var statement in whileStatement.Body.Statements)
                {
                    GenerateStatement(statement);
                }

                _w.Unindent();
                _w.WriteLine("}");
            }

            if (stmt is ReturnIR returnStatement)
            {
                _w.WriteLine(returnStatement.Value == null ? "return;" : $"return {GenerateExpression(returnStatement.Value)};");
            }

            if (stmt is BreakIR)
            {
                _w.WriteLine("break;");
            }

            if (stmt is ContinueIR)
            {
                _w.WriteLine("continue;");
            }
        }

        private string GenerateExpression(ExpressionIR expr)
        {
            return GenerateExpression(expr, 0);
        }

        private static int GetOperatorPrecedence(string op)
        {
            return op switch
            {
                "==" or "!=" or "<" or ">" or "<=" or ">=" => 1,
                "+" or "-" => 2,
                "*" or "/" or "%" => 3,
                _ => 0
            };
        }

        private string GenerateExpression(ExpressionIR expr, int parentPrecedence)
        {
            _ = _mappingTable;
            ApplyFormattingInfo(expr.FormattingInfo);

            return expr switch
            {
                IdentifierIR identifier => identifier.Name,
                LiteralIR literal => literal.Value,
                BinaryIR binary => GenerateBinaryExpression(binary, parentPrecedence),
                UnaryIR unary => unary.Operator == "++"
                    ? $"{GenerateExpression(unary.Operand)}++"
                    : $"{unary.Operator}{GenerateExpression(unary.Operand)}",
                ArrayAccessIR arrayAccess => $"{GenerateExpression(arrayAccess.Array)}[{GenerateExpression(arrayAccess.Index)}]",
                ArrayLiteralIR arrayLiteral => $"new {arrayLiteral.ElementType}[] {{ {string.Join(", ", arrayLiteral.Elements.Select(element => GenerateExpression(element)))} }}",
                NewExpressionIR arrayCreation => $"new {arrayCreation.TypeName}[{GenerateExpression(arrayCreation.Arguments[0])}]",
                CallExpressionIR call when call.MethodName == "コンソール.一行表示する" => $"{_mappingTable.ConsoleWriteLine["コンソール.一行表示する"]}({string.Join(", ", call.Arguments.Select(argument => GenerateExpression(argument)))});",
                CallExpressionIR call => $"{call.MethodName}({string.Join(", ", call.Arguments.Select(argument => GenerateExpression(argument)))})",
                MemberAccessIR memberAccess => $"{GenerateExpression(memberAccess.Target)}.{memberAccess.MemberName}",
                _ => string.Empty
            };
        }

        private string GenerateBinaryExpression(BinaryIR binary, int parentPrecedence)
        {
            var precedence = GetOperatorPrecedence(binary.Operator);
            // 右辺は同順位でも括弧を残し、"-"/"/"のような非結合演算子の意味を保つ
            var text = $"{GenerateExpression(binary.Left, precedence)} {binary.Operator} {GenerateExpression(binary.Right, precedence + 1)}";
            return precedence < parentPrecedence ? $"({text})" : text;
        }

        private string GenerateInlineStatement(StatementIR statement)
        {
            if (statement is VariableDeclarationIR variable)
            {
                return $"{variable.Type} {variable.Name} = {GenerateExpression(variable.Initializer)}";
            }

            return string.Empty;
        }

        private void ApplyFormattingInfo(string? formattingInfo)
        {
        }

        private void GenerateDocumentation(DocumentationIR? documentation)
        {
            if (documentation == null)
            {
                return;
            }

            var summary = !string.IsNullOrEmpty(documentation.NameJa)
                ? documentation.NameJa
                : documentation.Summary;

            if (!string.IsNullOrEmpty(summary))
            {
                _w.WriteLine($"/// <summary>{summary}</summary>");
            }

            foreach (var parameter in documentation.Parameters)
            {
                _w.WriteLine($"/// <param name=\"{parameter.Name}\">{parameter.Description}</param>");
            }
        }

        private Dictionary<string, AliasClass> BuildAliasMap(CompilationUnit cu)
        {
            var map = new Dictionary<string, AliasClass>();

            foreach (var import in cu.Imports)
            {
                foreach (var alias in import.AliasClasses)
                {
                    map[alias.OriginalName] = alias;
                }
            }

            return map;
        }

        private void GenerateClass(ClassDeclaration cls)
        {
            // XML コメント
            if (cls.Javadoc != null)
                WriteXmlComment(cls.Javadoc);

            // public class ProgramType
            var mods = string.Join(" ", cls.Modifiers);
            _w.WriteLine($"{mods} class {cls.NameEn}");
            _w.WriteLine("{");
            _w.Indent();

            foreach (var m in cls.Methods)
            {
                GenerateMethod(m);
                _w.WriteLine();
            }

            _w.Unindent();
            _w.WriteLine("}");
        }

        private void GenerateMethod(MethodDeclaration m)
        {
            if (m.Javadoc != null)
                WriteXmlComment(m.Javadoc);

            var mods = string.Join(" ", m.Modifiers);
            var paramList = string.Join(", ",
                m.Parameters.Select(p => $"{MapType(p.Type)} {p.NameEn}"));

            _w.WriteLine($"{mods} {MapType(m.ReturnType)} {m.NameEn}({paramList})");
            _w.WriteLine("{");
            _w.Indent();

            foreach (var stmt in m.Body)
            {
                GenerateStatement(stmt);
            }

            _w.Unindent();
            _w.WriteLine("}");
        }

        private void GenerateStatement(Statement stmt)
        {
            switch (stmt)
            {
                case LocalVariableDeclaration v:
                    GenerateLocalVariable(v);
                    break;

                case ExpressionStatement es:
                    _w.WriteLine($"{GenerateExpression(es.Expression)};");
                    break;

                case ForStatement fs:
                    GenerateFor(fs);
                    break;

                case WhileStatement ws:
                    GenerateWhile(ws);
                    break;

                case IfStatement ifs:
                    GenerateIf(ifs);
                    break;

                case AssignmentStatement ass:
                    _w.WriteLine($"{GenerateExpression(ass.Left)} = {GenerateExpression(ass.Right)};");
                    break;

                default:
                    _w.WriteLine("// 未対応ステートメント");
                    break;
            }
        }

        private void GenerateLocalVariable(LocalVariableDeclaration v)
        {
            if (v.Javadoc != null)
                WriteXmlComment(v.Javadoc);

            if (v.Initializer != null)
            {
                _w.WriteLine($"{MapType(v.Type)} {v.NameEn} = {GenerateExpression(v.Initializer)};");
            }
            else
            {
                _w.WriteLine($"{MapType(v.Type)} {v.NameEn};");
            }
        }

        private void GenerateFor(ForStatement fs)
        {
            var init = $"{MapType(fs.Initializer.Type)} {fs.Initializer.NameEn} = {GenerateExpression(fs.Initializer.Initializer)}";
            var cond = GenerateExpression(fs.Condition);
            var iter = GenerateExpression(fs.Iterator);

            _w.WriteLine($"for ({init}; {cond}; {iter})");
            _w.WriteLine("{");
            _w.Indent();

            foreach (var s in fs.Body)
                GenerateStatement(s);

            _w.Unindent();
            _w.WriteLine("}");
        }

        private void GenerateWhile(WhileStatement ws)
        {
            _w.WriteLine($"while ({GenerateExpression(ws.Condition)})");
            _w.WriteLine("{");
            _w.Indent();

            foreach (var s in ws.Body)
                GenerateStatement(s);

            _w.Unindent();
            _w.WriteLine("}");
        }

        private void GenerateIf(IfStatement ifs)
        {
            _w.WriteLine($"if ({GenerateExpression(ifs.Condition)})");
            _w.WriteLine("{");
            _w.Indent();

            foreach (var s in ifs.ThenBody)
                GenerateStatement(s);

            _w.Unindent();
            _w.WriteLine("}");

            if (ifs.ElseBody.Any())
            {
                _w.WriteLine("else");
                _w.WriteLine("{");
                _w.Indent();

                foreach (var s in ifs.ElseBody)
                    GenerateStatement(s);

                _w.Unindent();
                _w.WriteLine("}");
            }
        }

        private string GenerateExpression(Expression expr)
        {
            return expr switch
            {
                IdentifierExpression id => id.NameEn,
                LiteralExpression lit => lit.Value is string s ? $"\"{s}\"" : lit.Value!.ToString()!,
                BinaryExpression bin => $"{GenerateExpression(bin.Left)} {bin.Operator} {GenerateExpression(bin.Right)}",
                UnaryExpression un => $"{GenerateExpression(un.Operand)}{un.Operator}",
                MemberAccessExpression ma => $"{GenerateExpression(ma.Expression)}.{ma.MemberName}",
                ElementAccessExpression ea => $"{GenerateExpression(ea.ArrayExpression)}[{GenerateExpression(ea.IndexExpression)}]",
                InvocationExpression inv => GenerateInvocation(inv, _aliasMap),
                ArrayLiteralExpression arr => $"new {arr.ElementType}[] {{ {string.Join(", ", arr.Elements.Select(GenerateExpression))} }}",
                _ => "/* 未対応式 */"
            };
        }

        private string GenerateInvocation(InvocationExpression expr, Dictionary<string, AliasClass> aliasMap)
        {
            // ターゲットが MemberAccessExpression の場合（例：コンソール.一行表示する）
            if (expr.Target is MemberAccessExpression ma)
            {
                // 左側（コンソール）
                if (ma.Expression is IdentifierExpression id &&
                    aliasMap.TryGetValue(id.NameJa, out var alias))
                {
                    // メソッド名（例：一行表示する）
                    var originalMember = ma.MemberName;

                    // マッピングを探す
                    var member = alias.Members
                        .FirstOrDefault(m => m.OriginalName == originalMember);

                    var memberName = member?.TranspiledName ?? originalMember;

                    return $"{alias.TranspiledName}.{memberName}({GenerateArgs(expr.Arguments)})";
                }
                }

            // ターゲットが IdentifierExpression の場合（例：バブルソートする）
            if (expr.Target is IdentifierExpression id2)
            {
                if (aliasMap.TryGetValue(id2.NameJa, out var alias))
                {
                    // メソッド名は expr.Target.NameEn ではなく、expr.NameEn を使うべき
                    return $"{alias.TranspiledName}.{id2.NameEn}({GenerateArgs(expr.Arguments)})";
                }
            }

            // fallback
            return $"{GenerateExpression(expr.Target)}({GenerateArgs(expr.Arguments)})";
        }
        private string GenerateArgs(IEnumerable<Expression> args)
        {
            return string.Join(", ", args.Select(GenerateExpression));
        }

        private string GetInvocationMemberName(InvocationExpression expr)
        {
            return expr.Target switch
            {
                MemberAccessExpression ma => ma.MemberName,
                IdentifierExpression id => id.NameEn, // fallback
                _ => ""
            };
        }
        private void WriteXmlComment(JavadocComment j)
        {
            if (!string.IsNullOrEmpty(j.Summary))
                _w.WriteLine($"/// <summary>{j.Summary}</summary>");

            foreach (var p in j.Params)
                _w.WriteLine($"/// <param name=\"{p.NameEn}\">{p.NameJa}</param>");
        }

        private string MapType(string t)
        {
            return t switch
            {
                "string" => "string",
                "string[]" => "string[]",
                "int" => "int",
                "int[]" => "int[]",
                "void" => "void",
                _ => t
            };
        }
    }
}
