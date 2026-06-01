# Terminal

The terminal panel is suitable for two types of tasks: running scripts and directly observing command-line output. It is more like a work assistance area within the main program rather than a separate development documentation entry point.

## When to Open It

- Need to directly run PowerShell or CMD commands
- Want to execute scripts within the current project context
- Need to observe script output, errors, or interactive prompts
- Want to quickly perform a command-line check without switching out of the main program

## How You Typically Use It

### Selecting a Shell

The terminal typically supports at least two common shells:

- PowerShell
- CMD

If the current command depends on PowerShell syntax, do not retry directly in CMD; first confirm the shell type before executing.

### Running Scripts

Common practices include:

- Directly entering commands to run scripts
- Triggering "Run in Terminal" from resource management or related entry points
- During troubleshooting, first run the minimal command, then gradually add parameters

### Viewing Output and Errors

- Prioritize looking at the first error first
- Do not only focus on the last line
- If the script will continue to output, first confirm whether it is still running rather than assuming it has frozen

## Suggested Usage Order

1. First confirm whether you need PowerShell or CMD.
2. Then confirm whether the current working directory is your expected project location.
3. First execute the minimal command to confirm that environment, path, and permissions are all normal.
4. When a command fails, prioritize combining [Log Viewer](./log-viewer.md) and terminal output for diagnosis.

## Common Issues

### Command Runs but Results Differ from Expectations

- First confirm the current shell type
- Then confirm the current directory, environment variables, and script parameters
- If the script depends on external programs, first confirm whether those programs are actually available

### Terminal Has No Output or Appears Unresponsive

- First confirm whether the command is still waiting for input
- Then confirm whether you switched to the wrong shell
- If it's a long task, first give it some time to output the first batch of results; do not immediately resubmit the command

### Path or Permission Errors When Running Scripts

- First check whether the path is correct
- Then confirm whether the script file and dependency files exist
- For PowerShell scripts, prioritize confirming whether execution policy and invocation method are correct

### Interactive Commands Behave Abnormally

- First create a new terminal session and retry
- Then try switching to simpler commands to confirm whether it's an issue with the interactive command itself
- If you just want to see results, you don't necessarily need to complete all interaction in the terminal

## Continue Reading

- [Main Window Guide](./main-window.md)
- [Log Viewer](./log-viewer.md)
- [Common Issues](../troubleshooting/common-issues.md)

## Notes

- This page only retains the usage perspective of the terminal panel and no longer maintains ConPTY, VT100, or internal implementation notes.
- The specific implementation of terminal capabilities is located in `UI/ColorVision.UI/Terminal/`.