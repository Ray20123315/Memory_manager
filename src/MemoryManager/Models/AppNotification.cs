namespace Ray.MemoryManager.Models;
public sealed record AppNotification(DateTimeOffset Time, string Title, string Message, string Kind = "info");
