using System;

namespace TaskMansagement.Models
{
    public enum Role
    {
        Admin = 0,
        Manage = 1,
        Employee = 2
    }

    public class User
    {
        public Guid Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public Role Role { get; set; }
        public string PasswordHash { get; set; } = null!;
    }
}