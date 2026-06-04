using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskManager.Domain.Enums;
using TaskManager.Domain.Models;
using TaskManager.DTOs;
#pragma warning disable DAP005

namespace TaskManager.Data.Repositories;

public interface ITaskRepository
{   
    Task<TaskManager.Domain.Models.Task?> GetByIdAsync(int id);
    Task<List<TaskManager.Domain.Models.Task>> GetByUserIdAsync(int userId);
    Task<TaskManager.Domain.Models.Task> AddAsync (TaskManager.Domain.Models.Task task);
    System.Threading.Tasks.Task UpdateAsync(TaskManager.Domain.Models.Task task);
    Task<List<TaskManager.Domain.Models.Task>> GetOverdueAsync();
    Task<List<TaskSummaryDto>> GetSummaryByUserAsync(int userId);
}

public class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;

    public TaskRepository(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

     public async Task<TaskManager.Domain.Models.Task?> GetByIdAsync(int id)
    {
        return await _context.Tasks
            .Include(t => t.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<List<TaskManager.Domain.Models.Task>> GetByUserIdAsync(int userId)
    {
        return await _context.Tasks
            .Where(t => t.UserId == userId)
            .Include(t => t.User)
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<TaskManager.Domain.Models.Task> AddAsync(TaskManager.Domain.Models.Task task)
    {
        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }

    public async System.Threading.Tasks.Task UpdateAsync(TaskManager.Domain.Models.Task task)
    {
        _context.Entry(task).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    // job to search to Hangfire job
    public async Task<List<TaskManager.Domain.Models.Task>> GetOverdueAsync()
    {
        return await _context.Tasks
            .Where(t =>
                t.DueDate < DateTime.UtcNow &&
                (t.Status == TaskManager.Domain.Enums.TaskStatus.Pending ||
                t.Status == TaskManager.Domain.Enums.TaskStatus.InProgress))
            .ToListAsync();
    }

    // Report with dapper to execute faster than EF
    public async Task<List<TaskSummaryDto>> GetSummaryByUserAsync(int userId)
    {
        var connectionString = _config.GetConnectionString("DefaultConnection");

        await using var connection = new NpgsqlConnection(connectionString);

        var sql = @"
                SELECT 
                    u.""Name""          AS UserName,
                    u.""Email""         AS UserEmail,
                    COUNT(t.""Id"")     AS TotalTasks,
                    SUM(CASE WHEN t.""Status"" = 'Pending'    THEN 1 ELSE 0 END) AS Pending,
                    SUM(CASE WHEN t.""Status"" = 'InProgress' THEN 1 ELSE 0 END) AS InProgress,
                    SUM(CASE WHEN t.""Status"" = 'Completed'  THEN 1 ELSE 0 END) AS Completed,
                    SUM(CASE WHEN t.""Status"" = 'Overdue'    THEN 1 ELSE 0 END) AS Overdue
                FROM ""Users"" u
                LEFT JOIN ""Tasks"" t ON t.""UserId"" = u.""Id""
                WHERE u.""Id"" = @UserId
                GROUP BY u.""Name"", u.""Email""";

        var result = await connection.QueryAsync<TaskSummaryDto>(sql, new { UserId = userId });

        return result.ToList();
    }
}