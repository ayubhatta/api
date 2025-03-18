using HomeBikeServiceAPI.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text.Json;

namespace HomeBikeServiceAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<BikeProduct> BikeProducts { get; set; }
        public DbSet<BikeParts> BikeParts { get; set; }
        public DbSet<Mechanic> Mechanics { get; set; }
        public DbSet<Cart> Carts { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure User relationship
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure BikeProduct relationship
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Bike)
                .WithMany()
                .HasForeignKey(b => b.BikeId)
                .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId);

            modelBuilder.Entity<Mechanic>()
                .HasOne(m => m.User)
                .WithOne()
                .HasForeignKey<Mechanic>(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

        }
    }
}
