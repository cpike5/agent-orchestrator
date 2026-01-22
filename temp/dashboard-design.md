# APMAS Real-Time Dashboard - UI/UX Design Specification

## 1. Design Philosophy

The APMAS dashboard is a **monitoring interface** designed for operators to observe autonomous agent orchestration in real-time. The design prioritizes:

- **Clarity**: Status should be immediately apparent at a glance
- **Density**: Show maximum information without clutter (operators need the big picture)
- **Real-time feedback**: Visual changes must be obvious but not distracting
- **Low eye strain**: Dark theme for extended monitoring sessions
- **Professional aesthetic**: Clean, business-appropriate design

## 2. Overall Layout

### 2.1 Page Structure (ASCII Wireframe)

```
┌─────────────────────────────────────────────────────────────────────┐
│ APMAS Dashboard                    🟢 Connected      [00:15:23]     │
│ Project: My Web App                Phase: Building                  │
│ ████████░░░░░░░░░░░░░░░░  3/8 agents complete                       │
└─────────────────────────────────────────────────────────────────────┘
┌──────────────────────────────┬──────────────────────────────────────┐
│                              │                                      │
│  AGENT STATUS                │  MESSAGE STREAM                      │
│                              │  ┌────────────────────────────────┐  │
│  ┌────────────────────────┐  │  │ Filter: [All Agents ▼] [Types]│  │
│  │ 🟢 architect           │  │  └────────────────────────────────┘  │
│  │ Completed • 14m 23s    │  │                                      │
│  │ ✓ All dependencies met │  │  ┌────────────────────────────────┐ │
│  │ ██████████ 100%        │  │  │ 10:15:23  architect → dev      │ │
│  └────────────────────────┘  │  │ DONE   Spec ready for impl     │ │
│                              │  ├────────────────────────────────┤ │
│  ┌────────────────────────┐  │  │ 10:14:58  architect            │ │
│  │ 🔵 developer           │  │  │ PROGRESS  Writing API design   │ │
│  │ Running • 8m 12s       │  │  ├────────────────────────────────┤ │
│  │ Retry: 1               │  │  │ 10:12:41  supervisor           │ │
│  │ ⏳ Waiting: architect   │  │  │ INFO   Spawning developer...   │ │
│  │ ██████░░░░ 60%         │  │  ├────────────────────────────────┤ │
│  └────────────────────────┘  │  │ 10:10:15  architect            │ │
│                              │  │ CHECKPOINT  Task 2/3 complete  │ │
│  ┌────────────────────────┐  │  └────────────────────────────────┘ │
│  │ ⚫ tester              │  │                                      │
│  │ Pending                │  │                ↓ Auto-scroll ⏸     │
│  │ ⏳ Waiting: developer   │  │                                      │
│  └────────────────────────┘  │                                      │
│                              │                                      │
│  DEPENDENCY GRAPH            │                                      │
│                              │                                      │
│  architect ✓                 │                                      │
│    ├─→ developer (running)   │                                      │
│    └─→ tester (waiting)      │                                      │
│                              │                                      │
└──────────────────────────────┴──────────────────────────────────────┘
```

### 2.2 Grid Layout Specification

**Desktop Layout (1024px+):**
- Header: Full width, fixed height (80px)
- Main content: CSS Grid with 2 columns
  - Left column (agents): 40% width, min 400px
  - Right column (messages): 60% width, min 600px
- Minimum viewport width: 1024px
- Both columns scroll independently

**Responsive Breakpoint:**
- Below 1024px: Stack vertically (agents on top, messages below)
- Consider hiding dependency graph on narrow screens

## 3. Color Palette & Design Tokens

### 3.1 Base Colors (Dark Theme)

```css
/* Background layers */
--color-bg-base: #0f1419;           /* Main background */
--color-bg-elevated: #1a1f28;       /* Cards, elevated surfaces */
--color-bg-overlay: #252b36;        /* Modals, dropdowns */
--color-bg-hover: #2a3140;          /* Hover states */

/* Text hierarchy */
--color-text-primary: #e3e8ef;      /* Primary text */
--color-text-secondary: #9ca3af;    /* Secondary text */
--color-text-tertiary: #6b7280;     /* Tertiary text, labels */
--color-text-disabled: #4b5563;     /* Disabled states */

/* Borders & dividers */
--color-border-default: #2d3748;    /* Default borders */
--color-border-subtle: #1f2937;     /* Subtle dividers */
--color-border-strong: #4a5568;     /* Strong emphasis */

/* Interactive elements */
--color-interactive-default: #3b82f6;   /* Links, buttons */
--color-interactive-hover: #60a5fa;     /* Hover state */
--color-interactive-active: #2563eb;    /* Active/pressed */
```

### 3.2 Status Colors (Agent States)

```css
/* Agent status indicators */
--status-pending: #6b7280;          /* Gray - not started */
--status-queued: #3b82f6;           /* Blue - queued */
--status-spawning: #06b6d4;         /* Cyan - spawning */
--status-running: #10b981;          /* Green - actively running */
--status-paused: #f59e0b;           /* Amber - paused/checkpoint */
--status-completed: #059669;        /* Dark green - success */
--status-failed: #ef4444;           /* Red - failed */
--status-timedout: #dc2626;         /* Dark red - timeout */
--status-escalated: #8b5cf6;        /* Purple - escalated */

/* Status with alpha for backgrounds */
--status-pending-bg: rgba(107, 114, 128, 0.15);
--status-queued-bg: rgba(59, 130, 246, 0.15);
--status-spawning-bg: rgba(6, 182, 212, 0.15);
--status-running-bg: rgba(16, 185, 129, 0.15);
--status-paused-bg: rgba(245, 158, 11, 0.15);
--status-completed-bg: rgba(5, 150, 105, 0.15);
--status-failed-bg: rgba(239, 68, 68, 0.15);
--status-timedout-bg: rgba(220, 38, 38, 0.15);
--status-escalated-bg: rgba(139, 92, 246, 0.15);
```

### 3.3 Message Type Colors

```css
/* Message type badges */
--msg-info: #3b82f6;        /* Blue */
--msg-progress: #10b981;    /* Green */
--msg-done: #059669;        /* Dark green */
--msg-error: #ef4444;       /* Red */
--msg-help: #f59e0b;        /* Amber */
--msg-heartbeat: #6b7280;   /* Gray */
--msg-checkpoint: #8b5cf6;  /* Purple */

/* Badge backgrounds */
--msg-info-bg: rgba(59, 130, 246, 0.2);
--msg-progress-bg: rgba(16, 185, 129, 0.2);
--msg-done-bg: rgba(5, 150, 105, 0.2);
--msg-error-bg: rgba(239, 68, 68, 0.2);
--msg-help-bg: rgba(245, 158, 11, 0.2);
--msg-heartbeat-bg: rgba(107, 114, 128, 0.2);
--msg-checkpoint-bg: rgba(139, 92, 246, 0.2);
```

### 3.4 Semantic Colors

```css
/* Success/Warning/Danger */
--color-success: #10b981;
--color-success-bg: rgba(16, 185, 129, 0.15);
--color-warning: #f59e0b;
--color-warning-bg: rgba(245, 158, 11, 0.15);
--color-danger: #ef4444;
--color-danger-bg: rgba(239, 68, 68, 0.15);

/* Connection status */
--connection-connected: #10b981;    /* Green */
--connection-reconnecting: #f59e0b; /* Amber */
--connection-disconnected: #ef4444; /* Red */
```

### 3.5 Typography Scale

```css
/* Font families */
--font-sans: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto,
             "Helvetica Neue", Arial, sans-serif;
--font-mono: "SF Mono", Monaco, Consolas, "Liberation Mono",
             "Courier New", monospace;

/* Font sizes */
--text-xs: 0.75rem;      /* 12px - timestamps, metadata */
--text-sm: 0.875rem;     /* 14px - secondary text */
--text-base: 1rem;       /* 16px - body text */
--text-lg: 1.125rem;     /* 18px - section headers */
--text-xl: 1.25rem;      /* 20px - card titles */
--text-2xl: 1.5rem;      /* 24px - page title */

/* Font weights */
--font-normal: 400;
--font-medium: 500;
--font-semibold: 600;
--font-bold: 700;

/* Line heights */
--leading-tight: 1.25;
--leading-normal: 1.5;
--leading-relaxed: 1.75;
```

### 3.6 Spacing Scale

```css
/* Spacing units (8px base) */
--space-1: 0.25rem;   /* 4px */
--space-2: 0.5rem;    /* 8px */
--space-3: 0.75rem;   /* 12px */
--space-4: 1rem;      /* 16px */
--space-5: 1.25rem;   /* 20px */
--space-6: 1.5rem;    /* 24px */
--space-8: 2rem;      /* 32px */
--space-10: 2.5rem;   /* 40px */
--space-12: 3rem;     /* 48px */
```

### 3.7 Border Radius & Shadows

```css
/* Border radius */
--radius-sm: 0.25rem;   /* 4px */
--radius-md: 0.375rem;  /* 6px */
--radius-lg: 0.5rem;    /* 8px */
--radius-xl: 0.75rem;   /* 12px */
--radius-full: 9999px;  /* Fully rounded */

/* Shadows */
--shadow-sm: 0 1px 2px 0 rgba(0, 0, 0, 0.3);
--shadow-md: 0 4px 6px -1px rgba(0, 0, 0, 0.4),
             0 2px 4px -1px rgba(0, 0, 0, 0.3);
--shadow-lg: 0 10px 15px -3px rgba(0, 0, 0, 0.5),
             0 4px 6px -2px rgba(0, 0, 0, 0.3);
--shadow-glow: 0 0 20px rgba(16, 185, 129, 0.3); /* For running status */
```

## 4. Component Specifications

### 4.1 Project Header

**Visual Design:**
- Full-width bar at top of page
- Dark background (`--color-bg-elevated`)
- Bottom border (`--color-border-default`)
- Padding: `--space-5` vertical, `--space-6` horizontal
- Height: 80px fixed

**Content Layout:**
```
┌─────────────────────────────────────────────────────────────────────┐
│ APMAS Dashboard  [Project Name]  Phase: [Building]  🟢 Connected    │
│ Started: Jan 22, 2026 10:00 AM • Elapsed: 00:15:23                  │
│ ████████████░░░░░░░░░░░░░░  3 of 8 agents complete (37%)            │
└─────────────────────────────────────────────────────────────────────┘
```

**Elements:**

1. **Dashboard Title**
   - Font: `--text-xl`, `--font-semibold`
   - Color: `--color-text-secondary`
   - Position: Left-aligned

2. **Project Name**
   - Font: `--text-2xl`, `--font-bold`
   - Color: `--color-text-primary`
   - Position: Next to title

3. **Project Phase Badge**
   - Font: `--text-sm`, `--font-medium`
   - Background: `--status-running-bg`
   - Border: 1px solid `--status-running`
   - Padding: `--space-1` `--space-3`
   - Border radius: `--radius-md`

4. **Connection Status Indicator**
   - Position: Top right
   - Display: Circle (8px diameter) + text
   - States:
     - Connected: Green circle + "Connected"
     - Reconnecting: Amber circle (pulsing) + "Reconnecting..."
     - Disconnected: Red circle + "Disconnected"

5. **Time Display**
   - Font: `--text-sm`, `--font-mono`
   - Color: `--color-text-secondary`
   - Format: "Started: MMM dd, yyyy h:mm a • Elapsed: HH:MM:SS"
   - Update elapsed time every second

6. **Progress Bar**
   - Width: 100%
   - Height: 8px
   - Background: `--color-bg-base`
   - Fill: `--status-running`
   - Border radius: `--radius-full`
   - Text below: "X of Y agents complete (Z%)"
   - Font: `--text-sm`, `--color-text-secondary`

### 4.2 Agent Status Card

**Card Container:**
- Background: `--color-bg-elevated`
- Border: 2px solid (status color - see below)
- Border radius: `--radius-lg`
- Padding: `--space-4`
- Margin bottom: `--space-4`
- Box shadow: `--shadow-md`
- Transition: all 0.3s ease

**Border Colors by Status:**
- Pending: `--status-pending`
- Queued: `--status-queued`
- Spawning: `--status-spawning`
- Running: `--status-running` + pulsing glow animation
- Paused: `--status-paused`
- Completed: `--status-completed`
- Failed: `--status-failed`
- TimedOut: `--status-timedout`
- Escalated: `--status-escalated`

**Card Layout:**
```
┌────────────────────────────┐
│ 🟢 architect               │  ← Status icon + role name
│ Completed • 14m 23s        │  ← Status text + elapsed time
│ Retry: 1                   │  ← Only if retryCount > 0
│ ✓ All dependencies met     │  ← Dependency status
│ ██████████ 100%            │  ← Progress bar (if checkpoint exists)
│ Last: Architecture done    │  ← Last message (truncated to 40 chars)
└────────────────────────────┘
```

**Elements:**

1. **Status Icon**
   - Size: 12px diameter circle
   - Color: Matches status color
   - Position: Top left, aligned with role name
   - Animation: Pulsing for "Running" status

2. **Agent Role Name**
   - Font: `--text-xl`, `--font-semibold`
   - Color: `--color-text-primary`
   - Transform: capitalize first letter

3. **Status Text + Time**
   - Font: `--text-sm`, `--font-medium`
   - Color: Status color
   - Format: "[Status] • [Xm Ys]" (or "HH:MM:SS" if > 1 hour)

4. **Retry Count** (conditional)
   - Only show if `retryCount > 0`
   - Font: `--text-sm`, `--font-medium`
   - Color: `--color-warning`
   - Format: "Retry: X"

5. **Dependency Status**
   - Font: `--text-sm`
   - States:
     - All satisfied: "✓ All dependencies met" (green)
     - Waiting: "⏳ Waiting: architect, tester" (amber, list unsatisfied)
     - No dependencies: Don't show this line

6. **Progress Bar** (conditional)
   - Only show if latest checkpoint exists
   - Width: 100%
   - Height: 6px
   - Background: `--color-bg-base`
   - Fill: Gradient from `--status-running` to `--status-completed`
   - Border radius: `--radius-full`
   - Text above: "Checkpoint: [summary]" (truncated to 30 chars)
   - Text below: "X of Y tasks (Z%)"
   - Font: `--text-xs`, `--color-text-tertiary`

7. **Last Message** (conditional)
   - Only show if `lastMessage` exists
   - Font: `--text-xs`, `--font-mono`
   - Color: `--color-text-tertiary`
   - Format: "Last: [message truncated to 40 chars]"
   - Truncation: Add "..." if truncated

**States:**

- **Hover**: Slight scale (1.02) and shadow increase
- **Running**: Box shadow includes `--shadow-glow` animation
- **Failed/TimedOut**: Shake animation on transition to this state
- **Completed**: Fade border to 50% opacity

### 4.3 Dependency Graph

**Container:**
- Background: `--color-bg-elevated`
- Border: 1px solid `--color-border-default`
- Border radius: `--radius-lg`
- Padding: `--space-4`
- Margin top: `--space-6`

**Header:**
- Text: "DEPENDENCY GRAPH"
- Font: `--text-lg`, `--font-semibold`
- Color: `--color-text-secondary`
- Margin bottom: `--space-3`

**Tree Visualization (Text-based for MVP):**

```
architect ✓
  ├─→ developer (running)
  └─→ tester (waiting)

database-admin ✓
  └─→ developer (running)
```

**Styling:**
- Font: `--font-mono`, `--text-sm`
- Line height: `--leading-relaxed`
- Colors:
  - Parent agent name: `--color-text-primary`
  - Tree characters (├─→): `--color-text-tertiary`
  - Completed dependencies: `--color-success` with ✓
  - Running dependencies: `--status-running` with status text
  - Waiting dependencies: `--color-text-secondary` with status text

**Layout Rules:**
1. Show agents in dependency order (independent agents first)
2. Indent children by 2 spaces + tree characters
3. Use `├─→` for non-last child, `└─→` for last child
4. Show status in parentheses after agent name
5. Mark satisfied dependencies with ✓ before parent name

### 4.4 Message Stream

**Container:**
- Background: `--color-bg-elevated`
- Border: 1px solid `--color-border-default`
- Border radius: `--radius-lg`
- Padding: `--space-4`
- Height: calc(100vh - 160px) /* Full viewport minus header and margins */
- Overflow-y: auto
- Custom scrollbar styling (dark theme)

**Scrollbar Styling:**
```css
::-webkit-scrollbar {
  width: 8px;
}
::-webkit-scrollbar-track {
  background: var(--color-bg-base);
  border-radius: var(--radius-full);
}
::-webkit-scrollbar-thumb {
  background: var(--color-border-strong);
  border-radius: var(--radius-full);
}
::-webkit-scrollbar-thumb:hover {
  background: var(--color-text-tertiary);
}
```

**Header:**
- Text: "MESSAGE STREAM"
- Font: `--text-lg`, `--font-semibold`
- Color: `--color-text-secondary`
- Position: Sticky top of scroll container

**Filter Controls:**
- Position: Below header, sticky
- Background: `--color-bg-elevated` (matches container)
- Padding bottom: `--space-3`
- Border bottom: 1px solid `--color-border-subtle`

**Filter Layout:**
```
┌──────────────────────────────────────┐
│ Filter: [All Agents ▼]  [Type: All ▼]│
│ [×] Info [×] Progress [✓] Error      │
└──────────────────────────────────────┘
```

**Filter Elements:**

1. **Agent Dropdown**
   - Width: 180px
   - Background: `--color-bg-overlay`
   - Border: 1px solid `--color-border-default`
   - Border radius: `--radius-md`
   - Padding: `--space-2` `--space-3`
   - Font: `--text-sm`
   - Options: "All Agents", then list of agent roles

2. **Type Filter Chips**
   - Display: Inline-flex
   - Background: `--color-bg-overlay` (unchecked), status color (checked)
   - Border: 1px solid `--color-border-default` (unchecked), status color (checked)
   - Border radius: `--radius-full`
   - Padding: `--space-1` `--space-3`
   - Font: `--text-xs`, `--font-medium`
   - Cursor: pointer
   - Transition: all 0.2s ease
   - Hover: Slight brightness increase

**Message Item:**

```
┌──────────────────────────────────────────┐
│ 10:15:23  architect → developer          │
│ [DONE] Architecture spec ready for impl  │
└──────────────────────────────────────────┘
```

**Message Layout:**
- Background: `--color-bg-base`
- Border left: 3px solid (message type color)
- Border radius: `--radius-md`
- Padding: `--space-3`
- Margin bottom: `--space-2`
- Hover: Background lightens to `--color-bg-hover`

**Message Elements:**

1. **Timestamp**
   - Font: `--font-mono`, `--text-xs`
   - Color: `--color-text-tertiary`
   - Format: "HH:MM:SS" (local timezone)
   - Position: Inline, left

2. **From/To Agents**
   - Font: `--text-sm`, `--font-medium`
   - Color: `--color-text-secondary`
   - Format: "from → to" or just "from" if broadcast
   - Position: Inline, after timestamp

3. **Type Badge**
   - Font: `--text-xs`, `--font-bold`, uppercase
   - Color: White
   - Background: Message type color (see 3.3)
   - Padding: `--space-1` `--space-2`
   - Border radius: `--radius-sm`
   - Position: Start of second line

4. **Message Content**
   - Font: `--text-sm`
   - Color: `--color-text-primary`
   - Position: After type badge, same line
   - Max lines: 3 (truncate with "..." and expand on click)

**Auto-scroll Control:**
- Position: Bottom right of container (floating)
- Button: "⏸ Pause" / "▶ Resume" toggle
- Background: `--color-bg-overlay`
- Border: 1px solid `--color-border-default`
- Border radius: `--radius-full`
- Padding: `--space-2` `--space-4`
- Box shadow: `--shadow-lg`
- Auto-scroll enabled by default
- Disable auto-scroll if user manually scrolls up
- Re-enable when scrolled to bottom

**Empty State:**
```
┌──────────────────────────────────────┐
│                                      │
│          No messages yet             │
│                                      │
│   Messages will appear here as       │
│   agents communicate...              │
│                                      │
└──────────────────────────────────────┘
```
- Font: `--text-base`, `--color-text-tertiary`
- Text align: center
- Padding: `--space-12` vertical

### 4.5 Section Headers

**Styling:**
- Text: "AGENT STATUS" / "MESSAGE STREAM"
- Font: `--text-lg`, `--font-semibold`, letter-spacing: 0.05em
- Color: `--color-text-secondary`
- Margin bottom: `--space-4`
- Padding bottom: `--space-2`
- Border bottom: 1px solid `--color-border-subtle`

## 5. Animations & Transitions

### 5.1 Status Pulsing (Running State)

```css
@keyframes pulse-glow {
  0%, 100% {
    box-shadow: 0 0 0 0 rgba(16, 185, 129, 0.4);
  }
  50% {
    box-shadow: 0 0 0 8px rgba(16, 185, 129, 0);
  }
}

.agent-card--running {
  animation: pulse-glow 2s ease-in-out infinite;
}
```

### 5.2 Connection Status Pulse (Reconnecting)

```css
@keyframes pulse-dot {
  0%, 100% {
    opacity: 1;
  }
  50% {
    opacity: 0.3;
  }
}

.connection-status--reconnecting .status-dot {
  animation: pulse-dot 1.5s ease-in-out infinite;
}
```

### 5.3 Shake Animation (Failed State)

```css
@keyframes shake {
  0%, 100% { transform: translateX(0); }
  10%, 30%, 50%, 70%, 90% { transform: translateX(-4px); }
  20%, 40%, 60%, 80% { transform: translateX(4px); }
}

.agent-card--failed {
  animation: shake 0.5s ease-in-out;
}
```

### 5.4 Fade In (New Messages)

```css
@keyframes fade-in {
  from {
    opacity: 0;
    transform: translateY(-10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.message-item--new {
  animation: fade-in 0.3s ease-out;
}
```

### 5.5 Progress Bar Fill

```css
.progress-bar__fill {
  transition: width 0.5s ease-out;
}
```

### 5.6 Card Hover

```css
.agent-card {
  transition: all 0.3s ease;
}

.agent-card:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-lg);
}
```

## 6. Interactive States

### 6.1 Connection States

**Connected:**
- Indicator: Green circle (solid)
- Text: "Connected"
- All features active

**Reconnecting:**
- Indicator: Amber circle (pulsing)
- Text: "Reconnecting..."
- Overlay message: "Connection lost. Attempting to reconnect..."
- Position: Center of viewport, modal overlay
- Background: `--color-bg-overlay` with 90% opacity
- Auto-dismiss when reconnected

**Disconnected:**
- Indicator: Red circle (solid)
- Text: "Disconnected"
- Overlay message: "Connection lost. Please refresh the page."
- Disable auto-scroll
- Show last known state

### 6.2 Loading State (Initial Page Load)

**Skeleton Screens:**
- Use placeholder cards with animated gradient
- Show 3 skeleton agent cards
- Show 5 skeleton message items
- Animation: Shimmer effect (gradient moving left to right)

```css
@keyframes shimmer {
  0% {
    background-position: -1000px 0;
  }
  100% {
    background-position: 1000px 0;
  }
}

.skeleton {
  background: linear-gradient(
    90deg,
    var(--color-bg-elevated) 0%,
    var(--color-bg-hover) 20%,
    var(--color-bg-elevated) 40%,
    var(--color-bg-elevated) 100%
  );
  background-size: 1000px 100%;
  animation: shimmer 2s infinite linear;
}
```

### 6.3 Error State

**API Error:**
- Display error banner at top of page
- Background: `--color-danger-bg`
- Border: 1px solid `--color-danger`
- Icon: ⚠️
- Text: Error message from server
- Dismissible with × button
- Auto-dismiss after 10 seconds

**Empty State:**
- Show when no agents configured
- Center message: "No agents configured. Configure agents in apmas.json."
- Font: `--text-lg`, `--color-text-secondary`

## 7. Accessibility Considerations

### 7.1 Color Contrast

All color combinations meet WCAG 2.1 AA standards:
- Text on background: 4.5:1 minimum ratio
- Interactive elements: 3:1 minimum ratio
- Status colors distinguishable without color (icons + text labels)

### 7.2 Keyboard Navigation

- All interactive elements focusable with Tab
- Focus visible: 2px outline in `--color-interactive-default`
- Escape key: Close dropdowns/modals
- Space/Enter: Activate buttons/toggles

### 7.3 Screen Reader Support

- Semantic HTML (`<header>`, `<main>`, `<section>`, `<article>`)
- ARIA labels for icons: `aria-label="Connection status: connected"`
- Live regions for real-time updates: `aria-live="polite"` on message stream
- Progress bars: `role="progressbar"` with `aria-valuenow`, `aria-valuemin`, `aria-valuemax`

### 7.4 Reduced Motion

Respect `prefers-reduced-motion`:
```css
@media (prefers-reduced-motion: reduce) {
  * {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }
}
```

## 8. Responsive Considerations

### 8.1 Desktop (1024px+)

Standard 2-column layout as described above.

### 8.2 Tablet (768px - 1023px)

- Stack columns vertically
- Agents section: Full width, max height 50vh with scroll
- Messages section: Full width, height 50vh with scroll
- Reduce card padding: `--space-3`
- Reduce font sizes by 1 step (text-xl → text-lg, etc.)

### 8.3 Mobile (< 768px) - Out of Scope

Desktop-first design. Mobile support is a future enhancement.
Show message: "This dashboard is optimized for desktop viewing. Please use a device with screen width ≥ 1024px."

## 9. Performance Guidelines

### 9.1 Message Stream Limits

- Display maximum 100 messages in DOM
- Remove oldest messages when limit exceeded
- Virtual scrolling for future enhancement (if > 1000 messages needed)

### 9.2 Update Throttling

- Agent card updates: Max 1 per second per agent
- Progress bar updates: Max 2 per second
- Batch DOM updates using `requestAnimationFrame()`

### 9.3 Efficient Rendering

- Use CSS transitions instead of JavaScript animations where possible
- Avoid layout thrashing: batch DOM reads, then batch DOM writes
- Use `will-change` CSS property sparingly for animated elements

## 10. CSS Architecture

### 10.1 Organization

Structure CSS in this order:
1. CSS Custom Properties (design tokens)
2. CSS Reset / Normalize
3. Base styles (html, body, typography)
4. Layout utilities (grid, flex)
5. Component styles (in order of appearance)
6. Utility classes (text-center, hidden, etc.)
7. Animations
8. Media queries

### 10.2 Naming Convention

Use BEM (Block Element Modifier) pattern:
```css
.agent-card { }                  /* Block */
.agent-card__header { }          /* Element */
.agent-card--running { }         /* Modifier */
.agent-card__status--failed { }  /* Element + Modifier */
```

### 10.3 Utility Classes

Common utilities:
```css
.text-center { text-align: center; }
.hidden { display: none; }
.truncate {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.sr-only { /* Screen reader only */ }
```

## 11. JavaScript Architecture Notes

### 11.1 State Management

Single source of truth:
```javascript
const state = {
  project: { name, phase, startedAt, ... },
  agents: Map<role, agentData>,
  messages: Array<message>,
  filters: { agent: null, types: Set<MessageType> },
  connection: 'connected' | 'reconnecting' | 'disconnected'
};
```

### 11.2 Rendering Strategy

- Initial render: Load state from `/api/dashboard/state`
- Updates: Apply incremental updates from SSE events
- Use template literals for HTML generation
- Consider using `DocumentFragment` for batch inserts

### 11.3 Event Handling

- Centralized event listeners on parent containers (event delegation)
- Debounce filter changes (300ms)
- Throttle scroll position checks (100ms)

## 12. Implementation Checklist

### Phase 1: HTML Structure
- [ ] Create dashboard.html with semantic structure
- [ ] Add header with all required elements
- [ ] Add main grid layout (2 columns)
- [ ] Add agent status section
- [ ] Add message stream section
- [ ] Add dependency graph section

### Phase 2: CSS Styling
- [ ] Define all CSS custom properties (design tokens)
- [ ] Style header and project overview
- [ ] Style agent status cards (all states)
- [ ] Style dependency graph
- [ ] Style message stream and filters
- [ ] Add animations (pulse, shake, fade)
- [ ] Add responsive breakpoints
- [ ] Test color contrast (WCAG AA)

### Phase 3: JavaScript Core
- [ ] Implement state management
- [ ] Implement API client (fetch /api/dashboard/state)
- [ ] Implement SSE client (EventSource)
- [ ] Implement render functions for all sections
- [ ] Implement filter logic
- [ ] Implement auto-scroll logic
- [ ] Add error handling and retry logic

### Phase 4: Polish
- [ ] Add loading skeleton screens
- [ ] Add empty states
- [ ] Add error states
- [ ] Test keyboard navigation
- [ ] Test screen reader compatibility
- [ ] Add accessibility attributes
- [ ] Performance test with 100+ messages
- [ ] Cross-browser testing (Chrome, Firefox, Edge)

## 13. Future Enhancements

### 13.1 Visual Dependency Graph (SVG)

Replace text-based graph with interactive SVG visualization:
- Nodes: Circular nodes with agent name and status color
- Edges: Arrows showing dependency direction
- Layout: Top-to-bottom flow (independent agents at top)
- Interactions: Hover to highlight dependencies, click to filter messages

**Library consideration:** D3.js or plain SVG with manual layout

### 13.2 Dark/Light Theme Toggle

Add theme switcher:
- Button in header: "☀️ / 🌙"
- Duplicate color tokens for light theme
- Save preference to localStorage
- Respect `prefers-color-scheme` media query

### 13.3 Agent Control Actions

Add interactive controls (requires backend support):
- Pause button: Pause running agent
- Resume button: Resume paused agent
- Restart button: Restart failed agent
- Kill button: Force terminate agent
- Confirmation modals for destructive actions

### 13.4 Log Viewer

Integrate agent logs:
- Click agent card to open log viewer modal
- Stream logs via SSE endpoint
- Syntax highlighting for structured logs
- Filter by log level (Info, Warning, Error)

### 13.5 Time-Series Charts

Add historical metrics:
- Agent activity timeline (when each agent was active)
- Message volume over time (line chart)
- Checkpoint progress over time
- Use Chart.js or similar lightweight library

## 14. Design Mockup (Detailed)

### Full Page Mockup (ASCII Art)

```
┌───────────────────────────────────────────────────────────────────────────────────────┐
│ APMAS Dashboard    My Web App    Phase: Building    🟢 Connected   [⚙️ Settings]      │
│ Started: Jan 22, 2026 10:00:00 AM • Elapsed: 00:15:23                                 │
│ ████████████░░░░░░░░░░░░░░░░░░░░░░  3 of 8 agents complete (37%)                     │
└───────────────────────────────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────┬─────────────────────────────────────────────┐
│ AGENT STATUS                            │ MESSAGE STREAM                              │
│                                         │ ┌─────────────────────────────────────────┐ │
│ ┌─────────────────────────────────────┐ │ │ Filter: [All Agents ▼]  Type: [All ▼]  │ │
│ │ 🟢 architect                        │ │ │ ☐ Info  ☐ Progress  ☑ Error  ☐ Done    │ │
│ │ Completed • 14m 23s                 │ │ └─────────────────────────────────────────┘ │
│ │ ✓ All dependencies met              │ │                                             │
│ │ ██████████ 100%                     │ │ ┌─────────────────────────────────────────┐ │
│ │ Last: Architecture design complete  │ │ │ 10:15:23  architect → developer         │ │
│ └─────────────────────────────────────┘ │ │ [DONE] Architecture spec ready          │ │
│                                         │ └─────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────┐ │                                             │
│ │ 🔵 developer                        │ │ ┌─────────────────────────────────────────┐ │
│ │ Running • 8m 12s                    │ │ │ 10:14:58  architect                     │ │
│ │ Retry: 1                            │ │ │ [PROGRESS] Writing API design section   │ │
│ │ ⏳ Waiting: architect                │ │ └─────────────────────────────────────────┘ │
│ │ ██████░░░░ 60%                      │ │                                             │
│ │ Checkpoint: Implemented auth module │ │ ┌─────────────────────────────────────────┐ │
│ │ Last: Working on API endpoints...   │ │ │ 10:12:41  supervisor                    │ │
│ └─────────────────────────────────────┘ │ │ [INFO] Spawning developer agent...      │ │
│                                         │ └─────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────┐ │                                             │
│ │ ⚫ tester                           │ │ ┌─────────────────────────────────────────┐ │
│ │ Pending                             │ │ │ 10:10:15  architect                     │ │
│ │ ⏳ Waiting: developer                │ │ │ [CHECKPOINT] Completed task 2 of 3      │ │
│ └─────────────────────────────────────┘ │ └─────────────────────────────────────────┘ │
│                                         │                                             │
│ ┌─────────────────────────────────────┐ │ ┌─────────────────────────────────────────┐ │
│ │ 🔴 database-admin                   │ │ │ 10:08:32  supervisor                    │ │
│ │ Failed • 3m 45s                     │ │ │ [INFO] Spawning architect agent...      │ │
│ │ Retry: 3                            │ │ └─────────────────────────────────────────┘ │
│ │ ⏳ Waiting: none                     │ │                                             │
│ │ Last error: Connection timeout      │ │                       [⏸ Pause Auto-scroll]│
│ └─────────────────────────────────────┘ │                                             │
│                                         │                                             │
│ DEPENDENCY GRAPH                        │                                             │
│ ┌─────────────────────────────────────┐ │                                             │
│ │ architect ✓                         │ │                                             │
│ │   ├─→ developer (running)           │ │                                             │
│ │   └─→ tester (waiting)              │ │                                             │
│ │                                     │ │                                             │
│ │ database-admin ✗                    │ │                                             │
│ │   └─→ developer (waiting)           │ │                                             │
│ └─────────────────────────────────────┘ │                                             │
└─────────────────────────────────────────┴─────────────────────────────────────────────┘
```

## 15. Timezone Display Implementation

**All timestamps must display in local timezone:**

1. **Server Response Format:**
   - All timestamps sent as ISO 8601 UTC: `"2026-01-22T10:00:00Z"`

2. **Client-Side Conversion:**
   ```javascript
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
   ```

3. **Timezone Indicator:**
   - Add subtle timezone indicator in header
   - Format: "All times in [PST/EST/etc.]"
   - Position: Bottom right of header
   - Font: `--text-xs`, `--color-text-tertiary`

## 16. Summary

This design provides a comprehensive blueprint for implementing the APMAS real-time dashboard. Key features:

- **Professional dark theme** optimized for monitoring
- **Clear visual hierarchy** with color-coded agent statuses
- **Real-time updates** with smooth animations
- **Information density** without clutter
- **Accessible** design meeting WCAG 2.1 AA standards
- **Performant** architecture for 100+ messages
- **Vanilla HTML/CSS/JS** with no build dependencies

The design balances visual appeal with practical functionality, ensuring operators can quickly understand system state and track agent progress in real-time.
