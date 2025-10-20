# GitHub Copilot Quick Questions (VSIX)

Store a list of common questions in a local text file and use them from a ComboBox embedded in the Copilot chat panel. Pick a question to auto-focus Copilot and paste the full text into the input.

---

## Features

* Loads with Visual Studio and attaches to the Copilot chat panel only when available.
* Displays a minimal ComboBox anchored to the Copilot pane.
* Reads your questions from a plain text file under Local AppData.
* On selection, focuses Copilot input and pastes the question text.
* Optional dark-mode aware visuals.


![img](https://raw.githubusercontent.com/p3k22/GithubCopilotQuickQuestions/refs/heads/main/preview.png)

---

## Installation

1. Build and install the VSIX as usual.
2. Start Visual Studio. The extension initializes automatically.
3. Open the GitHub Copilot chat panel. The drop-down appears at the panel edge.

---

## Configure your quick questions

Create or edit the file:

```
C:\Users\<YourUserName>\AppData\Local\GithubCopilotQuickQuestions\copilot-quick-questions.txt
```

### File format

* Each entry starts with a **title** line beginning with `#`.
* One or more **body** lines begin with `##` and are concatenated with newlines.
* Blank lines separate entries.

**Example**

```
# Explain this method
## Explain what this method does.
## Point out edge cases and suggest tests.

# Refactor to SOLID
## Refactor using SRP and DI.
## Keep behavior identical. Add unit tests.
```

> Notes
>
> * Extra whitespace is ignored.
> * Non-prefixed lines after a title are also appended to the body.

---

## Usage

1. Open the Copilot chat panel in Visual Studio.
2. Click the drop-down and choose a question by title.
3. The extension sets focus to Copilot and pastes the body text into the input.

**Mouse wheel behavior**

* Hover-wheel scrolling over the combo is ignored to prevent accidental selection changes. Click to open the list.

---

## Behavior and placement

* The combo tracks the Copilot pane position (docked or floating).
* It hides when Visual Studio is minimized and shows again when Copilot is active.
* The drop-down opens in a direction that avoids covering the input field where possible.

---

## Logging

Logs are written to:

```
C:\Users\<YourUserName>\AppData\Local\GithubCopilotQuickQuestions\log.txt
```

---

## Troubleshooting

* **Combo not visible**: Ensure the Copilot chat panel is open.
* **No paste after selection**: Confirm clipboard access and that Visual Studio can execute **Edit.Paste**.
* **Empty drop-down**: Confirm the file path and format. Titles require `#`, body lines `##`.
* **Accidental scroll selection**: Expected to be disabled. Click the button area to open.

---

## Privacy

All prompts stay local under your user profile. The extension does not send data elsewhere.

---

