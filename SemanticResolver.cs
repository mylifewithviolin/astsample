using System;
using System.Collections.Generic;
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
                    foreach (var method in classDeclaration.Methods)
                    {
                        ResolveMethod(method);
                    }
                }
            }
        }

        private void ResolveMethod(MethodDeclaration method)
        {
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var parameter in method.Parameters)
            {
                names[parameter.NameJa] = parameter.NameEn;
            }
            ResolveStatements(method.Body, names);
        }

        private void ResolveStatements(IEnumerable<Statement> statements, Dictionary<string, string> names)
        {
            foreach (var statement in statements)
            {
                switch (statement)
                {
                    case LocalVariableDeclaration localVariable:
                        if (localVariable.Initializer != null)
                        {
                            ResolveExpressionNames(localVariable.Initializer, names);
                        }
                        names[localVariable.NameJa] = localVariable.NameEn;
                        break;
                    case AssignmentStatement assignment:
                        ResolveExpressionNames(assignment.Left, names);
                        ResolveExpressionNames(assignment.Right, names);
                        break;
                    case ExpressionStatement expressionStatement:
                        ResolveExpressionNames(expressionStatement.Expression, names);
                        break;
                    case IfStatement ifStatement:
                        ResolveExpressionNames(ifStatement.Condition, names);
                        ResolveStatements(ifStatement.ThenBody, new Dictionary<string, string>(names, StringComparer.Ordinal));
                        ResolveStatements(ifStatement.ElseBody, new Dictionary<string, string>(names, StringComparer.Ordinal));
                        break;
                    case WhileStatement whileStatement:
                        ResolveExpressionNames(whileStatement.Condition, names);
                        ResolveStatements(whileStatement.Body, new Dictionary<string, string>(names, StringComparer.Ordinal));
                        break;
                    case ForStatement forStatement:
                        var loopNames = new Dictionary<string, string>(names, StringComparer.Ordinal);
                        if (forStatement.Initializer != null)
                        {
                            ResolveExpressionNames(forStatement.Initializer.Initializer!, loopNames);
                            loopNames[forStatement.Initializer.NameJa] = forStatement.Initializer.NameEn;
                        }
                        if (forStatement.Condition != null) ResolveExpressionNames(forStatement.Condition, loopNames);
                        if (forStatement.Iterator != null) ResolveExpressionNames(forStatement.Iterator, loopNames);
                        ResolveStatements(forStatement.Body, loopNames);
                        break;
                    case ReturnStatement returnStatement:
                        if (returnStatement.Value != null)
                        {
                            ResolveExpressionNames(returnStatement.Value, names);
                        }
                        break;
                }
            }
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
        public Symbol? ResolveIdentifier(string identifierName)
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
            var targetText = invocation.Target.ToString();
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
