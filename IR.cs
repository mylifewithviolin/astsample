using System.Collections.Generic;

namespace ReMindAst
{
    public class DocumentationIR
    {
        public string NameJa { get; set; } = "";
        public string? Summary { get; set; }
        public List<DocumentationParamIR> Parameters { get; } = new();
    }

    public class DocumentationParamIR
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class ProgramIR
    {
        public string? Comment { get; set; }
        public string? FormattingInfo { get; set; }
        public List<string> Namespaces { get; } = new();
        public List<string> Imports { get; } = new();
        public List<ClassIR> Classes { get; } = new();
    }

    public class ClassIR
    {
        public string? Comment { get; set; }
        public string? FormattingInfo { get; set; }
        public string Name { get; set; } = "";
        public DocumentationIR? Documentation { get; set; }
        public List<FieldIR> Fields { get; } = new();
        public List<MethodIR> Methods { get; } = new();
    }

    public class MethodIR
    {
        public string? Comment { get; set; }
        public string? FormattingInfo { get; set; }
        public DocumentationIR? Documentation { get; set; }
        public string Name { get; set; } = "";
        public string ReturnType { get; set; } = "void";
        public List<string> Modifiers { get; } = new();
        public List<string> Parameters { get; } = new();
        public List<string> LocalVariables { get; } = new();
        public BlockIR Body { get; } = new();
    }

    public class BlockIR
    {
        public List<StatementIR> Statements { get; } = new();
    }

    public class FieldIR
    {
        public DocumentationIR? Documentation { get; set; }
        public List<string> Modifiers { get; } = new();
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public ExpressionIR? Initializer { get; set; }
    }

    public abstract class StatementIR
    {
        public string? Comment { get; set; }
        public string? FormattingInfo { get; set; }
    }

    public class VariableDeclarationIR : StatementIR
    {
        public DocumentationIR? Documentation { get; set; }
        public bool IsConstant { get; set; }
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public ExpressionIR Initializer { get; set; } = null!;
    }

    public class AssignmentIR : StatementIR
    {
        public string Target { get; set; } = "";
        public ExpressionIR? TargetExpression { get; set; }
        public ExpressionIR Value { get; set; } = null!;
    }

    public class CallIR : StatementIR
    {
        public string MethodName { get; set; } = "";
        public List<ExpressionIR> Arguments { get; } = new();
    }

    public class ExpressionStatementIR : StatementIR
    {
        public ExpressionIR Expression { get; set; } = null!;
    }

    public class IfIR : StatementIR
    {
        public ExpressionIR Condition { get; set; } = null!;
        public BlockIR ThenBlock { get; } = new();
        public BlockIR? ElseBlock { get; set; }
    }

    public class WhileIR : StatementIR
    {
        public ExpressionIR Condition { get; set; } = null!;
        public BlockIR Body { get; } = new();
    }

    public class DoWhileIR : StatementIR
    {
        public ExpressionIR Condition { get; set; } = null!;
        public BlockIR Body { get; } = new();
    }

    public class ForIR : StatementIR
    {
        public StatementIR Initializer { get; set; } = null!;
        public ExpressionIR Condition { get; set; } = null!;
        public ExpressionIR Iterator { get; set; } = null!;
        public BlockIR Body { get; } = new();
    }

    public class ReturnIR : StatementIR
    {
        public ExpressionIR? Value { get; set; }
    }

    public class BreakIR : StatementIR
    {
    }

    public class ContinueIR : StatementIR
    {
    }

    public abstract class ExpressionIR
    {
        public string? Comment { get; set; }
        public string? FormattingInfo { get; set; }
    }

    public class IdentifierIR : ExpressionIR
    {
        public string Name { get; set; } = "";
    }

    public class LiteralIR : ExpressionIR
    {
        public string Value { get; set; } = "";
    }

    public class BinaryIR : ExpressionIR
    {
        public ExpressionIR Left { get; set; } = null!;
        public string Operator { get; set; } = "";
        public ExpressionIR Right { get; set; } = null!;
    }

    public class UnaryIR : ExpressionIR
    {
        public string Operator { get; set; } = "";
        public ExpressionIR Operand { get; set; } = null!;
    }

    public class CallExpressionIR : ExpressionIR
    {
        public string MethodName { get; set; } = "";
        public List<ExpressionIR> Arguments { get; } = new();
    }

    public class MemberAccessIR : ExpressionIR
    {
        public ExpressionIR Target { get; set; } = null!;
        public string MemberName { get; set; } = "";
    }

    public class ArrayAccessIR : ExpressionIR
    {
        public ExpressionIR Array { get; set; } = null!;
        public ExpressionIR Index { get; set; } = null!;
    }

    public class NewExpressionIR : ExpressionIR
    {
        public string TypeName { get; set; } = "";
        public List<ExpressionIR> Arguments { get; } = new();
    }

    public class ArrayLiteralIR : ExpressionIR
    {
        public string ElementType { get; set; } = "";
        public List<ExpressionIR> Elements { get; } = new();
    }
}
