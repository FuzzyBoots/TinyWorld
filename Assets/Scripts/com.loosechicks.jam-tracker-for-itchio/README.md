# Jam Tracker for Itch.io for Unity

## Overview

**Jam Tracker for Itch.io** is a Unity plugin that lets you conveniently track time left until the end of the submission for your jam, so you never miss a deadline (no guarantees though, using alarm clocks still recommended).

## How to use

- In Unity, go to Tools -> 🐤 loose chicks -> Itch.io Game Jam Tracker
- List all jams will be parsed from [itch.io/jams page](https://itch.io/jams)
- Select the one you're participating in
  - Feel free to use quick search and/or
  - Filters by status
- Decide where to show the progress and timer:
  - Scene view overlay (movable and dockable)
  - Top toolbar (requires a dependency to be installed)
- Jam name is clickable and will open the jam page in your browser

## Dependencies

If you want to see the progress and timer in the **top toolbar**, you need to install the following package:
- [Unity Toolbar Extender UI Toolkit](https://github.com/Sammmte/unity-toolbar-extender-ui-toolkit)

This is **not required** for the scene view overlay. I've included a button to install it in the plugin's settings window.