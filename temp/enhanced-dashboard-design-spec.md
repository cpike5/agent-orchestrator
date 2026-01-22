# APMAS Dashboard - Enhanced Design Specification

## Document Overview

This specification defines the enhanced design for the APMAS Dashboard, addressing critical accessibility issues, implementing visual refinements, and adding new features to improve usability and information density. This document serves as the complete implementation guide for the html-prototyper agent.

**Version:** 2.0
**Date:** 2026-01-22
**Target:** `src/Apmas.Server/wwwroot/dashboard.html`

---

## Table of Contents

1. [Critical Fixes](#1-critical-fixes)
2. [Color Token Updates](#2-color-token-updates)
3. [Header Enhancements](#3-header-enhancements)
4. [Agent Card Improvements](#4-agent-card-improvements)
5. [Agent Detail Panel](#5-agent-detail-panel)
6. [Message Stream Enhancements](#6-message-stream-enhancements)
7. [Dependency Graph Refinements](#7-dependency-graph-refinements)
8. [Summary Statistics Row](#8-summary-statistics-row)
9. [Animation & Interaction Updates](#9-animation--interaction-updates)
10. [Accessibility Requirements](#10-accessibility-requirements)
11. [Implementation Checklist](#11-implementation-checklist)

---

## 1. Critical Fixes

### 1.1 ARIA Live Regions

**Issue:** Screen reader users cannot perceive real-time updates to agent status or new messages.

**Solution:** Add ARIA live regions for dynamic content areas.

#### Implementation

**Agent Cards Container:**
```html
<div class="agent-cards" id="agentCards" aria-live="polite" aria-atomic="false">
    <!-- Agent cards here -->
</div>
```

**Message Stream Container:**
```html
<div class="messages-list" id="messagesList" aria-live="polite" aria-atomic="false">
    <!-- Messages here -->
</div>
```

**Connection Status:**
```html
<div class="connection-status" id="connectionStatus" role="status" aria-live="polite">
    <span class="status-dot status-dot--disconnected"></span>
    <span id="connectionText">Connecting...</span>
</div>
```

**Notes:**
- Use `aria-live="polite"` to avoid interrupting screen reader users
- Set `aria-atomic="false"` so only new content is announced, not the entire container
- Connection status uses `role="status"` for immediate updates

---

### 1.2 Improved Tertiary Text Contrast

**Issue:** Current tertiary text color `#6b7280` has a contrast ratio of 3.8:1 against `#0f1419`, which is at the lower boundary of WCAG AA compliance.

**Solution:** Increase tertiary text color to `#8b95a5` for a safer contrast ratio of 5.2:1.

#### CSS Variable Update

```css
:root {
    /* OLD: --color-text-tertiary: #6b7280; */
    --color-text-tertiary: #8b95a5;
}
```

**Affected Elements:**
- `.agent-checkpoint` - Checkpoint summaries in agent cards
- `.agent-last-message` - Last message preview in agent cards
- `.message-timestamp` - Message timestamps
- `.timezone-info` - Timezone display in header
- `.filter-label` - Filter control labels
- `.dep-tree` - Dependency graph tree lines
- Empty state text

**Impact:** Improved readability for colorblind users and users in bright environments.

---

### 1.3 Timezone Display with Intl API

**Issue:** Current timezone display shows raw UTC offset (e.g., "UTC+8:00") which requires manual interpretation.

**Solution:** Use JavaScript Intl API to display proper timezone names (e.g., "Pacific Standard Time").

#### JavaScript Implementation

Replace the `getTimezoneOffset()` function with:

```javascript
function getTimezoneInfo() {
    const date = new Date();
    const formatter = new Intl.DateTimeFormat('en-US', {
        timeZoneName: 'long'
    });

    // Extract timezone name from formatted string
    const parts = formatter.formatToParts(date);
    const timeZonePart = parts.find(part => part.type === 'timeZoneName');
    const timeZoneName = timeZonePart ? timeZonePart.value : 'Local Time';

    // Get short abbreviation (PST, EST, etc.)
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
```

Update the header rendering:

```javascript
function renderHeader() {
    // ... existing code ...

    // Timezone display
    const tzInfo = getTimezoneInfo();
    const tzElement = document.getElementById('timezoneInfo');
    tzElement.textContent = `All times in ${tzInfo.short} (${tzInfo.long})`;
    tzElement.title = `Your local timezone: ${tzInfo.long}`;
}
```

**Example Output:**
- "All times in PST (Pacific Standard Time)"
- "All times in EST (Eastern Standard Time)"

---

### 1.4 Accessibility Text for Emoji Indicators

**Issue:** Emoji indicators (✓, ⏳) in agent cards and dependency graphs are announced as their literal Unicode names by screen readers.

**Solution:** Replace emojis with accessible text wrapped in appropriate ARIA labels.

#### Implementation

**Agent Dependency Satisfied:**
```html
<!-- OLD -->
<div class="agent-dependency agent-dependency--satisfied">✓ All dependencies met</div>

<!-- NEW -->
<div class="agent-dependency agent-dependency--satisfied">
    <span aria-label="Completed">
        <svg class="dependency-icon dependency-icon--satisfied" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" aria-hidden="true">
            <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
        </svg>
    </span>
    All dependencies met
</div>
```

**Agent Dependency Waiting:**
```html
<!-- OLD -->
<div class="agent-dependency agent-dependency--waiting">⏳ Waiting: Backend, Database</div>

<!-- NEW -->
<div class="agent-dependency agent-dependency--waiting">
    <span aria-label="Waiting">
        <svg class="dependency-icon dependency-icon--waiting" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" aria-hidden="true">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6l4 2" />
            <circle cx="12" cy="12" r="10" />
        </svg>
    </span>
    Waiting: Backend, Database
</div>
```

**CSS for Icons:**
```css
.dependency-icon {
    width: 14px;
    height: 14px;
    display: inline-block;
    vertical-align: middle;
    margin-right: var(--space-1);
}

.dependency-icon--satisfied {
    color: var(--color-success);
}

.dependency-icon--waiting {
    color: var(--color-warning);
}
```

**Dependency Graph:**

For the text-based dependency graph, add screen reader context:

```javascript
function renderAgentNode(agent, depth, visited, processed) {
    if (visited.has(agent.role)) {
        return '';
    }
    visited.add(agent.role);

    const indent = '  '.repeat(depth);
    const statusIcon = agent.status === 'Completed' ? '✓' : '';
    const statusText = agent.status !== 'Completed' ? ` (${agent.status.toLowerCase()})` : '';

    // Add screen reader text
    const srText = agent.status === 'Completed' ? 'Completed: ' : '';

    let output = indent + (statusIcon ? statusIcon + ' ' : '') + srText + agent.role + statusText + '\n';

    // ... rest of function
}
```

**Alternative:** For simplicity, you can also use `aria-label` on parent elements:

```html
<div class="agent-dependency agent-dependency--satisfied" aria-label="All dependencies completed">
    <span aria-hidden="true">✓</span> All dependencies met
</div>

<div class="agent-dependency agent-dependency--waiting" aria-label="Waiting for dependencies">
    <span aria-hidden="true">⏳</span> Waiting: Backend, Database
</div>
```

---

### 1.5 Clearer Filter Chip Labeling

**Issue:** The "Type Filters" label is ambiguous—users may not understand that clicking chips toggles message type visibility.

**Solution:** Update filter control labels to be more explicit and add a "Clear All" button.

#### HTML Updates

```html
<div class="filter-controls">
    <label class="filter-label">
        <span class="filter-label-text">Show from:</span>
        <select class="filter-select" id="agentFilter" aria-label="Filter messages by agent">
            <option value="">All Agents</option>
        </select>
    </label>

    <div class="filter-group">
        <span class="filter-label-text">Show message types:</span>
        <div class="filter-chips" id="typeFilters" role="group" aria-label="Message type filters">
            <!-- Dynamically populated chips -->
        </div>
        <button class="filter-reset" id="filterReset" aria-label="Clear all type filters">
            Clear All
        </button>
    </div>
</div>
```

#### CSS Updates

```css
.filter-controls {
    display: grid;
    grid-template-columns: auto 1fr;
    gap: var(--space-4);
    padding-bottom: var(--space-3);
    border-bottom: 1px solid var(--color-border-subtle);
    margin-bottom: var(--space-3);
    align-items: start;
}

.filter-label {
    font-size: var(--text-xs);
    color: var(--color-text-tertiary);
    font-weight: var(--font-semibold);
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
}

.filter-label-text {
    text-transform: uppercase;
    letter-spacing: 0.05em;
}

.filter-group {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
}

.filter-group .filter-label-text {
    font-size: var(--text-xs);
    color: var(--color-text-tertiary);
    text-transform: uppercase;
    font-weight: var(--font-semibold);
    letter-spacing: 0.05em;
}

.filter-chips {
    display: flex;
    gap: var(--space-2);
    flex-wrap: wrap;
}

.filter-reset {
    background-color: transparent;
    border: 1px solid var(--color-border-default);
    border-radius: var(--radius-md);
    padding: var(--space-1) var(--space-3);
    font-size: var(--text-xs);
    font-weight: var(--font-medium);
    color: var(--color-text-secondary);
    cursor: pointer;
    transition: all 0.2s ease;
    align-self: flex-start;
}

.filter-reset:hover {
    background-color: var(--color-bg-hover);
    border-color: var(--color-border-strong);
    color: var(--color-text-primary);
}

.filter-reset:disabled {
    opacity: 0.5;
    cursor: not-allowed;
}
```

#### JavaScript Implementation

```javascript
// Add to event listeners section
document.getElementById('filterReset').addEventListener('click', () => {
    // Clear all type filters
    state.filters.types.clear();

    // Remove active state from all chips
    document.querySelectorAll('.filter-chip').forEach(chip => {
        chip.classList.remove('filter-chip--active');
    });

    // Disable reset button
    document.getElementById('filterReset').disabled = true;

    // Re-render messages
    renderMessages();
});

// Update chip click handler to enable/disable reset button
chip.addEventListener('click', () => {
    if (state.filters.types.has(type)) {
        state.filters.types.delete(type);
        chip.classList.remove('filter-chip--active');
    } else {
        state.filters.types.add(type);
        chip.classList.add('filter-chip--active');
    }

    // Enable/disable reset button
    const resetButton = document.getElementById('filterReset');
    resetButton.disabled = state.filters.types.size === 0;

    renderMessages();
});
```

---

## 2. Color Token Updates

### 2.1 Updated Tertiary Text Color

```css
:root {
    /* Text hierarchy */
    --color-text-primary: #e3e8ef;
    --color-text-secondary: #9ca3af;
    --color-text-tertiary: #8b95a5;  /* UPDATED from #6b7280 */
    --color-text-disabled: #4b5563;
}
```

**Contrast Ratios (Against #0f1419):**
- Primary: 18.5:1 (AAA)
- Secondary: 6.2:1 (AA)
- Tertiary: 5.2:1 (AA) ← Improved from 3.8:1
- Disabled: 2.8:1 (Decorative only)

---

### 2.2 New Tokens for Enhanced Features

Add these new tokens to the `:root` section:

```css
:root {
    /* ... existing tokens ... */

    /* Header accent gradient */
    --color-header-accent-start: #1e3a5f;
    --color-header-accent-end: #0f1419;

    /* Agent detail panel */
    --color-panel-bg: #1a1f28;
    --color-panel-overlay: rgba(0, 0, 0, 0.85);

    /* Summary statistics */
    --color-stats-bg: #252b36;
    --color-stats-border: #2d3748;
    --color-stats-value: #3b82f6;

    /* Message group headers */
    --color-group-header-bg: #1a1f28;
    --color-group-header-text: #9ca3af;

    /* Empty state illustration colors */
    --color-empty-icon: #4a5568;
    --color-empty-icon-bg: rgba(59, 130, 246, 0.1);

    /* Tooltip background */
    --color-tooltip-bg: #2d3748;
    --color-tooltip-text: #e3e8ef;
    --color-tooltip-border: #4a5568;
}
```

---

## 3. Header Enhancements

### 3.1 Subtle Gradient Background

**Current:** Solid `--color-bg-elevated` background
**Enhancement:** Subtle vertical gradient for visual interest

#### CSS Implementation

```css
header {
    background: linear-gradient(
        180deg,
        var(--color-header-accent-start) 0%,
        var(--color-bg-elevated) 100%
    );
    border-bottom: 1px solid var(--color-border-default);
    padding: var(--space-5) var(--space-6);
    position: sticky;
    top: 0;
    z-index: 100;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
}
```

**Visual Effect:** Subtle dark blue gradient that transitions to the elevated background color, creating depth without being distracting.

---

### 3.2 Enhanced Phase Badge

**Current:** Simple border and background
**Enhancement:** Add icon indicator and animation for active phases

#### HTML Structure

```html
<span class="phase-badge" id="phaseBadge" data-phase="Initializing">
    <svg class="phase-badge__icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" aria-hidden="true">
        <path stroke-linecap="round" stroke-linejoin="round" d="M13 10V3L4 14h7v7l9-11h-7z" />
    </svg>
    <span class="phase-badge__text">Phase: Initializing</span>
</span>
```

#### CSS Implementation

```css
.phase-badge {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    font-size: var(--text-sm);
    font-weight: var(--font-medium);
    background-color: var(--status-running-bg);
    border: 1px solid var(--status-running);
    padding: var(--space-1) var(--space-3);
    border-radius: var(--radius-md);
    color: var(--status-running);
    transition: all 0.3s ease;
}

.phase-badge[data-phase="Execution"] {
    animation: pulse-badge 2s ease-in-out infinite;
}

.phase-badge__icon {
    width: 16px;
    height: 16px;
    display: inline-block;
}

@keyframes pulse-badge {
    0%, 100% {
        opacity: 1;
        transform: scale(1);
    }
    50% {
        opacity: 0.8;
        transform: scale(1.02);
    }
}
```

---

### 3.3 Enhanced Progress Bar

**Current:** Simple horizontal bar
**Enhancement:** Add visual markers for key milestones

#### HTML Structure

```html
<div class="progress-section">
    <div class="progress-bar">
        <div class="progress-bar__fill" id="progressBarFill" style="width: 0%">
            <div class="progress-bar__shine"></div>
        </div>
        <div class="progress-bar__markers" id="progressMarkers">
            <!-- Dynamically populated milestone markers -->
        </div>
    </div>
    <span class="progress-text" id="progressText">0 of 0 agents complete (0%)</span>
</div>
```

#### CSS Implementation

```css
.progress-bar {
    flex: 1;
    height: 8px;
    background-color: var(--color-bg-base);
    border-radius: var(--radius-full);
    overflow: visible;
    min-width: 300px;
    position: relative;
}

.progress-bar__fill {
    height: 100%;
    background: linear-gradient(
        90deg,
        var(--status-running) 0%,
        var(--status-completed) 100%
    );
    border-radius: var(--radius-full);
    transition: width 0.5s ease-out;
    position: relative;
    overflow: hidden;
}

.progress-bar__shine {
    position: absolute;
    top: 0;
    left: -100%;
    width: 100%;
    height: 100%;
    background: linear-gradient(
        90deg,
        transparent,
        rgba(255, 255, 255, 0.3),
        transparent
    );
    animation: shine 2s ease-in-out infinite;
}

@keyframes shine {
    0% {
        left: -100%;
    }
    100% {
        left: 200%;
    }
}

.progress-bar__markers {
    position: absolute;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    pointer-events: none;
}

.progress-marker {
    position: absolute;
    top: -2px;
    width: 2px;
    height: 12px;
    background-color: var(--color-border-strong);
    transform: translateX(-50%);
}

.progress-marker--major {
    height: 16px;
    top: -4px;
    width: 3px;
    background-color: var(--color-text-secondary);
}
```

#### JavaScript Implementation

```javascript
function renderProgressMarkers() {
    const container = document.getElementById('progressMarkers');
    container.innerHTML = '';

    const totalAgents = state.project.totalAgentCount;
    if (totalAgents <= 1) return;

    // Add markers at 25%, 50%, 75%
    const milestones = [25, 50, 75];
    milestones.forEach(percent => {
        const marker = document.createElement('div');
        marker.className = 'progress-marker progress-marker--major';
        marker.style.left = `${percent}%`;
        marker.title = `${percent}% completion`;
        container.appendChild(marker);
    });
}

// Call in renderHeader() after setting progress
renderProgressMarkers();
```

---

## 4. Agent Card Improvements

### 4.1 Make Cards Interactive

**Current:** Cards are static display elements
**Enhancement:** Cards become clickable to open detail panel

#### CSS Implementation

```css
.agent-card {
    background-color: var(--color-bg-elevated);
    border: 2px solid;
    border-radius: var(--radius-lg);
    padding: var(--space-4);
    box-shadow: var(--shadow-md);
    transition: all 0.3s ease;
    cursor: pointer; /* NEW */
    position: relative; /* NEW */
}

.agent-card:hover {
    transform: translateY(-2px);
    box-shadow: var(--shadow-lg);
}

.agent-card:focus-within {
    outline: 2px solid var(--color-interactive-default);
    outline-offset: 2px;
}

.agent-card::after {
    content: '';
    position: absolute;
    top: var(--space-4);
    right: var(--space-4);
    width: 20px;
    height: 20px;
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 24 24' stroke-width='2' stroke='%239ca3af'%3E%3Cpath stroke-linecap='round' stroke-linejoin='round' d='M9 5l7 7-7 7' /%3E%3C/svg%3E");
    background-size: contain;
    opacity: 0;
    transition: opacity 0.2s ease, transform 0.2s ease;
}

.agent-card:hover::after {
    opacity: 1;
    transform: translateX(2px);
}

/* Add keyboard support */
.agent-card[tabindex] {
    outline: none;
}

.agent-card[tabindex]:focus {
    outline: 2px solid var(--color-interactive-default);
    outline-offset: 2px;
}
```

#### JavaScript Implementation

```javascript
function createAgentCard(agent) {
    const card = document.createElement('div');
    card.className = `agent-card ${getStatusClass(agent.status)}`;
    card.setAttribute('data-agent', agent.role);
    card.setAttribute('tabindex', '0'); // Make keyboard accessible
    card.setAttribute('role', 'button');
    card.setAttribute('aria-label', `View details for ${agent.role}`);

    // ... existing card HTML generation ...

    // Add click handler
    card.addEventListener('click', () => {
        openAgentDetailPanel(agent.role);
    });

    // Add keyboard handler
    card.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            openAgentDetailPanel(agent.role);
        }
    });

    return card;
}
```

---

### 4.2 Enhanced Checkpoint Display

**Current:** Truncated to 30 characters
**Enhancement:** Add tooltip on hover showing full checkpoint summary

#### CSS Implementation

```css
.agent-checkpoint {
    font-size: var(--text-xs);
    color: var(--color-text-tertiary);
    font-family: var(--font-mono);
    margin-bottom: var(--space-2);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    cursor: help; /* NEW */
    position: relative; /* NEW */
}

/* Tooltip styles */
.tooltip {
    position: absolute;
    bottom: 100%;
    left: 0;
    margin-bottom: var(--space-2);
    padding: var(--space-3);
    background-color: var(--color-tooltip-bg);
    border: 1px solid var(--color-tooltip-border);
    border-radius: var(--radius-md);
    color: var(--color-tooltip-text);
    font-size: var(--text-sm);
    white-space: normal;
    max-width: 320px;
    z-index: 1000;
    box-shadow: var(--shadow-lg);
    opacity: 0;
    pointer-events: none;
    transition: opacity 0.2s ease;
}

.agent-checkpoint:hover .tooltip {
    opacity: 1;
}

/* Arrow pointing down */
.tooltip::after {
    content: '';
    position: absolute;
    top: 100%;
    left: var(--space-4);
    border: 6px solid transparent;
    border-top-color: var(--color-tooltip-bg);
}
```

#### HTML Update in createAgentCard()

```javascript
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
```

---

### 4.3 Visual Status Indicator Enhancement

**Current:** Small colored dot
**Enhancement:** Add pulsing ring animation for Running status

#### CSS Implementation

```css
.agent-status-icon {
    width: 12px;
    height: 12px;
    border-radius: 50%;
    flex-shrink: 0;
    position: relative; /* NEW */
}

.agent-card--running .agent-status-icon::before {
    content: '';
    position: absolute;
    top: -4px;
    left: -4px;
    right: -4px;
    bottom: -4px;
    border: 2px solid var(--status-running);
    border-radius: 50%;
    animation: pulse-ring 2s ease-out infinite;
}

@keyframes pulse-ring {
    0% {
        transform: scale(0.95);
        opacity: 1;
    }
    100% {
        transform: scale(1.3);
        opacity: 0;
    }
}
```

---

## 5. Agent Detail Panel

### 5.1 Panel Structure

**Feature:** Slide-out panel from the right side displaying comprehensive agent details when an agent card is clicked.

#### HTML Structure

Add to the end of `<body>`, before closing tag:

```html
<!-- Agent Detail Panel -->
<div class="detail-panel-overlay" id="detailPanelOverlay">
    <div class="detail-panel" id="detailPanel" role="dialog" aria-labelledby="detailPanelTitle" aria-modal="true">
        <div class="detail-panel__header">
            <div class="detail-panel__title-row">
                <h2 class="detail-panel__title" id="detailPanelTitle">Agent Details</h2>
                <button class="detail-panel__close" id="detailPanelClose" aria-label="Close agent details">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                    </svg>
                </button>
            </div>
            <div class="detail-panel__subtitle" id="detailPanelSubtitle"></div>
        </div>

        <div class="detail-panel__content" id="detailPanelContent">
            <!-- Dynamically populated -->
        </div>
    </div>
</div>
```

---

### 5.2 Panel Styling

#### CSS Implementation

```css
/* Overlay */
.detail-panel-overlay {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background-color: var(--color-panel-overlay);
    z-index: 2000;
    display: none;
    opacity: 0;
    transition: opacity 0.3s ease;
}

.detail-panel-overlay.visible {
    display: block;
    opacity: 1;
}

/* Panel */
.detail-panel {
    position: fixed;
    top: 0;
    right: 0;
    width: 600px;
    max-width: 90vw;
    height: 100vh;
    background-color: var(--color-panel-bg);
    box-shadow: -4px 0 24px rgba(0, 0, 0, 0.5);
    display: flex;
    flex-direction: column;
    transform: translateX(100%);
    transition: transform 0.3s ease;
    overflow: hidden;
}

.detail-panel-overlay.visible .detail-panel {
    transform: translateX(0);
}

/* Header */
.detail-panel__header {
    background-color: var(--color-bg-elevated);
    border-bottom: 1px solid var(--color-border-default);
    padding: var(--space-6);
}

.detail-panel__title-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: var(--space-2);
}

.detail-panel__title {
    font-size: var(--text-2xl);
    font-weight: var(--font-bold);
    color: var(--color-text-primary);
}

.detail-panel__close {
    background: none;
    border: none;
    color: var(--color-text-secondary);
    cursor: pointer;
    padding: var(--space-2);
    border-radius: var(--radius-md);
    transition: all 0.2s ease;
    display: flex;
    align-items: center;
    justify-content: center;
}

.detail-panel__close svg {
    width: 24px;
    height: 24px;
}

.detail-panel__close:hover {
    background-color: var(--color-bg-hover);
    color: var(--color-text-primary);
}

.detail-panel__subtitle {
    font-size: var(--text-sm);
    color: var(--color-text-secondary);
    font-family: var(--font-mono);
}

/* Content */
.detail-panel__content {
    flex: 1;
    overflow-y: auto;
    padding: var(--space-6);
}

.detail-panel__content::-webkit-scrollbar {
    width: 8px;
}

.detail-panel__content::-webkit-scrollbar-track {
    background: var(--color-bg-base);
    border-radius: var(--radius-full);
}

.detail-panel__content::-webkit-scrollbar-thumb {
    background: var(--color-border-strong);
    border-radius: var(--radius-full);
}

/* Detail sections */
.detail-section {
    margin-bottom: var(--space-8);
}

.detail-section__title {
    font-size: var(--text-lg);
    font-weight: var(--font-semibold);
    color: var(--color-text-secondary);
    margin-bottom: var(--space-4);
    padding-bottom: var(--space-2);
    border-bottom: 1px solid var(--color-border-subtle);
    text-transform: uppercase;
    letter-spacing: 0.05em;
}

.detail-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: var(--space-3) 0;
    border-bottom: 1px solid var(--color-border-subtle);
}

.detail-row:last-child {
    border-bottom: none;
}

.detail-row__label {
    font-size: var(--text-sm);
    font-weight: var(--font-medium);
    color: var(--color-text-secondary);
}

.detail-row__value {
    font-size: var(--text-sm);
    color: var(--color-text-primary);
    font-family: var(--font-mono);
    text-align: right;
}

.detail-row__value--highlight {
    color: var(--color-interactive-default);
    font-weight: var(--font-semibold);
}

/* Checkpoint details */
.checkpoint-detail {
    background-color: var(--color-bg-base);
    border: 1px solid var(--color-border-default);
    border-radius: var(--radius-lg);
    padding: var(--space-4);
    margin-top: var(--space-3);
}

.checkpoint-detail__summary {
    font-size: var(--text-sm);
    color: var(--color-text-primary);
    line-height: var(--leading-relaxed);
    margin-bottom: var(--space-4);
}

.checkpoint-detail__progress {
    display: flex;
    align-items: center;
    gap: var(--space-3);
}

.checkpoint-detail__bar {
    flex: 1;
    height: 8px;
    background-color: var(--color-bg-elevated);
    border-radius: var(--radius-full);
    overflow: hidden;
}

.checkpoint-detail__bar-fill {
    height: 100%;
    background: linear-gradient(90deg, var(--status-running), var(--status-completed));
    border-radius: var(--radius-full);
    transition: width 0.5s ease-out;
}

.checkpoint-detail__percent {
    font-size: var(--text-sm);
    font-weight: var(--font-semibold);
    color: var(--color-text-primary);
}

/* Message list in panel */
.panel-message-list {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    max-height: 400px;
    overflow-y: auto;
}

.panel-message-item {
    background-color: var(--color-bg-base);
    border-left: 3px solid;
    border-radius: var(--radius-md);
    padding: var(--space-3);
    font-size: var(--text-sm);
}

.panel-message-item__time {
    font-size: var(--text-xs);
    color: var(--color-text-tertiary);
    font-family: var(--font-mono);
    margin-bottom: var(--space-1);
}

.panel-message-item__content {
    color: var(--color-text-primary);
    line-height: var(--leading-normal);
}

/* Dependency list */
.dependency-list {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
}

.dependency-item {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    padding: var(--space-2);
    background-color: var(--color-bg-base);
    border-radius: var(--radius-md);
    font-size: var(--text-sm);
}

.dependency-item__icon {
    width: 16px;
    height: 16px;
    flex-shrink: 0;
}

.dependency-item__name {
    flex: 1;
    font-family: var(--font-mono);
    color: var(--color-text-primary);
}

.dependency-item__status {
    font-size: var(--text-xs);
    padding: var(--space-1) var(--space-2);
    border-radius: var(--radius-sm);
    font-weight: var(--font-medium);
}

.dependency-item__status--completed {
    background-color: var(--status-completed-bg);
    color: var(--status-completed);
}

.dependency-item__status--pending {
    background-color: var(--status-pending-bg);
    color: var(--status-pending);
}

.dependency-item__status--running {
    background-color: var(--status-running-bg);
    color: var(--status-running);
}
```

---

### 5.3 Panel JavaScript Implementation

```javascript
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

    // Set title and subtitle
    title.textContent = agent.role;
    subtitle.textContent = `Status: ${agent.status} • Elapsed: ${formatTime(agent.elapsedSeconds)}`;

    // Build content
    content.innerHTML = renderAgentDetailContent(agent);

    // Show panel
    overlay.classList.add('visible');

    // Focus trap - focus close button
    document.getElementById('detailPanelClose').focus();
}

function closeAgentDetailPanel() {
    const overlay = document.getElementById('detailPanelOverlay');
    overlay.classList.remove('visible');
}

function renderAgentDetailContent(agent) {
    let html = '';

    // Status section
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

    // Dependencies section
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
                    <svg class="dependency-item__icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
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

    // Checkpoint section
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

    // Recent messages section
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

// Event listeners for panel
document.getElementById('detailPanelClose').addEventListener('click', closeAgentDetailPanel);

document.getElementById('detailPanelOverlay').addEventListener('click', (e) => {
    if (e.target.id === 'detailPanelOverlay') {
        closeAgentDetailPanel();
    }
});

// Keyboard support - Escape to close
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        const overlay = document.getElementById('detailPanelOverlay');
        if (overlay.classList.contains('visible')) {
            closeAgentDetailPanel();
        }
    }
});
```

---

## 6. Message Stream Enhancements

### 6.1 Message Grouping by Time Period

**Feature:** Group messages into "Today", "Yesterday", "Earlier" sections for better temporal context.

#### HTML Structure

Messages will be wrapped in time period groups:

```html
<div class="messages-list" id="messagesList" aria-live="polite" aria-atomic="false">
    <div class="message-group">
        <div class="message-group__header">Today</div>
        <div class="message-group__items">
            <!-- Message items here -->
        </div>
    </div>
    <div class="message-group">
        <div class="message-group__header">Yesterday</div>
        <div class="message-group__items">
            <!-- Message items here -->
        </div>
    </div>
</div>
```

#### CSS Implementation

```css
.message-group {
    margin-bottom: var(--space-6);
}

.message-group:last-child {
    margin-bottom: 0;
}

.message-group__header {
    position: sticky;
    top: 0;
    background-color: var(--color-group-header-bg);
    color: var(--color-group-header-text);
    font-size: var(--text-xs);
    font-weight: var(--font-semibold);
    text-transform: uppercase;
    letter-spacing: 0.1em;
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-md);
    margin-bottom: var(--space-3);
    z-index: 10;
    backdrop-filter: blur(8px);
}

.message-group__items {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
}
```

#### JavaScript Implementation

```javascript
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

function renderMessages() {
    const container = document.getElementById('messagesList');

    // Filter messages
    const filtered = state.messages.filter(msg => {
        const agentMatch = !state.filters.agent || msg.from === state.filters.agent;
        const typeMatch = state.filters.types.size === 0 || state.filters.types.has(msg.type);
        return agentMatch && typeMatch;
    });

    if (filtered.length === 0) {
        container.innerHTML = `
            <div class="messages-empty">
                <div class="messages-empty-icon">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M20.25 8.511c.884.284 1.5 1.128 1.5 2.097v4.286c0 1.136-.847 2.1-1.98 2.193-.34.027-.68.052-1.02.072v3.091l-3-3c-1.354 0-2.694-.055-4.02-.163a2.115 2.115 0 01-.825-.242m9.345-8.334a2.126 2.126 0 00-.476-.095 48.64 48.64 0 00-8.048 0c-1.131.094-1.976 1.057-1.976 2.192v4.286c0 .837.46 1.58 1.155 1.951m9.345-8.334V6.637c0-1.621-1.152-3.026-2.76-3.235A48.455 48.455 0 0011.25 3c-2.115 0-4.198.137-6.24.402-1.608.209-2.76 1.614-2.76 3.235v6.226c0 1.621 1.152 3.026 2.76 3.235.577.075 1.157.14 1.74.194V21l4.155-4.155" />
                    </svg>
                </div>
                <div class="messages-empty-title">No messages</div>
                <div class="messages-empty-text">Try adjusting your filters...</div>
            </div>
        `;
        return;
    }

    // Group messages by time
    const groups = groupMessagesByTime(filtered);

    // Clear and rebuild
    container.innerHTML = '';

    // Render each group
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

    // Auto-scroll if enabled
    if (state.autoScroll) {
        setTimeout(() => {
            container.scrollTop = container.scrollHeight;
        }, 0);
    }
}
```

---

### 6.2 Message Search/Filter by Content

**Feature:** Add search input to filter messages by content.

#### HTML Structure

Add to filter controls section:

```html
<div class="filter-controls">
    <label class="filter-label">
        <span class="filter-label-text">Show from:</span>
        <select class="filter-select" id="agentFilter" aria-label="Filter messages by agent">
            <option value="">All Agents</option>
        </select>
    </label>

    <div class="filter-group">
        <span class="filter-label-text">Search messages:</span>
        <div class="search-input-wrapper">
            <svg class="search-input__icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z" />
            </svg>
            <input
                type="text"
                class="search-input"
                id="messageSearch"
                placeholder="Filter by message content..."
                aria-label="Search message content"
            />
            <button class="search-input__clear" id="searchClear" aria-label="Clear search" style="display: none;">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                </svg>
            </button>
        </div>
    </div>

    <div class="filter-group">
        <span class="filter-label-text">Show message types:</span>
        <div class="filter-chips" id="typeFilters" role="group" aria-label="Message type filters">
            <!-- Chips here -->
        </div>
        <button class="filter-reset" id="filterReset" aria-label="Clear all type filters" disabled>
            Clear All
        </button>
    </div>
</div>
```

#### CSS Implementation

```css
.search-input-wrapper {
    position: relative;
    display: flex;
    align-items: center;
}

.search-input {
    width: 100%;
    background-color: var(--color-bg-overlay);
    border: 1px solid var(--color-border-default);
    border-radius: var(--radius-md);
    padding: var(--space-2) var(--space-3) var(--space-2) var(--space-8);
    font-size: var(--text-sm);
    color: var(--color-text-primary);
    font-family: var(--font-sans);
    transition: border-color 0.2s ease, box-shadow 0.2s ease;
}

.search-input::placeholder {
    color: var(--color-text-tertiary);
}

.search-input:focus {
    outline: none;
    border-color: var(--color-interactive-default);
    box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.2);
}

.search-input__icon {
    position: absolute;
    left: var(--space-2);
    width: 18px;
    height: 18px;
    color: var(--color-text-tertiary);
    pointer-events: none;
}

.search-input__clear {
    position: absolute;
    right: var(--space-2);
    background: none;
    border: none;
    color: var(--color-text-tertiary);
    cursor: pointer;
    padding: var(--space-1);
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: var(--radius-sm);
    transition: all 0.2s ease;
}

.search-input__clear svg {
    width: 16px;
    height: 16px;
}

.search-input__clear:hover {
    background-color: var(--color-bg-hover);
    color: var(--color-text-primary);
}
```

#### JavaScript Implementation

```javascript
// Add to state object
const state = {
    // ... existing state ...
    filters: {
        agent: '',
        types: new Set(),
        searchQuery: '' // NEW
    },
    // ... rest of state ...
};

// Add event listeners
document.getElementById('messageSearch').addEventListener('input', (e) => {
    state.filters.searchQuery = e.target.value.toLowerCase().trim();

    // Show/hide clear button
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

// Update renderMessages() filter logic
function renderMessages() {
    const container = document.getElementById('messagesList');

    // Filter messages
    const filtered = state.messages.filter(msg => {
        const agentMatch = !state.filters.agent || msg.from === state.filters.agent;
        const typeMatch = state.filters.types.size === 0 || state.filters.types.has(msg.type);
        const searchMatch = !state.filters.searchQuery ||
                           msg.content.toLowerCase().includes(state.filters.searchQuery);
        return agentMatch && typeMatch && searchMatch;
    });

    // ... rest of renderMessages() ...
}
```

---

### 6.3 Enhanced Empty State

**Current:** Simple text
**Enhancement:** Add illustration icon and better typography

#### CSS Implementation

```css
.messages-empty {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    height: 100%;
    text-align: center;
    padding: var(--space-12) var(--space-4);
    color: var(--color-text-tertiary);
}

.messages-empty-icon {
    width: 80px;
    height: 80px;
    background-color: var(--color-empty-icon-bg);
    border-radius: var(--radius-full);
    display: flex;
    align-items: center;
    justify-content: center;
    margin-bottom: var(--space-6);
}

.messages-empty-icon svg {
    width: 40px;
    height: 40px;
    color: var(--color-empty-icon);
}

.messages-empty-title {
    font-size: var(--text-lg);
    font-weight: var(--font-semibold);
    color: var(--color-text-secondary);
    margin-bottom: var(--space-2);
}

.messages-empty-text {
    font-size: var(--text-sm);
    color: var(--color-text-tertiary);
    max-width: 300px;
}
```

---

## 7. Dependency Graph Refinements

### 7.1 Collapsible Sections

**Feature:** For large agent counts (10+), allow collapsing/expanding dependency subtrees.

#### HTML Structure

```html
<div class="dependency-graph-container">
    <div class="dependency-graph-header">
        <h3 class="section-header">DEPENDENCY GRAPH</h3>
        <button class="dependency-graph-toggle" id="dependencyToggle" aria-label="Toggle graph visibility">
            <svg class="dependency-graph-toggle__icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" d="M19 9l-7 7-7-7" />
            </svg>
        </button>
    </div>
    <div class="dependency-graph" id="dependencyGraph">
        <!-- Content here -->
    </div>
</div>
```

#### CSS Implementation

```css
.dependency-graph-container {
    background-color: var(--color-bg-elevated);
    border: 1px solid var(--color-border-default);
    border-radius: var(--radius-lg);
    padding: var(--space-4);
    transition: all 0.3s ease;
}

.dependency-graph-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: var(--space-4);
}

.dependency-graph-header .section-header {
    margin-bottom: 0;
    border-bottom: none;
    padding-bottom: 0;
}

.dependency-graph-toggle {
    background: none;
    border: 1px solid var(--color-border-default);
    color: var(--color-text-secondary);
    cursor: pointer;
    padding: var(--space-2);
    border-radius: var(--radius-md);
    display: flex;
    align-items: center;
    justify-content: center;
    transition: all 0.2s ease;
}

.dependency-graph-toggle:hover {
    background-color: var(--color-bg-hover);
    border-color: var(--color-border-strong);
    color: var(--color-text-primary);
}

.dependency-graph-toggle__icon {
    width: 20px;
    height: 20px;
    transition: transform 0.3s ease;
}

.dependency-graph-container.collapsed .dependency-graph-toggle__icon {
    transform: rotate(-90deg);
}

.dependency-graph {
    font-family: var(--font-mono);
    font-size: var(--text-sm);
    line-height: var(--leading-relaxed);
    white-space: pre-wrap;
    word-break: break-word;
    max-height: 500px;
    overflow-y: auto;
    transition: max-height 0.3s ease, opacity 0.3s ease;
}

.dependency-graph-container.collapsed .dependency-graph {
    max-height: 0;
    opacity: 0;
    overflow: hidden;
}
```

#### JavaScript Implementation

```javascript
// Add to event listeners section
document.getElementById('dependencyToggle').addEventListener('click', () => {
    const container = document.querySelector('.dependency-graph-container');
    container.classList.toggle('collapsed');

    const button = document.getElementById('dependencyToggle');
    const isCollapsed = container.classList.contains('collapsed');
    button.setAttribute('aria-label', isCollapsed ? 'Expand dependency graph' : 'Collapse dependency graph');
});
```

---

### 7.2 Enhanced Visual Hierarchy

**Current:** Monochrome text tree
**Enhancement:** Color-coded nodes with better visual distinction

#### CSS Implementation

```css
.dependency-graph {
    font-family: var(--font-mono);
    font-size: var(--text-sm);
    line-height: var(--leading-relaxed);
    white-space: pre-wrap;
    word-break: break-word;
    max-height: 500px;
    overflow-y: auto;
}

.dep-node {
    display: inline;
    padding: 1px 4px;
    border-radius: var(--radius-sm);
    transition: background-color 0.2s ease;
}

.dep-node:hover {
    background-color: var(--color-bg-hover);
    cursor: pointer;
}

.dep-parent {
    color: var(--color-text-primary);
    font-weight: var(--font-semibold);
}

.dep-tree {
    color: var(--color-text-tertiary);
    opacity: 0.6;
}

.dep-completed {
    color: var(--color-success);
    font-weight: var(--font-medium);
}

.dep-running {
    color: var(--status-running);
    font-weight: var(--font-medium);
}

.dep-running::before {
    content: '●';
    margin-right: 4px;
    animation: pulse-dot 1.5s ease-in-out infinite;
}

.dep-waiting {
    color: var(--color-text-secondary);
}

.dep-failed {
    color: var(--status-failed);
    font-weight: var(--font-medium);
}

.dep-icon {
    display: inline-block;
    margin-right: 4px;
}
```

#### JavaScript Update

```javascript
function renderAgentNode(agent, depth, visited, processed) {
    if (visited.has(agent.role)) {
        return '';
    }
    visited.add(agent.role);

    const indent = '  '.repeat(depth);

    // Determine status class
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

    const statusText = agent.status !== 'Completed' ? ` (${agent.status.toLowerCase()})` : '';

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

                output += `${'  '.repeat(depth + 1)}<span class="dep-tree">${prefix}</span><span class="dep-node ${depStatusClass}">${dep}</span> <span class="dep-waiting">(${depAgent.status.toLowerCase()})</span>\n`;
            }
        });
    }

    processed.add(agent.role);
    return output;
}

// Update renderDependencyGraph to set innerHTML instead of textContent
function renderDependencyGraph() {
    const container = document.getElementById('dependencyGraph');
    let html = '';

    // ... existing logic to build tree ...

    container.innerHTML = html.trim(); // Changed from textContent
}
```

---

## 8. Summary Statistics Row

### 8.1 Statistics Bar Above Message Stream

**Feature:** Show key metrics (total messages, errors, average response time, active agents) in a summary bar.

#### HTML Structure

Add between messages header and filter controls:

```html
<div class="messages-column">
    <div class="messages-header">
        <h2 class="messages-title">MESSAGE STREAM</h2>
    </div>

    <!-- NEW: Summary Statistics -->
    <div class="summary-stats">
        <div class="summary-stat">
            <div class="summary-stat__icon">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M8 10h.01M12 10h.01M16 10h.01M9 16H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-5l-5 5v-5z" />
                </svg>
            </div>
            <div class="summary-stat__content">
                <div class="summary-stat__value" id="statTotalMessages">0</div>
                <div class="summary-stat__label">Messages</div>
            </div>
        </div>

        <div class="summary-stat">
            <div class="summary-stat__icon summary-stat__icon--error">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" />
                </svg>
            </div>
            <div class="summary-stat__content">
                <div class="summary-stat__value summary-stat__value--error" id="statErrors">0</div>
                <div class="summary-stat__label">Errors</div>
            </div>
        </div>

        <div class="summary-stat">
            <div class="summary-stat__icon summary-stat__icon--success">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
            </div>
            <div class="summary-stat__content">
                <div class="summary-stat__value summary-stat__value--success" id="statActive">0</div>
                <div class="summary-stat__label">Active</div>
            </div>
        </div>

        <div class="summary-stat">
            <div class="summary-stat__icon">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
            </div>
            <div class="summary-stat__content">
                <div class="summary-stat__value" id="statAvgTime">--</div>
                <div class="summary-stat__label">Avg Time</div>
            </div>
        </div>
    </div>

    <!-- Filter Controls -->
    <div class="filter-controls">
        <!-- ... -->
    </div>

    <!-- ... rest of messages column ... -->
</div>
```

#### CSS Implementation

```css
.summary-stats {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: var(--space-4);
    padding: var(--space-4);
    background-color: var(--color-stats-bg);
    border: 1px solid var(--color-stats-border);
    border-radius: var(--radius-lg);
    margin-bottom: var(--space-4);
}

.summary-stat {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    padding: var(--space-3);
    background-color: var(--color-bg-elevated);
    border-radius: var(--radius-md);
    transition: all 0.2s ease;
}

.summary-stat:hover {
    background-color: var(--color-bg-hover);
    transform: translateY(-1px);
}

.summary-stat__icon {
    width: 40px;
    height: 40px;
    background-color: rgba(59, 130, 246, 0.15);
    border-radius: var(--radius-md);
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
}

.summary-stat__icon svg {
    width: 24px;
    height: 24px;
    color: var(--color-stats-value);
}

.summary-stat__icon--error {
    background-color: var(--status-failed-bg);
}

.summary-stat__icon--error svg {
    color: var(--status-failed);
}

.summary-stat__icon--success {
    background-color: var(--status-completed-bg);
}

.summary-stat__icon--success svg {
    color: var(--status-completed);
}

.summary-stat__content {
    flex: 1;
    min-width: 0;
}

.summary-stat__value {
    font-size: var(--text-xl);
    font-weight: var(--font-bold);
    color: var(--color-text-primary);
    font-family: var(--font-mono);
    line-height: 1;
    margin-bottom: var(--space-1);
}

.summary-stat__value--error {
    color: var(--status-failed);
}

.summary-stat__value--success {
    color: var(--status-completed);
}

.summary-stat__label {
    font-size: var(--text-xs);
    color: var(--color-text-tertiary);
    text-transform: uppercase;
    letter-spacing: 0.05em;
    font-weight: var(--font-semibold);
}

/* Responsive: Stack on smaller screens */
@media (max-width: 1280px) {
    .summary-stats {
        grid-template-columns: repeat(2, 1fr);
    }
}
```

#### JavaScript Implementation

```javascript
function updateSummaryStats() {
    // Total messages
    document.getElementById('statTotalMessages').textContent = state.messages.length;

    // Error count
    const errorCount = state.messages.filter(m => m.type === 'Error').length;
    document.getElementById('statErrors').textContent = errorCount;

    // Active agents (Running or Spawning)
    const activeCount = Array.from(state.agents.values())
        .filter(a => a.status === 'Running' || a.status === 'Spawning').length;
    document.getElementById('statActive').textContent = activeCount;

    // Average elapsed time for completed agents
    const completedAgents = Array.from(state.agents.values())
        .filter(a => a.status === 'Completed');

    if (completedAgents.length > 0) {
        const avgTime = completedAgents.reduce((sum, a) => sum + a.elapsedSeconds, 0) / completedAgents.length;
        document.getElementById('statAvgTime').textContent = formatTime(Math.round(avgTime));
    } else {
        document.getElementById('statAvgTime').textContent = '--';
    }
}

// Call in render() and after state updates
function render() {
    renderHeader();
    renderAgentCards();
    renderDependencyGraph();
    renderMessages();
    updateSummaryStats(); // NEW
}

// Also call when messages or agents update
state.eventSource.addEventListener('agent-update', (event) => {
    // ... existing logic ...
    updateSummaryStats(); // NEW
});

state.eventSource.addEventListener('message', (event) => {
    // ... existing logic ...
    updateSummaryStats(); // NEW
});
```

---

## 9. Animation & Interaction Updates

### 9.1 Refined Transition Timings

**Current:** Consistent 0.2s–0.3s transitions
**Enhancement:** Use CSS custom properties for easier maintenance and more intentional timing

#### CSS Implementation

```css
:root {
    /* ... existing tokens ... */

    /* Animation durations */
    --duration-fast: 0.15s;
    --duration-normal: 0.25s;
    --duration-slow: 0.4s;

    /* Easing functions */
    --ease-in-out: cubic-bezier(0.4, 0, 0.2, 1);
    --ease-out: cubic-bezier(0, 0, 0.2, 1);
    --ease-in: cubic-bezier(0.4, 0, 1, 1);
    --ease-bounce: cubic-bezier(0.68, -0.55, 0.265, 1.55);
}

/* Apply to interactive elements */
.agent-card {
    transition: all var(--duration-normal) var(--ease-out);
}

.message-item {
    transition: all var(--duration-fast) var(--ease-out);
}

.filter-chip {
    transition: all var(--duration-fast) var(--ease-in-out);
}

button {
    transition: all var(--duration-fast) var(--ease-out);
}

.detail-panel {
    transition: transform var(--duration-slow) var(--ease-out);
}

.detail-panel-overlay {
    transition: opacity var(--duration-normal) var(--ease-in-out);
}
```

---

### 9.2 Loading Skeleton States

**Feature:** Show skeleton loading for agent cards on initial load

#### HTML Structure (Temporary during load)

```html
<div class="agent-card agent-card--skeleton">
    <div class="skeleton skeleton-header"></div>
    <div class="skeleton skeleton-text"></div>
    <div class="skeleton skeleton-text skeleton-text--short"></div>
</div>
```

#### CSS Implementation

```css
.agent-card--skeleton {
    pointer-events: none;
    cursor: default;
}

.skeleton {
    background: linear-gradient(
        90deg,
        var(--color-bg-elevated) 0%,
        var(--color-bg-hover) 20%,
        var(--color-bg-elevated) 40%,
        var(--color-bg-elevated) 100%
    );
    background-size: 200% 100%;
    animation: shimmer 1.5s ease-in-out infinite;
    border-radius: var(--radius-md);
}

.skeleton-header {
    width: 60%;
    height: 24px;
    margin-bottom: var(--space-3);
}

.skeleton-text {
    width: 100%;
    height: 16px;
    margin-bottom: var(--space-2);
}

.skeleton-text--short {
    width: 40%;
}

@keyframes shimmer {
    0% {
        background-position: -200% 0;
    }
    100% {
        background-position: 200% 0;
    }
}
```

#### JavaScript Implementation

```javascript
function renderLoadingSkeleton() {
    const container = document.getElementById('agentCards');
    container.innerHTML = '';

    // Show 3 skeleton cards
    for (let i = 0; i < 3; i++) {
        const skeleton = document.createElement('div');
        skeleton.className = 'agent-card agent-card--skeleton';
        skeleton.innerHTML = `
            <div class="skeleton skeleton-header"></div>
            <div class="skeleton skeleton-text"></div>
            <div class="skeleton skeleton-text skeleton-text--short"></div>
        `;
        container.appendChild(skeleton);
    }
}

async function loadInitialState() {
    try {
        // Show loading state
        renderLoadingSkeleton();

        const response = await fetch('/api/dashboard/state');
        // ... rest of logic ...
    } catch (error) {
        // ... error handling ...
    }
}
```

---

### 9.3 Micro-interactions

**Enhancement:** Add subtle hover feedback and state transitions

#### CSS Implementation

```css
/* Button press effect */
button:active {
    transform: scale(0.98);
}

/* Card active state (when clicked) */
.agent-card:active {
    transform: translateY(0);
}

/* Filter chip active animation */
.filter-chip.filter-chip--active {
    animation: chip-activate 0.3s var(--ease-bounce);
}

@keyframes chip-activate {
    0% {
        transform: scale(1);
    }
    50% {
        transform: scale(1.05);
    }
    100% {
        transform: scale(1);
    }
}

/* Message item entrance animation */
.message-item {
    animation: slide-in-up 0.4s var(--ease-out);
}

@keyframes slide-in-up {
    from {
        opacity: 0;
        transform: translateY(20px);
    }
    to {
        opacity: 1;
        transform: translateY(0);
    }
}

/* Progress bar smooth fill */
.progress-bar__fill {
    transition: width 0.8s var(--ease-out);
}

/* Status dot color transition */
.status-dot {
    transition: background-color 0.3s var(--ease-in-out);
}
```

---

## 10. Accessibility Requirements

### 10.1 Summary of All Accessibility Improvements

1. **ARIA Live Regions** (Section 1.1)
   - `aria-live="polite"` on agent cards container
   - `aria-live="polite"` on messages list
   - `role="status"` on connection indicator

2. **Improved Contrast** (Section 1.2)
   - Tertiary text: `#6b7280` → `#8b95a5` (3.8:1 → 5.2:1)

3. **Timezone Clarity** (Section 1.3)
   - Use Intl API for proper timezone names
   - Show both short and long formats

4. **Emoji Replacement** (Section 1.4)
   - SVG icons with `aria-label` for dependency indicators
   - `aria-hidden="true"` on decorative emojis

5. **Clear Filter Labels** (Section 1.5)
   - Explicit "Show from:" and "Show message types:" labels
   - "Clear All" button with `aria-label`

6. **Keyboard Navigation**
   - Agent cards: `tabindex="0"`, `role="button"`, Enter/Space support
   - Detail panel: focus trap, Escape to close
   - All interactive elements keyboard accessible

7. **Focus Management**
   - Visible focus indicators (2px solid outline)
   - Focus trap in detail panel
   - Return focus to trigger element on panel close

8. **Screen Reader Support**
   - Semantic HTML (`role="dialog"`, `aria-modal`, `aria-labelledby`)
   - All images/icons have `aria-hidden="true"` or `aria-label`
   - Form inputs have proper labels

9. **Reduced Motion**
   - Existing `@media (prefers-reduced-motion: reduce)` respected
   - All animations use duration tokens for easy override

---

### 10.2 Testing Checklist

- [ ] Test with NVDA/JAWS screen reader
- [ ] Verify all interactive elements keyboard accessible
- [ ] Confirm contrast ratios with WebAIM tool
- [ ] Test with Windows High Contrast mode
- [ ] Verify focus indicators visible on all elements
- [ ] Test with `prefers-reduced-motion: reduce`
- [ ] Validate HTML with W3C validator
- [ ] Run axe DevTools accessibility audit
- [ ] Test with keyboard only (no mouse)
- [ ] Verify ARIA live region announcements

---

## 11. Implementation Checklist

### Phase 1: Critical Fixes (Must Do)

- [ ] Add ARIA live regions to agent cards and messages
- [ ] Update tertiary text color to `#8b95a5`
- [ ] Implement timezone display with Intl API
- [ ] Replace emoji indicators with accessible SVG icons
- [ ] Update filter control labels and add "Clear All" button

### Phase 2: Visual Enhancements (Should Do)

- [ ] Add header gradient background
- [ ] Enhance phase badge with icon
- [ ] Add progress bar shine animation and markers
- [ ] Make agent cards interactive (clickable)
- [ ] Add checkpoint tooltip on hover
- [ ] Enhance status icon with pulsing ring

### Phase 3: New Features (Nice to Have)

- [ ] Implement agent detail panel (slide-out)
- [ ] Add message grouping by time period (Today/Yesterday/Earlier)
- [ ] Implement message search by content
- [ ] Add enhanced empty state with icon
- [ ] Implement collapsible dependency graph
- [ ] Enhance dependency graph visual hierarchy
- [ ] Add summary statistics row
- [ ] Implement loading skeleton states

### Phase 4: Polish & Refinement (Optional)

- [ ] Refine transition timings with CSS custom properties
- [ ] Add micro-interactions (button press, chip activate, etc.)
- [ ] Final accessibility testing and refinements
- [ ] Cross-browser testing (Chrome, Firefox, Safari, Edge)
- [ ] Performance optimization (if needed)

---

## Appendix A: File Structure

After implementation, the enhanced dashboard will remain a single HTML file at:

```
src/Apmas.Server/wwwroot/dashboard.html
```

**Estimated Line Count:** ~2,800 lines (up from ~1,759)

**New Sections:**
- Agent detail panel (HTML, CSS, JS): ~400 lines
- Message grouping: ~150 lines
- Search functionality: ~100 lines
- Summary statistics: ~200 lines
- Enhanced styles and micro-interactions: ~200 lines

---

## Appendix B: Browser Support

**Target Support:**
- Chrome/Edge 90+
- Firefox 88+
- Safari 14+

**Key Features Used:**
- CSS Grid
- CSS Custom Properties
- Intl API (DateTimeFormat)
- EventSource (SSE)
- Modern JavaScript (ES6+)
- ARIA 1.2 attributes

---

## Appendix C: Performance Considerations

1. **Message Limiting:** Already implemented (last 100 messages)
2. **Render Optimization:** Use `requestAnimationFrame` for smooth updates
3. **Event Debouncing:** Search input should debounce at 300ms
4. **Lazy Rendering:** Consider virtual scrolling for 500+ messages (future)
5. **CSS Animations:** Use `will-change` sparingly, respect reduced motion

---

## Document End

**Next Steps:**
1. Review this specification with stakeholders
2. Implement Phase 1 (Critical Fixes) first
3. Test accessibility compliance
4. Proceed with Phases 2-4 based on priority

**Questions or Clarifications:**
Contact the UI Design Agent for any ambiguities in this specification.

---

**Version History:**
- v2.0 (2026-01-22): Initial enhanced specification based on comprehensive UI critique
