# Quick Start

This page is aimed at readers new to ColorVision, with the goal of completing a closed loop of "launch program → recognize the interface → perform a basic operation → know where to go next" in the shortest path.

## Before You Begin

It is recommended to complete the following preparations first:

1. Confirmed [System Requirements](./prerequisites.md)
2. Completed [Installation Guide](./installation.md)
3. The main program can launch normally

## 10-Minute Experience Path

### Step 1: Launch and Confirm the Main Interface is Normal

- Launch ColorVision
- Confirm the main window displays correctly
- If the first launch is slow, wait for initialization and plugin scanning to complete

To familiarize yourself with the interface structure first, read [Main Window Tour](../01-user-guide/interface/main-window.md).

### Step 2: Get to Know a Few Core Areas

It is recommended to focus on just these areas first:

- Menu Bar: access main functions such as File, Devices, Workflows, Help
- Central Workspace: view images, workflows, or results
- Property Panel: view and modify current object parameters
- Status Bar & Logs: confirm whether the program has errors and whether the current status is normal

### Step 3: Perform One Minimal Operation

If you just want to confirm the program is functional, choose one of the two simplest paths below:

#### Path A: Open an Image

1. Open a local image from the File menu
2. Confirm the image displays in the workspace
3. Observe whether the property panel or related tools respond normally

#### Path B: Add a Simulated or Existing Device

1. Enter the device-related menu
2. Add a simulated device, or connect to an existing device
3. Confirm the device appears in the device area and responds normally

For device-related content, continue with [Device Service Overview](../01-user-guide/devices/overview.md).

### Step 4: Try a More Real-World Entry Point

Choose your next reading based on your goal:

- For image viewing and annotation: go to [Image Editor Overview](../01-user-guide/image-editor/overview.md)
- To configure devices and acquisition pipelines: go to [Device Service Overview](../01-user-guide/devices/overview.md)
- To start understanding how workflows are designed and executed: go to [Workflow Overview](../01-user-guide/workflow/README.md)

## Common Routing

### I'm Just an End User, I Want to Learn to Use It First

Continue to [User Guide](../01-user-guide/README.md).

### I Want to Do Secondary Development or Locate Code

First read [Developer Guide](../02-developer-guide/README.md), then enter [Architecture Design](../03-architecture/README.md) or [API Reference](../04-api-reference/README.md) as needed.

### I Just Want to Know Where the Main Modules Are in the Repository

Go directly to [Project Structure Overview](../05-resources/project-structure/README.md).

## A One-Sentence Understanding of This Page's Boundaries

This page only gets you into the system; it is not responsible for explaining installer implementation, module design history, or all library details.