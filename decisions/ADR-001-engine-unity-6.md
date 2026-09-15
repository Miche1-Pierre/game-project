# ADR-001: Engine, Unity 6

## Status
Accepted (2026-09-15)

## Context
Small team (2), target PC/Steam, need for a fast iteration loop driven by an AI agent.

## Options
- Unity 6
- Unreal Engine 5
- Godot

## Decision
Unity 6 (editor 6000.6.0f1), URP.

## Why
- The entire studied viral co-op wave (Lethal, PEAK, R.E.P.O., Content Warning, Schedule I, Phasmophobia, Among Us) runs on Unity: a proven path.
- Official Unity plugin for Claude Code (MCP), Sept. 2026: live editor control by the agent.
- Unity Personal license is enough for our scale.
- C# is known to the team.

## Consequences
- Dependency on the Unity ecosystem and its MCP.
- URP: good stylized/perf tradeoff, no HDRP.

## Revisit if
The Unity MCP turns out unusable, or a major technical need not covered by Unity appears.
