using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GolfApp.Api.Models.DTOs
{
    public class ScoreSyncRequestDto
    {
        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; }
        
        [JsonPropertyName("tournament_id")]
        public Guid TournamentId { get; set; }
        
        [JsonPropertyName("player_id")]
        public Guid PlayerId { get; set; }
        
        [JsonPropertyName("client_timestamp")]
        public DateTime ClientTimestamp { get; set; }
        
        [JsonPropertyName("scores")]
        public List<ScoreSyncItemDto> Scores { get; set; }
    }

    public class ScoreSyncItemDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        
        [JsonPropertyName("tournament_id")]
        public Guid TournamentId { get; set; }
        
        [JsonPropertyName("player_id")]
        public Guid PlayerId { get; set; }
        
        [JsonPropertyName("round")]
        public int Round { get; set; }
        
        [JsonPropertyName("hole")]
        public int Hole { get; set; }
        
        [JsonPropertyName("strokes")]
        public int Strokes { get; set; }
        
        [JsonPropertyName("putts")]
        public int? Putts { get; set; }
        
        [JsonPropertyName("penalty_strokes")]
        public int? PenaltyStrokes { get; set; }
        
        [JsonPropertyName("fairway_hit")]
        public bool? FairwayHit { get; set; }
        
        [JsonPropertyName("green_in_regulation")]
        public bool? GreenInRegulation { get; set; }
        
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    public class SyncResultDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        
        [JsonPropertyName("status")]
        public string Status { get; set; } // created, updated, conflict
        
        [JsonPropertyName("entity")]
        public string Entity { get; set; } // score, player, etc.
        
        [JsonPropertyName("server_data")]
        public object? ServerData { get; set; } // In case of conflict
    }

    public class ScoreSyncResponseDto
    {
        [JsonPropertyName("results")]
        public List<SyncResultDto> Results { get; set; } = new();
        
        [JsonPropertyName("server_timestamp")]
        public DateTime ServerTimestamp { get; set; }
    }
}
