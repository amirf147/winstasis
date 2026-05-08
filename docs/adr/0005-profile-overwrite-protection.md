# 5. Profile Overwrite Protection

**Status:** Accepted

## Context
When users run the save command (e.g., `winstasis save coding`), there is a high likelihood they might accidentally type the name of an existing profile, destroying a carefully crafted window layout they spent time arranging.

## Decision
We will implement a **Safety Prompt** system for profile saves. 
If a profile with the requested name already exists in the `sessions/` directory, `winstasis` will halt execution and prompt the user in the CLI: `"Profile '[name]' already exists. Overwrite? (y/n)"`. 
To support automated scripts or rapid power-user workflows, a `--force` (or `-f`) flag can be appended to the command to bypass the prompt and silently overwrite the file.

## Consequences
* **Positive:** Prevents accidental data loss of complex layouts.
* **Positive:** Retains scriptability via the `--force` flag.
* **Negative:** Slightly interrupts flow for users who habitually tweak and re-save layouts manually, though they will quickly learn the `-f` flag.
