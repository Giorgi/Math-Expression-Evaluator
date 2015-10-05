﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SimpleExpressionEvaluator
{
    public class ExpressionEvaluator : DynamicObject
    {
        private readonly Stack<Expression> expressionStack = new Stack<Expression>();
        private readonly Stack<Symbol> operatorStack = new Stack<Symbol>();
        private readonly List<string> parameters = new List<string>();


        public Func<object, decimal> Compile(string expression)
        {
            var compiled = Parse(expression);

            Func<List<string>, Func<object, decimal>> curriedResult = list => argument =>
            {
                var arguments = ParseArguments(argument);
                return Execute(compiled, arguments, list);
            };

            var result = curriedResult(parameters.ToList());

            return result;
        }

        public decimal Evaluate(string expression, object argument = null)
        {
            var arguments = ParseArguments(argument);

            return Evaluate(expression, arguments);
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            if ("Evaluate" != binder.Name)
            {
                return base.TryInvokeMember(binder, args, out result);
            }

            if (!(args[0] is string))
            {
                throw new ArgumentException("No expression specified for parsing");
            }

            //args will contain expression and arguments,
            //ArgumentNames will contain only named arguments
            if (args.Length != binder.CallInfo.ArgumentNames.Count + 1)
            {
                throw new ArgumentException("Argument names missing.");
            }

            var arguments = new Dictionary<string, decimal>();

            for (int i = 0; i < binder.CallInfo.ArgumentNames.Count; i++)
            {
                if (IsNumeric(args[i + 1].GetType()))
                {
                    arguments.Add(binder.CallInfo.ArgumentNames[i], Convert.ToDecimal(args[i + 1]));
                }
            }

            result = Evaluate((string)args[0], arguments);

            return true;
        }


        private Func<decimal[], decimal> Parse(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return s => 0;
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
                        if (next == '-' && expressionStack.Count == 0)
                        {
                            reader.Read();
                            operatorStack.Push(Operation.UnaryMinus);
                            continue;
                        }

                        var currentOperation = ReadOperation(reader);

                        EvaluateWhile(() => operatorStack.Count > 0 && operatorStack.Peek() != Parentheses.Left &&
                            currentOperation.Precedence <= ((Operation)operatorStack.Peek()).Precedence);

                        operatorStack.Push(currentOperation);
                        continue;
                    }

                    if (next == '(')
                    {
                        reader.Read();
                        operatorStack.Push(Parentheses.Left);

                        if (reader.Peek() == '-')
                        {
                            reader.Read();
                            operatorStack.Push(Operation.UnaryMinus);
                        }

                        continue;
                    }

                    if (next == ')')
                    {
                        reader.Read();
                        EvaluateWhile(() => operatorStack.Count > 0 && operatorStack.Peek() != Parentheses.Left);
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
            return compiled;
        }

        private Dictionary<string, decimal> ParseArguments(object argument)
        {
            if (argument == null)
            {
                return new Dictionary<string, decimal>();
            }

            var argumentType = argument.GetType();

            var properties = argumentType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && IsNumeric(p.PropertyType));

            var arguments = properties.ToDictionary(property => property.Name,
                property => Convert.ToDecimal(property.GetValue(argument, null)));

            return arguments;
        }

        private decimal Evaluate(string expression, Dictionary<string, decimal> arguments)
        {
            var compiled = Parse(expression);

            return Execute(compiled, arguments, parameters);
        }

        private decimal Execute(Func<decimal[], decimal> compiled, Dictionary<string, decimal> arguments, List<string> parameters)
        {
            arguments = arguments ?? new Dictionary<string, decimal>();

            if (parameters.Count != arguments.Count)
            {
                throw new ArgumentException(string.Format("Expression contains {0} parameters but got only {1}",
                    parameters.Count, arguments.Count));
            }

            var missingParameters = parameters.Where(p => !arguments.ContainsKey(p)).ToList();

            if (missingParameters.Any())
            {
                throw new ArgumentException("No values provided for parameters: " + string.Join(",", missingParameters));
            }

            var values = parameters.Select(parameter => arguments[parameter]).ToArray();

            return compiled(values);
        }


        private void EvaluateWhile(Func<bool> condition)
        {
            while (condition())
            {
                var operation = (Operation)operatorStack.Pop();

                var expressions = new Expression[operation.NumberOfOperands];
                for (var i = operation.NumberOfOperands - 1; i >= 0; i--)
                {
                    expressions[i] = expressionStack.Pop();
                }

                expressionStack.Push(operation.Apply(expressions));
            }
        }

        private Expression ReadOperand(TextReader reader)
        {
            var operand = string.Empty;

            int peek;

            while ((peek = reader.Peek()) > -1)
            {
                var next = (char)peek;

                var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
                var groupSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator[0];

                if (char.IsDigit(next) || next == decimalSeparator || next == groupSeparator)
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

            return Expression.ArrayIndex(arrayParameter, Expression.Constant(parameters.IndexOf(parameter)));
        }


        private bool IsNumeric(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
            }
            return false;
        }
    }

    internal sealed class Operation : Symbol
    {
        private readonly Func<Expression, Expression, Expression> operation;
        private readonly Func<Expression, Expression> unaryOperation;

        public static readonly Operation Addition = new Operation(1, Expression.Add, "Addition");
        public static readonly Operation Subtraction = new Operation(1, Expression.Subtract, "Subtraction");
        public static readonly Operation Multiplication = new Operation(2, Expression.Multiply, "Multiplication");
        public static readonly Operation Division = new Operation(2, Expression.Divide, "Division");
        public static readonly Operation UnaryMinus = new Operation(2, Expression.Negate, "Negation");

        private static readonly Dictionary<char, Operation> Operations = new Dictionary<char, Operation>
        {
            { '+', Addition },
            { '-', Subtraction },
            { '*', Multiplication},
            { '/', Division }
        };

        private Operation(int precedence, string name)
        {
            Name = name;
            Precedence = precedence;
        }

        private Operation(int precedence, Func<Expression, Expression> unaryOperation, string name) : this(precedence, name)
        {
            this.unaryOperation = unaryOperation;
            NumberOfOperands = 1;
        }

        private Operation(int precedence, Func<Expression, Expression, Expression> operation, string name) : this(precedence, name)
        {
            this.operation = operation;
            NumberOfOperands = 2;
        }

        public string Name { get; private set; }

        public int NumberOfOperands { get; private set; }

        public int Precedence { get; private set; }

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

        private Expression Apply(Expression expression)
        {
            return unaryOperation(expression);
        }

        private Expression Apply(Expression left, Expression right)
        {
            return operation(left, right);
        }

        public static bool IsDefined(char operation)
        {
            return Operations.ContainsKey(operation);
        }

        public Expression Apply(params Expression[] expressions)
        {
            if (expressions.Length == 1)
            {
                return Apply(expressions[0]);
            }

            if (expressions.Length == 2)
            {
                return Apply(expressions[0], expressions[1]);
            }

            throw new NotImplementedException();
        }
    }

    internal class Parentheses : Symbol
    {
        public static readonly Parentheses Left = new Parentheses();
        public static readonly Parentheses Right = new Parentheses();

        private Parentheses()
        {

        }
    }

    internal class Symbol
    {
    }
}