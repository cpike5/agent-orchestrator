# Implementation Spec: Review Feedback Loop

## Overview

Add support for iterative review cycles where the Reviewer can request changes from the Developer, triggering automatic rework until approval.

**Current State:** Workflow is linear (Developer → Reviewer → Done). Review feedback is logged but ignored.

**Target State:** Reviewer can send `ChangesRequested` → Developer is respawned with feedback context → Developer fixes issues → Reviewer re-reviews → Loop until approved or escalated.

---

## Architecture

```
Developer completes
       ↓
Reviewer spawned (existing dependency system)
       ↓
Reviewer finds blockers
       ↓
Reviewer calls apmas_send_message(to: "developer", type: "changes_requested", content: "...")
       ↓
Reviewer calls apmas_complete
       ↓
SupervisorService detects ChangesRequested message
       ↓
Supervisor queues Developer with feedback as RecoveryContext
       ↓
Supervisor ALSO resets Reviewer to Pending (so it re-spawns when Developer completes)
       ↓
Developer fixes issues, calls apmas_complete
       ↓
Reviewer auto-spawns (existing dependency: reviewer depends on developer)
       ↓
Loop until Approved or max iterations reached → Escalate
```

---

## Files to Modify

### 1. SendMessageTool.cs
**Path:** `src/Apmas.Server/Mcp/Tools/SendMessageTool.cs`

Add review message types to schema and mapping:
- Add `"changes_requested"`, `"approved"`, `"needs_review"` to allowed types
- Map to `MessageType.ChangesRequested`, `MessageType.Approved`, `MessageType.NeedsReview`

### 2. ReviewerPrompt.cs
**Path:** `src/Apmas.Server/Agents/Prompts/ReviewerPrompt.cs`

Update `GetTaskDescription()` to instruct:
- Send `apmas_send_message(to: "developer", type: "changes_requested", ...)` when blockers found
- Include structured feedback: file paths, line numbers, severity, description
- Send `apmas_send_message(to: "developer", type: "approved", ...)` when code passes review
- Only call `apmas_complete` after sending the appropriate message

### 3. DeveloperPrompt.cs
**Path:** `src/Apmas.Server/Agents/Prompts/DeveloperPrompt.cs`

Update to handle rework context:
- Check `## Additional Context` section for review feedback
- If feedback present, focus on fixing listed issues only
- After fixes, call `apmas_complete` to trigger re-review

### 4. SupervisorService.cs
**Path:** `src/Apmas.Server/Core/Services/SupervisorService.cs`

Add `CheckForReviewFeedbackAsync()` method:
- Query MessageBus for `ChangesRequested` messages
- Filter for unprocessed messages (need tracking)
- Respawn target agent with feedback as RecoveryContext
- Track iteration count to prevent infinite loops

Add to polling loop after `CheckDependenciesAsync()`.

### 5. AgentState.cs
**Path:** `src/Apmas.Server/Core/Models/AgentState.cs`

Add field for feedback tracking:
- `ReviewIterationCount` (int) - tracks review cycles

### 6. AgentMessage.cs
**Path:** `src/Apmas.Server/Core/Models/AgentMessage.cs`

Add field to track processing:
- `ProcessedAt` (DateTime?) - marks when supervisor acted on message

### 7. IMessageBus.cs / MessageBus.cs
**Path:** `src/Apmas.Server/Core/Services/MessageBus.cs`

Add method to mark messages as processed:
```csharp
Task MarkProcessedAsync(string messageId);
```

### 8. IStateStore.cs / SqliteStateStore.cs
**Path:** `src/Apmas.Server/Storage/SqliteStateStore.cs`

Add storage method:
```csharp
Task UpdateMessageProcessedAsync(string messageId, DateTime processedAt);
```

### 9. ApmasOptions.cs
**Path:** `src/Apmas.Server/Configuration/ApmasOptions.cs`

Add configuration:
- `MaxReviewIterations` (int, default: 3) - cap before escalation

---

## Detailed Changes

### SendMessageTool.cs Changes

```csharp
// In GetInputSchema() - add to enum
"type": {
    "type": "string",
    "enum": ["question", "answer", "info", "request", "changes_requested", "approved", "needs_review"],
    "description": "Message type"
}

// In ExecuteAsync() - add mappings
var messageType = type switch
{
    "question" => MessageType.Question,
    "answer" => MessageType.Answer,
    "info" => MessageType.Info,
    "request" => MessageType.Request,
    "changes_requested" => MessageType.ChangesRequested,
    "approved" => MessageType.Approved,
    "needs_review" => MessageType.NeedsReview,
    _ => throw new ArgumentException($"Invalid message type: {type}")
};
```

### SupervisorService.cs Changes

```csharp
// New method
private async Task CheckForReviewFeedbackAsync(CancellationToken ct)
{
    // Get unprocessed ChangesRequested messages
    var messages = await _messageBus.GetMessagesAsync();
    var feedbackMessages = messages
        .Where(m => m.Type == MessageType.ChangesRequested)
        .Where(m => m.ProcessedAt == null)
        .Where(m => m.To != "supervisor" && m.To != "all")
        .ToList();

    foreach (var msg in feedbackMessages)
    {
        var targetAgent = await _stateManager.GetAgentStateAsync(msg.To);

        // Only act if target agent is Completed
        if (targetAgent.Status != AgentStatus.Completed)
            continue;

        // Check iteration limit
        if (targetAgent.ReviewIterationCount >= _options.MaxReviewIterations)
        {
            await EscalateForReviewFailure(targetAgent, msg);
            continue;
        }

        // Build rework context
        var context = BuildReworkContext(msg);

        // Queue Developer for respawn with feedback
        await _stateManager.UpdateAgentStateAsync(msg.To, a =>
        {
            a.Status = AgentStatus.Queued;
            a.RecoveryContext = context;
            a.ReviewIterationCount++;
            return a;
        });

        // Reset Reviewer to Pending so it re-spawns when Developer completes
        // (uses existing dependency system: reviewer depends on developer)
        await _stateManager.UpdateAgentStateAsync(msg.From, a =>
        {
            a.Status = AgentStatus.Pending;
            a.CompletedAt = null;
            a.SpawnedAt = null;
            return a;
        });

        // Mark message as processed
        await _messageBus.MarkProcessedAsync(msg.Id);

        _logger.LogInformation(
            "Agent {Agent} queued for rework (iteration {N}), {Reviewer} reset to Pending",
            msg.To, targetAgent.ReviewIterationCount + 1, msg.From);
    }
}

private string BuildReworkContext(AgentMessage feedback)
{
    return $"""
        ## Review Feedback - Changes Required

        The code reviewer has identified issues that must be fixed:

        {feedback.Content}

        ---

        **Instructions:**
        1. Address each issue listed above
        2. Focus only on the identified problems
        3. Call apmas_complete when fixes are done
        4. The reviewer will automatically re-review your changes
        """;
}
```

### ReviewerPrompt.cs Changes

Update `GetTaskDescription()`:

```csharp
protected override string GetTaskDescription() => """
    1. Review architecture document to understand expected patterns
    2. Examine implemented code in `src/` directory
    3. Check for adherence to architecture and design specs
    4. Identify code smells, bugs, and security vulnerabilities
    5. Evaluate error handling and edge cases

    ## Providing Feedback

    **If issues are found (blockers or significant problems):**
    - Call `apmas_send_message` with:
      - `to`: "developer"
      - `type`: "changes_requested"
      - `content`: Structured feedback with:
        - List of issues with severity (critical/major/minor)
        - File paths and line numbers
        - Clear description of what needs to change
        - Suggested fixes where appropriate

    **If code passes review:**
    - Call `apmas_send_message` with:
      - `to`: "developer"
      - `type`: "approved"
      - `content`: Summary of what was reviewed and approval notes

    6. After sending the appropriate message, call `apmas_complete`
    """;
```

### DeveloperPrompt.cs Changes

Update `GetTaskDescription()`:

```csharp
protected override string GetTaskDescription() => """
    ## Check for Review Feedback First

    Before starting work, check if you have review feedback to address:
    - Look in the "Additional Context" section below for review feedback
    - If feedback is present, your task is to FIX THE IDENTIFIED ISSUES ONLY
    - Do not add new features or refactor unrelated code

    ## Normal Development Flow

    If no review feedback is present:
    1. Review architecture document for component structure
    2. Review design specifications for UI requirements
    3. Implement features following established patterns
    4. Ensure proper error handling and logging
    5. Write code that is testable and maintainable
    6. Follow security best practices

    ## After Completing Work

    Call `apmas_complete` - the reviewer will automatically review your changes.
    """;
```

---

## Escalation Path

When `ReviewIterationCount >= MaxReviewIterations`:
1. Set agent status to `Escalated`
2. Send notification via NotificationService
3. Include all feedback history in escalation message
4. Human reviews and either:
   - Resolves manually
   - Resets iteration count to allow more attempts
   - Marks as acceptable despite issues

---

## Configuration

```json
{
  "Apmas": {
    "MaxReviewIterations": 3
  }
}
```

---

## Verification Plan

1. **Unit Tests:**
   - SendMessageTool accepts new message types
   - SupervisorService.CheckForReviewFeedbackAsync queues agents correctly
   - Iteration limit triggers escalation

2. **Integration Test:**
   - Run workflow with Developer that produces code with deliberate issues
   - Verify Reviewer sends ChangesRequested
   - Verify Developer is respawned with feedback context
   - Verify loop terminates on Approved or escalation

3. **Manual Test:**
   - Start APMAS with test project
   - Monitor dashboard for feedback loop visibility
   - Check logs for state transitions

---

## Implementation Order

1. **ApmasOptions.cs** - Add `MaxReviewIterations` config
2. **AgentState.cs** - Add `ReviewIterationCount` field
3. **AgentMessage.cs** - Add `ProcessedAt` field
4. **IStateStore / SqliteStateStore** - Add `UpdateMessageProcessedAsync()` method
5. **IMessageBus / MessageBus** - Add `MarkProcessedAsync()` method
6. **SendMessageTool.cs** - Enable review message types
7. **SupervisorService.cs** - Add `CheckForReviewFeedbackAsync()` method
8. **ReviewerPrompt.cs** - Update task instructions
9. **DeveloperPrompt.cs** - Update task instructions for rework handling
10. **EF Migration** - Add new columns to database schema

---

## Out of Scope (Future)

- Re-review only changed files (partial review)
- Reviewer memory of previous review (context optimization)
- Multiple reviewers with vote aggregation
- Automatic severity-based decisions (minor issues → approve anyway)
- Tester requesting changes (same pattern could be applied later)
