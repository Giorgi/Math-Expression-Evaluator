using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;

namespace SimpleExpressionEvaluator
{
    public class ExpressionEvaluator
    {
        #region Fields

        private readonly string _decimalSeparator;

        #endregion

        #region Constrcutors

        public ExpressionEvaluator()
        {
            _decimalSeparator = NumberFormatInfo.CurrentInfo.NumberDecimalSeparator;
        }

        #endregion


        #region Private Methods

        private CompiledExpression Parse(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return new CompiledExpression(s => 0, new List<string>());
            }

            var arrayParameter = Expression.Parameter(typeof(decimal[]), "args");

            var expressionStack = new Stack<Expression>();
            var operatorStack = new Stack<char>();
            var parameters = new List<string>();

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
                        expressionStack.Push(ReadParameter(reader, arrayParameter, parameters));
                        continue;
                    }

                    if (Operation.IsDefined(next))
                    {
                        var currentOperation = ReadOperation(reader);

                        EvaluateWhile(() => operatorStack.Count > 0 &&
                                            operatorStack.Peek() != '(' &&
                                            currentOperation.Precedence <= ((Operation) operatorStack.Peek()).Precedence,
                                      expressionStack, operatorStack);

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
                        EvaluateWhile(() => operatorStack.Count > 0 && operatorStack.Peek() != '(',
                                      expressionStack, operatorStack);

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

            EvaluateWhile(() => operatorStack.Count > 0, expressionStack, operatorStack);

            var lambda = Expression.Lambda<Func<decimal[], decimal>>(expressionStack.Pop(), arrayParameter);
            var compiled = lambda.Compile();

            return new CompiledExpression(compiled, parameters);
        }

        private void EvaluateWhile(Func<bool> condition, Stack<Expression> expressionStack, Stack<char> operatorStack)
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

                if (char.IsDigit(next))
                {
                    reader.Read();
                    operand += next;
                }
                else if ((next == '.') || (next == ','))
                {
                    reader.Read();
                    operand += _decimalSeparator;
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

        private Expression ReadParameter(TextReader reader, Expression arrayParameter, List<string> parameters)
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

            return Expression.ArrayIndex(arrayParameter, Expression.Constant(parameters.IndexOf(parameter)));
        }

        #endregion

        #region Public Methods

        public CompiledExpression Compile(string expression)
        {
            return Parse(expression);
        }

        #endregion

        #region Inner Classes

        private sealed class Operation
        {
            private readonly int _precedence;
            private readonly Func<Expression, Expression, Expression> _operation;

            private static readonly Dictionary<char, Operation> Operations;

            static Operation()
            {
                Operations = new Dictionary<char, Operation>
                                 {
                                     {'+', new Operation(1, Expression.Add)},
                                     {'-', new Operation(1, Expression.Subtract)},
                                     {'*', new Operation(2, Expression.Multiply)},
                                     {'/', new Operation(2, Expression.Divide)}
                                 };
            }

            private Operation(int precedence, Func<Expression, Expression, Expression> operation)
            {
                _precedence = precedence;
                _operation = operation;
            }

            public int Precedence
            {
                get { return _precedence; }
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
                return _operation(left, right);
            }

            public static bool IsDefined(char operation)
            {
                return Operations.ContainsKey(operation);
            }
        }

        #endregion
    }
}