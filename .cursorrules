# NPC Project - AI Agent Instructions

These instructions are intended to be injected into the AI's system prompt or workspace rules to ensure consistent behavior across all sessions.

## 1. Codeidx & Documentation Enforcement
**CRITICAL DIRECTIVE:** The `codeidx` skill is mandatory for this project. 
Whenever you create, modify, or refactor a core type or architecture component (such as classes, interfaces, or systems), you **MUST** do the following before concluding your task:

1. **Re-index the codebase:** Run `python -m codeidx index .` from the repository root to ensure the structural index is up to date.
2. **Document reasoning:** Use the codeidx notes tools to persist your architectural decisions. 
   - Run `python -m codeidx notes get-or-create <SymbolName>` to ensure the note exists.
   - Run `python -m codeidx notes append <SymbolName> --text "<Your reasoning here>"` to log *why* the change was made and *what* it does.
3. **Never skip this step:** You must treat this as a strict requirement for every architectural change.

## 2. General Rules
- Prefer clean, decoupled architecture (e.g., Event-driven, Interfaces).
- When writing UI code, prioritize smooth redrawing (double-buffering) over raw console clearing to prevent flickering.
