---
name: codeidx
description: >-
  Answers code-structure questions using the codeidx SQLite index (symbols,
  edges, FTS) via the configured SQLite MCP tools. Includes optional
  string_ref edges when the index was built with --index-string-literals
  (heuristic quoted-name to type-like symbols; not Roslyn). Use when the user
  asks about references, callers, inheritance, symbols, file paths in the index,
  or navigation that should use structured queries instead of scanning the whole
  tree; or when exploring relationships in indexed C# code.
---

# codeidx index (MCP)

## Assumptions

- DB is **per-repo** at **`<repo>/.codeidx/db/codeidx.db`**. MCP **auto-builds** on first start if missing (background daemon, ~1–2 min) — no manual `codeidx index` needed for first use. Run `codeidx index .` from the repo root only to force a full refresh.
- **SQLite MCP** is already configured in Cursor (e.g. server name `codeidx` from `codeidx init-agents`) and points at that database.

## Auto-build on first start

If no DB exists when MCP starts, codeidx bootstraps an empty schema and runs a full `--all-solutions` index in a **background daemon thread** — MCP is ready immediately. Early queries return empty while it builds. Do **not** prompt the user to build manually. Subsequent sessions use the cached DB; only JIT per-file reindexes occur.

## JIT reindex (`hint_file_path`) — MANDATORY

**Pass `hint_file_path` on every `read_query` that targets a specific symbol or file.** No exceptions.

`read_query` accepts an optional `hint_file_path` (absolute path to a `.cs` file). When provided, codeidx re-parses it (~2 s first touch, zero cost on repeat), then runs the query against fresh data.

**After JIT**: files directly referenced via resolved `implements`, `inherits`, `injects`, or `calls` edges are **also JIT-indexed** in the same pass (up to 10 files) — the queried file and its immediate dependency cluster are all fresh in one round-trip.

**Correct pattern:**
```
1. SELECT f.path FROM symbols s JOIN files f ON f.id = s.file_id WHERE s.name = 'Foo'
2. read_query(query=<your symbol query>, hint_file_path=<path from step 1>)
```

## Empty index fallback (index still building)

**When `SELECT COUNT(*) FROM symbols` = 0 AND user asks about a specific symbol:**

**DO NOT** tell the user to run an index manually. **DO NOT** give up. Instead:

1. **Find the file fast:** `find <repo> -name "ClassName.cs" | head -5` (C# = class name is file name)
2. **JIT it:** pass that path as `hint_file_path` to `read_query`
3. **Answer from JIT results**
4. Note once: "Background index still building"

## Notes workflow (business context)

0. **Read existing note first:** before any SQL query for a target symbol, call **`get_or_create_note`** for its qualified name. Notes may contain prior reasoning, design decisions, migration history, or "why" context written by previous sessions. If the **`## Notes`** section has content, read it and factor it into the investigation.
1. Use **MCP / SQL** (this skill) for **structure**: symbols, edges, FTS, callers.
2. For **semantic** context (invariants, business rules, work logs), the **`<repo>/.codeidx/notes/<Symbol>.md`** top half is auto-generated from the index; the **`## Notes`** section is human/AI prose.
3. After you **edit a core type** in source, persist reasoning via **codeidx MCP**: call **`get_or_create_note`** if the note may not exist, then **`append_note`** with your prose (under **`## Notes`** only). Use **`sync_note_structure`** when the auto-generated sections should be refreshed from the index.

Use the **MCP tools** the server exposes for **structure** (e.g. `read_query`, `list_tables`, `describe_table` — SQL is read-only) and for **notes** (`get_or_create_note`, `append_note`, `sync_note_structure`, which write markdown under `.codeidx/notes/`). You do **not** need `SELECT name FROM sqlite_master` on every turn if you already have the table list below; use **`list_tables` / `describe_table`** only when you need a column you are unsure of (see [schema.sql](./schema.sql) next to this file).

## Schema reference (codeidx v1)

**Base tables (alphabetical):** `edges`, `files`, `folders`, `meta`, `project_edges`, `project_files`, `projects`, `symbols`.

| Name | Role |
|------|------|
| `meta` | Key-value store (e.g. `last_index_ms` after a run). |
| `folders` | Folder path chain; `files.folder_id` points here. |
| `files` | One row per indexed source file: `path` (unique), `language`, `size` / `mtime_ns` / `sha256`, optional `content` if index used `--store-content`. |
| `projects` | MSBuild roots: `name`, `path` (csproj), `kind` (e.g. `csproj`). |
| `project_files` | Many-to-many: which `file_id` belongs to which `project_id`. |
| `project_edges` | Project graph: `edge_kind` is `project_reference` (to another project) or `package_reference` (NuGet; `dst_project_id` may be null). `target` holds the path or package id. |
| `symbols` | `file_id`, `kind`, `name`, `qualified_name`, line/column spans. |
| `edges` | `src_file_id` always set; `src_symbol_id` / `dst_symbol_id` nullable; `edge_type`, `confidence`, `ref_*` line/col, `meta_json` (JSON string). |

**FTS5 (virtual, not in `sqlite_master` the same way as base tables):** `files_fts` (index: `path`), `symbols_fts` (`name`, `qualified_name`); **`file_contents_fts`** only exists if content was indexed (`--store-content`).

**`edges.edge_type` (C# v1):** `calls` | `injects` | `implements` | `inherits` | `imports` | optional `string_ref` (with `--index-string-literals`) | **`mvvm_view`** | **`mvvm_primary_service`** (heuristic MVVM links; **on by default** after index — disable with **`--no-mvvm-edges`**).

**`edges.confidence`:** `exact` | `heuristic` | `unresolved`.

**`symbols.kind` (typical C#):** `type`, `interface`, `enum`, `method`, `constructor`, `property`, `field`, `enum_member`, `delegate`, etc.

**Joins:** `symbols.file_id` → `files.id`; for a reference site, `edges.src_file_id` → `files.id`; `edges.dst_symbol_id` / `src_symbol_id` → `symbols.id`.

## CRITICAL: Empty index fallback (background build in progress)

**When `SELECT COUNT(*) FROM symbols` = 0 (index empty / still building) AND user asks about a specific symbol:**

**DO NOT** tell the user to run an index manually. **DO NOT** give up. Instead:

1. **Find the file fast (two steps):**  
   - First: `find <repo> -name "ClassName.cs" 2>/dev/null | head -5` (C# = class name is file name — instant)  
   - If not found: `grep -rl "class ClassName" <repo> --include="*.cs" 2>/dev/null | head -5`
2. **JIT it:** pass that path as `hint_file_path` to `read_query` — indexes just that file (~2 s) and its direct dependencies
3. **Answer from JIT results**
4. **Note once:** "Background index still building — full graph available in ~1–2 min"

Background build starts automatically on first MCP start. Never prompt user to run `codeidx index`.

## Empty results — retry before giving up

If the first query, **`find-symbol`**, or FTS **`MATCH`** returns **nothing**, **do not stop**. Retry in order, keeping **`LIMIT`** small:

1. **Individual words from the target name**  
   Split compound identifiers: `AutoTimeService` → try `AutoTime`, `Service`, `Time`. Split `qualified_name` on `.` and search **`name`** or **`LIKE '%segment%'`** for **one segment at a time** (symbols table or `symbols_fts`).

2. **Similar / related words**  
   Try **synonyms or alternate role words** (e.g. *handler* / *consumer* / *processor*), **abbreviations** vs full words, and **casing** (`AutoTime` vs `Autotime`) with `LIKE` or case-insensitive patterns if your SQL layer supports it.

3. **Shorter needles**  
   Drop namespaces: match on **unqualified `name`** or the **last segment** of `qualified_name` only. Avoid matching the full `Ns.A.B.LongTypeName` in one go unless you know it is exact.

4. **Looser FTS**  
   Use **prefix** tokens where FTS5 allows (`term*`), **fewer quoted phrases**, or **one token per query** instead of a multi-word `MATCH` string.

5. **Path and file filters**  
   If you know a folder (e.g. `Services`, `Integrations`), constrain with **`files.path LIKE '%...%'`** and combine with a **broad symbol `name LIKE`**.

6. **Content grep last**  
   **`grep-text`** / `file_contents_fts` only if content was indexed (`--store-content`); use **short patterns** and retry with **single words**.

Stay bounded; iterate terms before falling back to wide repo grep or bulk file reads.

## Type symbols and incoming edges

**A type symbol often has no rows** where `dst_symbol_id = <that id>` (and none where `src_symbol_id = <that id>` except its own declaration edges). The index does **not** model every **mention** of a type (generic arguments, field types, `RegisterType<T>()`, DI, etc.)—only **`calls`**, constructor **`injects`**, base-list **`inherits`**/**`implements`**, **`imports`**, and (optionally) **`string_ref`**.

For **“who uses this type”**, use **`symbols_fts`**, bounded **`LIKE`** on `name`/`qualified_name`, path filters, and **`grep-text`** if content was indexed. Monorepos with **many** `.sln` files: use **`python -m codeidx index <root> --all-solutions`** to merge all solutions’ projects in **one** graph (stronger than **`--no-sln`**; avoids interactive single-sln pick). If the index was built with **`--index-string-literals`**, **`find-references`** may also list **`string_ref`** rows (quoted name ↔ unique type-like symbol—**heuristic, not Roslyn**). Do not treat empty **`find-references`** as proof the type is unused.

## Optional: `string_ref` (index flag)

- **Index with:** `python -m codeidx index . --index-string-literals` (or add the flag to your usual **`--sln` / path** command). **Default is off** (larger, noisier edge set when on).
- **What gets stored:** `edge_type = 'string_ref'`, `confidence = 'heuristic'`, from C# **`"..."`** literals whose text passes the PascalCase-like filter and **uniquely** matches one symbol with `kind IN ('type','interface','enum','delegate')` by **`name`** in the **whole** DB. Candidates are **capped per file** (see indexer). **`calls`** is unchanged—string sites do not appear as call edges.
- **Queries:** `SELECT … FROM edges WHERE edge_type = 'string_ref' AND dst_symbol_id = ?` or **`query find-references --symbol-id`** (includes all edge types pointing at the symbol). Filter **`edge_type = 'calls'`** when you only want invocation edges.
- **Precision:** **Low**; see codeidx documentation for full rules (interpolated **`$"..."`** is not emitted as `string_ref` in v1).

## Workflow

1. **Schema:** Use the **Schema reference** above; use **`list_tables` / `describe_table`** only if a column is missing from memory.
2. **Structured questions:** Prefer **SQL** against core tables:
   - `symbols`, `edges`, `files`, `projects`, `project_edges`
   - FTS: `symbols_fts`, `files_fts` (and `file_contents_fts` if content was indexed)
3. **Edge types** include `calls`, `injects`, `inherits`, `implements`, `imports`, optionally **`string_ref`** (with `--index-string-literals`), and by default **`mvvm_view`** / **`mvvm_primary_service`** (heuristic view↔ViewModel and VM→primary inject; disable with **`--no-mvvm-edges`**). **`string_ref`** is a quoted string whose text uniquely matches a **type-like** symbol name—low semantic precision, not Roslyn references. `confidence` is `exact`, `heuristic`, or `unresolved`. Resolution is mostly syntactic—treat **non-exact** confidence as exploratory, not proof of the resolved target.
4. For **callers** / **callees**, join `edges` (`edge_type = 'calls'`) with `symbols` and `files`. Qualify column names (`symbols.id`, `files.id`) when joining both tables.
5. **Interface implementers:** for interface symbol id `I`, query edges with `dst_symbol_id = I` and `symbols.kind = 'interface'`; include `edge_type IN ('implements','inherits')` only for legacy DBs. Prefer **`implements`** for C# interface implementation; **`inherits`** here means a resolved **class/struct** base (first in list), not “interface inheritance.” Use `edges.meta_json` (`base_resolved`, `dst_kind`, `base_kind_hint`) when `dst_symbol_id` is null. Indexing with a **solution** (`--sln`) resolves types across project references when the interface is in the same index.

Reserve **repo-wide grep** or reading dozens of files for cases the index cannot answer (non-indexed languages, comments-only search, etc.).

## JIT reindex (`hint_file_path`) — MANDATORY

**You MUST pass `hint_file_path` on every `read_query` that targets a specific symbol or file.** No exceptions. Do not skip it because the DB "looks fresh" or was just auto-built.

`read_query` accepts an optional `hint_file_path` (absolute path to a `.cs` file). When provided and the file hasn't been re-indexed this session, codeidx re-parses it, updates the DB, then runs the query against fresh data. Each file is re-indexed **at most once per session** (~2 s first touch, zero cost on repeat). After reindex: symbol notes sync/create with UTC timestamp; vault files refresh in background.

**How to get the path:** from a prior `SELECT f.path FROM symbols s JOIN files f ON f.id = s.file_id WHERE s.name = ?` — always resolve the file path before querying symbols, then pass it as `hint_file_path`.

**Correct pattern:**
```
1. SELECT f.path FROM symbols s JOIN files f ON f.id = s.file_id WHERE s.name = 'Foo'
2. read_query(query=<your symbol query>, hint_file_path=<path from step 1>)
```

After JIT-ing the hint file, codeidx **also JIT-indexes directly referenced files** (implements/inherits/injects/calls targets, up to 10). The queried file and its immediate cluster are all fresh in one call.

**Wrong:** calling `read_query` without `hint_file_path` when you know which file the symbol lives in.

## Scoped vault (`scoped_vault`)

Generate a persistent named Obsidian subset without regenerating the full vault:

| Param | Effect |
|-------|--------|
| `root_symbol` + `depth` | BFS through edge graph N hops; collects all reachable types/interfaces/enums/delegates |
| `namespace_prefix` | All symbols whose `qualified_name` starts with the prefix (e.g. `"Billing.Invoicing.NSB"`) |
| `symbol_names` | Explicit list of symbol or qualified names |

Modes are combinable. Output persists at `.codeidx/scoped_vaults/<out_name>/` until deleted.

## Auto-build on first start

If no DB exists when the MCP server starts, codeidx bootstraps an empty schema and runs a full `--all-solutions` index in a **background daemon thread** — MCP is ready immediately. Early queries return empty; the background pass populates the full graph in ~1–2 min. Do **not** prompt the user to build the index manually — it happens on its own. Subsequent sessions use the cached DB; only JIT per-file reindexes occur.

## Stale data

If results look wrong after large edits, suggest re-indexing: `python -m codeidx index .` from the repo root, then re-query via MCP. For single-file freshness without a full re-index, pass `hint_file_path` to `read_query`.

## Optional CLI fallback

If MCP is unavailable in a session, the same DB can be queried with **Python** (preferred on Windows; do not assume `sqlite3` is on PATH):

```bash
python -m codeidx query stats
python -m codeidx query find-symbol --name <Symbol>
python -m codeidx query path-search --substring <pathFragment>
python -m codeidx query callers-of --symbol-id <id>
python -m codeidx query implementations-of --symbol-id <id>
```

Use `--db` only if the database is not at `<repo>/.codeidx/db/codeidx.db`. Confirm with `codeidx query stats` from the repo root.
