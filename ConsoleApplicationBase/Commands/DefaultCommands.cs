using System;

// All console commands must be in the sub-namespace Commands:
namespace ConsoleApplicationBase.Commands
{
    // Must be a public static class:
    public static class DefaultCommands
    {
        // Methods used as console commands must be public and must return a string

        public static string DoSomething(int id, string data)
        {
            return ConsoleFormatting.Indent(2) + $"I did something to the record Id {id} and saved the data '{data}'";
        }


        public static string DoSomethingElse(DateTime date)
        {
            return ConsoleFormatting.Indent(2) + $"I did something else on {date}";
        }


        public static string DoSomethingOptional(int id, string data = "No Data Provided")
        {
            var result = ConsoleFormatting.Indent(2) + $"I did something to the record Id {id} and saved the data {data}";

            if(data == "No Data Provided")
            {
                result = ConsoleFormatting.Indent(2) + $"I did something to the record Id {id} but the optinal parameter was not provided, so I saved the value '{data}'";
            }
            return result;
        }
    }
}
