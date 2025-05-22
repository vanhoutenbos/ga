using Microsoft.EntityFrameworkCore;
using GolfApp.Api.Data;
using GolfApp.Api.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace GolfApp.Api.Services
{
    /// <summary>
    /// Service that handles tournament operations using Entity Framework Core
    /// connecting to the Supabase PostgreSQL database
    /// </summary>
    public class TournamentService : ITournamentService
    {
        private readonly GolfAppDbContext _context;
        
        public TournamentService(GolfAppDbContext context)
        {
            _context = context;
        }
        
        /// <summary>
        /// Gets all tournaments with option to filter by status
        /// </summary>
        public async Task<IEnumerable<Tournament>> GetTournamentsAsync(string? status = null)
        {
            IQueryable<Tournament> query = _context.Tournaments;
            
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(t => t.Status == status);
            }
            
            return await query.OrderByDescending(t => t.StartDate)
                              .ToListAsync();
        }
        
        /// <summary>
        /// Gets a tournament by its ID
        /// </summary>
        public async Task<Tournament?> GetTournamentByIdAsync(Guid id)
        {
            return await _context.Tournaments
                .Include(t => t.Rounds)
                .ThenInclude(r => r.Course)
                .Include(t => t.Players)
                .ThenInclude(tp => tp.Player)
                .FirstOrDefaultAsync(t => t.Id == id);
        }
        
        /// <summary>
        /// Creates a new tournament
        /// </summary>
        public async Task<Tournament> CreateTournamentAsync(Tournament tournament)
        {
            tournament.Id = Guid.NewGuid();
            tournament.CreatedAt = DateTime.UtcNow;
            tournament.UpdatedAt = DateTime.UtcNow;
            
            _context.Tournaments.Add(tournament);
            await _context.SaveChangesAsync();
            
            return tournament;
        }
        
        /// <summary>
        /// Updates an existing tournament
        /// </summary>
        public async Task<Tournament?> UpdateTournamentAsync(Guid id, Tournament tournament)
        {
            var existingTournament = await _context.Tournaments.FindAsync(id);
            
            if (existingTournament == null)
            {
                return null;
            }
            
            existingTournament.Name = tournament.Name;
            existingTournament.Description = tournament.Description;
            existingTournament.StartDate = tournament.StartDate;
            existingTournament.EndDate = tournament.EndDate;
            existingTournament.MaxParticipants = tournament.MaxParticipants;
            existingTournament.IsPublic = tournament.IsPublic;
            existingTournament.Status = tournament.Status;
            existingTournament.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            return existingTournament;
        }
        
        /// <summary>
        /// Deletes a tournament
        /// </summary>
        public async Task<bool> DeleteTournamentAsync(Guid id)
        {
            var tournament = await _context.Tournaments.FindAsync(id);
            
            if (tournament == null)
            {
                return false;
            }
            
            _context.Tournaments.Remove(tournament);
            await _context.SaveChangesAsync();
            
            return true;
        }
        
        /// <summary>
        /// Adds a player to a tournament
        /// </summary>
        public async Task<bool> AddPlayerToTournamentAsync(Guid tournamentId, Guid playerId)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            var player = await _context.Players.FindAsync(playerId);
            
            if (tournament == null || player == null)
            {
                return false;
            }
            
            var tournamentPlayer = new TournamentPlayer
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                PlayerId = playerId,
                Status = "registered",
                JoinedAt = DateTime.UtcNow
            };
            
            _context.Set<TournamentPlayer>().Add(tournamentPlayer);
            await _context.SaveChangesAsync();
            
            return true;
        }
    }
    
    /// <summary>
    /// Interface for tournament service operations
    /// </summary>
    public interface ITournamentService
    {
        Task<IEnumerable<Tournament>> GetTournamentsAsync(string? status = null);
        Task<Tournament?> GetTournamentByIdAsync(Guid id);
        Task<Tournament> CreateTournamentAsync(Tournament tournament);
        Task<Tournament?> UpdateTournamentAsync(Guid id, Tournament tournament);
        Task<bool> DeleteTournamentAsync(Guid id);
        Task<bool> AddPlayerToTournamentAsync(Guid tournamentId, Guid playerId);
    }
}
