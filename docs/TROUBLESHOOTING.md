# APMAS Troubleshooting Guide

This guide covers common issues and their solutions when working with APMAS.

## Startup Issues

### Dependency Graph Validation Failed

**Error:**
```
Dependency graph validation failed:
  - Agent 'developer' has dependency 'designer' which is not in the roster
APMAS cannot start due to invalid dependency configuration
```

**Cause:** An agent's dependency references a role that doesn't exist in the roster.

**Solution:**
1. Check `appsettings.json` for typos in dependency names
2. Ensure all referenced dependencies are defined in the roster
3. Verify the dependency graph has no cycles

```json
{
  "Agents": {
    "Roster": [
      { "Role": "architect", "Dependencies": [] },
      { "Role": "developer", "Dependencies": ["architect"] }  // "architect" must exist
    ]
  }
}
```

### Database Creation Failed

**Error:**
```
Failed to create SQLite database at .apmas/state.db
```

**Cause:** Permission issues or the directory doesn't exist.

**Solution:**
1. Ensure the working directory exists
2. Check write permissions on the directory
3. Manually create the `.apmas` directory if needed

```bash
mkdir .apmas
```

### Claude Code CLI Not Found

**Error:**
```
Could not find claude executable
```

**Cause:** Claude Code CLI is not installed or not in PATH.

**Solution:**
1. Install Claude Code: `npm install -g @anthropic-ai/claude-code`
2. Verify installation: `claude --version`
3. If using a custom path, set `Spawner.ClaudeCodePath` in configuration

## Agent Issues

### Agent Not Starting

**Symptoms:** Agent stays in "Pending" or "Queued" state indefinitely.

**Possible Causes:**
1. Dependencies not met
2. Claude Code CLI issues
3. Configuration errors

**Diagnosis:**
1. Check agent dependencies are completed:
   ```bash
   # Look for completed agents in logs
   grep "Status.*Completed" .apmas/logs/apmas-*.log
   ```

2. Verify Claude Code works:
   ```bash
   claude --version
   echo "Hello" | claude --print
   ```

3. Check the spawner configuration in `appsettings.json`

### Agent Timeout

**Symptoms:** Agent is marked as "TimedOut" and restarted.

**Possible Causes:**
1. Agent is stuck or working on a large task
2. Heartbeat not being sent
3. Timeout configured too short

**Solution:**
1. Increase timeout for specific agent:
   ```json
   {
     "Timeouts": {
       "AgentOverrides": {
         "developer": 60
       }
     }
   }
   ```

2. Check agent prompts include heartbeat instructions

3. Review agent's last checkpoint for progress:
   ```bash
   ls -la .apmas/checkpoints/
   ```

### Agent Escalated to Human

**Symptoms:** Agent status is "Escalated" after multiple failures.

**Cause:** Agent failed `MaxRetries` times (default: 3).

**Solution:**
1. Check logs for failure reasons:
   ```bash
   grep -i "error\|failed" .apmas/logs/apmas-*.log | tail -50
   ```

2. Review the agent's last checkpoint:
   ```bash
   cat .apmas/checkpoints/<agent-role>-latest.json
   ```

3. Fix the underlying issue (often missing dependencies or incorrect prompts)

4. Reset agent state and retry (requires database modification)

### Agent Not Sending Heartbeats

**Symptoms:** Agent marked unhealthy despite working.

**Cause:** Agent prompt doesn't include heartbeat instructions.

**Solution:**
1. Verify the agent prompt includes heartbeat reminders
2. Check `HeartbeatIntervalMinutes` and `HeartbeatTimeoutMinutes` settings
3. Review BaseAgentPrompt for required tool usage instructions

## Communication Issues

### Messages Not Being Delivered

**Symptoms:** `apmas_send_message` succeeds but target agent doesn't receive message.

**Possible Causes:**
1. Target agent not running
2. Target agent not checking messages
3. Message bus issues

**Diagnosis:**
1. Check message bus logs:
   ```bash
   grep -i "message\|publish" .apmas/logs/apmas-*.log
   ```

2. Verify target agent calls `apmas_get_context` to check messages

### Context Not Available

**Symptoms:** `apmas_get_context` returns empty or stale data.

**Cause:** State not persisted or agent completed before saving.

**Solution:**
1. Ensure agents call `apmas_checkpoint` after subtask completion
2. Check database connectivity
3. Verify `apmas_complete` is called with artifacts

## Logging Issues

### Logs Not Appearing in Seq

**Symptoms:** Console logs work but Seq shows nothing.

**Cause:** Seq not running or wrong URL.

**Solution:**
1. Verify Seq is running: http://localhost:5341
2. Check firewall rules
3. If using a different port, update the Serilog configuration

### Log Files Not Being Created

**Symptoms:** No files in `.apmas/logs/`

**Cause:** Directory permissions or disk space.

**Solution:**
1. Check directory permissions
2. Verify disk space
3. Check Serilog configuration for file path

## Database Issues

### State Not Persisting

**Symptoms:** State lost after restart.

**Cause:** Database connection issues or file locking.

**Solution:**
1. Check `.apmas/state.db` exists and is writable
2. Ensure no other process has the database locked
3. Check for SQLite errors in logs

### Checkpoint Recovery Failed

**Symptoms:** Agent restarts without previous context.

**Cause:** Checkpoint file corrupted or missing.

**Solution:**
1. Check checkpoint directory:
   ```bash
   ls -la .apmas/checkpoints/
   ```

2. Verify checkpoint JSON is valid:
   ```bash
   cat .apmas/checkpoints/<agent>-*.json | jq .
   ```

## Reading Logs

### Log Format

APMAS logs use this format:
```
[HH:mm:ss LVL] [SourceContext] Message
```

Example:
```
[14:32:15 INF] [SupervisorService] Agent 'developer' spawned successfully
[14:35:22 WRN] [HeartbeatMonitor] Agent 'developer' missed heartbeat
[14:40:30 ERR] [TimeoutHandler] Agent 'developer' timed out (attempt 1/3)
```

### Log Levels

| Level | Meaning |
|-------|---------|
| VRB | Verbose - detailed debugging |
| DBG | Debug - diagnostic information |
| INF | Information - normal operation |
| WRN | Warning - potential issues |
| ERR | Error - failures that may be recoverable |
| FTL | Fatal - unrecoverable failures |

### Useful Log Searches

```bash
# Find all errors
grep "\[ERR\]\|\[FTL\]" .apmas/logs/apmas-*.log

# Track specific agent
grep "developer" .apmas/logs/apmas-*.log

# Find timeout events
grep -i "timeout\|timed out" .apmas/logs/apmas-*.log

# Find escalations
grep -i "escalat" .apmas/logs/apmas-*.log

# Find heartbeat issues
grep -i "heartbeat" .apmas/logs/apmas-*.log
```

### Using Seq for Log Analysis

1. Open Seq at http://localhost:5341
2. Use structured queries:
   - `AgentRole = "developer"` - Filter by agent
   - `@Level = "Error"` - Show only errors
   - `Application = "APMAS"` - All APMAS logs

## Manual Recovery Steps

### Reset Agent State

If an agent is stuck, you can manually reset its state:

1. Stop APMAS server
2. Open `.apmas/state.db` with a SQLite client
3. Update the agent status:
   ```sql
   UPDATE AgentStates
   SET Status = 'Pending', RetryCount = 0
   WHERE Role = 'developer';
   ```
4. Restart APMAS server

### Clear All State

To start fresh:

1. Stop APMAS server
2. Delete or rename the `.apmas` directory:
   ```bash
   mv .apmas .apmas-backup
   ```
3. Restart APMAS server

### Manually Trigger Agent

For debugging, you can manually run an agent using Claude Code:

```bash
claude --print --model sonnet "Your prompt here"
```

## Performance Issues

### Slow Agent Spawning

**Cause:** Model loading or network latency.

**Solution:**
1. Consider using `sonnet` model (faster) vs `opus` for non-critical agents
2. Check network connectivity
3. Review agent prompt size (large prompts = slower start)

### High Memory Usage

**Cause:** Many concurrent agents or large checkpoints.

**Solution:**
1. Limit concurrent agents in roster design
2. Clear old checkpoint files periodically
3. Monitor with task manager or `htop`

## Getting Help

If you encounter issues not covered here:

1. Check the [APMAS Specification](../APMAS-SPEC.md) for detailed behavior
2. Review logs with increased verbosity (set `MinimumLevel` to `Debug`)
3. File an issue at https://github.com/cpike5/agent-orchestrator/issues
