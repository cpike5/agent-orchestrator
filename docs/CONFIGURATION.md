# APMAS Configuration Reference

This document provides a complete reference for all APMAS configuration options.

## Configuration File

APMAS is configured through `appsettings.json` in the `src/Apmas.Server` directory. All APMAS-specific settings are under the `Apmas` section.

## Root Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ProjectName` | string | `"untitled-project"` | Name of the project being managed |
| `WorkingDirectory` | string | Current directory | Working directory for the target project |
| `DataDirectory` | string | `".apmas"` | Directory for runtime data (relative to WorkingDirectory) |
| `Timeouts` | object | See below | Timeout configuration |
| `Agents` | object | See below | Agent roster configuration |
| `Spawner` | object | See below | Agent spawner configuration |
| `Decomposition` | object | See below | Task decomposition settings |
| `Notifications` | object | See below | Notification configuration |
| `Metrics` | object | See below | Metrics and observability |

## Timeout Configuration

Controls agent lifecycle timeouts and retry behavior.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultMinutes` | int | `30` | Default timeout in minutes for agent tasks |
| `HeartbeatIntervalMinutes` | int | `5` | Expected heartbeat interval (agents should call `apmas_heartbeat` at this frequency) |
| `HeartbeatTimeoutMinutes` | int | `10` | Time without heartbeat before agent is considered unhealthy |
| `MaxRetries` | int | `3` | Maximum retry attempts before escalating to human |
| `PollingIntervalSeconds` | int | `30` | Supervisor polling interval |
| `AgentOverrides` | object | `{}` | Per-agent timeout overrides (key: role name, value: minutes) |

### Example

```json
{
  "Apmas": {
    "Timeouts": {
      "DefaultMinutes": 30,
      "HeartbeatIntervalMinutes": 5,
      "HeartbeatTimeoutMinutes": 10,
      "MaxRetries": 3,
      "PollingIntervalSeconds": 30,
      "AgentOverrides": {
        "architect": 15,
        "developer": 45,
        "reviewer": 20
      }
    }
  }
}
```

## Agent Roster Configuration

Defines the agents that APMAS will orchestrate.

### AgentOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Roster` | array | `[]` | List of agent definitions |

### AgentDefinition

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Role` | string | Yes | Unique role name (e.g., "architect", "developer") |
| `SubagentType` | string | Yes | Claude Code subagent type (e.g., "systems-architect", "dotnet-specialist") |
| `Dependencies` | array | No | List of agent roles that must complete first |
| `Description` | string | No | Description of agent's responsibilities |
| `TimeoutOverrideMinutes` | int | No | Timeout override for this specific agent |
| `PromptType` | string | No | Prompt template class name (e.g., "ArchitectPrompt") |

### Available SubagentTypes

| SubagentType | Use Case |
|--------------|----------|
| `systems-architect` | System architecture and technical design |
| `design-specialist` | UI/UX design and design systems |
| `html-prototyper` | HTML/CSS prototypes |
| `dotnet-specialist` | .NET/C# implementation |
| `test-writer` | Unit and integration tests |
| `code-reviewer` | Code review and quality checks |
| `docs-writer` | Documentation |

### Example

```json
{
  "Apmas": {
    "Agents": {
      "Roster": [
        {
          "Role": "architect",
          "SubagentType": "systems-architect",
          "Dependencies": [],
          "Description": "System architecture and technical design",
          "PromptType": "ArchitectPrompt"
        },
        {
          "Role": "developer",
          "SubagentType": "dotnet-specialist",
          "Dependencies": ["architect"],
          "Description": "Implementation and coding",
          "TimeoutOverrideMinutes": 60
        }
      ]
    }
  }
}
```

## Spawner Configuration

Controls how Claude Code agents are launched.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ClaudeCodePath` | string | `"claude"` | Path to Claude Code CLI (assumes in PATH if just "claude") |
| `Model` | string | `"sonnet"` | Model to use ("sonnet", "opus") |
| `MaxTurns` | int | `100` | Maximum turns before agent stops |
| `McpConfigPath` | string | `null` | Path to MCP config file (generated if null) |
| `GracefulShutdownTimeoutMs` | int | `5000` | Timeout for graceful termination |
| `DangerouslySkipPermissions` | bool | `true` | Skip permission checks when spawning |
| `OutputFormat` | string | `"stream-json"` | Output format for Claude Code CLI |

> **Note:** Agent prompts are defined as C# classes in `src/Apmas.Server/Agents/Prompts/` and registered via `IPromptFactory`. Each prompt class specifies its `SubagentType` property which is used for resolution.

### Example

```json
{
  "Apmas": {
    "Spawner": {
      "ClaudeCodePath": "claude",
      "Model": "sonnet",
      "MaxTurns": 100,
      "GracefulShutdownTimeoutMs": 5000,
      "DangerouslySkipPermissions": true,
      "OutputFormat": "stream-json"
    }
  }
}
```

## Task Decomposition Configuration

Controls automatic task decomposition for large tasks.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SafeContextTokens` | int | `50000` | Safe context window size in tokens |
| `TokensPerFile` | int | `15000` | Estimated tokens per file for context estimation |

### Example

```json
{
  "Apmas": {
    "Decomposition": {
      "SafeContextTokens": 50000,
      "TokensPerFile": 15000
    }
  }
}
```

## Notification Configuration

Controls escalation notifications when agents fail.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Provider` | string | `"Console"` | Notification provider: "Console", "Email", or "Slack" |
| `Email` | object | See below | Email notification settings |
| `Slack` | object | See below | Slack notification settings |

### Email Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SmtpHost` | string | `"localhost"` | SMTP server hostname |
| `SmtpPort` | int | `25` | SMTP server port |
| `UseSsl` | bool | `false` | Use SSL/TLS for SMTP |
| `Username` | string | `null` | SMTP username (optional) |
| `Password` | string | `null` | SMTP password (use secrets in production!) |
| `FromAddress` | string | `"apmas@localhost"` | Sender email address |
| `FromName` | string | `"APMAS"` | Sender display name |
| `ToAddresses` | array | `[]` | Recipient email addresses |

### Slack Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `WebhookUrl` | string | `null` | Slack webhook URL |
| `Channel` | string | `null` | Channel to post to (optional) |
| `Username` | string | `"APMAS"` | Bot username |
| `IconEmoji` | string | `":robot_face:"` | Bot emoji icon |

### Example

```json
{
  "Apmas": {
    "Notifications": {
      "Provider": "Slack",
      "Slack": {
        "WebhookUrl": "https://hooks.slack.com/services/xxx/yyy/zzz",
        "Channel": "#apmas-alerts",
        "Username": "APMAS Bot",
        "IconEmoji": ":robot_face:"
      }
    }
  }
}
```

## Metrics Configuration

Controls metrics collection and export.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | `true` | Enable metrics collection |
| `OpenTelemetry` | object | See below | OpenTelemetry exporter settings |

### OpenTelemetry Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | bool | `false` | Enable OpenTelemetry export |
| `Endpoint` | string | `null` | OTLP endpoint URL |
| `Protocol` | string | `"grpc"` | Protocol: "grpc" or "http/protobuf" |

### Example

```json
{
  "Apmas": {
    "Metrics": {
      "Enabled": true,
      "OpenTelemetry": {
        "Enabled": true,
        "Endpoint": "http://localhost:4317",
        "Protocol": "grpc"
      }
    }
  }
}
```

## Logging Configuration

APMAS uses Serilog for structured logging. Configure in the `Serilog` section:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

### Log Destinations

APMAS logs to:
1. **Console** - With timestamps and source context
2. **Seq** - At http://localhost:5341 (requires Seq running)
3. **File** - Rolling daily files in `.apmas/logs/`

## Complete Example

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  },
  "Apmas": {
    "ProjectName": "my-web-app",
    "WorkingDirectory": "C:/projects/my-web-app",
    "DataDirectory": ".apmas",
    "Timeouts": {
      "DefaultMinutes": 30,
      "HeartbeatIntervalMinutes": 5,
      "HeartbeatTimeoutMinutes": 10,
      "MaxRetries": 3,
      "PollingIntervalSeconds": 30,
      "AgentOverrides": {
        "architect": 15,
        "designer": 15,
        "developer": 45,
        "reviewer": 20,
        "tester": 30
      }
    },
    "Agents": {
      "Roster": [
        {
          "Role": "architect",
          "SubagentType": "systems-architect",
          "Dependencies": [],
          "Description": "System architecture and technical design",
          "PromptType": "ArchitectPrompt"
        },
        {
          "Role": "designer",
          "SubagentType": "design-specialist",
          "Dependencies": [],
          "Description": "UI/UX design and design systems",
          "PromptType": "DesignerPrompt"
        },
        {
          "Role": "developer",
          "SubagentType": "dotnet-specialist",
          "Dependencies": ["architect", "designer"],
          "Description": "Implementation and coding",
          "PromptType": "DeveloperPrompt"
        },
        {
          "Role": "tester",
          "SubagentType": "test-writer",
          "Dependencies": ["developer"],
          "Description": "Testing and quality assurance",
          "PromptType": "TesterPrompt"
        },
        {
          "Role": "reviewer",
          "SubagentType": "code-reviewer",
          "Dependencies": ["developer"],
          "Description": "Code review and quality checks",
          "PromptType": "ReviewerPrompt"
        }
      ]
    },
    "Spawner": {
      "ClaudeCodePath": "claude",
      "Model": "sonnet",
      "MaxTurns": 100,
      "DangerouslySkipPermissions": true
    },
    "Decomposition": {
      "SafeContextTokens": 50000,
      "TokensPerFile": 15000
    },
    "Notifications": {
      "Provider": "Slack",
      "Slack": {
        "WebhookUrl": "https://hooks.slack.com/services/xxx/yyy/zzz",
        "Channel": "#apmas-alerts"
      }
    },
    "Metrics": {
      "Enabled": true,
      "OpenTelemetry": {
        "Enabled": false
      }
    }
  }
}
```

## Environment-Specific Configuration

Use separate configuration files for different environments:

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- `appsettings.Production.json` - Production overrides

### Example: Development Override

```json
{
  "Apmas": {
    "Notifications": {
      "Provider": "Console"
    },
    "Spawner": {
      "DangerouslySkipPermissions": true
    }
  }
}
```

### Example: Production Override

```json
{
  "Apmas": {
    "Notifications": {
      "Provider": "Slack"
    },
    "Spawner": {
      "DangerouslySkipPermissions": false
    }
  }
}
```

## Security Considerations

- **Never commit secrets** (passwords, API keys) to appsettings.json
- Use **User Secrets** for development: `dotnet user-secrets`
- Use **environment variables** or **Azure Key Vault** for production
- The `DangerouslySkipPermissions` option should be `false` in production
