# ADR-001: Engine, Unity 6

## Status
Accepted (2026-09-15). **Amended 2026-09-17**, see below.

## Amendment (2026-09-17): the render pipeline is Built-in, not URP
The project runs on **Built-in RP**. It was created from the URP template, but no URP
package was ever installed: `Packages/manifest.json` has no
`com.unity.render-pipelines.universal`, and `Shader.Find("Universal Render Pipeline/Lit")`
returns null in the running editor.

The engine decision itself stands. The pipeline line below is kept as written because
it is what was decided, not what is true. Every third-party pack in the project ships
Built-in materials, so moving to URP now means upgrading all of them first. See
`../03_TECHNICAL/ASSET_AUDIT.md`.

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
