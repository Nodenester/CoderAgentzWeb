using Docker.DotNet;
using Docker.DotNet.Models;

namespace CoderAgentzWeb.Services;

public class DockerService
{
    private readonly DockerClient _client;
    private const string ImageName = "coderagentz:latest";

    public DockerService()
    {
        // Windows uses named pipe, Linux uses unix socket
        var dockerUri = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? new Uri("npipe://./pipe/docker_engine")
            : new Uri("unix:///var/run/docker.sock");

        _client = new DockerClientConfiguration(dockerUri).CreateClient();
    }

    private static string GetOutputPath(string taskId)
    {
        // Use OUTPUT_PATH env var if set (for container), otherwise use current directory
        var basePath = Environment.GetEnvironmentVariable("OUTPUT_PATH")
            ?? Path.Combine(Environment.CurrentDirectory, "output");
        return Path.Combine(basePath, taskId);
    }

    private static string GetClaudeCredentialsPath()
    {
        // Use CLAUDE_HOST_PATH env var - this must be the HOST path, not container path
        // because Docker bind mounts work from the host filesystem
        var hostPath = Environment.GetEnvironmentVariable("CLAUDE_HOST_PATH");
        if (!string.IsNullOrEmpty(hostPath))
            return hostPath;

        // Default to ~/.claude (works when running directly on host, not in container)
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDir, ".claude");
    }

    public async Task<string> StartAgentAsync(AgentTask task)
    {
        var containerName = $"coderagentz-{task.Id}";

        // Debug: Log ClaudeMd value
        Console.WriteLine($"[DockerService] ClaudeMd is null: {task.ClaudeMd == null}");
        Console.WriteLine($"[DockerService] ClaudeMd is empty: {string.IsNullOrEmpty(task.ClaudeMd)}");
        Console.WriteLine($"[DockerService] ClaudeMd length: {task.ClaudeMd?.Length ?? 0}");
        if (!string.IsNullOrEmpty(task.ClaudeMd))
            Console.WriteLine($"[DockerService] ClaudeMd first 100 chars: {task.ClaudeMd.Substring(0, Math.Min(100, task.ClaudeMd.Length))}");

        // Build environment variables
        var env = new List<string>
        {
            $"GITHUB_TOKEN={task.GitHubToken}",
            $"REPO_URL={task.RepoUrl}",
            $"TASK_DESCRIPTION={task.TaskDescription}",
            $"AGENT_MODE={task.AgentMode}",
            $"VERBOSE=1"
        };

        if (!string.IsNullOrEmpty(task.BranchName))
            env.Add($"BRANCH_NAME={task.BranchName}");

        if (!string.IsNullOrEmpty(task.BaseBranch))
            env.Add($"BASE_BRANCH={task.BaseBranch}");

        if (!string.IsNullOrEmpty(task.ClaudeMd))
            env.Add($"CLAUDE_MD={task.ClaudeMd}");

        if (!string.IsNullOrEmpty(task.Model))
            env.Add($"MODEL={task.Model}");

        // Port bindings for noVNC
        var hostPort = 6080 + (task.Id.GetHashCode() % 100);
        var vncPort = 5900 + (task.Id.GetHashCode() % 100);

        task.NoVncPort = hostPort;
        task.VncPort = vncPort;

        var createParams = new CreateContainerParameters
        {
            Image = ImageName,
            Name = containerName,
            Env = env,
            Tty = true,
            OpenStdin = true,
            HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    { "6080/tcp", new List<PortBinding> { new() { HostPort = hostPort.ToString() } } },
                    { "5900/tcp", new List<PortBinding> { new() { HostPort = vncPort.ToString() } } }
                },
                Binds = new List<string>
                {
                    $"{GetOutputPath(task.Id)}:/workspace/output:rw",
                    $"{GetClaudeCredentialsPath()}:/claude-credentials:ro"
                },
                Memory = 8L * 1024 * 1024 * 1024, // 8GB
                NanoCPUs = 4_000_000_000 // 4 CPUs
            }
        };

        try
        {
            // Ensure output directory exists
            Directory.CreateDirectory(GetOutputPath(task.Id));

            var response = await _client.Containers.CreateContainerAsync(createParams);
            task.ContainerId = response.ID;

            await _client.Containers.StartContainerAsync(response.ID, new ContainerStartParameters());

            return response.ID;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to start container: {ex.Message}", ex);
        }
    }

    public async Task<ContainerState> GetContainerStateAsync(string containerId)
    {
        try
        {
            var container = await _client.Containers.InspectContainerAsync(containerId);
            return new ContainerState
            {
                Status = container.State.Status,
                Running = container.State.Running,
                ExitCode = container.State.ExitCode,
                StartedAt = container.State.StartedAt
            };
        }
        catch
        {
            return new ContainerState { Status = "not_found" };
        }
    }

    public async Task<string> GetLogsAsync(string containerId, int tailLines = 100)
    {
        try
        {
            var parameters = new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Tail = tailLines.ToString(),
                Timestamps = true
            };

            // Use multiplexed stream for proper log handling
            using var stream = await _client.Containers.GetContainerLogsAsync(containerId, true, parameters);

            var stdout = new MemoryStream();
            var stderr = new MemoryStream();
            await stream.CopyOutputToAsync(null, stdout, stderr, default);

            stdout.Position = 0;
            stderr.Position = 0;

            using var stdoutReader = new StreamReader(stdout);
            using var stderrReader = new StreamReader(stderr);

            var logs = await stdoutReader.ReadToEndAsync();
            var errors = await stderrReader.ReadToEndAsync();

            return string.IsNullOrEmpty(errors) ? logs : $"{logs}\n--- STDERR ---\n{errors}";
        }
        catch (Exception ex)
        {
            return $"Error fetching logs: {ex.Message}";
        }
    }

    public async Task StopContainerAsync(string containerId)
    {
        try
        {
            await _client.Containers.StopContainerAsync(containerId, new ContainerStopParameters
            {
                WaitBeforeKillSeconds = 10
            });
        }
        catch { }
    }

    public async Task RemoveContainerAsync(string containerId)
    {
        try
        {
            await _client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters
            {
                Force = true,
                RemoveVolumes = true
            });
        }
        catch { }
    }

    public async Task<bool> ImageExistsAsync()
    {
        try
        {
            var images = await _client.Images.ListImagesAsync(new ImagesListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    { "reference", new Dictionary<string, bool> { { ImageName, true } } }
                }
            });
            return images.Any();
        }
        catch
        {
            return false;
        }
    }
}

public class ContainerState
{
    public string Status { get; set; } = "";
    public bool Running { get; set; }
    public long ExitCode { get; set; }
    public string? StartedAt { get; set; }
}

public class AgentTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string RepoUrl { get; set; } = "";
    public string TaskDescription { get; set; } = "";
    public string GitHubToken { get; set; } = "";
    public string? BranchName { get; set; }
    public string BaseBranch { get; set; } = "main";
    public string AgentMode { get; set; } = "task";
    public string Model { get; set; } = "sonnet";
    public string? ClaudeMd { get; set; }
    public string? ContainerId { get; set; }
    public int NoVncPort { get; set; }
    public int VncPort { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "pending";
    public string? UserName { get; set; }
}
