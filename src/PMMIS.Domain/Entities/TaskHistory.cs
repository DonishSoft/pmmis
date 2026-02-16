using System.ComponentModel.DataAnnotations;

namespace PMMIS.Domain.Entities;

/// <summary>
/// История изменений задачи
/// </summary>
public class TaskHistory : BaseEntity
{
    public int TaskId { get; set; }
    public ProjectTask? Task { get; set; }
    
    [Required]
    public string ChangedById { get; set; } = string.Empty;
    public ApplicationUser? ChangedBy { get; set; }
    
    /// <summary>
    /// Тип изменения
    /// </summary>
    public TaskChangeType ChangeType { get; set; }
    
    /// <summary>
    /// Название изменённого поля
    /// </summary>
    [MaxLength(100)]
    public string? FieldName { get; set; }
    
    /// <summary>
    /// Старое значение
    /// </summary>
    public string? OldValue { get; set; }
    
    /// <summary>
    /// Новое значение
    /// </summary>
    public string? NewValue { get; set; }
    
    /// <summary>
    /// Описание изменения
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Тип изменения истории
/// </summary>
public enum TaskChangeType
{
    Created = 0,        // Создание
    StatusChanged = 1,  // Изменение статуса
    PriorityChanged = 2, // Изменение приоритета
    AssigneeChanged = 3, // Назначение исполнителя
    DueDateChanged = 4,  // Изменение срока
    DescriptionChanged = 5, // Изменение описания
    AttachmentAdded = 6,    // Добавление вложения
    AttachmentRemoved = 7,  // Удаление вложения
    CommentAdded = 8,       // Добавление комментария
    ExtensionRequested = 9, // Запрос продление
    ExtensionApproved = 10, // Одобрение продления
    ExtensionRejected = 11, // Отклонение продления
    SubTaskAdded = 12,      // Добавление подзадачи
    SubTaskCompleted = 13   // Завершение подзадачи
}
