// ============================================
// State Management
// ============================================
const state = {
    project: null,
    agents: new Map(),
    messages: [],
    filters: {
        agent: '',
        types: new Set(),
        searchQuery: ''
    },
    connection: 'connecting',
    autoScroll: true,
    startTime: null,
    eventSource: null
};

// ============================================
// Utility Functions
// ============================================
function formatTime(seconds) {
    // Handle invalid/negative values (e.g., clock skew)
    if (seconds == null || isNaN(seconds) || seconds < 0) return '0s';

    seconds = Math.floor(seconds);
    if (seconds < 60) return `${seconds}s`;
    if (seconds < 3600) {
        const m = Math.floor(seconds / 60);
        const s = seconds % 60;
        return `${m}m ${s}s`;
    }
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = seconds % 60;
    return `${h}h ${m}m ${s}s`;
}

function formatTimestamp(isoString) {
    const date = new Date(isoString);
    return date.toLocaleString('en-US', {
        month: 'short',
        day: 'numeric',
        year: 'numeric',
        hour: 'numeric',
        minute: '2-digit',
        second: '2-digit',
        hour12: true
    });
}

function formatTimeOnly(isoString) {
    const date = new Date(isoString);
    return date.toLocaleTimeString('en-US', {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false
    });
}

function getTimezoneInfo() {
    const date = new Date();
    const formatter = new Intl.DateTimeFormat('en-US', {
        timeZoneName: 'long'
    });

    const parts = formatter.formatToParts(date);
    const timeZonePart = parts.find(part => part.type === 'timeZoneName');
    const timeZoneName = timeZonePart ? timeZonePart.value : 'Local Time';

    const shortFormatter = new Intl.DateTimeFormat('en-US', {
        timeZoneName: 'short'
    });
    const shortParts = shortFormatter.formatToParts(date);
    const shortTimeZonePart = shortParts.find(part => part.type === 'timeZoneName');
    const shortName = shortTimeZonePart ? shortTimeZonePart.value : '';

    return {
        long: timeZoneName,
        short: shortName
    };
}

function truncate(str, length) {
    if (!str) return '';
    if (str.length <= length) return str;
    return str.substring(0, length) + '...';
}

function getStatusColor(status) {
    const colors = {
        'Pending': '#6b7280',
        'Queued': '#3b82f6',
        'Spawning': '#06b6d4',
        'Running': '#10b981',
        'Paused': '#f59e0b',
        'Completed': '#059669',
        'Failed': '#ef4444',
        'TimedOut': '#dc2626',
        'Escalated': '#8b5cf6'
    };
    return colors[status] || '#6b7280';
}

function getStatusClass(status) {
    return `agent-card--${(status || 'pending').toLowerCase()}`;
}

function getMessageTypeColor(type) {
    const colors = {
        'Info': 'info',
        'Progress': 'progress',
        'Done': 'done',
        'Error': 'error',
        'Help': 'help',
        'Heartbeat': 'heartbeat',
        'Checkpoint': 'checkpoint'
    };
    return colors[type] || 'info';
}

function groupMessagesByTime(messages) {
    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const yesterday = new Date(today);
    yesterday.setDate(yesterday.getDate() - 1);

    const groups = {
        today: [],
        yesterday: [],
        earlier: []
    };

    messages.forEach(msg => {
        const msgDate = new Date(msg.timestamp);
        const msgDay = new Date(msgDate.getFullYear(), msgDate.getMonth(), msgDate.getDate());

        if (msgDay.getTime() === today.getTime()) {
            groups.today.push(msg);
        } else if (msgDay.getTime() === yesterday.getTime()) {
            groups.yesterday.push(msg);
        } else {
            groups.earlier.push(msg);
        }
    });

    return groups;
}

// ============================================
// API Functions
// ============================================
async function loadInitialState() {
    try {
        const response = await fetch('/api/dashboard/state');
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        const data = await response.json();
        state.project = data.project;
        state.startTime = new Date(data.project.startedAt).getTime();

        data.agents.forEach(agent => {
            state.agents.set(agent.role, agent);
        });

        state.messages = data.recentMessages || [];
        updateAgentFilterOptions();
        render();
        connectSSE();
    } catch (error) {
        console.error('Failed to load initial state:', error);
        showError('Failed to load dashboard', error.message);
        setTimeout(loadInitialState, 3000);
    }
}

function connectSSE() {
    if (state.eventSource) {
        state.eventSource.close();
    }

    state.eventSource = new EventSource('/dashboard/events');

    state.eventSource.onopen = () => {
        setConnectionStatus('connected');
    };

    state.eventSource.addEventListener('agent-update', (event) => {
        try {
            const data = JSON.parse(event.data);
            if (data.data) {
                state.agents.set(data.data.role, data.data);
                renderAgentCard(data.data);
                renderDependencyGraph();
                updateSummaryStats();
            }
        } catch (error) {
            console.error('Error processing agent-update:', error);
        }
    });

    state.eventSource.addEventListener('message', (event) => {
        try {
            const data = JSON.parse(event.data);
            if (data.data) {
                state.messages.unshift(data.data);
                if (state.messages.length > 100) {
                    state.messages.pop();
                }
                renderMessages();
                updateSummaryStats();
            }
        } catch (error) {
            console.error('Error processing message:', error);
        }
    });

    state.eventSource.addEventListener('checkpoint', (event) => {
        try {
            const data = JSON.parse(event.data);
        } catch (error) {
            console.error('Error processing checkpoint:', error);
        }
    });

    state.eventSource.onerror = () => {
        setConnectionStatus('reconnecting');
        setTimeout(() => {
            if (state.connection === 'reconnecting') {
                connectSSE();
            }
        }, 5000);
    };
}

// ============================================
// Render Functions
// ============================================
function render() {
    renderHeader();
    renderAgentCards();
    renderDependencyGraph();
    renderMessages();
    updateSummaryStats();
}

function renderHeader() {
    const projectElement = document.getElementById('projectName');
    const phaseElement = document.getElementById('phaseBadge');

    if (state.project) {
        projectElement.textContent = state.project.name;
        phaseElement.setAttribute('data-phase', state.project.phase);
        const phaseText = phaseElement.querySelector('.phase-badge__text');
        if (phaseText) {
            phaseText.textContent = `Phase: ${state.project.phase}`;
        }

        const startedElement = document.getElementById('startedAt');
        startedElement.textContent = formatTimestamp(state.project.startedAt);

        const tzInfo = getTimezoneInfo();
        const tzElement = document.getElementById('timezoneInfo');
        tzElement.textContent = `All times in ${tzInfo.short} (${tzInfo.long})`;
        tzElement.title = `Your local timezone: ${tzInfo.long}`;

        const completed = state.project.completedAgentCount;
        const total = state.project.totalAgentCount;
        const percentage = total > 0 ? Math.round((completed / total) * 100) : 0;

        document.getElementById('progressBarFill').style.width = `${percentage}%`;
        document.getElementById('progressText').textContent = `${completed} of ${total} agents complete (${percentage}%)`;

        renderProgressMarkers();
    }
}

function renderProgressMarkers() {
    const container = document.getElementById('progressMarkers');
    container.innerHTML = '';

    const totalAgents = state.project.totalAgentCount;
    if (totalAgents <= 1) return;

    const milestones = [25, 50, 75];
    milestones.forEach(percent => {
        const marker = document.createElement('div');
        marker.className = 'progress-marker progress-marker--major';
        marker.style.left = `${percent}%`;
        marker.title = `${percent}% completion`;
        container.appendChild(marker);
    });
}

function updateHeaderTime() {
    if (!state.project) return;

    const now = Date.now();
    const elapsed = Math.floor((now - state.startTime) / 1000);
    document.getElementById('elapsedTime').textContent = formatTime(elapsed);
}

function renderAgentCards() {
    const container = document.getElementById('agentCards');
    container.innerHTML = '';

    state.agents.forEach((agent) => {
        const card = createAgentCard(agent);
        container.appendChild(card);
    });
}

function renderAgentCard(agent) {
    const container = document.getElementById('agentCards');
    const existingCard = container.querySelector(`[data-agent="${agent.role}"]`);

    const newCard = createAgentCard(agent);

    if (existingCard) {
        existingCard.replaceWith(newCard);
    } else {
        container.appendChild(newCard);
    }
}

function createAgentCard(agent) {
    const card = document.createElement('div');
    card.className = `agent-card ${getStatusClass(agent.status)}`;
    card.setAttribute('data-agent', agent.role);
    card.setAttribute('tabindex', '0');
    card.setAttribute('role', 'button');
    card.setAttribute('aria-label', `View details for ${agent.role}`);

    const statusColor = getStatusColor(agent.status);

    let html = `
                <div class="agent-header">
                    <div class="agent-status-icon" style="background-color: ${statusColor}"></div>
                    <h3 class="agent-name">${agent.role}</h3>
                </div>
                <div class="agent-status-time" style="color: ${statusColor}">
                    ${agent.status} • ${formatTime(agent.elapsedSeconds)}
                </div>
            `;

    if (agent.retryCount > 0) {
        html += `<div class="agent-retry">Retry: ${agent.retryCount}</div>`;
    }

    if (agent.dependencies && agent.dependencies.length > 0) {
        const allMet = agent.dependencies.every(dep => {
            const depAgent = state.agents.get(dep);
            return depAgent && depAgent.status === 'Completed';
        });

        if (allMet) {
            html += `
                        <div class="agent-dependency agent-dependency--satisfied">
                            <span aria-label="Completed">
                                <svg class="dependency-icon dependency-icon--satisfied" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" aria-hidden="true">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
                                </svg>
                            </span>
                            All dependencies met
                        </div>
                    `;
        } else {
            const unmet = agent.dependencies.filter(dep => {
                const depAgent = state.agents.get(dep);
                return !depAgent || depAgent.status !== 'Completed';
            });
            html += `
                        <div class="agent-dependency agent-dependency--waiting">
                            <span aria-label="Waiting">
                                <svg class="dependency-icon dependency-icon--waiting" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" aria-hidden="true">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6l4 2" />
                                    <circle cx="12" cy="12" r="10" />
                                </svg>
                            </span>
                            Waiting: ${unmet.join(', ')}
                        </div>
                    `;
        }
    }

    if (agent.checkpoint) {
        html += `
                    <div class="agent-checkpoint" aria-describedby="checkpoint-${agent.role}">
                        ${truncate(agent.checkpoint.summary, 30)}
                        <div class="tooltip" id="checkpoint-${agent.role}" role="tooltip">
                            <strong>Checkpoint Summary:</strong><br>
                            ${escapeHtml(agent.checkpoint.summary)}
                        </div>
                    </div>
                    <div class="agent-progress-bar">
                        <div class="agent-progress-bar__fill" style="width: ${agent.checkpoint.progress || 0}%"></div>
                    </div>
                    <div class="agent-progress-text">${agent.checkpoint.progress || 0}% complete</div>
                `;
    }

    if (agent.lastMessage) {
        html += `<div class="agent-last-message">Last: ${truncate(agent.lastMessage, 40)}</div>`;
    }

    card.innerHTML = html;

    card.addEventListener('click', () => {
        openAgentDetailPanel(agent.role);
    });

    card.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            openAgentDetailPanel(agent.role);
        }
    });

    return card;
}

function renderDependencyGraph() {
    const container = document.getElementById('dependencyGraph');
    let html = '';

    const visited = new Set();
    const processed = new Set();

    state.agents.forEach((agent) => {
        if (!agent.dependencies || agent.dependencies.length === 0) {
            if (!visited.has(agent.role)) {
                html += renderAgentNode(agent, 0, visited, processed);
            }
        }
    });

    state.agents.forEach((agent) => {
        if (!processed.has(agent.role)) {
            html += renderAgentNode(agent, 0, visited, processed);
        }
    });

    container.innerHTML = html.trim();
}

function renderAgentNode(agent, depth, visited, processed) {
    if (visited.has(agent.role)) {
        return '';
    }
    visited.add(agent.role);

    const indent = '  '.repeat(depth);

    let statusClass = 'dep-waiting';
    let statusIcon = '';

    if (agent.status === 'Completed') {
        statusClass = 'dep-completed';
        statusIcon = '✓';
    } else if (agent.status === 'Running') {
        statusClass = 'dep-running';
        statusIcon = '';
    } else if (agent.status === 'Failed' || agent.status === 'TimedOut') {
        statusClass = 'dep-failed';
        statusIcon = '✗';
    }

    const statusText = agent.status && agent.status !== 'Completed' ? ` (${agent.status.toLowerCase()})` : '';

    let output = `${indent}<span class="dep-node ${statusClass}">${statusIcon ? '<span class="dep-icon">' + statusIcon + '</span>' : ''}${agent.role}${statusText}</span>\n`;

    if (agent.dependencies && agent.dependencies.length > 0) {
        agent.dependencies.forEach((dep, index) => {
            const depAgent = state.agents.get(dep);
            if (depAgent && !visited.has(dep)) {
                const isLast = index === agent.dependencies.length - 1;
                const prefix = isLast ? '└─→ ' : '├─→ ';

                let depStatusClass = 'dep-waiting';
                if (depAgent.status === 'Completed') depStatusClass = 'dep-completed';
                else if (depAgent.status === 'Running') depStatusClass = 'dep-running';
                else if (depAgent.status === 'Failed' || depAgent.status === 'TimedOut') depStatusClass = 'dep-failed';

                output += `${'  '.repeat(depth + 1)}<span class="dep-tree">${prefix}</span><span class="dep-node ${depStatusClass}">${dep}</span> <span class="dep-waiting">(${(depAgent.status || 'pending').toLowerCase()})</span>\n`;
            }
        });
    }

    processed.add(agent.role);
    return output;
}

function updateAgentFilterOptions() {
    const select = document.getElementById('agentFilter');
    const existingOptions = Array.from(select.options).map(o => o.value);

    state.agents.forEach((agent) => {
        if (agent.role !== '' && !existingOptions.includes(agent.role)) {
            const option = document.createElement('option');
            option.value = agent.role;
            option.textContent = agent.role;
            select.appendChild(option);
        }
    });
}

function renderMessages() {
    const container = document.getElementById('messagesList');

    const filtered = state.messages.filter(msg => {
        const agentMatch = !state.filters.agent || msg.from === state.filters.agent || msg.to === state.filters.agent;
        const typeMatch = state.filters.types.size === 0 || state.filters.types.has(msg.type);
        const searchMatch = !state.filters.searchQuery ||
            msg.content.toLowerCase().includes(state.filters.searchQuery);
        return agentMatch && typeMatch && searchMatch;
    });

    if (filtered.length === 0) {
        container.innerHTML = `
                    <div class="messages-empty">
                        <div class="messages-empty-icon">
                            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" aria-hidden="true">
                                <path stroke-linecap="round" stroke-linejoin="round" d="M20.25 8.511c.884.284 1.5 1.128 1.5 2.097v4.286c0 1.136-.847 2.1-1.98 2.193-.34.027-.68.052-1.02.072v3.091l-3-3c-1.354 0-2.694-.055-4.02-.163a2.115 2.115 0 01-.825-.242m9.345-8.334a2.126 2.126 0 00-.476-.095 48.64 48.64 0 00-8.048 0c-1.131.094-1.976 1.057-1.976 2.192v4.286c0 .837.46 1.58 1.155 1.951m9.345-8.334V6.637c0-1.621-1.152-3.026-2.76-3.235A48.455 48.455 0 0011.25 3c-2.115 0-4.198.137-6.24.402-1.608.209-2.76 1.614-2.76 3.235v6.226c0 1.621 1.152 3.026 2.76 3.235.577.075 1.157.14 1.74.194V21l4.155-4.155" />
                            </svg>
                        </div>
                        <div class="messages-empty-title">No messages</div>
                        <div class="messages-empty-text">Try adjusting your filters...</div>
                    </div>
                `;
        return;
    }

    const groups = groupMessagesByTime(filtered);
    container.innerHTML = '';

    if (groups.today.length > 0) {
        const groupDiv = document.createElement('div');
        groupDiv.className = 'message-group';
        groupDiv.innerHTML = `
                    <div class="message-group__header">Today</div>
                    <div class="message-group__items"></div>
                `;
        const itemsContainer = groupDiv.querySelector('.message-group__items');
        groups.today.forEach(msg => {
            itemsContainer.appendChild(createMessageItem(msg));
        });
        container.appendChild(groupDiv);
    }

    if (groups.yesterday.length > 0) {
        const groupDiv = document.createElement('div');
        groupDiv.className = 'message-group';
        groupDiv.innerHTML = `
                    <div class="message-group__header">Yesterday</div>
                    <div class="message-group__items"></div>
                `;
        const itemsContainer = groupDiv.querySelector('.message-group__items');
        groups.yesterday.forEach(msg => {
            itemsContainer.appendChild(createMessageItem(msg));
        });
        container.appendChild(groupDiv);
    }

    if (groups.earlier.length > 0) {
        const groupDiv = document.createElement('div');
        groupDiv.className = 'message-group';
        groupDiv.innerHTML = `
                    <div class="message-group__header">Earlier</div>
                    <div class="message-group__items"></div>
                `;
        const itemsContainer = groupDiv.querySelector('.message-group__items');
        groups.earlier.forEach(msg => {
            itemsContainer.appendChild(createMessageItem(msg));
        });
        container.appendChild(groupDiv);
    }

    if (state.autoScroll) {
        setTimeout(() => {
            container.scrollTop = 0;
        }, 0);
    }
}

function createMessageItem(msg) {
    const item = document.createElement('div');
    const typeColor = getMessageTypeColor(msg.type);

    item.className = `message-item message-item--${typeColor}`;
    item.innerHTML = `
                <div class="message-header">
                    <span class="message-timestamp">${formatTimeOnly(msg.timestamp)}</span>
                    <span class="message-from-to">${msg.from}${msg.to ? ' → ' + msg.to : ''}</span>
                    <span class="message-type-badge message-type-badge--${typeColor}">${msg.type}</span>
                </div>
                <div class="message-content">${escapeHtml(msg.content)}</div>
            `;

    return item;
}

function updateSummaryStats() {
    document.getElementById('statTotalMessages').textContent = state.messages.length;

    const errorCount = state.messages.filter(m => m.type === 'Error').length;
    document.getElementById('statErrors').textContent = errorCount;

    const activeCount = Array.from(state.agents.values())
        .filter(a => a.status === 'Running' || a.status === 'Spawning').length;
    document.getElementById('statActive').textContent = activeCount;

    const completedAgents = Array.from(state.agents.values())
        .filter(a => a.status === 'Completed');

    if (completedAgents.length > 0) {
        const avgTime = completedAgents.reduce((sum, a) => sum + a.elapsedSeconds, 0) / completedAgents.length;
        document.getElementById('statAvgTime').textContent = formatTime(Math.round(avgTime));
    } else {
        document.getElementById('statAvgTime').textContent = '--';
    }
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// ============================================
// Agent Detail Panel Functions
// ============================================
function openAgentDetailPanel(agentRole) {
    const agent = state.agents.get(agentRole);
    if (!agent) return;

    const overlay = document.getElementById('detailPanelOverlay');
    const title = document.getElementById('detailPanelTitle');
    const subtitle = document.getElementById('detailPanelSubtitle');
    const content = document.getElementById('detailPanelContent');

    title.textContent = agent.role;
    subtitle.textContent = `Status: ${agent.status} • Elapsed: ${formatTime(agent.elapsedSeconds)}`;

    content.innerHTML = renderAgentDetailContent(agent);

    overlay.classList.add('visible');
    document.getElementById('detailPanelClose').focus();
}

function closeAgentDetailPanel() {
    const overlay = document.getElementById('detailPanelOverlay');
    overlay.classList.remove('visible');
}

function renderAgentDetailContent(agent) {
    let html = '';

    html += `
                <div class="detail-section">
                    <h3 class="detail-section__title">Status Information</h3>
                    <div class="detail-row">
                        <span class="detail-row__label">Current Status</span>
                        <span class="detail-row__value detail-row__value--highlight" style="color: ${getStatusColor(agent.status)}">${agent.status}</span>
                    </div>
                    <div class="detail-row">
                        <span class="detail-row__label">Elapsed Time</span>
                        <span class="detail-row__value">${formatTime(agent.elapsedSeconds)}</span>
                    </div>
                    <div class="detail-row">
                        <span class="detail-row__label">Retry Count</span>
                        <span class="detail-row__value">${agent.retryCount}</span>
                    </div>
                    <div class="detail-row">
                        <span class="detail-row__label">Last Updated</span>
                        <span class="detail-row__value">${agent.lastUpdatedAt ? formatTimestamp(agent.lastUpdatedAt) : 'N/A'}</span>
                    </div>
                </div>
            `;

    if (agent.dependencies && agent.dependencies.length > 0) {
        html += `
                    <div class="detail-section">
                        <h3 class="detail-section__title">Dependencies</h3>
                        <div class="dependency-list">
                `;

        agent.dependencies.forEach(dep => {
            const depAgent = state.agents.get(dep);
            const status = depAgent ? depAgent.status : 'Unknown';
            const statusClass = depAgent && depAgent.status === 'Completed' ? 'completed' :
                depAgent && depAgent.status === 'Running' ? 'running' : 'pending';

            const icon = depAgent && depAgent.status === 'Completed'
                ? '<path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />'
                : '<path stroke-linecap="round" stroke-linejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />';

            html += `
                        <div class="dependency-item">
                            <svg class="dependency-item__icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" aria-hidden="true">
                                ${icon}
                            </svg>
                            <span class="dependency-item__name">${dep}</span>
                            <span class="dependency-item__status dependency-item__status--${statusClass}">${status}</span>
                        </div>
                    `;
        });

        html += `
                        </div>
                    </div>
                `;
    }

    if (agent.checkpoint) {
        html += `
                    <div class="detail-section">
                        <h3 class="detail-section__title">Progress Checkpoint</h3>
                        <div class="checkpoint-detail">
                            <div class="checkpoint-detail__summary">${escapeHtml(agent.checkpoint.summary)}</div>
                            <div class="checkpoint-detail__progress">
                                <div class="checkpoint-detail__bar">
                                    <div class="checkpoint-detail__bar-fill" style="width: ${agent.checkpoint.progress || 0}%"></div>
                                </div>
                                <span class="checkpoint-detail__percent">${agent.checkpoint.progress || 0}%</span>
                            </div>
                        </div>
                        <div class="detail-row">
                            <span class="detail-row__label">Checkpoint Created</span>
                            <span class="detail-row__value">${formatTimestamp(agent.checkpoint.timestamp)}</span>
                        </div>
                    </div>
                `;
    }

    const agentMessages = state.messages.filter(m => m.from === agent.role || m.to === agent.role).slice(0, 10);
    if (agentMessages.length > 0) {
        html += `
                    <div class="detail-section">
                        <h3 class="detail-section__title">Recent Messages (Last 10)</h3>
                        <div class="panel-message-list">
                `;

        agentMessages.forEach(msg => {
            const typeColor = getMessageTypeColor(msg.type);
            const borderColor = `var(--msg-${typeColor})`;

            html += `
                        <div class="panel-message-item" style="border-left-color: ${borderColor}">
                            <div class="panel-message-item__time">
                                ${formatTimeOnly(msg.timestamp)} • ${msg.type}
                                ${msg.to ? ` → ${msg.to}` : ''}
                            </div>
                            <div class="panel-message-item__content">${escapeHtml(msg.content)}</div>
                        </div>
                    `;
        });

        html += `
                        </div>
                    </div>
                `;
    }

    return html;
}

// ============================================
// Connection Status
// ============================================
function setConnectionStatus(status) {
    state.connection = status;

    const indicator = document.getElementById('connectionStatus');
    const dot = indicator.querySelector('.status-dot');
    const text = document.getElementById('connectionText');
    const overlay = document.getElementById('reconnectingOverlay');

    switch (status) {
        case 'connected':
            dot.className = 'status-dot status-dot--connected';
            text.textContent = 'Connected';
            overlay.classList.remove('visible');
            break;
        case 'reconnecting':
            dot.className = 'status-dot status-dot--reconnecting';
            text.textContent = 'Reconnecting...';
            overlay.classList.add('visible');
            break;
        case 'disconnected':
            dot.className = 'status-dot status-dot--disconnected';
            text.textContent = 'Disconnected';
            overlay.classList.add('visible');
            break;
    }
}

// ============================================
// Error Handling
// ============================================
function showError(title, message) {
    const banner = document.getElementById('errorBanner');
    const titleEl = document.getElementById('errorTitle');
    const textEl = document.getElementById('errorText');

    titleEl.textContent = title;
    textEl.textContent = message;
    banner.classList.add('visible');

    setTimeout(() => {
        banner.classList.remove('visible');
    }, 10000);
}

// ============================================
// Event Listeners
// ============================================
document.getElementById('errorClose').addEventListener('click', () => {
    document.getElementById('errorBanner').classList.remove('visible');
});

document.getElementById('agentFilter').addEventListener('change', (e) => {
    state.filters.agent = e.target.value;
    renderMessages();
});

// Type filter chips
const messageTypes = ['Info', 'Progress', 'Done', 'Error', 'Help', 'Heartbeat', 'Checkpoint'];
const chipContainer = document.getElementById('typeFilters');

messageTypes.forEach(type => {
    const chip = document.createElement('button');
    chip.className = 'filter-chip';
    chip.textContent = type;
    chip.setAttribute('data-type', type);

    chip.addEventListener('click', () => {
        if (state.filters.types.has(type)) {
            state.filters.types.delete(type);
            chip.classList.remove('filter-chip--active');
        } else {
            state.filters.types.add(type);
            chip.classList.add('filter-chip--active');
        }

        const resetButton = document.getElementById('filterReset');
        resetButton.disabled = state.filters.types.size === 0;

        renderMessages();
    });

    chipContainer.appendChild(chip);
});

// Message search
document.getElementById('messageSearch').addEventListener('input', (e) => {
    state.filters.searchQuery = e.target.value.toLowerCase().trim();

    const clearButton = document.getElementById('searchClear');
    if (state.filters.searchQuery) {
        clearButton.style.display = 'flex';
    } else {
        clearButton.style.display = 'none';
    }

    renderMessages();
});

document.getElementById('searchClear').addEventListener('click', () => {
    document.getElementById('messageSearch').value = '';
    state.filters.searchQuery = '';
    document.getElementById('searchClear').style.display = 'none';
    renderMessages();
});

// Clear all type filters
document.getElementById('filterReset').addEventListener('click', () => {
    state.filters.types.clear();

    document.querySelectorAll('.filter-chip').forEach(chip => {
        chip.classList.remove('filter-chip--active');
    });

    document.getElementById('filterReset').disabled = true;

    renderMessages();
});

// Auto-scroll control
const messagesList = document.getElementById('messagesList');
const autoScrollButton = document.getElementById('autoScrollControl');

messagesList.addEventListener('scroll', () => {
    const isAtTop = messagesList.scrollTop < 50;
    state.autoScroll = isAtTop;

    if (state.autoScroll) {
        autoScrollButton.classList.remove('visible');
    } else {
        autoScrollButton.classList.add('visible');
    }
});

autoScrollButton.addEventListener('click', () => {
    state.autoScroll = true;
    messagesList.scrollTop = 0;
    autoScrollButton.classList.remove('visible');
});

// Detail panel close
document.getElementById('detailPanelClose').addEventListener('click', closeAgentDetailPanel);

document.getElementById('detailPanelOverlay').addEventListener('click', (e) => {
    if (e.target.id === 'detailPanelOverlay') {
        closeAgentDetailPanel();
    }
});

// Keyboard: Escape to close detail panel
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        const overlay = document.getElementById('detailPanelOverlay');
        if (overlay.classList.contains('visible')) {
            closeAgentDetailPanel();
        }
    }
});

// Dependency graph toggle
document.getElementById('dependencyToggle').addEventListener('click', () => {
    const container = document.querySelector('.dependency-graph-container');
    container.classList.toggle('collapsed');

    const button = document.getElementById('dependencyToggle');
    const isCollapsed = container.classList.contains('collapsed');
    button.setAttribute('aria-label', isCollapsed ? 'Expand dependency graph' : 'Collapse dependency graph');
});

// Update elapsed time every second
setInterval(updateHeaderTime, 1000);

// ============================================
// Initialization
// ============================================
loadInitialState();