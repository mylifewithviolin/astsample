using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ReMindAst;

namespace ReMindBackend
{
    public class JavaCodeGenerator
    {
        private static readonly HashSet<string> JavaReservedWords = new(StringComparer.Ordinal)
        {
            "abstract","assert","boolean","break","byte","case","catch","char","class","const","continue",
            "default","do","double","else","enum","extends","final","finally","float","for","goto","if",
            "implements","import","instanceof","int","interface","long","native","new","package","private",
            "protected","public","return","short","static","strictfp","super","switch","synchronized","this",
            "throw","throws","transient","try","void","volatile","while","true","false","null"
        };

        private readonly IndentWriter _w = new();
        private readonly MappingTable _mappingTable;
        private Dictionary<string, AliasClass> _aliasMap = new(StringComparer.Ordinal);

        public JavaCodeGenerator() : this(new MappingTable())
        {
        }

        public JavaCodeGenerator(MappingTable mappingTable)
        {
            _mappingTable = mappingTable;
        }

        public string Generate(CompilationUnit cu)
        {
            // imports → import System; など
            foreach (var imp in cu.Imports)
            {
                _w.WriteLine($"import {imp.Name};");
            }
            _w.WriteLine();

            // namespace → package
            foreach (var ns in cu.Namespaces)
            {
                _w.WriteLine($"package {NormalizeJavaPackageName(ns.Name)};");
                _w.WriteLine();

                foreach (var cls in ns.Classes)
                {
                    GenerateClass(cls);
                    _w.WriteLine();
                }
            }

            return _w.ToString();
        }

        public string Generate(ProgramIR program)
        {
            _aliasMap = program.AliasClasses.ToDictionary(alias => alias.OriginalName, StringComparer.Ordinal);
            foreach (var namespaceName in program.Namespaces)
            {
                _w.WriteLine($"package {NormalizeJavaPackageName(namespaceName)};");
                _w.WriteLine();
            }

            foreach (var import in program.Imports)
            {
                _w.WriteLine($"import {MapImport(import)};");
            }
            if (program.Imports.Count > 0)
            {
                _w.WriteLine();
            }

            foreach (var classIR in program.Classes)
            {
                GenerateClass(classIR);
                _w.WriteLine();
            }

            return _w.ToString();
        }

        private void GenerateClass(ClassIR classIR)
        {
            GenerateDocumentation(classIR.Documentation);
            var className = NormalizeJavaClassName(classIR.Name);
            var baseType = string.IsNullOrEmpty(classIR.BaseType) ? "" : $" extends {NormalizeJavaClassName(classIR.BaseType)}";
            _w.WriteLine($"public class {className}{baseType}");
            _w.WriteLine("{");
            _w.Indent();

            foreach (var field in classIR.Fields)
            {
                var fieldName = NormalizeJavaFieldName(field.Name);
                var declaration = field.Modifiers.Contains("const", StringComparer.Ordinal)
                    ? $"private static final {MapType(field.Type)} {fieldName} = {GenerateExpression(field.Initializer!)};"
                    : $"{string.Join(" ", field.Modifiers)} {MapType(field.Type)} {fieldName};";
                _w.WriteLine(declaration);
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
            var methodName = NormalizeJavaMethodName(methodIR.Name);
            var methodModifiers = string.Join(" ", methodIR.Modifiers.Select(modifier => NormalizeJavaIdentifier(modifier)));
            var modifierPrefix = string.IsNullOrEmpty(methodModifiers) ? "" : $"{methodModifiers} ";
            _w.WriteLine($"{modifierPrefix}{MapType(methodIR.ReturnType)} {methodName}({string.Join(", ", methodIR.Parameters.Select(MapParameter))})");
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
            switch (stmt)
            {
                case VariableDeclarationIR variable:
                    GenerateDocumentation(variable.Documentation);
                    var variableName = NormalizeJavaIdentifier(variable.Name);
                    var declarationPrefix = variable.IsConstant ? "final " : string.Empty;
                    _w.WriteLine($"{declarationPrefix}{MapType(variable.Type)} {variableName} = {GenerateExpression(variable.Initializer)};");
                    break;
                case AssignmentIR assignment:
                    var target = assignment.TargetExpression == null
                        ? NormalizeJavaIdentifier(assignment.Target)
                        : GenerateExpression(assignment.TargetExpression);
                    _w.WriteLine($"{target} = {GenerateExpression(assignment.Value)};");
                    break;
                case CallIR call:
                    _w.WriteLine($"{MapCall(call.MethodName)}({string.Join(", ", call.Arguments.Select(GenerateExpression))});");
                    break;
                case ExpressionStatementIR expression:
                    _w.WriteLine($"{GenerateExpression(expression.Expression)};");
                    break;
                case ReturnIR returnStatement:
                    _w.WriteLine(returnStatement.Value == null
                        ? "return;"
                        : $"return {GenerateExpression(returnStatement.Value)};");
                    break;
                case BreakIR:
                    _w.WriteLine("break;");
                    break;
                case ContinueIR:
                    _w.WriteLine("continue;");
                    break;
                case ThrowIR throwStatement:
                    _w.WriteLine($"throw {GenerateExpression(throwStatement.Value)};");
                    break;
                case SwitchIR switchStatement:
                    _w.WriteLine($"switch ({GenerateExpression(switchStatement.Expression)}) {{");
                    _w.Indent();
                    foreach (var switchCase in switchStatement.Cases)
                    {
                        _w.WriteLine(switchCase.Value == null
                            ? "default:"
                            : $"case {GenerateExpression(switchCase.Value)}:");
                        _w.Indent();
                        foreach (var nested in switchCase.Body.Statements)
                        {
                            GenerateStatement(nested);
                        }
                        _w.Unindent();
                    }
                    _w.Unindent();
                    _w.WriteLine("}");
                    break;
                case TryCatchIR tryCatch:
                    _w.WriteLine("try {");
                    _w.Indent();
                    foreach (var nested in tryCatch.TryBlock.Statements)
                    {
                        GenerateStatement(nested);
                    }
                    _w.Unindent();
                    _w.WriteLine("}");
                    foreach (var clause in tryCatch.CatchClauses)
                    {
                        _w.WriteLine($"catch ({clause.ExceptionType} {clause.VariableName}) {{");
                        _w.Indent();
                        foreach (var nested in clause.Body.Statements)
                        {
                            GenerateStatement(nested);
                        }
                        _w.Unindent();
                        _w.WriteLine("}");
                    }
                    if (tryCatch.FinallyBlock.Statements.Count > 0)
                    {
                        _w.WriteLine("finally {");
                        _w.Indent();
                        foreach (var nested in tryCatch.FinallyBlock.Statements)
                        {
                            GenerateStatement(nested);
                        }
                        _w.Unindent();
                        _w.WriteLine("}");
                    }
                    break;
                case IfIR conditional:
                    _w.WriteLine($"if ({GenerateExpression(conditional.Condition)})");
                    _w.WriteLine("{");
                    _w.Indent();
                    foreach (var nested in conditional.ThenBlock.Statements)
                    {
                        GenerateStatement(nested);
                    }
                    _w.Unindent();
                    _w.WriteLine("}");
                    if (conditional.ElseBlock != null && conditional.ElseBlock.Statements.Count > 0)
                    {
                        _w.WriteLine("else");
                        _w.WriteLine("{");
                        _w.Indent();
                        foreach (var nested in conditional.ElseBlock.Statements)
                        {
                            GenerateStatement(nested);
                        }
                        _w.Unindent();
                        _w.WriteLine("}");
                    }
                    break;
                case WhileIR loop:
                    _w.WriteLine($"while ({GenerateExpression(loop.Condition)})");
                    _w.WriteLine("{");
                    _w.Indent();
                    foreach (var nested in loop.Body.Statements)
                    {
                        GenerateStatement(nested);
                    }
                    _w.Unindent();
                    _w.WriteLine("}");
                    break;
                case DoWhileIR loop:
                    _w.WriteLine("do");
                    _w.WriteLine("{");
                    _w.Indent();
                    foreach (var nested in loop.Body.Statements)
                    {
                        GenerateStatement(nested);
                    }
                    _w.Unindent();
                    _w.WriteLine($"}} while ({GenerateExpression(loop.Condition)});");
                    break;
                case ForIR loop:
                    _w.WriteLine($"for ({GenerateInlineStatement(loop.Initializer)}; {GenerateExpression(loop.Condition)}; {GenerateExpression(loop.Iterator)})");
                    _w.WriteLine("{");
                    _w.Indent();
                    foreach (var nested in loop.Body.Statements)
                    {
                        GenerateStatement(nested);
                    }
                    _w.Unindent();
                    _w.WriteLine("}");
                    break;
            }
        }

        private string GenerateExpression(ExpressionIR expr)
        {
            return expr switch
            {
                IdentifierIR identifier => NormalizeJavaIdentifier(identifier.Name),
                LiteralIR literal => literal.Value,
                BinaryIR binary => $"{GenerateExpression(binary.Left)} {binary.Operator} {GenerateExpression(binary.Right)}",
                UnaryIR unary => GenerateUnaryExpression(unary),
                NewExpressionIR newExpression => $"new {newExpression.TypeName}({string.Join(", ", newExpression.Arguments.Select(argument => GenerateExpression(argument)))})",
                CallExpressionIR call when call.IsNullConditional => GenerateNullConditionalCall(call),
                CallExpressionIR call => $"{MapCall(call.MethodName)}({string.Join(", ", call.Arguments.Select(GenerateExpression))})",
                MemberAccessIR member => member.MemberName == "Length"
                    ? $"{GenerateExpression(member.Target)}.length"
                    : member.IsNullConditional
                        ? $"({GenerateExpression(member.Target)} == null ? null : {GenerateExpression(member.Target)}.{NormalizeJavaIdentifier(member.MemberName)})"
                        : $"{GenerateExpression(member.Target)}.{NormalizeJavaIdentifier(member.MemberName)}",
                ArrayAccessIR array => $"{GenerateExpression(array.Array)}[{GenerateExpression(array.Index)}]",
                ArrayLiteralIR array => $"new {MapType(array.ElementType)}[] {{ {string.Join(", ", array.Elements.Select(GenerateExpression))} }}",
                _ => string.Empty
            };
        }

        private string GenerateUnaryExpression(UnaryIR unary)
        {
            return unary.Operator == "++"
                ? $"{GenerateExpression(unary.Operand)}++"
                : $"{unary.Operator}{GenerateExpression(unary.Operand)}";
        }

        private string GenerateNullConditionalCall(CallExpressionIR call)
        {
            var separator = call.MethodName.IndexOf("?.", StringComparison.Ordinal);
            var separatorLength = separator >= 0 ? 2 : 1;
            if (separator < 0)
            {
                separator = call.MethodName.IndexOf('.', StringComparison.Ordinal);
            }
            var target = separator > 0 ? call.MethodName[..separator] : call.MethodName;
            var method = separator > 0 ? call.MethodName[(separator + separatorLength)..] : "";
            var arguments = string.Join(", ", call.Arguments.Select(argument => GenerateExpression(argument)));
            return $"({target} == null ? null : {target}.{method}({arguments}))";
        }

        private string GenerateInlineStatement(StatementIR statement)
        {
            return statement is VariableDeclarationIR variable
                ? $"{MapType(variable.Type)} {NormalizeJavaIdentifier(variable.Name)} = {GenerateExpression(variable.Initializer)}"
                : string.Empty;
        }

        private string MapParameter(string parameter)
        {
            var parts = parameter.Split(' ', 2);
            if (parts.Length == 2)
            {
                return $"{MapType(parts[0])} {NormalizeJavaIdentifier(parts[1])}";
            }
            return parameter;
        }

        private string MapCall(string methodName)
        {
            if (string.IsNullOrWhiteSpace(methodName))
            {
                return methodName;
            }

            if (string.Equals(methodName, "ConsoleOut", StringComparison.OrdinalIgnoreCase))
            {
                return "System.out.println";
            }

            var resolvedMethodName = ResolveAliasCall(methodName);
            return resolvedMethodName switch
            {
                "コンソール.一行表示する" => "System.out.println",
                "Console.WriteLine" => "System.out.println",
                "System.out.println" => "System.out.println",
                _ => NormalizeJavaMethodName(resolvedMethodName)
            };
        }

        private string ResolveAliasCall(string methodName)
        {
            var separator = methodName.IndexOf('.', StringComparison.Ordinal);
            if (separator <= 0)
            {
                return methodName;
            }

            var className = methodName[..separator];
            var memberName = methodName[(separator + 1)..];
            if (!_aliasMap.TryGetValue(className, out var aliasClass))
            {
                return methodName;
            }

            var aliasMember = aliasClass.Members.FirstOrDefault(member => member.OriginalName == memberName);
            return aliasMember == null
                ? methodName
                : $"{aliasClass.TranspiledName}.{aliasMember.TranspiledName}";
        }

        private string MapImport(string import)
        {
            return import == "System" ? "java.lang.System" : import;
        }

        private void GenerateDocumentation(DocumentationIR? documentation)
        {
            if (documentation == null)
            {
                return;
            }

            _w.WriteLine("/**");
            if (!string.IsNullOrEmpty(documentation.NameJa))
            {
                _w.WriteLine($" * {documentation.NameJa}");
            }
            foreach (var parameter in documentation.Parameters)
            {
                _w.WriteLine($" * @param {parameter.Name} {parameter.Description}");
            }
            _w.WriteLine(" */");
        }

        private void GenerateClass(ClassDeclaration cls)
        {
            if (cls.Javadoc != null)
                WriteJavadoc(cls.Javadoc);

            var mods = string.Join(" ", cls.Modifiers);
            if (!string.IsNullOrEmpty(mods))
                mods += " ";

            _w.WriteLine($"{mods}class {NormalizeJavaClassName(cls.NameEn)} " + "{");
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
                WriteJavadoc(m.Javadoc, m.NameJa);

            var mods = string.Join(" ", m.Modifiers);
            if (!string.IsNullOrEmpty(mods))
                mods += " ";

            var paramList = string.Join(", ",
                m.Parameters.Select(p => $"{MapType(p.Type)} {NormalizeJavaIdentifier(p.NameEn)}"));

            _w.WriteLine($"{mods}{MapType(m.ReturnType)} {NormalizeJavaMethodName(m.NameEn)}({paramList}) " + "{");
            _w.Indent();

            foreach (var stmt in m.Body)
                GenerateStatement(stmt);

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
                WriteJavadoc(v.Javadoc);

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
            var init = $"{MapType(fs.Initializer!.Type)} {fs.Initializer.NameEn} = {GenerateExpression(fs.Initializer.Initializer!)}";
            var cond = GenerateExpression(fs.Condition!);
            var iter = GenerateExpression(fs.Iterator!);

            _w.WriteLine($"for ({init}; {cond}; {iter}) " + "{");
            _w.Indent();

            foreach (var s in fs.Body)
                GenerateStatement(s);

            _w.Unindent();
            _w.WriteLine("}");
        }

        private void GenerateWhile(WhileStatement ws)
        {
            _w.WriteLine($"while ({GenerateExpression(ws.Condition)}) " + "{");
            _w.Indent();

            foreach (var s in ws.Body)
                GenerateStatement(s);

            _w.Unindent();
            _w.WriteLine("}");
        }

        private void GenerateIf(IfStatement ifs)
        {
            _w.WriteLine($"if ({GenerateExpression(ifs.Condition)}) " + "{");
            _w.Indent();

            foreach (var s in ifs.ThenBody)
                GenerateStatement(s);

            _w.Unindent();
            _w.WriteLine("}");

            if (ifs.ElseBody.Any())
            {
                _w.WriteLine("else {");
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
                MemberAccessExpression ma => GenerateMemberAccess(ma),
                ElementAccessExpression ea => $"{GenerateExpression(ea.ArrayExpression)}[{GenerateExpression(ea.IndexExpression)}]",
                InvocationExpression inv => $"{GenerateExpression(inv.Target)}({string.Join(", ", inv.Arguments.Select(GenerateExpression))})",
                ArrayLiteralExpression arr => $"new {MapType(arr.ElementType)}[] {{ {string.Join(", ", arr.Elements.Select(GenerateExpression))} }}",
                _ => "/* 未対応式 */"
            };
        }

        private string GenerateMemberAccess(MemberAccessExpression ma)
        {
            // C# AST → Java では配列 length の扱いに差異あり
            // array.length / list.size() 等だが、今回は配列 Length → length にだけ対応
            if (ma.Expression is IdentifierExpression id &&
                id.NameEn == "array" &&
                ma.MemberName == "Length")
            {
                return $"{id.NameEn}.length";
            }

            return $"{GenerateExpression(ma.Expression)}.{ma.MemberName}";
        }

        private void WriteJavadoc(JavadocComment j, string? methodNameJa = null)
        {
            _w.WriteLine("/**");

            if (!string.IsNullOrEmpty(methodNameJa))
            {
                // 関数名の注釈を日本語関数名にする
                _w.WriteLine($" * {methodNameJa}");
            }
            else if (!string.IsNullOrEmpty(j.Summary))
            {
                _w.WriteLine($" * {j.Summary}");
            }

            foreach (var p in j.Params)
            {
                // @param 英語 日本語
                _w.WriteLine($" * @param {p.NameEn} {p.NameJa}");
            }

            _w.WriteLine(" */");
        }

        private string MapType(string t)
        {
            return t switch
            {
                "string" => "String",
                "string?" => "String",
                "string[]" => "String[]",
                "string?[]" => "String[]",
                "int" => "int",
                "int[]" => "int[]",
                "bool" => "boolean",
                "void" => "void",
                _ => t
            };
        }

        private static string NormalizeJavaPackageName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "unnamed";
            }

            var trimmed = name.Trim();
            var normalized = new StringBuilder(trimmed.Length);
            foreach (var ch in trimmed)
            {
                if (char.IsLetterOrDigit(ch) || ch == '.')
                {
                    normalized.Append(ch);
                }
            }

            var parts = normalized.ToString().Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                parts[i] = NormalizeJavaIdentifier(parts[i], true, false);
            }

            return string.Join(".", parts);
        }

        private static string NormalizeJavaClassName(string name)
        {
            return NormalizeJavaIdentifier(name, false, true);
        }

        private static string NormalizeJavaMethodName(string name)
        {
            if (string.Equals(name, "Main", StringComparison.Ordinal))
            {
                return "main";
            }

            if (string.Equals(name, "ConsoleOut", StringComparison.OrdinalIgnoreCase))
            {
                return "consoleOut";
            }

            if (string.Equals(name, "BubbleSort", StringComparison.OrdinalIgnoreCase))
            {
                return "bubbleSort";
            }

            return NormalizeJavaIdentifier(name, false, false);
        }

        private static string NormalizeJavaFieldName(string name)
        {
            return NormalizeJavaIdentifier(name, false, false);
        }

        private static string NormalizeJavaIdentifier(string name, bool isPackageName = false, bool isClassName = false)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return isPackageName ? "unnamed" : "value";
            }

            var builder = new StringBuilder(name.Length);
            foreach (var ch in name.Trim())
            {
                if (char.IsLetterOrDigit(ch) || ch == '_')
                {
                    builder.Append(ch);
                }
            }

            var result = builder.ToString();
            if (string.IsNullOrEmpty(result))
            {
                return isPackageName ? "unnamed" : "value";
            }

            if (char.IsDigit(result[0]))
            {
                result = "_" + result;
            }

            if (JavaReservedWords.Contains(result))
            {
                result = "_" + result;
            }

            if (isPackageName)
            {
                return result.ToLowerInvariant();
            }

            if (isClassName)
            {
                if (char.IsLower(result[0]))
                {
                    result = char.ToUpperInvariant(result[0]) + result.Substring(1);
                }
                return result;
            }

            if (string.IsNullOrEmpty(result))
            {
                return "value";
            }

            if (result.Length > 1 && char.IsUpper(result[0]) && char.IsUpper(result[1]))
            {
                // acronym-like names (e.g. URL) stay mostly intact, but method names become lower camel.
                result = char.ToLowerInvariant(result[0]) + result.Substring(1);
                return result;
            }

            if (char.IsUpper(result[0]))
            {
                result = char.ToLowerInvariant(result[0]) + result.Substring(1);
            }

            return result;
        }
    }
}


