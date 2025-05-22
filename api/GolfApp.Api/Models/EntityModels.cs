using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GolfApp.Api.Models
{
    /// <summary>
    /// Represents a golf tournament in the application
    /// Maps to the 'tournaments' table in Supabase
    /// </summary>
    public class Tournament
    {
        [Key]
        public Guid Id { get; set; }
        
        public string Name { get; set; } = null!;
        
        public DateTime StartDate { get; set; }
        
        public DateTime? EndDate { get; set; }
        
        public string? Description { get; set; }
        
        public int? MaxParticipants { get; set; }
        
        public bool IsPublic { get; set; }
        
        public string? Status { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime UpdatedAt { get; set; }
        
        public Guid OrganizerId { get; set; }
        
        // Navigation properties
        public virtual ICollection<TournamentPlayer>? Players { get; set; }
        
        public virtual ICollection<TournamentRound>? Rounds { get; set; }
    }
    
    /// <summary>
    /// Represents a player in the application
    /// Maps to the 'players' table in Supabase
    /// </summary>
    public class Player
    {
        [Key]
        public Guid Id { get; set; }
        
        public string FirstName { get; set; } = null!;
        
        public string LastName { get; set; } = null!;
        
        public string? Email { get; set; }
        
        public decimal? Handicap { get; set; }
        
        public string? ProfileImage { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime UpdatedAt { get; set; }
        
        // Navigation properties
        public virtual ICollection<TournamentPlayer>? Tournaments { get; set; }
        
        public virtual ICollection<Score>? Scores { get; set; }
    }
    
    /// <summary>
    /// Represents a golf club that can have multiple courses
    /// Maps to the 'clubs' table in Supabase
    /// </summary>
    public class Club
    {
        [Key]
        public Guid Id { get; set; }
        
        public string Name { get; set; } = null!;
        
        public string? Location { get; set; }
        
        public string? Address { get; set; }
        
        public string? Phone { get; set; }
        
        public string? Website { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime UpdatedAt { get; set; }
        
        // Navigation properties
        public virtual ICollection<Course>? Courses { get; set; }
    }
    
    /// <summary>
    /// Represents a golf course belonging to a club
    /// Maps to the 'courses' table in Supabase
    /// </summary>
    public class Course
    {
        [Key]
        public Guid Id { get; set; }
        
        public string Name { get; set; } = null!;
        
        public Guid ClubId { get; set; }
        
        public int? Par { get; set; }
        
        public int? Holes { get; set; }
        
        public string? CourseRating { get; set; }
        
        public string? SlopeRating { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime UpdatedAt { get; set; }
        
        // Navigation properties
        public virtual Club? Club { get; set; }
        
        public virtual ICollection<Hole>? Holes { get; set; }
        
        public virtual ICollection<Tee>? Tees { get; set; }
    }
    
    /// <summary>
    /// Join table for tournament players
    /// Maps to the 'tournament_players' table in Supabase
    /// </summary>
    public class TournamentPlayer
    {
        [Key]
        public Guid Id { get; set; }
        
        public Guid TournamentId { get; set; }
        
        public Guid PlayerId { get; set; }
        
        public string? Status { get; set; }
        
        public DateTime JoinedAt { get; set; }
        
        // Navigation properties
        public virtual Tournament? Tournament { get; set; }
        
        public virtual Player? Player { get; set; }
    }
    
    /// <summary>
    /// Represents a round in a tournament
    /// Maps to the 'tournament_rounds' table in Supabase
    /// </summary>
    public class TournamentRound
    {
        [Key]
        public Guid Id { get; set; }
        
        public Guid TournamentId { get; set; }
        
        public int RoundNumber { get; set; }
        
        public DateTime Date { get; set; }
        
        public Guid CourseId { get; set; }
        
        // Navigation properties
        public virtual Tournament? Tournament { get; set; }
        
        public virtual Course? Course { get; set; }
        
        public virtual ICollection<Score>? Scores { get; set; }
    }
    
    /// <summary>
    /// Represents a tee on a golf course
    /// Maps to the 'tees' table in Supabase
    /// </summary>
    public class Tee
    {
        [Key]
        public Guid Id { get; set; }
        
        public Guid CourseId { get; set; }
        
        public string Color { get; set; } = null!;
        
        public int? TotalYards { get; set; }
        
        // Navigation properties
        public virtual Course? Course { get; set; }
    }
    
    /// <summary>
    /// Represents a hole on a golf course
    /// Maps to the 'holes' table in Supabase
    /// </summary>
    public class Hole
    {
        [Key]
        public Guid Id { get; set; }
        
        public Guid CourseId { get; set; }
        
        public int HoleNumber { get; set; }
        
        public int Par { get; set; }
        
        public int? Yards { get; set; }
        
        public int? Handicap { get; set; }
        
        // Navigation properties
        public virtual Course? Course { get; set; }
    }
    
    /// <summary>
    /// Represents a player's score for a hole in a tournament round
    /// Maps to the 'scores' table in Supabase
    /// </summary>
    public class Score
    {
        [Key]
        public Guid Id { get; set; }
        
        public Guid RoundId { get; set; }
        
        public Guid PlayerId { get; set; }
        
        public Guid HoleId { get; set; }
        
        public int StrokeCount { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime UpdatedAt { get; set; }
        
        // Navigation properties
        public virtual TournamentRound? Round { get; set; }
        
        public virtual Player? Player { get; set; }
        
        public virtual Hole? Hole { get; set; }
    }
}
