using System;
using System.Text.Json.Serialization;

namespace GolfApp.Api.Models.DTOs
{
    public class PlayerStatisticsDto
    {
        [JsonPropertyName("player_id")]
        public Guid PlayerId { get; set; }
        
        [JsonPropertyName("player_name")]
        public string PlayerName { get; set; }
        
        [JsonPropertyName("tournament_id")]
        public Guid TournamentId { get; set; }
        
        [JsonPropertyName("tournament_name")]
        public string TournamentName { get; set; }
        
        [JsonPropertyName("round")]
        public int? Round { get; set; }
        
        [JsonPropertyName("total_strokes")]
        public int TotalStrokes { get; set; }
        
        [JsonPropertyName("average_putts_per_hole")]
        public decimal AveragePuttsPerHole { get; set; }
        
        [JsonPropertyName("fairways_hit_percentage")]
        public decimal FairwaysHitPercentage { get; set; }
        
        [JsonPropertyName("greens_in_regulation_percentage")]
        public decimal GreenInRegulationPercentage { get; set; }
        
        [JsonPropertyName("par_saving_percentage")]
        public decimal ParSavingPercentage { get; set; }
        
        [JsonPropertyName("sand_save_percentage")]
        public decimal SandSavePercentage { get; set; }
        
        [JsonPropertyName("up_and_down_percentage")]
        public decimal UpAndDownPercentage { get; set; }
        
        [JsonPropertyName("scoring_average_par3")]
        public decimal ScoringAveragePar3 { get; set; }
        
        [JsonPropertyName("scoring_average_par4")]
        public decimal ScoringAveragePar4 { get; set; }
        
        [JsonPropertyName("scoring_average_par5")]
        public decimal ScoringAveragePar5 { get; set; }
        
        [JsonPropertyName("eagles")]
        public int Eagles { get; set; }
        
        [JsonPropertyName("birdies")]
        public int Birdies { get; set; }
        
        [JsonPropertyName("pars")]
        public int Pars { get; set; }
        
        [JsonPropertyName("bogeys")]
        public int Bogeys { get; set; }
        
        [JsonPropertyName("double_bogeys")]
        public int DoubleBogeys { get; set; }
        
        [JsonPropertyName("worse_than_double")]
        public int WorseThanDouble { get; set; }
    }
}
