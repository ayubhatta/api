﻿using HomeBikeServiceAPI.Data;
using HomeBikeServiceAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HomeBikeServiceAPI.Repositories
{
    public class MechanicRepository : IMechanicRepository
    {
        private readonly AppDbContext _context;

        public MechanicRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Mechanic>> GetAllMechanicsAsync()
        {
            return await _context.Mechanics.Include(m => m.Booking).ToListAsync();
        }

        public async Task<Mechanic> GetMechanicByIdAsync(int id)
        {
            return await _context.Mechanics.Include(m => m.Booking).FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<Mechanic> CreateMechanicAsync(Mechanic mechanic)
        {
            mechanic.IsAssignedTo = null; // Ensure IsAssignedTo is null
            _context.Mechanics.Add(mechanic);
            await _context.SaveChangesAsync();
            return mechanic;
        }

        public async Task<Mechanic> UpdateMechanicAsync(Mechanic mechanic)
        {
            var existingMechanic = await _context.Mechanics.FindAsync(mechanic.Id);
            if (existingMechanic == null) return null;

            existingMechanic.Name = mechanic.Name;
            existingMechanic.PhoneNumber = mechanic.PhoneNumber;
            existingMechanic.IsAssignedTo = mechanic.IsAssignedTo;

            _context.Mechanics.Update(existingMechanic);
            await _context.SaveChangesAsync();
            return existingMechanic;
        }

        public async Task<bool> DeleteMechanicAsync(int id)
        {
            var mechanic = await _context.Mechanics.FindAsync(id);
            if (mechanic == null) return false;

            _context.Mechanics.Remove(mechanic);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
