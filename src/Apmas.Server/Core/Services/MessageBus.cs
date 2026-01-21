using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Apmas.Server.Core.Models;
using Apmas.Server.Storage;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Inter-agent messaging system with guaranteed delivery and real-time subscriptions.
/// </summary>
public class MessageBus : IMessageBus
{
    private readonly IStateStore _store;
    private readonly IApmasMetrics _metrics;
    private readonly ILogger<MessageBus> _logger;
    private readonly Channel<AgentMessage> _messageChannel;

    public MessageBus(
        IStateStore store,
        IApmasMetrics metrics,
        ILogger<MessageBus> logger)
    {
        _store = store;
        _metrics = metrics;
        _logger = logger;

        // Use unbounded channel for message broadcasting
        // Messages are persisted before being sent to the channel, so we won't lose them
        _messageChannel = Channel.CreateUnbounded<AgentMessage>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false
        });
    }

    public async Task PublishAsync(AgentMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(message.From))
            throw new ArgumentException("Message must have a sender (From)", nameof(message));

        if (string.IsNullOrWhiteSpace(message.To))
            throw new ArgumentException("Message must have a recipient (To)", nameof(message));

        // Persist the message first to guarantee delivery
        await _store.SaveMessageAsync(message);

        _metrics.RecordMessageSent(message.Type.ToString());

        _logger.LogInformation("Published message {MessageId} from {From} to {To} (Type: {MessageType})",
            message.Id, message.From, message.To, message.Type);

        // Broadcast to subscribers (fire-and-forget, as persistence is already done)
        // If this fails, subscribers can still retrieve messages via GetMessagesForAgentAsync
        try
        {
            await _messageChannel.Writer.WriteAsync(message);
        }
        catch (Exception ex)
        {
            // Log but don't throw - the message is already persisted
            _logger.LogWarning(ex, "Failed to broadcast message {MessageId} to subscribers", message.Id);
        }
    }

    public async Task<IReadOnlyList<AgentMessage>> GetMessagesForAgentAsync(string agentRole, DateTime? since = null)
    {
        if (string.IsNullOrWhiteSpace(agentRole))
            throw new ArgumentException("Agent role cannot be null or empty", nameof(agentRole));

        // Get messages from storage where:
        // 1. Message is directly addressed to this agent (To == agentRole)
        // 2. Message is a broadcast (To == "all")
        // 3. Message is from this agent (From == agentRole) - for tracking sent messages
        var allMessages = await _store.GetMessagesAsync(role: agentRole, since: since);

        _logger.LogDebug("Retrieved {Count} messages for agent {AgentRole} since {Since}",
            allMessages.Count, agentRole, since?.ToString("O") ?? "beginning");

        return allMessages;
    }

    public async Task<IReadOnlyList<AgentMessage>> GetAllMessagesAsync(int? limit = null)
    {
        var messages = await _store.GetMessagesAsync(role: null, since: null, limit: limit);

        _logger.LogDebug("Retrieved {Count} messages (limit: {Limit})",
            messages.Count, limit?.ToString() ?? "none");

        return messages;
    }

    public async IAsyncEnumerable<AgentMessage> SubscribeAsync(
        string? agentRole = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("New subscription started for agent {AgentRole}", agentRole ?? "all");

        await foreach (var message in _messageChannel.Reader.ReadAllAsync(ct))
        {
            // Filter messages based on agentRole
            if (agentRole == null)
            {
                // No filter - return all messages
                yield return message;
            }
            else if (message.To == agentRole || message.To == "all" || message.From == agentRole)
            {
                // Message is relevant to this agent
                yield return message;
            }
            // Otherwise skip this message
        }

        _logger.LogInformation("Subscription ended for agent {AgentRole}", agentRole ?? "all");
    }
}
