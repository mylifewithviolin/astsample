using System;
using System.Collections.Generic;

namespace ReMindAst
{
    // ベースノード
    public abstract class AstNode
    {
    }

    // ルート
    public class CompilationUnit : AstNode
    {
        public List<NamespaceDeclaration> Namespaces { get; } = new();
        public List<ImportDeclaration> Imports { get; } = new();
        public List<Statement> Statements { get; } = new();
    }

    public class NamespaceDeclaration : AstNode
    {
        public string Name { get; set; } = "";
        public List<ClassDeclaration> Classes { get; } = new();
    }

    public class ClassDeclaration : AstNode
    {
        public string NameJa { get; set; } = "";
        public string NameEn { get; set; } = ""; // transpiledName
        public List<string> Modifiers { get; } = new();
        public JavadocComment? Javadoc { get; set; }
        public List<FieldDeclaration> Fields { get; } = new();
        public List<MethodDeclaration> Methods { get; } = new();
    }

    public class JavadocComment
    {
        public string? Summary { get; set; }
        public List<JavadocParam> Params { get; } = new();
    }

    public class JavadocParam
    {
        public string NameJa { get; set; } = "";
        public string NameEn { get; set; } = "";
    }

    public class MethodDeclaration : AstNode
    {
        public string NameJa { get; set; } = "";
        public string NameEn { get; set; } = "";
        public string ReturnType { get; set; } = "void";
        public List<string> Modifiers { get; } = new();
        public JavadocComment? Javadoc { get; set; }
        public List<Parameter> Parameters { get; } = new();
        public List<Statement> Body { get; } = new();
    }

    public class FieldDeclaration : AstNode
    {
        public JavadocComment? Javadoc { get; set; }
        public string Type { get; set; } = "";
        public string NameJa { get; set; } = "";
        public string NameEn { get; set; } = "";
        public Expression? Initializer { get; set; }
        public List<string> Modifiers { get; } = new();
    }

    public class Parameter : AstNode
    {
        public string Type { get; set; } = "";
        public string NameJa { get; set; } = "";
        public string NameEn { get; set; } = "";
    }

    // --- ステートメント基底 ---

    public abstract class Statement : AstNode
    {
    }

    public class LocalVariableDeclaration : Statement
    {
        public JavadocComment? Javadoc { get; set; }
        public bool IsConstant { get; set; }
        public string Type { get; set; } = "";
        public string NameJa { get; set; } = "";
        public string NameEn { get; set; } = "";
        public Expression? Initializer { get; set; }
    }

    public class ConstantDeclaration : Statement
    {
        public JavadocComment? Javadoc { get; set; }
        public string Type { get; set; } = "";
        public string NameJa { get; set; } = "";
        public string NameEn { get; set; } = "";
        public Expression? Initializer { get; set; }
        public List<string> Modifiers { get; } = new();
    }

    public class ExpressionStatement : Statement
    {
        public Expression Expression { get; set; } = null!;
    }

    public class ForStatement : Statement
    {
        public LocalVariableDeclaration? Initializer { get; set; }
        public Expression? Condition { get; set; }
        public Expression? Iterator { get; set; }
        public List<Statement> Body { get; } = new();
    }

    public class WhileStatement : Statement
    {
        public Expression Condition { get; set; } = null!;
        public List<Statement> Body { get; } = new();
    }

    public class IfStatement : Statement
    {
        public Expression Condition { get; set; } = null!;
        public List<Statement> ThenBody { get; } = new();
        public List<Statement> ElseBody { get; } = new();
    }

    public class BlockStatement : Statement
    {
        public List<Statement> Statements { get; } = new();
    }

    public class AssignmentStatement : Statement
    {
        public Expression Left { get; set; } = null!;
        public Expression Right { get; set; } = null!;
    }

    public class ReturnStatement : Statement
    {
        public Expression? Value { get; set; }
    }

    public class BreakStatement : Statement
    {
    }

    public class ContinueStatement : Statement
    {
    }

    public class TryCatchStatement : Statement
    {
        public List<Statement> TryBody { get; } = new();
        public List<CatchClause> CatchClauses { get; } = new();
        public List<Statement> FinallyBody { get; } = new();
    }

    public class CatchClause : AstNode
    {
        public string ExceptionType { get; set; } = "";
        public string VariableName { get; set; } = "";
        public List<Statement> Body { get; } = new();
    }

    public class SwitchStatement : Statement
    {
        public Expression Expression { get; set; } = null!;
        public List<SwitchCase> Cases { get; } = new();
    }

    public class SwitchCase : AstNode
    {
        public Expression? Value { get; set; }
        public List<Statement> Body { get; } = new();
    }

    // --- 式基底 ---

    public abstract class Expression : AstNode
    {
    }

    public class IdentifierExpression : Expression
    {
        public string NameJa { get; set; } = "";
        public string NameEn { get; set; } = "";
    }

    public class LiteralExpression : Expression
    {
        public object? Value { get; set; }
    }

    public class BinaryExpression : Expression
    {
        public string Operator { get; set; } = "";
        public Expression Left { get; set; } = null!;
        public Expression Right { get; set; } = null!;
    }

    public class UnaryExpression : Expression
    {
        public string Operator { get; set; } = "";
        public Expression Operand { get; set; } = null!;
    }

    public class MemberAccessExpression : Expression
    {
        public Expression Expression { get; set; } = null!;
        public string MemberName { get; set; } = "";
    }

    public class ElementAccessExpression : Expression
    {
        public Expression ArrayExpression { get; set; } = null!;
        public Expression IndexExpression { get; set; } = null!;
    }

    public class InvocationExpression : Expression
    {
        public Expression Target { get; set; } = null!;
        public List<Expression> Arguments { get; } = new();
    }

    public class ArrayLiteralExpression : Expression
    {
        public string ElementType { get; set; } = "";
        public List<Expression> Elements { get; } = new();
    }

    public class NewExpression : Expression
    {
        public string TypeName { get; set; } = "";
        public List<Expression> Arguments { get; } = new();
    }

    public class NullLiteralExpression : Expression
    {
    }

    public class ConditionalExpression : Expression
    {
        public Expression Condition { get; set; } = null!;
        public Expression ThenExpression { get; set; } = null!;
        public Expression ElseExpression { get; set; } = null!;
    }

    public class AssignmentExpression : Expression
    {
        public Expression Left { get; set; } = null!;
        public Expression Right { get; set; } = null!;
    }

    public class ArrayCreationExpression : Expression
    {
        public string ElementType { get; set; } = "";
        public List<Expression> Initializers { get; } = new();
    }

    // --- import / System.Console マッピング ---

    public class ImportDeclaration : AstNode
    {
        public string Name { get; set; } = "";
        public List<AliasClass> AliasClasses { get; } = new();
    }

    public class AliasClass
    {
        public string OriginalName { get; set; } = "";
        public string TranspiledName { get; set; } = "";
        public List<AliasMember> Members { get; } = new();
    }

    public class AliasMember
    {
        public string OriginalName { get; set; } = "";
        public string TranspiledName { get; set; } = "";
        public List<Parameter> Parameters { get; } = new();
    }

    public class TypeReference : AstNode
    {
        public string Name { get; set; } = "";
        public List<TypeReference> TypeArguments { get; } = new();
    }
}
