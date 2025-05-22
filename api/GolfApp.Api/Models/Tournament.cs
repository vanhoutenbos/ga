using System;
using System.Text.Json.Serialization;
using Postgrest.Attributes;
using Postgrest.Models;

namespace GolfApp.Api.Models
{
    [Table("tournaments")]
    public class Tournament : BaseModel
    {
        [PrimaryKey("id", InsertBehavior = PrimaryKeyBehavior.Default)]
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [Column("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [Column("start_date")]
        [JsonPropertyName("start_date")]
        public DateTime StartDate { get; set; }

        [Column("end_date")]
        [JsonPropertyName("end_date")]
        public DateTime EndDate { get; set; }

        [Column("location")]
        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [Column("format")]
        [JsonPropertyName("format")]
        public string Format { get; set; } // stroke, stableford, match

        [Column("rounds")]
        [JsonPropertyName("rounds")]
        public int Rounds { get; set; }

        [Column("course_id")]
        [JsonPropertyName("course_id")]
        public Guid CourseId { get; set; }

        [Column("tenant_id")]
        [JsonPropertyName("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("status")]
        [JsonPropertyName("status")]
        public string Status { get; set; } // draft, active, completed, canceled

        [Column("registration_open")]
        [JsonPropertyName("registration_open")]
        public bool RegistrationOpen { get; set; }

        [Column("max_players")]
        [JsonPropertyName("max_players")]
        public int? MaxPlayers { get; set; }

        [Column("description")]
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [Column("created_at")]
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("created_by")]
        [JsonPropertyName("created_by")]
        public Guid CreatedBy { get; set; }
    }
}
