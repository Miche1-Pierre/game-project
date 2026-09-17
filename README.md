# GAME PROJECT (provisional codename)

**Status: GREYBOX.** The concept is locked (ADR-004). The game still has no name.

This repository is the project's **decision repository**: it explains why we build, how we validate, what it costs, and how we distribute. The Unity code is only one part (`UnityProject/`).

## Where we are
- **Phase:** greybox (concept locked, spec frozen)
- **Concept:** steal under watch. A moving crew empties a house while the owner is present, and steals what it can get away with.
- **Current objective:** build the greybox and find out whether anyone laughs
- **Next milestone:** the go / no-go on a playable greybox
- Living dashboard: [`00_PROJECT/PROJECT_STATE.md`](00_PROJECT/PROJECT_STATE.md)

## Method
Research, Design, Experiments, Unity, Playtests, Decisions, Production, Business, Steam, Marketing, Analytics.
We optimize for **learning speed** before development volume.

## Current priority
DO NOT build content. VALIDATE the core gameplay. Scope is frozen in [`02_GAME_DESIGN/GREYBOX_SPEC.md`](02_GAME_DESIGN/GREYBOX_SPEC.md).

## Stack
Unity 6 (6000.6.0f1), C#, Unity MCP + Claude Code, Steam (PC). Team: 2 (Miche1-Pierre, Spykernv).

## For the agent (Claude)
Read [`CLAUDE.md`](CLAUDE.md) first, then [`00_PROJECT/PROJECT_STATE.md`](00_PROJECT/PROJECT_STATE.md).

## Structure
`00_PROJECT` direction, `01_RESEARCH` benchmark, `02_GAME_DESIGN` design, `03_TECHNICAL`, `04_PRODUCTION`, `05_ART`, `06_AUDIO`, `07_MULTIPLAYER`, `08_BUSINESS`, `09_STEAM`, `10_MARKETING`, `11_TESTING`, `12_ANALYTICS`, `13_RELEASE`, `decisions/` ADR, `experiments/`, `playtests/`, `changelog/`, `UnityProject/`.
