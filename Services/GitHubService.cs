using Octokit;

namespace CoderAgentzWeb.Services;

public class GitHubService
{
    public async Task<IReadOnlyList<Repository>> GetUserRepositoriesAsync(string accessToken)
    {
        var client = CreateClient(accessToken);

        var repos = await client.Repository.GetAllForCurrent(new RepositoryRequest
        {
            Sort = RepositorySort.Updated,
            Direction = SortDirection.Descending
        });

        return repos;
    }

    public async Task<IReadOnlyList<Branch>> GetBranchesAsync(string accessToken, string owner, string repo)
    {
        var client = CreateClient(accessToken);

        try
        {
            var branches = await client.Repository.Branch.GetAll(owner, repo);
            return branches;
        }
        catch
        {
            return new List<Branch>();
        }
    }

    public async Task<string?> GetDefaultBranchAsync(string accessToken, string owner, string repo)
    {
        var client = CreateClient(accessToken);

        try
        {
            var repository = await client.Repository.Get(owner, repo);
            return repository.DefaultBranch;
        }
        catch
        {
            return "main";
        }
    }

    public async Task<User?> GetCurrentUserAsync(string accessToken)
    {
        var client = CreateClient(accessToken);

        try
        {
            return await client.User.Current();
        }
        catch
        {
            return null;
        }
    }

    private static GitHubClient CreateClient(string accessToken)
    {
        var client = new GitHubClient(new ProductHeaderValue("CoderAgentzWeb"));
        client.Credentials = new Credentials(accessToken);
        return client;
    }
}

public class RepoInfo
{
    public string FullName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Owner { get; set; } = "";
    public string DefaultBranch { get; set; } = "main";
    public string? Description { get; set; }
    public bool IsPrivate { get; set; }
    public DateTime UpdatedAt { get; set; }
}
