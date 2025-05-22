using System;
using System.Text.Json.Serialization;
using Postgrest.Attributes;
using Postgrest.Models;

namespace GolfApp.Api.Models
{
    [Table("courses")]
    public class Course : BaseModel
    {
        [PrimaryKey("id", InsertBehavior = PrimaryKeyBehavior.Default)]
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [Column("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [Column("club_name")]
        [JsonPropertyName("club_name")]
        public string ClubName { get; set; }

        [Column("address")]
        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [Column("city")]
        [JsonPropertyName("city")]
        public string? City { get; set; }

        [Column("country")]
        [JsonPropertyName("country")]
        public string Country { get; set; }

        [Column("course_rating")]
        [JsonPropertyName("course_rating")]
        public decimal CourseRating { get; set; }

        [Column("slope_rating")]
        [JsonPropertyName("slope_rating")]
        public decimal SlopeRating { get; set; }

        [Column("num_holes")]
        [JsonPropertyName("num_holes")]
        public int NumHoles { get; set; } = 18;

        [Column("latitude")]
        [JsonPropertyName("latitude")]
        public decimal? Latitude { get; set; }

        [Column("longitude")]
        [JsonPropertyName("longitude")]
        public decimal? Longitude { get; set; }

        [Column("created_at")]
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
