# GitHub Copilot Quick Questions (VSIX)

A Visual Studio extension that overlays a custom combo box on the GitHub Copilot chat panel, allowing instant access to frequently-used questions and prompts stored in a local configuration file.

---

## Features

* **Persistent Overlay**: Custom Win32 combo box that follows the Copilot pane (docked or floating)
* **INI-Based Configuration**: Simple text file format for managing quick questions
* **Auto-Inject**: Pastes selected question text directly into Copilot's input field
* **Smart Positioning**: Dynamically adjusts to pane size and position, opens dropdown upward to avoid input field
* **Minimal UI Impact**: Non-intrusive design that hides when VS is minimized or Copilot is closed

![img](https://raw.githubusercontent.com/p3k22/GithubCopilotQuickQuestions/refs/heads/main/preview.png)

---

## Installation

1. Download and install the VSIX package
2. Restart Visual Studio
3. Open the GitHub Copilot chat panel (View → GitHub Copilot Chat)
4. The combo box appears automatically anchored to the Copilot pane

---

## Configuration

The extension reads settings and questions from:

```
%LOCALAPPDATA%\GithubCopilotQuickQuestions\copilot-quick-questions.ini
```

### INI Format

```ini
[Settings]
autoLoadCopilotChatOnVsStart=true
logging=false

[Questions]
~ Select Quick Question ~=
Add XML Summaries=Add / Edit xml summaries for all functions and classes. Use inline comments above fields and properties
Refactor Code=Refactor this code to improve readability and performance. Follow SOLID principles.
Fix Bugs=Review this code for potential bugs, edge cases, and error handling issues. Suggest fixes.
Add Unit Tests=Generate comprehensive unit tests for this code. Cover happy paths and edge cases.
Optimize Performance=Analyze this code for performance bottlenecks and suggest optimizations.
Explain Code=Explain what this code does in simple terms. Highlight key design decisions.
```

### Settings Section

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `autoLoadCopilotChatOnVsStart` | boolean | `true` | Automatically opens Copilot chat panel when VS starts |
| `logging` | boolean | `false` | Enables diagnostic logging to `log.txt` |

### Questions Section

* **Format**: `Display Title=Prompt Text`
* **Placeholder**: The first entry should have an empty value to serve as the default placeholder text
* **Prompt Text**: Can be any length; this is what gets pasted into Copilot
* **No Limit**: Add as many questions as needed

**Example Questions:**

```ini
Generate README=Create a comprehensive README.md for this project. Include installation, usage, and examples.
Security Review=Perform a security audit of this code. Identify vulnerabilities and suggest mitigations.
Database Optimize=Review this SQL/database code for optimization opportunities and potential indexing improvements.
API Documentation=Generate OpenAPI/Swagger documentation for these API endpoints. Include request/response examples.
```

---

## Logging

When `logging=true` in `config.ini`, diagnostic logs are written to:

```
%LOCALAPPDATA%\GithubCopilotQuickQuestions\log.txt
```

Logs include:
* Extension initialization and shutdown events
* Pane detection and positioning calculations
* Window parenting and subclass operations
* Error conditions and recovery attempts

---

## Troubleshooting

### Combo Box Not Visible

* **Verify Copilot is Open**: Extension only shows overlay when Copilot chat panel is visible
* **Check VS is Foreground**: Overlay hides when VS is not the active application
* **Inspect Logs**: Enable `logging=true` and check `log.txt` for positioning errors

### Questions Not Loading

* **Verify File Path**: Ensure `config.ini` exists in the correct AppData location
* **Check INI Format**: Questions must be in `[Questions]` section with `Title=Text` format
* **Empty Values**: First entry should be the placeholder with an empty value (e.g., `~ Select ~=`)

### Text Not Pasting

* **Focus Issues**: Extension uses clipboard + Edit.Paste command; verify clipboard permissions
* **Timing**: There's a 300ms delay between selection and paste; avoid rapid clicks
* **Copilot Ready**: Ensure Copilot input field is ready to accept input

### Dropdown Not Opening Upward

* **Space Constraints**: Dropdown automatically positions above/below based on available space
* **Pane Height**: Very small pane heights may limit dropdown visibility

### Performance Issues

* **Polling Overhead**: 120ms update interval is optimized for responsiveness
* **UI Automation**: First pane detection may take 1-2 seconds on complex VS layouts
* **Disable Logging**: Set `logging=false` to reduce I/O overhead

---

## Privacy & Security

* **Local Only**: All configuration and prompts remain on your local machine
* **No Telemetry**: Extension does not collect, transmit, or store usage data
* **No Network**: No external connections are made by this extension
* **Clipboard Use**: Selected prompts are temporarily copied to clipboard for paste operation
