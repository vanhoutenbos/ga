using System;
using System.Text.Json.Serialization;
using Postgrest.Attributes;
using Postgrest.Models;

namespace GolfApp.Api.Models
{
    [Table("users")]
    public class User : BaseModel
    {
        [PrimaryKey("id", InsertBehavior = PrimaryKeyBehavior.Default)]
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [Column("email")]
        [JsonPropertyName("email")]
        public string Email { get; set; }

        [Column("first_name")]
        [JsonPropertyName("first_name")]
        public string FirstName { get; set; }

        [Column("last_name")]
        [JsonPropertyName("last_name")]
        public string LastName { get; set; }

        [Column("handicap")]
        [JsonPropertyName("handicap")]
        public decimal? Handicap { get; set; }

        [Column("phone")]
        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [Column("avatar_url")]
        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }

        [Column("created_at")]
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("last_sign_in")]
        [JsonPropertyName("last_sign_in")]
        public DateTime? LastSignIn { get; set; }
    }
}
