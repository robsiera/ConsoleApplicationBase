using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace ConsoleApplicationBase
{
    /// <summary>
    /// See also http://www.codeproject.com/Articles/816301/Csharp-Building-a-Useful-Extensible-NET-Console-Ap
    /// </summary>
    internal class Program
    {
        private const string COMMAND_NAMESPACE = "ConsoleApplicationBase.Commands";
        private static Dictionary<string, Dictionary<string, IEnumerable<ParameterInfo>>> _commandLibraries;

        private static void Main(string[] args)
        {
            Console.Title = typeof(Program).Name;

            // Any static classes containing commands for use from the 
            // console are located in the Commands namespace. Load 
            // references to each type in that namespace via reflection:
            _commandLibraries = new Dictionary<string, Dictionary<string, IEnumerable<ParameterInfo>>>();

            // Use reflection to load all of the classes in the Commands namespace:
            var q = from t in Assembly.GetExecutingAssembly().GetTypes()
                    where t.IsClass && t.Namespace == COMMAND_NAMESPACE
                    select t;
            var commandClasses = q.ToList();

            foreach (var commandClass in commandClasses)
            {
                // Load the method info from each class into a dictionary:
                var methods = commandClass.GetMethods(BindingFlags.Static | BindingFlags.Public);
                var methodDictionary = new Dictionary<string, IEnumerable<ParameterInfo>>();
                foreach (var method in methods)
                {
                    string commandName = method.Name;
                    methodDictionary.Add(commandName, method.GetParameters());
                }
                // Add the dictionary of methods for the current class into a dictionary of command classes:
                _commandLibraries.Add(commandClass.Name, methodDictionary);
            }
            Run();
        }


        private static void Run()
        {
            while (true)
            {
                var consoleInput = ReadFromConsole();
                if (string.IsNullOrWhiteSpace(consoleInput)) continue;

                try
                {
                    // Create a ConsoleCommand instance:
                    var cmd = new ConsoleCommand(consoleInput);

                    switch (cmd.Name)
                    {
                        case "help":
                        case "?":
                            WriteToConsole(BuildHelpMessage());
                            break;
                        case "exit":
                        case "quit":
                            WriteToConsole("Closing program...");
                            return;
                        default:
                            // Execute the command:
                            string result = Execute(cmd);
                            // Write out the result:
                            WriteToConsole(result);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // OOPS! Something went wrong - Write out the problem:
                    WriteToConsole(ex.Message);
                }
            }
        }


        private static string Execute(ConsoleCommand command)
        {
            // Validate the class name and command name:
            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

            string badCommandMessage = $"Unrecognized command '{command.LibraryClassName}.{command.Name}'. Please type a valid command.";

            // Validate the command name:
            if (!_commandLibraries.ContainsKey(command.LibraryClassName))
            {
                return badCommandMessage + BuildHelpMessage();
            }
            var methodDictionary = _commandLibraries[command.LibraryClassName];
            if (!methodDictionary.ContainsKey(command.Name))
            {
                return badCommandMessage + Environment.NewLine + BuildHelpMessage(command.LibraryClassName);
            }

            // Make sure the corret number of required arguments are provided:
            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

            var methodParameterValueList = new List<object>();
            IEnumerable<ParameterInfo> paramInfoList = methodDictionary[command.Name].ToList();

            // Validate proper # of required arguments provided. Some may be optional:
            var requiredParams = paramInfoList.Where(p => p.IsOptional == false);
            var optionalParams = paramInfoList.Where(p => p.IsOptional == true);
            int requiredCount = requiredParams.Count();
            int optionalCount = optionalParams.Count();
            int providedCount = command.Arguments.Count();

            if (requiredCount > providedCount)
            {
                var message = $"Missing required parameters. {requiredCount} required, {optionalCount} optinal, {providedCount} provided";
                string comando = command.LibraryClassName + "." + command.Name;
                message += Environment.NewLine + paramInfoList.Aggregate<ParameterInfo, string>("Call: " + comando, (total, next) => total + "  " + next.Name + " <" + next.ParameterType.Name + ">");
                return message;
            }

            // Make sure all arguments are coerced to the proper type, and that there is a 
            // value for every emthod parameter. The InvokeMember method fails if the number 
            // of arguments provided does not match the number of parameters in the 
            // method signature, even if some are optional:
            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

            if (paramInfoList.Any())
            {
                // Populate the list with default values:
                foreach (var param in paramInfoList)
                {
                    // This will either add a null object reference if the param is required 
                    // by the method, or will set a default value for optional parameters. in 
                    // any case, there will be a value or null for each method argument 
                    // in the method signature:
                    methodParameterValueList.Add(param.DefaultValue);
                }

                // Now walk through all the arguments passed from the console and assign 
                // accordingly. Any optional arguments not provided have already been set to 
                // the default specified by the method signature:
                for (int i = 0; i < command.Arguments.Count(); i++)
                {
                    var methodParam = paramInfoList.ElementAt(i);
                    var typeRequired = methodParam.ParameterType;
                    try
                    {
                        // Coming from the Console, all of our arguments are passed in as 
                        // strings. Coerce to the type to match the method paramter:
                        var value = CoerceArgument(typeRequired, command.Arguments.ElementAt(i));
                        methodParameterValueList.RemoveAt(i);
                        methodParameterValueList.Insert(i, value);
                    }
                    catch (ArgumentException ex)
                    {
                        string argumentName = methodParam.Name;
                        string argumentTypeName = typeRequired.Name;
                        string message = $"The value passed for argument '{argumentName}' cannot be parsed to type '{argumentTypeName}'";
                        throw new ArgumentException(message, ex);
                    }
                }
            }

            // Set up to invoke the method using reflection:
            // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

            Assembly current = typeof(Program).Assembly;

            // Need the full Namespace for this:
            Type commandLibaryClass = current.GetType(COMMAND_NAMESPACE + "." + command.LibraryClassName);

            object[] inputArgs = null;
            if (methodParameterValueList.Count > 0)
            {
                inputArgs = methodParameterValueList.ToArray();
            }
            var typeInfo = commandLibaryClass;

            // This will throw if the number of arguments provided does not match the number 
            // required by the method signature, even if some are optional:
            try
            {
                var result = typeInfo.InvokeMember(
                    command.Name,
                    BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.Public,
                    null, null, inputArgs);
                return result.ToString();
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                    throw ex.InnerException;
                else
                    throw;
            }
        }


        private static object CoerceArgument(Type requiredType, string inputValue)
        {
            var requiredTypeCode = Type.GetTypeCode(requiredType);
            string exceptionMessage = $"Cannnot coerce the input argument {inputValue} to required type {requiredType.Name}";

            object result = null;
            switch (requiredTypeCode)
            {
                case TypeCode.String:
                    result = inputValue;
                    break;

                case TypeCode.Int16:
                    short number16;
                    if (short.TryParse(inputValue, out number16))
                    {
                        result = number16;
                    }
                    else
                    {
                        throw new ArgumentException(exceptionMessage);
                    }
                    break;

                case TypeCode.Int32:
                    int number32;
                    if (int.TryParse(inputValue, out number32))
                    {
                        result = number32;
                    }
                    else
                    {
                        throw new ArgumentException(exceptionMessage);
                    }
                    break;

                case TypeCode.Int64:
                    long number64;
                    if (long.TryParse(inputValue, out number64))
                    {
                        result = number64;
                    }
                    else
                    {
                        throw new ArgumentException(exceptionMessage);
                    }
                    break;

                case TypeCode.Boolean:
                    bool trueFalse;
                    if (bool.TryParse(inputValue, out trueFalse))
                    {
                        result = trueFalse;
                    }
                    else
                    {
                        throw new ArgumentException(exceptionMessage);
                    }
                    break;

                case TypeCode.Byte:
                    byte byteValue;
                    if (byte.TryParse(inputValue, out byteValue))
                    {
                        result = byteValue;
                    }
                    else
                    {
                        throw new ArgumentException(exceptionMessage);
                    }
                    break;

                case TypeCode.Char:
                    char charValue;
                    if (char.TryParse(inputValue, out charValue))
                    {
                        result = charValue;
                    }
                    else
                    {
                        throw new ArgumentException(exceptionMessage);
                    }
                    break;

                case TypeCode.DateTime:
                    DateTime dateValue;
                    if (DateTime.TryParse(inputValue, out dateValue))
                    {
                        result = dateValue;
                    }
                    else
                    {
                        throw new ArgumentException(exceptionMessage);
                    }
                    break;
                case TypeCode.Decimal:
                    decimal decimalValue;
                    if (decimal.TryParse(inputValue, out decimalValue))
                    {
                        result = decimalValue;
                    }
                    else
                    {
                        throw new ArgumentException(exceptionMessage);
                    }
                    break;
                case TypeCode.Double:
                    double doubleValue;
                    if (double.TryParse(inputValue, out doubleValue))
                    {
                        result = doubleValue;
                    }
                    else
                    {
                        throw new ArgumentException(exceptionMessage);
                    }
                    break;
                case TypeCode.Single:
                    float singleValue;
                    if (float.TryParse(inputValue, out singleValue))
                    {
                        result = singleValue;
                    }
                    else
                    {
                        throw new ArgumentException(exceptionMessage);
                    }
                    break;
                case TypeCode.UInt16:
                    ushort uInt16Value;
                    if (ushort.TryParse(inputValue, out uInt16Value))
                    {
                        result = uInt16Value;
                    }
                    else
                    {
                        throw new ArgumentException(exceptionMessage);
                    }
                    break;
                case TypeCode.UInt32:
                    uint uInt32Value;
                    if (uint.TryParse(inputValue, out uInt32Value))
                    {
                        result = uInt32Value;
                    }
                    else
                    {
                        throw new ArgumentException(exceptionMessage);
                    }
                    break;
                case TypeCode.UInt64:
                    ulong uInt64Value;
                    if (ulong.TryParse(inputValue, out uInt64Value))
                    {
                        result = uInt64Value;
                    }
                    else
                    {
                        throw new ArgumentException(exceptionMessage);
                    }
                    break;
                default:
                    throw new ArgumentException(exceptionMessage);
            }
            return result;
        }


        public static void WriteToConsole(string message = "")
        {
            if (message.Length > 0)
            {
                Console.WriteLine(message);
            }
        }


        private const string READ_PROMPT = "console> ";
        public static string ReadFromConsole(string promptMessage = "")
        {
            // Show a prompt, and get input:
            Console.Write(READ_PROMPT + promptMessage);
            return Console.ReadLine();
        }

        private static string BuildHelpMessage(string library = null)
        {
            var sb = new StringBuilder("Commands: ");
            sb.AppendLine();
            foreach (var item in _commandLibraries)
            {
                if (library != null && item.Key != library)
                    continue;

                foreach (var cmd in item.Value)
                {
                    sb.Append(ConsoleFormatting.Indent(1));
                    sb.Append(item.Key);
                    sb.Append(".");
                    sb.Append(cmd.Key);
                    sb.AppendLine();
                }

            }
            return sb.ToString();
        }
    }
}
