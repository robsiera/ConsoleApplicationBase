using System.Linq;
using System.Text;
using ConsoleApplicationBase.Models;

namespace ConsoleApplicationBase.Commands
{
    public static class Users
    {
        public static string Create(string firstName, string lastName)
        {
            int? maxId = (from u in SampleData.Users
                                  select u.Id).Max();
            int newId = 0;
            if(maxId.HasValue)
            {
                newId = maxId.Value + 1;
            }

            var newUser = new User { Id = newId, FirstName = firstName, LastName = lastName };
            SampleData.Users.Add(newUser);
            return "";
        }


        public static string Get()
        {
            var sb = new StringBuilder();
            foreach(var user in SampleData.Users)
            {
                sb.AppendLine(ConsoleFormatting.Indent(2) + $"Id:{user.Id} {user.FirstName} {user.LastName}");
            }
            return sb.ToString();
        }
    }
}
