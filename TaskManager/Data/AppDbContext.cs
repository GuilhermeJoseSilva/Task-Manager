using  Microsoft.EntityFrameworkCore;
using TaskManager.Domain.Models;
using Task = TaskManager.Domain.Models.Task;
using TaskManager.Domain.Enums;

namespace TaskManager.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<User> Users { get; set; }
        public DbSet<Task> Tasks { get; set; }
        public DbSet<StatusHistory> StatusHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Task>()
                .HasOne(t => t.User)
                .WithMany(u => u.Tasks)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Task>()
                .Property(t => t.Status)
                .HasConversion<string>();

            modelBuilder.Entity<StatusHistory>()
                .Property(h => h.PreviousStatus)
                .HasConversion<string>();

            modelBuilder.Entity<StatusHistory>()
                .Property(h => h.NewStatus)
                .HasConversion<string>();

            // best performance to search an specific task.
            modelBuilder.Entity<StatusHistory>()
                .HasIndex(h => h.TaskId);

                //search tasks by user + status
            modelBuilder.Entity<Task>()
                .HasIndex(t => new {t.UserId, t.Status});
        }
}