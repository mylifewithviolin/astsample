namespace ReMindAst
{
    public class IRBuilder
    {
            private DocumentationIR? BuildDocumentation(JavadocComment? documentation, string nameJa)
            {
                if (documentation == null)
                {
                    return null;
                }

                var result = new DocumentationIR
                {
                    NameJa = nameJa,
                    Summary = documentation.Summary
                };
                foreach (var parameter in documentation.Params)
                {
                    result.Parameters.Add(new DocumentationParamIR
                    {
                        Name = parameter.NameEn,
                        Description = parameter.NameJa
                    });
                }

                return result;
            }

        public ProgramIR Build(CompilationUnit ast)
        {
            var program = new ProgramIR();

            foreach (var import in ast.Imports)
            {
                program.Imports.Add(import.Name);
            }

            foreach (var ns in ast.Namespaces)
            {
                program.Namespaces.Add(ns.Name);
                foreach (var cls in ns.Classes)
                {
                    var classIR = new ClassIR
                    {
                        Name = cls.NameEn,
                        Documentation = BuildDocumentation(cls.Javadoc, cls.NameJa)
                    };

                    foreach (var field in cls.Fields)
                    {
                        var fieldIR = new FieldIR
                        {
                            Documentation = BuildDocumentation(field.Javadoc, field.NameJa),
                            Type = field.Type,
                            Name = field.NameEn
                        };
                        fieldIR.Modifiers.AddRange(field.Modifiers);
                        classIR.Fields.Add(fieldIR);
                    }

                    foreach (var method in cls.Methods)
                    {
                        var methodIR = new MethodIR
                        {
                            Name = method.NameEn,
                            ReturnType = method.ReturnType,
                            Documentation = BuildDocumentation(method.Javadoc, method.NameJa)
                        };

                        foreach (var parameter in method.Parameters)
                        {
                            methodIR.Parameters.Add($"{parameter.Type} {parameter.NameEn}");
                        }

                        foreach (var statement in method.Body)
                        {
                            methodIR.Body.Statements.Add(BuildStatement(statement));
                        }

                        classIR.Methods.Add(methodIR);
                    }

                    program.Classes.Add(classIR);
                }
            }

            return program;
        }

        public StatementIR BuildStatement(Statement stmt)
        {
            return stmt switch
            {
                AssignmentStatement assignment => new AssignmentIR
                {
                    Target = assignment.Left switch
                    {
                        IdentifierExpression identifier => identifier.NameEn,
                        MemberAccessExpression memberAccess => memberAccess.MemberName,
                        _ => ""
                    },
                    TargetExpression = assignment.Left is ElementAccessExpression
                        ? BuildExpression(assignment.Left)
                        : null,
                    Value = BuildExpression(assignment.Right)
                },
                LocalVariableDeclaration localVariable => new VariableDeclarationIR
                {
                    Documentation = BuildDocumentation(localVariable.Javadoc, localVariable.NameJa),
                    Type = localVariable.Type,
                    Name = localVariable.NameEn,
                    Initializer = BuildExpression(localVariable.Initializer!)
                },
                ExpressionStatement expressionStatement when expressionStatement.Expression is InvocationExpression invocation
                    => BuildCallStatement(invocation),
                ExpressionStatement expressionStatement
                    => new ExpressionStatementIR
                    {
                        Expression = BuildExpression(expressionStatement.Expression)
                    },
                IfStatement ifStatement => BuildIfStatement(ifStatement),
                WhileStatement whileStatement => BuildWhileStatement(whileStatement),
                ForStatement forStatement => BuildForStatement(forStatement),
                _ => throw new System.NotSupportedException()
            };
        }

        private ForIR BuildForStatement(ForStatement forStatement)
        {
            var result = new ForIR
            {
                Initializer = forStatement.Initializer == null
                    ? throw new System.NotSupportedException("For initializer is required.")
                    : BuildStatement(forStatement.Initializer),
                Condition = BuildExpression(forStatement.Condition!),
                Iterator = BuildExpression(forStatement.Iterator!)
            };

            foreach (var statement in forStatement.Body)
            {
                result.Body.Statements.Add(BuildStatement(statement));
            }

            return result;
        }

        private CallIR BuildCallStatement(InvocationExpression invocation)
        {
            var call = new CallIR
            {
                MethodName = invocation.Target switch
                {
                    IdentifierExpression identifier => identifier.NameEn,
                    MemberAccessExpression memberAccess when memberAccess.Expression is IdentifierExpression target
                        => $"{target.NameEn}.{memberAccess.MemberName}",
                    MemberAccessExpression memberAccess => memberAccess.MemberName,
                    _ => ""
                }
            };

            foreach (var argument in invocation.Arguments)
            {
                call.Arguments.Add(BuildExpression(argument));
            }

            return call;
        }

        private IfIR BuildIfStatement(IfStatement ifStatement)
        {
            var result = new IfIR
            {
                Condition = BuildExpression(ifStatement.Condition),
                ElseBlock = new BlockIR()
            };

            foreach (var statement in ifStatement.ThenBody)
            {
                result.ThenBlock.Statements.Add(BuildStatement(statement));
            }

            foreach (var statement in ifStatement.ElseBody)
            {
                result.ElseBlock.Statements.Add(BuildStatement(statement));
            }

            return result;
        }

        private WhileIR BuildWhileStatement(WhileStatement whileStatement)
        {
            var result = new WhileIR
            {
                Condition = BuildExpression(whileStatement.Condition)
            };

            foreach (var statement in whileStatement.Body)
            {
                result.Body.Statements.Add(BuildStatement(statement));
            }

            return result;
        }

        public ExpressionIR BuildExpression(Expression expr)
        {
            return expr switch
            {
                IdentifierExpression identifier => new IdentifierIR
                {
                    Name = identifier.NameEn
                },
                LiteralExpression literal => new LiteralIR
                {
                    Value = literal.Value is string stringValue
                        ? $"\"{stringValue}\""
                        : literal.Value?.ToString() ?? ""
                },
                MemberAccessExpression memberAccess => new MemberAccessIR
                {
                    Target = BuildExpression(memberAccess.Expression),
                    MemberName = memberAccess.MemberName
                },
                InvocationExpression invocation => BuildInvocationExpression(invocation),
                BinaryExpression binary => new BinaryIR
                {
                    Left = BuildExpression(binary.Left),
                    Operator = binary.Operator,
                    Right = BuildExpression(binary.Right)
                },
                UnaryExpression unary => new UnaryIR
                {
                    Operator = unary.Operator,
                    Operand = BuildExpression(unary.Operand)
                },
                ElementAccessExpression elementAccess => new ArrayAccessIR
                {
                    Array = BuildExpression(elementAccess.ArrayExpression),
                    Index = BuildExpression(elementAccess.IndexExpression)
                },
                ArrayLiteralExpression array => BuildArrayLiteral(array),
                _ => throw new System.NotSupportedException()
            };
        }

        private ArrayLiteralIR BuildArrayLiteral(ArrayLiteralExpression array)
        {
            var result = new ArrayLiteralIR
            {
                ElementType = array.ElementType
            };
            foreach (var element in array.Elements)
            {
                result.Elements.Add(BuildExpression(element));
            }
            return result;
        }

        private CallExpressionIR BuildInvocationExpression(InvocationExpression invocation)
        {
            var call = new CallExpressionIR
            {
                MethodName = invocation.Target switch
                {
                    IdentifierExpression identifier => identifier.NameEn,
                    MemberAccessExpression memberAccess when memberAccess.Expression is IdentifierExpression target
                        => $"{target.NameEn}.{memberAccess.MemberName}",
                    MemberAccessExpression memberAccess => memberAccess.MemberName,
                    _ => ""
                }
            };

            foreach (var argument in invocation.Arguments)
            {
                call.Arguments.Add(BuildExpression(argument));
            }

            return call;
        }
    }
}
