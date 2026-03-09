# CoderAgentzWeb

**January 2026** | Archived

---

Web controller for the CoderAgentz Docker coding agent. Provides a browser-based dashboard to launch, monitor, and manage autonomous AI coding containers that clone repos, make changes, and push commits.

## What it does

- GitHub OAuth login to access your repositories
- Select a repo and branch, describe a task, pick a Claude model (Haiku/Sonnet/Opus)
- Spins up isolated Docker containers running the CoderAgentz coding agent
- Embedded noVNC viewer to watch the agent work in real time
- Live container logs, start/stop/remove controls
- Saveable CLAUDE.md templates for reuse across tasks

## Tech stack

- **C# / .NET 8** (ASP.NET Core, Blazor Server)
- **Docker.DotNet** for container lifecycle management
- **Octokit** for GitHub API integration
- **GitHub OAuth** for authentication
- Runs as a Docker container itself, controlling sibling containers via the Docker socket

## Setup

```bash
cp .env.example .env
# Fill in your GitHub OAuth App credentials in .env
docker compose up -d
```

The web UI is available at `http://localhost:7200`. Requires the `coderagentz:latest` Docker image to be built separately.

## License

MIT

