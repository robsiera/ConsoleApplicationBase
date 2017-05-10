using System.Collections.Generic;

namespace ConsoleApplicationBase.Models
{
    // We'll use this for our examples:
    public class User
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }


    public static class SampleData
    {
        private static List<User> _userData;


        /// <summary>
        /// Gets the Users
        /// </summary>
        /// <remarks>
        /// List will be initialized with data the first time the static property is accessed
        /// </remarks>
        public static List<User> Users => _userData ?? (_userData = CreateInitialUsers());


        // Some test data:
        static List<User> CreateInitialUsers()
        {
            var initialUsers = new List<User>()
            {
                new User { Id = 1, FirstName = "John", LastName = "Lennon" },
                new User { Id = 2, FirstName = "Paul", LastName = "McCartney" },
                new User { Id = 3, FirstName = "George", LastName = "Harrison" },
                new User { Id = 4, FirstName = "Ringo", LastName = "Starr" },
            };
            return initialUsers;

        }
    }
}
