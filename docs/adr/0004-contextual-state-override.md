# 4. Contextual State Override

**Status:** Accepted

## Context
When a user takes a snapshot, some windows might be minimized to the taskbar. If a user later runs a command to restore a *single* window (`winst restore coding --target 1`), strictly applying the saved "Minimized" state might feel like a bug—the window would seemingly disappear or shrink, despite the user explicitly requesting to interact with it via the CLI.

## Decision
We will implement a **Contextual State Override** during restores:
* **Bulk Restore (`winst restore coding`):** The tool will strictly respect the saved states. If a window was minimized during the snapshot, it returns to the taskbar.
* **Single-Target Restore (`winst restore coding --target 1`):** The tool will override a saved "Minimized" state and force the window into a "Normal" (or "Maximized", if applicable) visible state. 

## Consequences
* **Positive:** Aligns with user intent. A user targeting a specific window likely wants it brought to their immediate attention.
* **Negative:** Adds a slight branch in logic during the Mover phase, meaning the bulk-restore and target-restore paths share coordinate logic but diverge on `ShowCmd` logic.
