using System;
using System.Text.Json.Serialization;
using Postgrest.Attributes;
using Postgrest.Models;

namespace GolfApp.Api.Models
{
    [Table("scores")]
    public class Score : BaseModel
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

        [Column("round")]
        [JsonPropertyName("round")]
        public int Round { get; set; }

        [Column("hole")]
        [JsonPropertyName("hole")]
        public int Hole { get; set; }

        [Column("strokes")]
        [JsonPropertyName("strokes")]
        public int Strokes { get; set; }

        [Column("putts")]
        [JsonPropertyName("putts")]
        public int? Putts { get; set; }

        [Column("penalty_strokes")]
        [JsonPropertyName("penalty_strokes")]
        public int? PenaltyStrokes { get; set; }

        [Column("fairway_hit")]
        [JsonPropertyName("fairway_hit")]
        public bool? FairwayHit { get; set; }

        [Column("green_in_regulation")]
        [JsonPropertyName("green_in_regulation")]
        public bool? GreenInRegulation { get; set; }

        [Column("sand_save")]
        [JsonPropertyName("sand_save")]
        public bool? SandSave { get; set; }

        [Column("up_and_down")]
        [JsonPropertyName("up_and_down")]
        public bool? UpAndDown { get; set; }

        [Column("created_at")]
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("created_by")]
        [JsonPropertyName("created_by")]
        public Guid? CreatedBy { get; set; }

        [Column("client_timestamp")]
        [JsonPropertyName("client_timestamp")]
        public DateTime? ClientTimestamp { get; set; }

        [Column("device_id")]
        [JsonPropertyName("device_id")]
        public string? DeviceId { get; set; }

        [Column("sync_status")]
        [JsonPropertyName("sync_status")]
        public string? SyncStatus { get; set; } // pending, synced, conflict
    }
}
