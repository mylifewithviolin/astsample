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
            return string.Empty;
        }

        private void GenerateClass(ClassIR classIR)
        {
        }

        private void GenerateMethod(MethodIR methodIR)
        {
        }

        private void GenerateStatement(StatementIR stmt)
        {
        }

        private string GenerateExpression(ExpressionIR expr)
        {
            return string.Empty;
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
            var startExpr = GenerateExpression(fs.Initializer.Initializer);

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

