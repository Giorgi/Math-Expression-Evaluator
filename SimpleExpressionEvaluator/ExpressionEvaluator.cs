using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;

namespace SimpleExpressionEvaluator
{
    public class ExpressionEvaluator
    {
        private readonly Stack<Expression> expressionStack = new Stack<Expression>();
        private readonly Stack<char> operatorStack = new Stack<char>();
        private readonly List<string> parameters = new List<string>();

        public decimal Evaluate(string expression, params decimal[] arguments)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return 0;
            }

            var arrayParameter = Expression.Parameter(typeof(decimal[]), "args");

            parameters.Clear();
            operatorStack.Clear();
            expressionStack.Clear();

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

                    if (char.IsLetter(next))
                    {
                        expressionStack.Push(ReadParameter(reader, arrayParameter));
                        continue;
                    }

                    if (Operation.IsDefined(next))
                    {
                        var currentOperation = ReadOperation(reader);

                        EvaluateWhile(() => operatorStack.Count > 0 && operatorStack.Peek() != '(' &&
                            currentOperation.Precedence <= ((Operation)operatorStack.Peek()).Precedence);

                        operatorStack.Push(next);
                        continue;
                    }

                    if (next == '(')
                    {
                        reader.Read();
                        operatorStack.Push('(');
                        continue;
                    }

                    if (next == ')')
                    {
                        reader.Read();
                        EvaluateWhile(() => operatorStack.Count > 0 && operatorStack.Peek() != '(');
                        operatorStack.Pop();
                        continue;
                    }

                    if (next == ' ')
                    {
                        reader.Read();
                    }
                    else
                    {
                        throw new ArgumentException(string.Format("Encountered invalid character {0}", next),
                            "expression");
                    }
                }
            }

            EvaluateWhile(() => operatorStack.Count > 0);

            var lambda = Expression.Lambda<Func<decimal[], decimal>>(expressionStack.Pop(), arrayParameter);
            var compiled = lambda.Compile();
            return compiled(arguments);
        }

        private void EvaluateWhile(Func<bool> condition)
        {
            while (condition())
            {
                var right = expressionStack.Pop();
                var left = expressionStack.Pop();

                expressionStack.Push(((Operation)operatorStack.Pop()).Apply(left, right));
            }
        }


        private Expression ReadOperand(TextReader reader)
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

            return Expression.Constant(decimal.Parse(operand));
        }

        private Operation ReadOperation(TextReader reader)
        {
            var operation = (char)reader.Read();
            return (Operation)operation;
        }

        private Expression ReadParameter(TextReader reader, Expression arrayParameter)
        {
            var parameter = string.Empty;

            int peek;

            while ((peek = reader.Peek()) > -1)
            {
                var next = (char)peek;

                if (char.IsLetter(next))
                {
                    reader.Read();
                    parameter += next;
                }
                else
                {
                    break;
                }
            }

            if (!parameters.Contains(parameter))
            {
                parameters.Add(parameter);
            }

            return Expression.ArrayAccess(arrayParameter, Expression.Constant(parameters.IndexOf(parameter)));
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