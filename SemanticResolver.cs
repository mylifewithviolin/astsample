using System;
using System.Collections.Generic;
using System.Linq;
using ReMindAst;

namespace ReMindParser
{
    /// <summary>
    /// 意味解析と名前解決を行うクラス
    /// AST に対してシンボルテーブルを構築し、型チェックと名前解決を実行する
    /// </summary>
    public class SemanticResolver
    {
        private CompilationUnit _ast = null!;
        private SymbolTable _symbolTable = null!;
        private Scope _currentScope = null!;
        private readonly Dictionary<string, MethodDeclaration> _methods = new(StringComparer.Ordinal);

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="ast">解析対象の CompilationUnit</param>
        public SemanticResolver(CompilationUnit ast)
        {
            _ast = ast;
            _symbolTable = new SymbolTable();
            _currentScope = _symbolTable.GlobalScope;
        }

        /// <summary>
        /// AST 全体を意味解析する
        /// </summary>
        public void Resolve()
        {
            foreach (var namespaceDeclaration in _ast.Namespaces)
            {
                foreach (var classDeclaration in namespaceDeclaration.Classes)
                {
                    _methods.Clear();
                    foreach (var method in classDeclaration.Methods)
                    {
                        _methods[method.NameJa] = method;
                        _methods[method.NameEn] = method;
                    }

                    foreach (var method in classDeclaration.Methods)
                    {
                        ResolveMethod(classDeclaration, method);
                    }
                }
            }
        }

        private void ResolveMethod(ClassDeclaration classDeclaration, MethodDeclaration method)
        {
            var symbols = new Dictionary<string, Symbol>(StringComparer.Ordinal);
            foreach (var field in classDeclaration.Fields)
            {
                var kind = field.Modifiers.Contains("const", StringComparer.Ordinal)
                    ? SymbolKind.Constant
                    : SymbolKind.Field;
                symbols[field.NameJa] = new Symbol(field.NameJa, field.Type, kind);
            }
            foreach (var parameter in method.Parameters)
            {
                symbols[parameter.NameJa] = new Symbol(parameter.NameJa, parameter.Type, SymbolKind.Parameter);
            }
            ResolveStatements(method.Body, symbols, method.ReturnType);
        }

        private void ResolveStatements(IEnumerable<Statement> statements, Dictionary<string, Symbol> symbols, string returnType)
        {
            foreach (var statement in statements)
            {
                switch (statement)
                {
                    case LocalVariableDeclaration localVariable:
                        if (localVariable.IsConstant && localVariable.Type.EndsWith("[]", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException($"Constant array '{localVariable.NameJa}' is not supported for local declarations.");
                        }
                        if (localVariable.Initializer != null)
                        {
                            if (localVariable.Initializer is NewExpression newExpression && string.IsNullOrEmpty(newExpression.TypeName))
                            {
                                newExpression.TypeName = localVariable.Type;
                            }
                            ValidateExpression(localVariable.Initializer, symbols);
                            RequireAssignable(localVariable.Type, InferType(localVariable.Initializer, symbols), localVariable.NameJa);
                        }
                        symbols[localVariable.NameJa] = new Symbol(localVariable.NameJa, localVariable.Type,
                            localVariable.IsConstant ? SymbolKind.Constant : SymbolKind.Variable);
                        break;
                    case AssignmentStatement assignment:
                        ValidateAssignment(assignment, symbols);
                        break;
                    case ExpressionStatement expressionStatement:
                        ValidateExpression(expressionStatement.Expression, symbols);
                        break;
                    case IfStatement ifStatement:
                        RequireBoolean(ifStatement.Condition, symbols, "If");
                        ResolveStatements(ifStatement.ThenBody, new Dictionary<string, Symbol>(symbols, StringComparer.Ordinal), returnType);
                        ResolveStatements(ifStatement.ElseBody, new Dictionary<string, Symbol>(symbols, StringComparer.Ordinal), returnType);
                        break;
                    case WhileStatement whileStatement:
                        RequireBoolean(whileStatement.Condition, symbols, "While");
                        ResolveStatements(whileStatement.Body, new Dictionary<string, Symbol>(symbols, StringComparer.Ordinal), returnType);
                        break;
                    case DoWhileStatement doWhileStatement:
                        RequireBoolean(doWhileStatement.Condition, symbols, "DoWhile");
                        ResolveStatements(doWhileStatement.Body, new Dictionary<string, Symbol>(symbols, StringComparer.Ordinal), returnType);
                        break;
                    case ForStatement forStatement:
                        var loopSymbols = new Dictionary<string, Symbol>(symbols, StringComparer.Ordinal);
                        if (forStatement.Initializer != null)
                        {
                            ValidateExpression(forStatement.Initializer.Initializer!, loopSymbols);
                            RequireAssignable(forStatement.Initializer.Type, InferType(forStatement.Initializer.Initializer!, loopSymbols), forStatement.Initializer.NameJa);
                            loopSymbols[forStatement.Initializer.NameJa] = new Symbol(forStatement.Initializer.NameJa, forStatement.Initializer.Type, SymbolKind.Variable);
                        }
                        if (forStatement.Condition != null) RequireBoolean(forStatement.Condition, loopSymbols, "For");
                        if (forStatement.Iterator != null) ValidateExpression(forStatement.Iterator, loopSymbols);
                        ResolveStatements(forStatement.Body, loopSymbols, returnType);
                        break;
                    case ReturnStatement returnStatement:
                        if (returnStatement.Value != null)
                        {
                            ValidateExpression(returnStatement.Value, symbols);
                            RequireAssignable(returnType, InferType(returnStatement.Value, symbols), "return");
                        }
                        else if (!string.Equals(returnType, "void", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException($"Return type mismatch: method returns '{returnType}' but no value was returned.");
                        }
                        break;
                    case ThrowStatement throwStatement:
                        ValidateExpression(throwStatement.Value, symbols);
                        break;
                    case SwitchStatement switchStatement:
                        ValidateExpression(switchStatement.Expression, symbols);
                        foreach (var switchCase in switchStatement.Cases)
                        {
                            if (switchCase.Value != null) ValidateExpression(switchCase.Value, symbols);
                            ResolveStatements(switchCase.Body, new Dictionary<string, Symbol>(symbols, StringComparer.Ordinal), returnType);
                        }
                        break;
                    case TryCatchStatement tryCatchStatement:
                        ResolveStatements(tryCatchStatement.TryBody, new Dictionary<string, Symbol>(symbols, StringComparer.Ordinal), returnType);
                        foreach (var clause in tryCatchStatement.CatchClauses)
                        {
                            ResolveStatements(clause.Body, new Dictionary<string, Symbol>(symbols, StringComparer.Ordinal), returnType);
                        }
                        ResolveStatements(tryCatchStatement.FinallyBody, new Dictionary<string, Symbol>(symbols, StringComparer.Ordinal), returnType);
                        break;
                }
            }
        }

        private void ValidateAssignment(AssignmentStatement assignment, Dictionary<string, Symbol> symbols)
        {
            if (assignment.Left is not IdentifierExpression identifier || !symbols.TryGetValue(identifier.NameJa, out var target))
            {
                throw new InvalidOperationException($"Undefined identifier '{GetExpressionName(assignment.Left)}'.");
            }
            if (target.Kind == SymbolKind.Constant)
            {
                throw new InvalidOperationException($"Cannot reassign constant '{identifier.NameJa}'.");
            }

            ValidateExpression(assignment.Right, symbols);
            RequireAssignable(target.Type, InferType(assignment.Right, symbols), identifier.NameJa);
            identifier.NameEn = ResolveTargetName(identifier.NameJa, identifier.NameEn);
        }

        private void RequireBoolean(Expression expression, Dictionary<string, Symbol> symbols, string context)
        {
            ValidateExpression(expression, symbols);
            var actualType = InferType(expression, symbols);
            if (!string.Equals(actualType, "bool", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{context} condition must be 'bool', but was '{actualType}'.");
            }
        }

        private void ValidateExpression(Expression expression, Dictionary<string, Symbol> symbols)
        {
            switch (expression)
            {
                case IdentifierExpression identifier:
                    if (!symbols.TryGetValue(identifier.NameJa, out var symbol))
                    {
                        throw new InvalidOperationException($"Undefined identifier '{identifier.NameJa}'.");
                    }
                    identifier.NameEn = ResolveTargetName(identifier.NameJa, identifier.NameEn);
                    break;
                case BinaryExpression binary:
                    ValidateExpression(binary.Left, symbols);
                    ValidateExpression(binary.Right, symbols);
                    ValidateBinaryExpression(binary, symbols);
                    break;
                case UnaryExpression unary:
                    ValidateExpression(unary.Operand, symbols);
                    if (unary.Operator == "!" && !string.Equals(InferType(unary.Operand, symbols), "bool", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Operator '!' requires a 'bool' operand.");
                    }
                    break;
                case InvocationExpression invocation:
                    ValidateInvocation(invocation, symbols);
                    break;
                case MemberAccessExpression memberAccess:
                    ValidateMemberAccess(memberAccess, symbols);
                    break;
                case ElementAccessExpression elementAccess:
                    ValidateExpression(elementAccess.ArrayExpression, symbols);
                    ValidateExpression(elementAccess.IndexExpression, symbols);
                    RequireAssignable("int", InferType(elementAccess.IndexExpression, symbols), "array index");
                    break;
                case ArrayLiteralExpression arrayLiteral:
                    foreach (var element in arrayLiteral.Elements)
                    {
                        ValidateExpression(element, symbols);
                    }
                    break;
            }
        }

        private void ValidateBinaryExpression(BinaryExpression binary, Dictionary<string, Symbol> symbols)
        {
            var leftType = InferType(binary.Left, symbols);
            var rightType = InferType(binary.Right, symbols);
            if (binary.Operator is "&&" or "||")
            {
                if (leftType != "bool" || rightType != "bool")
                {
                    throw new InvalidOperationException($"Operator '{binary.Operator}' requires two 'bool' operands.");
                }
                return;
            }

            if (binary.Operator == "??")
            {
                return;
            }

            if (binary.Operator is "==" or "!=" or "<" or ">" or "<=" or ">=")
            {
                if (leftType != rightType)
                {
                    throw new InvalidOperationException($"Operator '{binary.Operator}' cannot compare '{leftType}' and '{rightType}'.");
                }
                return;
            }

            if (leftType != "int" || rightType != "int")
            {
                throw new InvalidOperationException($"Operator '{binary.Operator}' requires two 'int' operands.");
            }
        }

        private void ValidateInvocation(InvocationExpression invocation, Dictionary<string, Symbol> symbols)
        {
            foreach (var argument in invocation.Arguments)
            {
                ValidateExpression(argument, symbols);
            }

            if (invocation.Target is MemberAccessExpression memberAccess && IsConsoleWriteLine(memberAccess))
            {
                return;
            }

            if (invocation.Target is MemberAccessExpression aliasedMember &&
                (IsAliasMember(aliasedMember) || IsInstanceMethod(aliasedMember, symbols) || IsBuiltInMember(aliasedMember.MemberName)))
            {
                return;
            }

            if (invocation.Target is not IdentifierExpression identifier || !_methods.TryGetValue(identifier.NameJa, out var method))
            {
                throw new InvalidOperationException($"Undefined method '{GetExpressionName(invocation.Target)}'.");
            }
            if (method.Parameters.Count != invocation.Arguments.Count)
            {
                throw new InvalidOperationException($"Method '{identifier.NameJa}' expects {method.Parameters.Count} arguments but received {invocation.Arguments.Count}.");
            }

            for (var index = 0; index < method.Parameters.Count; index++)
            {
                RequireAssignable(method.Parameters[index].Type, InferType(invocation.Arguments[index], symbols), $"argument {index + 1} of '{identifier.NameJa}'");
            }
            identifier.NameEn = method.NameEn;
        }

        private void ValidateMemberAccess(MemberAccessExpression memberAccess, Dictionary<string, Symbol> symbols)
        {
            if (IsConsoleWriteLine(memberAccess))
            {
                return;
            }
            ValidateExpression(memberAccess.Expression, symbols);
            if (memberAccess.Expression is IdentifierExpression identifier &&
                symbols.TryGetValue(identifier.NameJa, out var symbol) &&
                GetFieldType(symbol.Type, memberAccess.MemberName) == null &&
                !IsBuiltInMember(memberAccess.MemberName))
            {
                throw new InvalidOperationException($"Undefined member '{memberAccess.MemberName}' on '{symbol.Type}'.");
            }
        }

        private static bool IsConsoleWriteLine(MemberAccessExpression memberAccess)
        {
            return memberAccess.Expression is IdentifierExpression identifier &&
                identifier.NameJa == "コンソール" &&
                (memberAccess.MemberName == "WriteLine" || memberAccess.MemberName == "一行表示する");
        }

        private string InferType(Expression expression, Dictionary<string, Symbol> symbols)
        {
            return expression switch
            {
                LiteralExpression literal when literal.Value is bool => "bool",
                LiteralExpression literal when literal.Value is int => "int",
                LiteralExpression literal when literal.Value is string => "string",
                NullLiteralExpression => "null",
                NewExpression newExpression => newExpression.TypeName,
                IdentifierExpression identifier when symbols.TryGetValue(identifier.NameJa, out var symbol) => symbol.Type,
                BinaryExpression binary when binary.Operator is "==" or "!=" or "<" or ">" or "<=" or ">=" or "&&" or "||" => "bool",
                BinaryExpression binary when binary.Operator == "??" => InferType(binary.Right, symbols),
                BinaryExpression binary => InferType(binary.Left, symbols),
                UnaryExpression unary when unary.Operator == "!" => "bool",
                UnaryExpression unary => InferType(unary.Operand, symbols),
                InvocationExpression invocation when invocation.Target is IdentifierExpression identifier && _methods.TryGetValue(identifier.NameJa, out var method) => method.ReturnType,
                InvocationExpression invocation when invocation.Target is MemberAccessExpression memberAccess && IsAliasMember(memberAccess) => GetAliasMember(memberAccess).ReturnType,
                InvocationExpression invocation when invocation.Target is MemberAccessExpression memberAccess && IsBuiltInMember(memberAccess.MemberName) => "string",
                InvocationExpression invocation when invocation.Target is MemberAccessExpression memberAccess => InferInstanceMethodType(memberAccess, symbols) ?? "object",
                MemberAccessExpression memberAccess when memberAccess.Expression is IdentifierExpression identifier && symbols.TryGetValue(identifier.NameJa, out var symbol) => GetFieldType(symbol.Type, memberAccess.MemberName) ?? "object",
                ElementAccessExpression elementAccess => InferType(elementAccess.ArrayExpression, symbols).TrimEnd('[', ']'),
                ArrayLiteralExpression array => $"{array.ElementType}[]",
                _ => "object"
            };
        }

        private static void RequireAssignable(string expectedType, string actualType, string targetName)
        {
            if (actualType == "null" && expectedType.EndsWith("?", StringComparison.Ordinal))
            {
                return;
            }
            if (expectedType.EndsWith("?", StringComparison.Ordinal) &&
                string.Equals(expectedType.TrimEnd('?'), actualType, StringComparison.Ordinal))
            {
                return;
            }
            if (!string.Equals(expectedType, actualType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Type mismatch for '{targetName}': expected '{expectedType}' but was '{actualType}'.");
            }
        }

        private bool IsAliasMember(MemberAccessExpression memberAccess)
        {
            return memberAccess.Expression is IdentifierExpression identifier &&
                _ast.Imports.SelectMany(import => import.AliasClasses)
                    .Where(alias => alias.OriginalName == identifier.NameJa)
                    .SelectMany(alias => alias.Members)
                    .Any(member => member.OriginalName == memberAccess.MemberName);
        }

        private AliasMember GetAliasMember(MemberAccessExpression memberAccess)
        {
            var identifier = (IdentifierExpression)memberAccess.Expression;
            return _ast.Imports.SelectMany(import => import.AliasClasses)
                .Where(alias => alias.OriginalName == identifier.NameJa)
                .SelectMany(alias => alias.Members)
                .First(member => member.OriginalName == memberAccess.MemberName);
        }

        private bool IsInstanceMethod(MemberAccessExpression memberAccess, Dictionary<string, Symbol> symbols)
        {
            return memberAccess.Expression is IdentifierExpression identifier &&
                symbols.TryGetValue(identifier.NameJa, out var symbol) &&
                FindMethod(symbol.Type, memberAccess.MemberName) != null;
        }

        private string? InferInstanceMethodType(MemberAccessExpression memberAccess, Dictionary<string, Symbol> symbols)
        {
            if (memberAccess.Expression is not IdentifierExpression identifier || !symbols.TryGetValue(identifier.NameJa, out var symbol)) return null;
            return FindMethod(symbol.Type, memberAccess.MemberName);
        }

        private string? GetFieldType(string typeName, string fieldName)
        {
            return _ast.Namespaces.SelectMany(ns => ns.Classes)
                .FirstOrDefault(cls => cls.NameJa == typeName || cls.NameEn == typeName)?
                .Fields.FirstOrDefault(field => field.NameJa == fieldName || field.NameEn == fieldName)?.Type;
        }

        private string? FindMethod(string typeName, string methodName)
        {
            return _ast.Namespaces.SelectMany(ns => ns.Classes)
                .FirstOrDefault(cls => cls.NameJa == typeName || cls.NameEn == typeName)?
                .Methods.FirstOrDefault(method => method.NameJa == methodName || method.NameEn == methodName)?.ReturnType;
        }

            private static bool IsBuiltInMember(string memberName)
            {
                return memberName is "ToString" or "toString" or "Length";
            }

        private static string GetExpressionName(Expression expression)
        {
            return expression is IdentifierExpression identifier ? identifier.NameJa : expression.GetType().Name;
        }

        private static string ResolveTargetName(string nameJa, string currentNameEn)
        {
            return string.IsNullOrEmpty(currentNameEn) ? nameJa : currentNameEn;
        }

        private void ResolveExpressionNames(Expression expression, IReadOnlyDictionary<string, string> names)
        {
            switch (expression)
            {
                case IdentifierExpression identifier when names.TryGetValue(identifier.NameJa, out var resolvedName):
                    identifier.NameEn = resolvedName;
                    break;
                case BinaryExpression binary:
                    ResolveExpressionNames(binary.Left, names);
                    ResolveExpressionNames(binary.Right, names);
                    break;
                case UnaryExpression unary:
                    ResolveExpressionNames(unary.Operand, names);
                    break;
                case MemberAccessExpression memberAccess:
                    ResolveExpressionNames(memberAccess.Expression, names);
                    break;
                case ElementAccessExpression elementAccess:
                    ResolveExpressionNames(elementAccess.ArrayExpression, names);
                    ResolveExpressionNames(elementAccess.IndexExpression, names);
                    break;
                case InvocationExpression invocation:
                    ResolveExpressionNames(invocation.Target, names);
                    foreach (var argument in invocation.Arguments)
                    {
                        ResolveExpressionNames(argument, names);
                    }
                    break;
                case ArrayLiteralExpression arrayLiteral:
                    foreach (var element in arrayLiteral.Elements)
                    {
                        ResolveExpressionNames(element, names);
                    }
                    break;
            }
        }

        /// <summary>
        /// 式を意味解析する
        /// </summary>
        /// <param name="expression">解析対象の式</param>
        public void ResolveExpression(Expression expression)
        {
            // TODO: 式の型チェックと名前解決を実行
        }

        /// <summary>
        /// ステートメントを意味解析する
        /// </summary>
        /// <param name="statement">解析対象のステートメント</param>
        public void ResolveStatement(Statement statement)
        {
            // TODO: ステートメントの意味解析を実行
        }

        /// <summary>
        /// 識別子の解決を行う
        /// スコープから変数名を検索し、見つからない場合はエラーを返す
        /// </summary>
        public Symbol ResolveIdentifier(string identifierName)
        {
            var symbol = _currentScope.LookupSymbol(identifierName);
            if (symbol != null)
            {
                return symbol;
            }

            throw new InvalidOperationException($"Undefined identifier '{identifierName}' in current scope.");
        }

        /// <summary>
        /// ImportDeclaration / AliasClass / AliasMember を参照して、
        /// 日本語名を英語名へ変換する最小実装。
        /// 例: コンソール -> Console
        /// </summary>
        public string ResolveAliasName(string name)
        {
            foreach (var import in _ast.Imports)
            {
                foreach (var aliasClass in import.AliasClasses)
                {
                    if (string.Equals(aliasClass.OriginalName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return aliasClass.TranspiledName;
                    }

                    foreach (var aliasMember in aliasClass.Members)
                    {
                        if (string.Equals(aliasMember.OriginalName, name, StringComparison.OrdinalIgnoreCase))
                        {
                            return aliasMember.TranspiledName;
                        }
                    }
                }
            }

            return name;
        }

        /// <summary>
        /// MemberAccessExpression の Target が AliasClass の場合、
        /// AliasMember の英語名へ変換する最小実装。
        /// 例: コンソール.一行表示する -> Console.WriteLine
        /// </summary>
        public string ResolveMember(MemberAccessExpression memberAccess)
        {
            if (memberAccess.Expression is IdentifierExpression targetIdentifier)
            {
                var targetName = ResolveAliasName(targetIdentifier.NameJa);

                foreach (var import in _ast.Imports)
                {
                    foreach (var aliasClass in import.AliasClasses)
                    {
                        if (string.Equals(aliasClass.TranspiledName, targetName, StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var aliasMember in aliasClass.Members)
                            {
                                if (string.Equals(aliasMember.OriginalName, memberAccess.MemberName, StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(aliasMember.TranspiledName, memberAccess.MemberName, StringComparison.OrdinalIgnoreCase))
                                {
                                    return $"{aliasClass.TranspiledName}.{aliasMember.TranspiledName}";
                                }
                            }
                        }
                    }
                }
            }

            return $"{memberAccess.Expression}";
        }

        /// <summary>
        /// InvocationExpression の Target が解決済みのメンバーであることを確認し、
        /// 引数の型を解決する最小実装。
        /// </summary>
        public string ResolveInvocation(InvocationExpression invocation)
        {
            var targetText = invocation.Target.ToString() ?? string.Empty;
            var resolvedTarget = targetText;

            if (invocation.Target is MemberAccessExpression memberAccess)
            {
                resolvedTarget = ResolveMember(memberAccess);
            }
            else if (invocation.Target is IdentifierExpression identifier)
            {
                resolvedTarget = ResolveIdentifier(identifier.NameJa).Name;
            }

            foreach (var argument in invocation.Arguments)
            {
                if (argument is IdentifierExpression argumentIdentifier)
                {
                    var argumentSymbol = ResolveIdentifier(argumentIdentifier.NameJa);
                    _ = argumentSymbol.Type;
                }
            }

            return resolvedTarget;
        }

        /// <summary>
        /// 基本的な型推論を行う最小実装。
        /// Number -> int, String -> string, Identifier -> 変数の型
        /// </summary>
        public string InferType(Expression expression)
        {
            if (expression is LiteralExpression literal)
            {
                if (literal.Value is int || literal.Value is long || literal.Value is short || literal.Value is byte)
                {
                    return "int";
                }

                if (literal.Value is string)
                {
                    return "string";
                }

                if (literal.Value is bool)
                {
                    return "bool";
                }

                return "object";
            }

            if (expression is IdentifierExpression identifierExpression)
            {
                var symbol = ResolveIdentifier(identifierExpression.NameJa);
                return symbol?.Type ?? "object";
            }

            if (expression is BinaryExpression binaryExpression)
            {
                var leftType = InferType(binaryExpression.Left);
                var rightType = InferType(binaryExpression.Right);
                if (leftType == rightType)
                {
                    return leftType;
                }

                return "object";
            }

            if (expression is UnaryExpression unaryExpression)
            {
                return InferType(unaryExpression.Operand);
            }

            return "object";
        }

        /// <summary>
        /// Assignment の意味解析を行う。
        /// 左辺と右辺の型が一致するかを確認する最小実装。
        /// </summary>
        public bool ResolveAssignment(AssignmentStatement assignment)
        {
            var leftType = InferType(assignment.Left);
            var rightType = InferType(assignment.Right);

            if (string.Equals(leftType, rightType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            throw new InvalidOperationException($"Type mismatch in assignment: left is '{leftType}', right is '{rightType}'.");
        }

        /// <summary>
        /// IfStatement（◇構文）の意味解析を行う。
        /// 条件式が bool 型であることを確認する最小実装。
        /// </summary>
        public bool ResolveIfStatement(IfStatement ifStatement)
        {
            var conditionType = InferType(ifStatement.Condition);

            if (string.Equals(conditionType, "bool", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            throw new InvalidOperationException($"If condition must be bool, but was '{conditionType}'.");
        }

        /// <summary>
        /// LoopStatement（〇構文）の意味解析を行う。
        /// 条件式が bool 型であることを確認する最小実装。
        /// </summary>
        public bool ResolveLoopStatement(WhileStatement whileStatement)
        {
            var conditionType = InferType(whileStatement.Condition);

            if (string.Equals(conditionType, "bool", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            throw new InvalidOperationException($"Loop condition must be bool, but was '{conditionType}'.");
        }
    }

    /// <summary>
    /// シンボルテーブル
    /// 変数・メソッド・クラス・名前空間などのシンボル情報を管理する
    /// </summary>
    public class SymbolTable
    {
        private Scope _globalScope;
        private readonly Dictionary<string, Symbol> _variables = new();
        private readonly Dictionary<string, Symbol> _methods = new();
        private readonly Dictionary<string, Symbol> _classes = new();

        public Scope GlobalScope { get { return _globalScope; } }

        public Dictionary<string, Symbol> Variables => _variables;
        public Dictionary<string, Symbol> Methods => _methods;
        public Dictionary<string, Symbol> Classes => _classes;

        public SymbolTable()
        {
            _globalScope = new GlobalScope();
        }

        public void RegisterVariable(string name, Symbol symbol)
        {
            _variables[name] = symbol;
        }

        public Symbol? LookupVariable(string name)
        {
            return _variables.TryGetValue(name, out var symbol) ? symbol : null;
        }

        public void RegisterMethod(string name, Symbol symbol)
        {
            _methods[name] = symbol;
        }

        public Symbol? LookupMethod(string name)
        {
            return _methods.TryGetValue(name, out var symbol) ? symbol : null;
        }

        public void RegisterClass(string name, Symbol symbol)
        {
            _classes[name] = symbol;
        }

        public Symbol? LookupClass(string name)
        {
            return _classes.TryGetValue(name, out var symbol) ? symbol : null;
        }
    }

    /// <summary>
    /// スコープの基底クラス
    /// 変数やメソッドのシンボル情報を保持する
    /// </summary>
    public abstract class Scope
    {
        protected Dictionary<string, Symbol> _symbols = new();
        protected Scope? _parentScope;

        public Scope ParentScope => _parentScope!;

        public Scope(Scope? parentScope = null)
        {
            _parentScope = parentScope;
        }

        /// <summary>
        /// シンボルを登録する
        /// </summary>
        public void DefineSymbol(string name, Symbol symbol)
        {
            _symbols[name] = symbol;
        }

        /// <summary>
        /// ローカル変数を登録する
        /// </summary>
        public void DefineLocalVariable(string name, Symbol symbol)
        {
            DefineSymbol(name, symbol);
        }

        /// <summary>
        /// シンボルを検索する
        /// </summary>
        public Symbol? LookupSymbol(string name)
        {
            if (_symbols.TryGetValue(name, out var symbol))
            {
                return symbol;
            }

            if (_parentScope != null)
            {
                return _parentScope.LookupSymbol(name);
            }

            return null;
        }

        /// <summary>
        /// ローカル変数を検索する
        /// </summary>
        public Symbol? LookupLocalVariable(string name)
        {
            if (_symbols.TryGetValue(name, out var symbol))
            {
                return symbol;
            }

            if (_parentScope != null)
            {
                return _parentScope.LookupLocalVariable(name);
            }

            return null;
        }
    }

    /// <summary>
    /// グローバルスコープ
    /// プログラム全体の名前空間・クラス・グローバル関数を管理
    /// </summary>
    public class GlobalScope : Scope
    {
        public GlobalScope() : base(null)
        {
        }
    }

    /// <summary>
    /// ブロックスコープ
    /// if / while / for などのブロック内のローカル変数を管理
    /// </summary>
    public class BlockScope : Scope
    {
        public BlockScope(Scope parentScope) : base(parentScope)
        {
        }
    }

    /// <summary>
    /// メソッドスコープ
    /// メソッド内のパラメータとローカル変数を管理
    /// </summary>
    public class MethodScope : Scope
    {
        public MethodScope(Scope parentScope) : base(parentScope)
        {
        }
    }

    /// <summary>
    /// クラススコープ
    /// クラスのフィールドとメソッドを管理
    /// </summary>
    public class ClassScope : Scope
    {
        public ClassScope(Scope parentScope) : base(parentScope)
        {
        }
    }

    /// <summary>
    /// シンボル
    /// 変数・メソッド・クラスなどの情報を保持
    /// </summary>
    public class Symbol
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public SymbolKind Kind { get; set; }

        public Symbol(string name, string type, SymbolKind kind)
        {
            Name = name;
            Type = type;
            Kind = kind;
        }
    }

    /// <summary>
    /// シンボルの種類
    /// </summary>
    public enum SymbolKind
    {
        Variable,
        Parameter,
        Method,
        Class,
        Namespace,
        Field,
        Constant
    }
}
