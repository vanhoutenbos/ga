using System;
using System.Text.Json.Serialization;
using Postgrest.Attributes;
using Postgrest.Models;

namespace GolfApp.Api.Models
{
    [Table("flights")]
    public class Flight : BaseModel
    {
        [PrimaryKey("id", InsertBehavior = PrimaryKeyBehavior.Default)]
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [Column("tournament_id")]
        [JsonPropertyName("tournament_id")]
        public Guid TournamentId { get; set; }

        [Column("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [Column("start_time")]
        [JsonPropertyName("start_time")]
        public DateTime? StartTime { get; set; }

        [Column("tee")]
        [JsonPropertyName("tee")]
        public string? Tee { get; set; }

        [Column("order")]
        [JsonPropertyName("order")]
        public int Order { get; set; }

        [Column("handicap_min")]
        [JsonPropertyName("handicap_min")]
        public decimal? HandicapMin { get; set; }

        [Column("handicap_max")]
        [JsonPropertyName("handicap_max")]
        public decimal? HandicapMax { get; set; }

        [Column("created_at")]
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
