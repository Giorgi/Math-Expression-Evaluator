using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;

namespace SimpleExpressionEvaluator
{
    public class ExpressionEvaluator
    {
        public decimal Evaluate(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return 0;
            }

            var expressionStack = new Stack<Expression>();
            var operatorStack = new Stack<char>();

            using (var reader = new StringReader(expression))
            {
                int peek;
                while ((peek = reader.Peek()) > -1)
                {
                    var next = (char)peek;

                    if (char.IsDigit(next))
                    {
                        expressionStack.Push(ReadOperand(reader));
                        continue;
                    }

                    if (Operation.IsDefined(next))
                    {
                        var currentOperation = ReadOperation(reader);

                        while (true)
                        {
                            if (operatorStack.Count == 0)
                            {
                                operatorStack.Push(next);
                                break;
                            }

                            var lastOperition = operatorStack.Peek();

                            if (currentOperation.Precedence > ((Operation)lastOperition).Precedence)
                            {
                                operatorStack.Push(next);
                                break;
                            }

                            var right = expressionStack.Pop();
                            var left = expressionStack.Pop();

                            expressionStack.Push(((Operation)operatorStack.Pop()).Apply(left, right));
                        }
                        continue;
                    }

                    if (next != ' ')
                    {
                        throw new ArgumentException("Invalid character encountered", "expression");
                    }
                }
            }

            while (operatorStack.Count > 0)
            {
                var right = expressionStack.Pop();
                var left = expressionStack.Pop();

                expressionStack.Push(((Operation)operatorStack.Pop()).Apply(left, right));
            }

            var compiled = Expression.Lambda<Func<decimal>>(expressionStack.Pop()).Compile();
            return compiled();
        }

        private Expression ReadOperand(StringReader reader)
        {
            var operand = string.Empty;

            int peek;

            while ((peek = reader.Peek()) > -1)
            {
                var next = (char)peek;

                if (char.IsDigit(next) || next == '.')
                {
                    reader.Read();
                    operand += next;
                }
                else
                {
                    break;
                }
            }

            return Expression.Constant(string.IsNullOrEmpty(operand) ? decimal.Zero : decimal.Parse(operand));
        }

        private Operation ReadOperation(TextReader reader)
        {
            var operation = (char)reader.Read();
            return (Operation)operation;
        }
    }

    internal sealed class Operation
    {
        private readonly int precedence;
        private readonly string name;
        private readonly Func<Expression, Expression, Expression> operation;

        public static readonly Operation Addition = new Operation(1, Expression.Add, "Addition");
        public static readonly Operation Subtraction = new Operation(1, Expression.Subtract, "Subtraction");
        public static readonly Operation Multiplication = new Operation(2, Expression.Multiply, "Multiplication");
        public static readonly Operation Division = new Operation(2, Expression.Divide, "Division");

        private static readonly Dictionary<char, Operation> Operations = new Dictionary<char, Operation>
        {
            { '+', Addition },
            { '-', Subtraction },
            { '*', Multiplication},
            { '/', Division }
        };

        private Operation(int precedence, Func<Expression, Expression, Expression> operation, string name)
        {
            this.precedence = precedence;
            this.operation = operation;
            this.name = name;
        }

        public int Precedence
        {
            get { return precedence; }
        }

        public static explicit operator Operation(char operation)
        {
            Operation result;

            if (Operations.TryGetValue(operation, out result))
            {
                return result;
            }
            else
            {
                throw new InvalidCastException();
            }
        }

        public Expression Apply(Expression left, Expression right)
        {
            return operation(left, right);
        }

        public static bool IsDefined(char operation)
        {
            return Operations.ContainsKey(operation);
        }
    }
}