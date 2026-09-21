using System;
using System.Linq;
using ReMindAst;

namespace ReMindBackend
{
    public class VbNetCodeGenerator
    {
        private readonly IndentWriter _w = new();
        private readonly MappingTable _mappingTable;

        public VbNetCodeGenerator() : this(new MappingTable())
        {
        }

        public VbNetCodeGenerator(MappingTable mappingTable)
        {
            _mappingTable = mappingTable;
        }

        public string Generate(CompilationUnit cu)
        {
            // Imports
            foreach (var imp in cu.Imports)
            {
                _w.WriteLine($"Imports {imp.Name}");
            }
            _w.WriteLine();

            foreach (var ns in cu.Namespaces)
            {
                _w.WriteLine($"Namespace {ns.Name}");
                _w.Indent();

                foreach (var cls in ns.Classes)
                {
                    GenerateClass(cls);
                    _w.WriteLine();
                }

                _w.Unindent();
                _w.WriteLine("End Namespace");
            }

            return _w.ToString();
        }

        public string Generate(ProgramIR program)
        {
            foreach (var import in program.Imports)
            {
                _w.WriteLine($"Imports {import}");
            }
            if (program.Imports.Count > 0)
            {
                _w.WriteLine();
            }

            foreach (var namespaceName in program.Namespaces)
            {
                _w.WriteLine($"Namespace {namespaceName}");
                _w.Indent();
            }

            foreach (var classIR in program.Classes)
            {
                GenerateClass(classIR);
                _w.WriteLine();
            }

            for (var index = 0; index < program.Namespaces.Count; index++)
            {
                _w.Unindent();
                _w.WriteLine("End Namespace");
            }

            return _w.ToString();
        }

        private void GenerateClass(ClassIR classIR)
        {
            GenerateDocumentation(classIR.Documentation);
            _w.WriteLine($"Public Module {classIR.Name}");
            _w.Indent();

            foreach (var field in classIR.Fields)
            {
                _w.WriteLine($"{string.Join(" ", field.Modifiers.Select(MapModifier))} {field.Name} As {MapType(field.Type)}");
            }

            foreach (var method in classIR.Methods)
            {
                GenerateMethod(method);
                _w.WriteLine();
            }

            _w.Unindent();
            _w.WriteLine("End Module");
        }

        private void GenerateMethod(MethodIR methodIR)
        {
            GenerateDocumentation(methodIR.Documentation);
            var parameters = string.Join(", ", methodIR.Parameters.Select(MapParameter));
            if (methodIR.ReturnType == "void")
            {
                _w.WriteLine($"Public Shared Sub {methodIR.Name}({parameters})");
            }
            else
            {
                _w.WriteLine($"Public Shared Function {methodIR.Name}({parameters}) As {MapType(methodIR.ReturnType)}");
            }

            _w.Indent();
            foreach (var statement in methodIR.Body.Statements)
            {
                GenerateStatement(statement);
            }
            _w.Unindent();
            _w.WriteLine(methodIR.ReturnType == "void" ? "End Sub" : "End Function");
        }

        private void GenerateStatement(StatementIR stmt)
        {
            switch (stmt)
            {
                case VariableDeclarationIR variable:
                    GenerateDocumentation(variable.Documentation);
                    _w.WriteLine($"Dim {variable.Name} As {MapType(variable.Type)} = {GenerateExpression(variable.Initializer)}");
                    break;
                case AssignmentIR assignment:
                    var target = assignment.TargetExpression == null
                        ? assignment.Target
                        : GenerateExpression(assignment.TargetExpression);
                    _w.WriteLine($"{target} = {GenerateExpression(assignment.Value)}");
                    break;
                case CallIR call:
                    _w.WriteLine($"{MapCall(call.MethodName)}({string.Join(", ", call.Arguments.Select(GenerateExpression))})");
                    break;
                case ExpressionStatementIR expression:
                    _w.WriteLine(GenerateExpression(expression.Expression));
                    break;
                case IfIR conditional:
                    _w.WriteLine($"If {GenerateExpression(conditional.Condition)} Then");
                    _w.Indent();
                    foreach (var nested in conditional.ThenBlock.Statements)
                    {
                        GenerateStatement(nested);
                    }
                    _w.Unindent();
                    if (conditional.ElseBlock is { Statements.Count: > 0 })
                    {
                        _w.WriteLine("Else");
                        _w.Indent();
                        foreach (var nested in conditional.ElseBlock.Statements)
                        {
                            GenerateStatement(nested);
                        }
                        _w.Unindent();
                    }
                    _w.WriteLine("End If");
                    break;
                case WhileIR loop:
                    _w.WriteLine($"While {GenerateExpression(loop.Condition)}");
                    _w.Indent();
                    foreach (var nested in loop.Body.Statements)
                    {
                        GenerateStatement(nested);
                    }
                    _w.Unindent();
                    _w.WriteLine("End While");
                    break;
                case ForIR loop:
                    GenerateFor(loop);
                    break;
            }
        }

        private void GenerateFor(ForIR loop)
        {
            if (loop.Initializer is not VariableDeclarationIR variable ||
                loop.Condition is not BinaryIR condition ||
                loop.Iterator is not UnaryIR iterator ||
                iterator.Operator != "++")
            {
                _w.WriteLine("' 未対応のFor構文");
                return;
            }

            _w.WriteLine($"For {variable.Name} As {MapType(variable.Type)} = {GenerateExpression(variable.Initializer)} To {GenerateExpression(condition.Right)} - 1");
            _w.Indent();
            foreach (var statement in loop.Body.Statements)
            {
                GenerateStatement(statement);
            }
            _w.Unindent();
            _w.WriteLine("Next");
        }

        private string GenerateExpression(ExpressionIR expr)
        {
            return expr switch
            {
                IdentifierIR identifier => identifier.Name,
                LiteralIR literal => literal.Value,
                BinaryIR binary => $"{GenerateExpression(binary.Left)} {binary.Operator} {GenerateExpression(binary.Right)}",
                UnaryIR unary => $"{GenerateExpression(unary.Operand)} {MapUnaryOperator(unary.Operator)}",
                CallExpressionIR call => $"{MapCall(call.MethodName)}({string.Join(", ", call.Arguments.Select(GenerateExpression))})",
                MemberAccessIR member => $"{GenerateExpression(member.Target)}.{member.MemberName}",
                ArrayAccessIR array => $"{GenerateExpression(array.Array)}({GenerateExpression(array.Index)})",
                ArrayLiteralIR array => $"{{ {string.Join(", ", array.Elements.Select(GenerateExpression))} }}",
                _ => ""
            };
        }

        private string MapParameter(string parameter)
        {
            var parts = parameter.Split(' ', 2);
            return parts.Length == 2 ? $"{parts[1]} As {MapType(parts[0])}" : parameter;
        }

        private string MapCall(string methodName)
        {
            return methodName == "コンソール.一行表示する" ? "Console.WriteLine" : methodName;
        }

        private void GenerateDocumentation(DocumentationIR? documentation)
        {
            if (documentation == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(documentation.NameJa))
            {
                _w.WriteLine($"''' <summary>{documentation.NameJa}</summary>");
            }
            foreach (var parameter in documentation.Parameters)
            {
                _w.WriteLine($"''' <param name=\"{parameter.Name}\">{parameter.Description}</param>");
            }
        }

        private void GenerateClass(ClassDeclaration cls)
        {
            if (cls.Javadoc != null)
                WriteXmlComment(cls.Javadoc);

            var mods = string.Join(" ", cls.Modifiers.Select(MapModifier));
            if (!string.IsNullOrEmpty(mods))
                mods += " ";

            _w.WriteLine($"{mods}Class {cls.NameEn}");
            _w.Indent();

            foreach (var m in cls.Methods)
            {
                GenerateMethod(m);
                _w.WriteLine();
            }

            _w.Unindent();
            _w.WriteLine("End Class");
        }

        private void GenerateMethod(MethodDeclaration m)
        {
            if (m.Javadoc != null)
                WriteXmlComment(m.Javadoc, m.NameJa);

            var mods = string.Join(" ", m.Modifiers.Select(MapMethodModifier));
            if (!string.IsNullOrEmpty(mods))
                mods += " ";

            var paramList = string.Join(", ",
                m.Parameters.Select(p => $"{p.NameEn} As {MapType(p.Type)}"));

            if (m.ReturnType == "void")
            {
                _w.WriteLine($"{mods}Sub {m.NameEn}({paramList})");
            }
            else
            {
                _w.WriteLine($"{mods}Function {m.NameEn}({paramList}) As {MapType(m.ReturnType)}");
            }

            _w.Indent();

            foreach (var stmt in m.Body)
                GenerateStatement(stmt);

            _w.Unindent();

            if (m.ReturnType == "void")
                _w.WriteLine("End Sub");
            else
                _w.WriteLine("End Function");
        }

        private void GenerateStatement(Statement stmt)
        {
            switch (stmt)
            {
                case LocalVariableDeclaration v:
                    GenerateLocalVariable(v);
                    break;

                case ExpressionStatement es:
                    _w.WriteLine(GenerateExpression(es.Expression));
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
                    _w.WriteLine($"{GenerateExpression(ass.Left)} = {GenerateExpression(ass.Right)}");
                    break;

                default:
                    _w.WriteLine("' 未対応ステートメント");
                    break;
            }
        }

        private void GenerateLocalVariable(LocalVariableDeclaration v)
        {
            if (v.Javadoc != null)
                WriteXmlComment(v.Javadoc);

            if (v.Initializer != null)
            {
                _w.WriteLine($"Dim {v.NameEn} As {MapType(v.Type)} = {GenerateExpression(v.Initializer)}");
            }
            else
            {
                _w.WriteLine($"Dim {v.NameEn} As {MapType(v.Type)}");
            }
        }

        private void GenerateFor(ForStatement fs)
        {
            // Re:Mind の for (int i = 0; i < array.Length; i++) を
            // VB.NET の For i As Integer = 0 To array.Length - 1 に変換したい場合もあるが、
            // 今回は「そのまま For ... While 風」にせず、素直に For Next を生成することもできる。
            // ここでは単純に 0 ? array.Length-1 を仮定した実装にしておく（バブルソート用）。

            if (fs.Initializer == null ||
                fs.Condition is not BinaryExpression bin ||
                fs.Iterator is not UnaryExpression un ||
                un.Operator != "++")
            {
                // 簡易: そのままコメントで逃がす
                _w.WriteLine("' 未対応の For 形式");
                return;
            }

            // int i = 0;
            var varName = fs.Initializer.NameEn;
            var startExpr = GenerateExpression(fs.Initializer.Initializer!);

            // i < array.Length
            // → To array.Length - 1 を仮定
            var endExpr = $"{GenerateExpression(bin.Right)} - 1";

            _w.WriteLine($"For {varName} As {MapType(fs.Initializer.Type)} = {startExpr} To {endExpr}");
            _w.Indent();

            foreach (var s in fs.Body)
                GenerateStatement(s);

            _w.Unindent();
            _w.WriteLine("Next");
        }

        private void GenerateWhile(WhileStatement ws)
        {
            _w.WriteLine($"While {GenerateExpression(ws.Condition)}");
            _w.Indent();

            foreach (var s in ws.Body)
                GenerateStatement(s);

            _w.Unindent();
            _w.WriteLine("End While");
        }

        private void GenerateIf(IfStatement ifs)
        {
            _w.WriteLine($"If {GenerateExpression(ifs.Condition)} Then");
            _w.Indent();

            foreach (var s in ifs.ThenBody)
                GenerateStatement(s);

            _w.Unindent();

            if (ifs.ElseBody.Any())
            {
                _w.WriteLine("Else");
                _w.Indent();

                foreach (var s in ifs.ElseBody)
                    GenerateStatement(s);

                _w.Unindent();
            }

            _w.WriteLine("End If");
        }

        private string GenerateExpression(Expression expr)
        {
            return expr switch
            {
                IdentifierExpression id => id.NameEn,
                LiteralExpression lit => lit.Value is string s ? $"\"{s}\"" : lit.Value!.ToString()!,
                BinaryExpression bin => $"{GenerateExpression(bin.Left)} {bin.Operator} {GenerateExpression(bin.Right)}",
                UnaryExpression un => $"{GenerateExpression(un.Operand)} {MapUnaryOperator(un.Operator)}",
                MemberAccessExpression ma => $"{GenerateExpression(ma.Expression)}.{ma.MemberName}",
                ElementAccessExpression ea => $"{GenerateExpression(ea.ArrayExpression)}({GenerateExpression(ea.IndexExpression)})",
                InvocationExpression inv => $"{GenerateExpression(inv.Target)}({string.Join(", ", inv.Arguments.Select(GenerateExpression))})",
                ArrayLiteralExpression arr => $"{{ {string.Join(", ", arr.Elements.Select(GenerateExpression))} }}",
                _ => "' 未対応式"
            };
        }

        private void WriteXmlComment(JavadocComment j, string? methodNameJa = null)
        {
            if (!string.IsNullOrEmpty(methodNameJa))
            {
                _w.WriteLine("''' <summary>");
                _w.WriteLine($"''' {methodNameJa}");
                _w.WriteLine("''' </summary>");
            }
            else if (!string.IsNullOrEmpty(j.Summary))
            {
                _w.WriteLine("''' <summary>");
                _w.WriteLine($"''' {j.Summary}");
                _w.WriteLine("''' </summary>");
            }

            foreach (var p in j.Params)
            {
                _w.WriteLine($"''' <param name=\"{p.NameEn}\">{p.NameJa}</param>");
            }
        }

        private string MapType(string t)
        {
            return t switch
            {
                "string" => "String",
                "string[]" => "String()",
                "int" => "Integer",
                "int[]" => "Integer()",
                "void" => "Void", // Function の場合は使わない想定
                _ => t
            };
        }

        private string MapModifier(string m)
        {
            return m switch
            {
                "public" => "Public",
                "private" => "Private",
                "internal" => "Friend",
                _ => m
            };
        }

        private string MapMethodModifier(string m)
        {
            return m switch
            {
                "static" => "Shared",
                "public" => "Public",
                "private" => "Private",
                "internal" => "Friend",
                _ => m
            };
        }

        private string MapUnaryOperator(string op)
        {
            return op switch
            {
                "++" => "+= 1",
                "--" => "-= 1",
                _ => op
            };
        }
    }
}

