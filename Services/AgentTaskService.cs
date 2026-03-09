using System.Collections.Concurrent;

namespace CoderAgentzWeb.Services;

public class AgentTaskService
{
    private static readonly ConcurrentDictionary<string, AgentTask> _tasks = new();
    private readonly DockerService _docker;

    public AgentTaskService(DockerService docker)
    {
        _docker = docker;
    }

    public async Task<AgentTask> CreateAndStartTaskAsync(AgentTask task)
    {
        task.Status = "starting";
        _tasks[task.Id] = task;

        try
        {
            await _docker.StartAgentAsync(task);
            task.Status = "running";
        }
        catch (Exception ex)
        {
            task.Status = $"failed: {ex.Message}";
        }

        return task;
    }

    public AgentTask? GetTask(string taskId)
    {
        return _tasks.TryGetValue(taskId, out var task) ? task : null;
    }

    public IEnumerable<AgentTask> GetUserTasks(string userName)
    {
        return _tasks.Values
            .Where(t => t.UserName == userName)
            .OrderByDescending(t => t.CreatedAt);
    }

    public IEnumerable<AgentTask> GetAllTasks()
    {
        return _tasks.Values.OrderByDescending(t => t.CreatedAt);
    }

    public async Task<ContainerState> GetTaskStateAsync(string taskId)
    {
        var task = GetTask(taskId);
        if (task?.ContainerId == null)
            return new ContainerState { Status = "unknown" };

        var state = await _docker.GetContainerStateAsync(task.ContainerId);
        task.Status = state.Status;
        return state;
    }

    public async Task<string> GetTaskLogsAsync(string taskId, int lines = 100)
    {
        var task = GetTask(taskId);
        if (task?.ContainerId == null)
            return "No container found";

        return await _docker.GetLogsAsync(task.ContainerId, lines);
    }

    public async Task StopTaskAsync(string taskId)
    {
        var task = GetTask(taskId);
        if (task?.ContainerId != null)
        {
            await _docker.StopContainerAsync(task.ContainerId);
            task.Status = "stopped";
        }
    }

    public async Task RemoveTaskAsync(string taskId)
    {
        var task = GetTask(taskId);
        if (task?.ContainerId != null)
        {
            await _docker.RemoveContainerAsync(task.ContainerId);
        }
        _tasks.TryRemove(taskId, out _);
    }

    public async Task<bool> CheckDockerImageAsync()
    {
        return await _docker.ImageExistsAsync();
    }
}
