using System.Text.Json;

namespace CoderAgentzWeb.Services;

public class TemplateService
{
    private readonly string _storagePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public TemplateService()
    {
        _storagePath = Environment.GetEnvironmentVariable("TEMPLATES_PATH")
            ?? Path.Combine(Environment.CurrentDirectory, "data", "templates");
        Directory.CreateDirectory(_storagePath);
    }

    public async Task<List<ClaudeTemplate>> GetUserTemplatesAsync(string userName)
    {
        var filePath = GetUserFilePath(userName);
        if (!File.Exists(filePath))
            return new List<ClaudeTemplate>();

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<List<ClaudeTemplate>>(json, JsonOptions) ?? new List<ClaudeTemplate>();
    }

    public async Task<ClaudeTemplate?> GetTemplateAsync(string userName, string templateId)
    {
        var templates = await GetUserTemplatesAsync(userName);
        return templates.FirstOrDefault(t => t.Id == templateId);
    }

    public async Task SaveTemplateAsync(string userName, ClaudeTemplate template)
    {
        var templates = await GetUserTemplatesAsync(userName);

        var existing = templates.FirstOrDefault(t => t.Id == template.Id);
        if (existing != null)
        {
            existing.Name = template.Name;
            existing.Content = template.Content;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            template.Id = Guid.NewGuid().ToString("N")[..8];
            template.CreatedAt = DateTime.UtcNow;
            template.UpdatedAt = DateTime.UtcNow;
            templates.Add(template);
        }

        await SaveTemplatesAsync(userName, templates);
    }

    public async Task DeleteTemplateAsync(string userName, string templateId)
    {
        var templates = await GetUserTemplatesAsync(userName);
        templates.RemoveAll(t => t.Id == templateId);
        await SaveTemplatesAsync(userName, templates);
    }

    private async Task SaveTemplatesAsync(string userName, List<ClaudeTemplate> templates)
    {
        var filePath = GetUserFilePath(userName);
        var json = JsonSerializer.Serialize(templates, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    private string GetUserFilePath(string userName)
    {
        // Sanitize username for file path
        var safeUserName = string.Join("_", userName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_storagePath, $"{safeUserName}.json");
    }
}

public class ClaudeTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
