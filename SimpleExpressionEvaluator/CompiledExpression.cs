using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;

namespace SimpleExpressionEvaluator
{
    public class CompiledExpression : DynamicObject
    {
        #region Fields

        private readonly Func<decimal[], decimal> _compiledExpression;
        private readonly IList<string> _parameters;

        #endregion

        #region Constructors

        public CompiledExpression(Func<decimal[], decimal> compiledExpression, IList<string> parameters)
        {
            _compiledExpression = compiledExpression;
            _parameters = parameters;
        }

        #endregion

        #region Private Methods

        private decimal Execute(Dictionary<string, decimal> arguments)
        {
            arguments = arguments ?? new Dictionary<string, decimal>();

            if (_parameters.Count != arguments.Count)
            {
                throw new ArgumentException(string.Format("Expression contains {0} parameters but got only {1}",
                    _parameters.Count, arguments.Count));
            }

            var missingParameters = _parameters.Where(p => !arguments.ContainsKey(p)).ToList();

            if (missingParameters.Any())
            {
                throw new ArgumentException("No values provided for parameters: " + string.Join(",", missingParameters));
            }

            var values = _parameters.Select(parameter => arguments[parameter]).ToArray();

            return _compiledExpression(values);
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

        #endregion

        #region Public Methods

        public decimal Execute()
        {
            return Execute(null);
        }

        public decimal Execute(object argument)
        {
            var arguments = ParseArguments(argument);
            return Execute(arguments);
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            if ("Execute" != binder.Name)
            {
                return base.TryInvokeMember(binder, args, out result);
            }

            //args will contain expression and arguments,
            //ArgumentNames will contain only named arguments
            if (args.Length != binder.CallInfo.ArgumentNames.Count)
            {
                throw new ArgumentException("Argument names missing.");
            }

            var arguments = new Dictionary<string, decimal>();

            for (int i = 0; i < binder.CallInfo.ArgumentNames.Count; i++)
            {
                if (IsNumeric(args[i].GetType()))
                {
                    arguments.Add(binder.CallInfo.ArgumentNames[i], Convert.ToDecimal(args[i]));
                }
            }

            result = Execute(arguments);

            return true;
        }

        #endregion
    }
}
