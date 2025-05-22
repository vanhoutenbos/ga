using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GolfApp.Api.Models.DTOs
{
    public class LeaderboardEntryDto
    {
        [JsonPropertyName("position")]
        public int Position { get; set; }
        
        [JsonPropertyName("player_id")]
        public Guid PlayerId { get; set; }
        
        [JsonPropertyName("player_name")]
        public string PlayerName { get; set; }
        
        [JsonPropertyName("total_strokes")]
        public int? TotalStrokes { get; set; }
        
        [JsonPropertyName("total_points")]
        public int? TotalPoints { get; set; }
        
        [JsonPropertyName("handicap")]
        public decimal Handicap { get; set; }
        
        [JsonPropertyName("net_total")]
        public int? NetTotal { get; set; }
        
        [JsonPropertyName("to_par")]
        public int? ToPar { get; set; }
        
        [JsonPropertyName("round_scores")]
        public Dictionary<int, int> RoundScores { get; set; } = new();
        
        [JsonPropertyName("status")]
        public string Status { get; set; }
        
        [JsonPropertyName("flight_name")]
        public string FlightName { get; set; }
    }

    public class LeaderboardResultDto
    {
        [JsonPropertyName("tournament_id")]
        public Guid TournamentId { get; set; }
        
        [JsonPropertyName("tournament_name")]
        public string TournamentName { get; set; }
        
        [JsonPropertyName("format")]
        public string Format { get; set; }
        
        [JsonPropertyName("course_name")]
        public string CourseName { get; set; }
        
        [JsonPropertyName("last_updated")]
        public DateTime LastUpdated { get; set; }
        
        [JsonPropertyName("leaderboard")]
        public List<LeaderboardEntryDto> Leaderboard { get; set; } = new();
        
        [JsonPropertyName("round_info")]
        public Dictionary<int, string> RoundInfo { get; set; } = new();
    }
}
