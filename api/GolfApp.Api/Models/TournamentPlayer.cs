using System;
using System.Text.Json.Serialization;
using Postgrest.Attributes;
using Postgrest.Models;

namespace GolfApp.Api.Models
{
    [Table("tournament_players")]
    public class TournamentPlayer : BaseModel
    {
        [PrimaryKey("id", InsertBehavior = PrimaryKeyBehavior.Default)]
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [Column("tournament_id")]
        [JsonPropertyName("tournament_id")]
        public Guid TournamentId { get; set; }

        [Column("player_id")]
        [JsonPropertyName("player_id")]
        public Guid PlayerId { get; set; }

        [Column("flight_id")]
        [JsonPropertyName("flight_id")]
        public Guid? FlightId { get; set; }

        [Column("handicap")]
        [JsonPropertyName("handicap")]
        public decimal Handicap { get; set; }

        [Column("status")]
        [JsonPropertyName("status")]
        public string Status { get; set; } // registered, confirmed, withdrawn, disqualified

        [Column("registration_date")]
        [JsonPropertyName("registration_date")]
        public DateTime RegistrationDate { get; set; }

        [Column("created_at")]
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
