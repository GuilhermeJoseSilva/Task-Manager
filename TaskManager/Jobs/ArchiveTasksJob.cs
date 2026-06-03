using TaskManager.Data;
using TaskManager.Domain.Enums;
using TaskManager.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace TaskManager.Jobs;

// Job do Hangfire — roda em background, fora do ciclo de requisição HTTP
// Quando esse método executa, não tem nenhum request acontecendo
// O Hangfire chama isso automaticamente no horário configurado
public class ArchiveTasksJob
{
    private readonly AppDbContext _context;
    private readonly ILogger<ArchiveTasksJob> _logger;

    public ArchiveTasksJob(AppDbContext context, ILogger<ArchiveTasksJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task ExecuteAsync()
    {
        _logger.LogInformation("Job ArchiveTasks started at {Time}", DateTime.UtcNow);

        // Busca tasks que venceram há mais de 7 dias e ainda estão Overdue
        var limitDate = DateTime.UtcNow.AddDays(-7);

        var tasksToArchive = await _context.Tasks
            .Where(t =>
                t.Status == TaskManager.Domain.Enums.TaskStatus.Overdue &&
                t.DueDate < limitDate)
            .ToListAsync();

        if (!tasksToArchive.Any())
        {
            _logger.LogInformation("No tasks to archive.");
            return;
        }

        foreach (var task in tasksToArchive)
        {
            // Registra no histórico antes de arquivar
            // ChangedBy = "system" — foi o job, não um usuário
            var history = new StatusHistory
            {
                TaskId = task.Id,
                PreviousStatus = task.Status,
                NewStatus = TaskManager.Domain.Enums.TaskStatus.Archived,
                ChangedAt = DateTime.UtcNow,
                ChangedBy = "system"
            };

            task.Status = TaskManager.Domain.Enums.TaskStatus.Archived;
            task.UpdatedAt = DateTime.UtcNow;

            _context.StatusHistories.Add(history);
        }

        // SaveChangesAsync fora do loop — uma transação só pra tudo
        // Muito mais eficiente do que salvar dentro do foreach
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Job ArchiveTasks finished. {Total} tasks archived.",
            tasksToArchive.Count);
    }
}