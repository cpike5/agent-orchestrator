using Apmas.Server.Configuration;
using Apmas.Server.Core.Enums;
using Apmas.Server.Core.Models;
using Apmas.Server.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Apmas.Server.Tests.Core.Services;

public class ConsoleNotificationServiceTests
{
    private readonly ConsoleNotificationService _service;

    public ConsoleNotificationServiceTests()
    {
        _service = new ConsoleNotificationService(NullLogger<ConsoleNotificationService>.Instance);
    }

    [Fact]
    public async Task SendEscalationAsync_ExecutesWithoutThrowing()
    {
        // Arrange
        var notification = new EscalationNotification(
            AgentRole: "architect",
            FailureCount: 3,
            LastError: "Context limit exceeded",
            Checkpoint: new Checkpoint
            {
                AgentRole = "architect",
                Summary = "Designing database schema",
                CompletedTaskCount = 2,
                TotalTaskCount = 5
            },
            Artifacts: new List<string> { "docs/architecture.md", "src/Models/User.cs" },
            Context: "Agent was working on database design when context limit was hit"
        );

        // Act & Assert (should not throw)
        await _service.SendEscalationAsync(notification);
    }

    [Fact]
    public async Task SendEscalationAsync_HandlesMinimalNotification()
    {
        // Arrange
        var notification = new EscalationNotification(
            AgentRole: "developer",
            FailureCount: 1,
            LastError: null,
            Checkpoint: null,
            Artifacts: new List<string>(),
            Context: null
        );

        // Act & Assert (should not throw)
        await _service.SendEscalationAsync(notification);
    }

    [Fact]
    public async Task SendProjectCompleteAsync_ExecutesWithoutThrowing()
    {
        // Arrange
        var state = new ProjectState
        {
            Name = "TestProject",
            WorkingDirectory = "/test/project",
            Phase = ProjectPhase.Completed,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            CompletedAt = DateTime.UtcNow
        };

        // Act & Assert (should not throw)
        await _service.SendProjectCompleteAsync(state);
    }

    [Fact]
    public async Task SendProjectCompleteAsync_HandlesIncompleteProject()
    {
        // Arrange
        var state = new ProjectState
        {
            Name = "TestProject",
            WorkingDirectory = "/test/project",
            Phase = ProjectPhase.Building,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            CompletedAt = null
        };

        // Act & Assert (should not throw)
        await _service.SendProjectCompleteAsync(state);
    }

    [Fact]
    public async Task SendAlertAsync_ExecutesWithoutThrowing()
    {
        // Act & Assert (should not throw)
        await _service.SendAlertAsync("Test Alert", "This is a test alert message");
    }
}

public class EscalationNotificationTests
{
    [Fact]
    public void FromAgentState_CreatesNotificationWithAllFields()
    {
        // Arrange
        var agent = new AgentState
        {
            Role = "architect",
            SubagentType = "systems-architect",
            RetryCount = 2,
            LastError = "Timeout exceeded",
            LastMessage = "Working on design document",
            ArtifactsJson = "[\"doc1.md\", \"doc2.md\"]"
        };
        var checkpoint = new Checkpoint
        {
            AgentRole = "architect",
            Summary = "Phase 1 complete"
        };

        // Act
        var notification = EscalationNotification.FromAgentState(agent, checkpoint);

        // Assert
        Assert.Equal("architect", notification.AgentRole);
        Assert.Equal(2, notification.FailureCount);
        Assert.Equal("Timeout exceeded", notification.LastError);
        Assert.Same(checkpoint, notification.Checkpoint);
        Assert.Equal(2, notification.Artifacts.Count);
        Assert.Contains("doc1.md", notification.Artifacts);
        Assert.Contains("doc2.md", notification.Artifacts);
        Assert.Equal("Working on design document", notification.Context);
    }

    [Fact]
    public void FromAgentState_UsesAdditionalContext_WhenProvided()
    {
        // Arrange
        var agent = new AgentState
        {
            Role = "developer",
            SubagentType = "dotnet-specialist",
            LastMessage = "Default message"
        };

        // Act
        var notification = EscalationNotification.FromAgentState(
            agent,
            checkpoint: null,
            additionalContext: "Custom context override"
        );

        // Assert
        Assert.Equal("Custom context override", notification.Context);
    }

    [Fact]
    public void FromAgentState_HandlesNullArtifactsJson()
    {
        // Arrange
        var agent = new AgentState
        {
            Role = "developer",
            SubagentType = "dotnet-specialist",
            ArtifactsJson = null
        };

        // Act
        var notification = EscalationNotification.FromAgentState(agent, null);

        // Assert
        Assert.Empty(notification.Artifacts);
    }

    [Fact]
    public void FromAgentState_HandlesInvalidArtifactsJson()
    {
        // Arrange
        var agent = new AgentState
        {
            Role = "developer",
            SubagentType = "dotnet-specialist",
            ArtifactsJson = "not valid json"
        };

        // Act
        var notification = EscalationNotification.FromAgentState(agent, null);

        // Assert - should return empty list, not throw
        Assert.Empty(notification.Artifacts);
    }

    [Fact]
    public void FromAgentState_HandlesEmptyArtifactsJson()
    {
        // Arrange
        var agent = new AgentState
        {
            Role = "developer",
            SubagentType = "dotnet-specialist",
            ArtifactsJson = "[]"
        };

        // Act
        var notification = EscalationNotification.FromAgentState(agent, null);

        // Assert
        Assert.Empty(notification.Artifacts);
    }
}

public class EmailNotificationServiceTests
{
    private readonly EmailNotificationService _service;

    public EmailNotificationServiceTests()
    {
        var options = Options.Create(new NotificationOptions
        {
            Provider = NotificationProvider.Email,
            Email = new EmailNotificationOptions
            {
                SmtpHost = "localhost",
                SmtpPort = 25,
                FromAddress = "test@example.com",
                ToAddresses = new List<string>() // Empty, so emails won't actually be sent
            }
        });

        _service = new EmailNotificationService(
            options,
            NullLogger<EmailNotificationService>.Instance
        );
    }

    [Fact]
    public async Task SendEscalationAsync_SkipsWhenNoRecipients()
    {
        // Arrange
        var notification = new EscalationNotification(
            AgentRole: "architect",
            FailureCount: 3,
            LastError: "Test error",
            Checkpoint: null,
            Artifacts: new List<string>(),
            Context: null
        );

        // Act & Assert (should not throw, should skip silently)
        await _service.SendEscalationAsync(notification);
    }

    [Fact]
    public async Task SendProjectCompleteAsync_SkipsWhenNoRecipients()
    {
        // Arrange
        var state = new ProjectState
        {
            Name = "TestProject",
            WorkingDirectory = "/test",
            Phase = ProjectPhase.Completed
        };

        // Act & Assert (should not throw, should skip silently)
        await _service.SendProjectCompleteAsync(state);
    }

    [Fact]
    public async Task SendAlertAsync_SkipsWhenNoRecipients()
    {
        // Act & Assert (should not throw, should skip silently)
        await _service.SendAlertAsync("Test", "Test message");
    }
}

public class SlackNotificationServiceTests
{
    private readonly SlackNotificationService _service;

    public SlackNotificationServiceTests()
    {
        var options = Options.Create(new NotificationOptions
        {
            Provider = NotificationProvider.Slack,
            Slack = new SlackNotificationOptions
            {
                WebhookUrl = null, // Not configured, so no actual HTTP calls
                Username = "APMAS",
                IconEmoji = ":robot_face:"
            }
        });

        _service = new SlackNotificationService(
            options,
            new TestHttpClientFactory(),
            NullLogger<SlackNotificationService>.Instance
        );
    }

    [Fact]
    public async Task SendEscalationAsync_SkipsWhenNoWebhookUrl()
    {
        // Arrange
        var notification = new EscalationNotification(
            AgentRole: "architect",
            FailureCount: 3,
            LastError: "Test error",
            Checkpoint: null,
            Artifacts: new List<string>(),
            Context: null
        );

        // Act & Assert (should not throw, should skip silently)
        await _service.SendEscalationAsync(notification);
    }

    [Fact]
    public async Task SendProjectCompleteAsync_SkipsWhenNoWebhookUrl()
    {
        // Arrange
        var state = new ProjectState
        {
            Name = "TestProject",
            WorkingDirectory = "/test",
            Phase = ProjectPhase.Completed
        };

        // Act & Assert (should not throw, should skip silently)
        await _service.SendProjectCompleteAsync(state);
    }

    [Fact]
    public async Task SendAlertAsync_SkipsWhenNoWebhookUrl()
    {
        // Act & Assert (should not throw, should skip silently)
        await _service.SendAlertAsync("Test", "Test message");
    }

    private class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }
}
