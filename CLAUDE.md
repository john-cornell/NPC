

<!-- codeidx init-agents: start -->
## codeidx (project)

**Hooks** live in **`.claude/settings.local.json`**; the **codeidx MCP** server is merged into repo-root **`.mcp.json`** (Claude Code project scope — from **`codeidx init-agents --agent claude`**). Approve the project MCP server when prompted, or run **`claude mcp reset-project-choices`** to reset approvals. Hooks invoke **`codeidx hook …`** subcommands — there is no script file named `pre-grep-glob` on disk.

- **`codeidx hook pre-grep-glob`** — **PreToolUse** when the tool is **Grep** or **Glob**. Injects a reminder to use the codeidx SQLite index (MCP / `read_query`, FTS) for structure before huge repo scans.
- **`codeidx hook post-cs-edit`** — **PostToolUse** after **Edit/Write** of **`*.cs`**. Reminds you to log rationale in **markdown symbol notes** under **`.codeidx/notes/`** (not in `.cs` files).
- **`codeidx hook session-start`** — On session start/resume. Compares the index DB mtime to **`git`**; warns if the index is missing or older than the latest commit.

**Index DB for this repo (from last `init-agents`):** `C:\Code\NPC\.codeidx\db\codeidx.db`  
**Repo root:** `C:\Code\NPC`

**Symbol notes (prose):** use **codeidx MCP** tools (same server as SQL):

- `get_or_create_note` — open or create `<repo>/.codeidx/notes/<symbol>.md`; returns path and full file text.
- `append_note` — append under `## Notes` only (call `get_or_create_note` first if the file might be missing).
- `sync_note_structure` — rebuild the auto-generated top half from the index; preserves everything from `## Notes` onward.

**SQL via MCP** stays **read-only**: `read_query`, `list_tables`, `describe_table` only (SQLite opened `mode=ro`).
<!-- codeidx init-agents: end -->
