using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backendxd.Models
{
    [Table("users")]
    public class User
    {
        [Key] 
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Username { get; set; } = string.Empty;

        [Column("password")]
        public string Password { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("sub_start")]
        public long SubStart { get; set; }

        [Column("sub_end")]
        public long SubEnd { get; set; }

    }

    [Table("pending_registrations")]
    public class PendingRegistration
    {
        [Key]
        public int id { get; set; }
        public string username { get; set; } = string.Empty;
        public string email { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
        public string code { get; set; } = string.Empty;
        public DateTime expires_at { get; set; } = DateTime.UtcNow.AddMinutes(15);
    }

}
