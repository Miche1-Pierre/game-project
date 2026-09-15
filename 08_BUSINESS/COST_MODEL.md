# COST MODEL

## Objective
The project should stay extremely cost-efficient. The goal is not to minimize spending at all costs, but to ensure every significant expense has a clear relationship with development speed, product quality, player experience, marketability, distribution or commercial potential.

We do not spend money to compensate for an unvalidated game. The initial budget is therefore deliberately conservative. Our advantage is that we already have Claude, Unity and Blender, and we want to avoid heavy production.

## Cost categories
Amounts are one-time project costs unless marked recurring. Claude is a separate monthly subscription (see note below the table).

| Category | Minimum | Expected | Maximum | Status |
| --- | ---: | ---: | ---: | --- |
| Claude Max x5 (Pierre) | 108 €/mo | 108 €/mo | 108 €/mo | Recurring, personal plan. Shared-cost status TBD (see `TEAM_AGREEMENT.md`) |
| Unity | 0 € | 0 € | 0 € | Unity Personal |
| Blender | 0 € | 0 € | 0 € | Free |
| AI 3D / asset generation | 0 € | 100 € | 150 € | Only if needed (Astra, exact cost unknown) |
| AI image generation | 0 € | 0 € | 0 € | Not planned |
| AI audio / music | 0 € | 0 € | 0 € | Not planned |
| Steam Direct | ~90 € | ~90 € | ~90 € | Required, one-time ($100) |
| Domain | 0 € | 20 € | 30 € | Optional |
| Website hosting | 0 € | 0 € | 20 € | Prefer free/simple |
| Backend / cloud | 0 € | 0 € | 50 € | Avoid unless required |
| Paid assets / plugins | 0 € | 0 € | 0 € | Not planned initially |
| Trailer / video production | 0 € | 0 € | 100 € | Prefer in-house |
| Marketing / creator outreach | 0 € | 100 € | 200 € | Initially organic |
| Miscellaneous | 0 € | 50 € | 100 € | Contingency |
| **Total one-time (excl. Claude)** | **~90 €** | **~360 €** | **~740 €** | |

The maximum column is a theoretical ceiling per category, not a spending target. Most of these (marketing, trailer, paid assets, domain) only apply after the greybox is validated, when we already know whether the game has potential.

**Claude note:** Claude Max x5 is a recurring 108 €/month subscription that Pierre already pays. Over the active development window (roughly two weeks of sprint plus about a month to launch) that is roughly 200 to 250 € of Claude, ongoing rather than one-time. Whether it counts as a shared project expense or stays Pierre's personal cost is a `TEAM_AGREEMENT.md` item.

## Comfortable spending envelope (one-time, excluding Claude)
- **Minimum:** ~90 to 200 €. Covers Steam and basic unavoidable costs.
- **Expected:** ~350 to 500 €. Steam, some AI 3D generation, domain, small marketing, misc.
- **Maximum:** ~750 €. An initial financial risk ceiling, not an intended spend. Going above it requires evidence that the game is fun, technically viable, commercially promising, and that the extra spend has a clear expected benefit.

## Budget philosophy
**Before the greybox is validated:** spending close to zero. No final 3D assets, large asset packs, professional music, advertising, contractors, complex infrastructure or large subscriptions. The only meaningful expenses are development tools already justified by the workflow.

**After the greybox is validated:** spending can increase selectively, prioritizing expenses that save substantial dev time, improve perceived quality, improve Steam conversion, improve discoverability, or remove a real technical bottleneck.

## Per-category notes
- **AI coding (Claude):** primary accelerator. Used for C# implementation, Unity scripting, debugging, refactoring, docs, tests, editor tooling, boilerplate, project-management and research support. Must reduce human time without increasing technical debt.
- **AI asset generation:** may cover 3D models, textures, concept art, UI, promo visuals. Expected 100 to 150 €, only when the result clearly beats free/manual alternatives. Prefer a small number of tools used heavily over a large toolchain.
- **Unity / Blender:** 0 €. Unity Personal while eligible; Blender for mesh editing, cleanup, optimization, UV, materials, rigging where unavoidable, conversion.
- **Steam Direct:** ~90 € ($100), required one-time publishing fee.
- **Domain:** ~15 to 30 €/year, optional. No custom infrastructure needed initially.
- **Hosting / backend:** target 0 €. Avoid custom backend unless the final design requires it (game servers, relay, databases, analytics, auth, cloud storage).
- **Paid assets / plugins:** 0 € initially. Justified only when the cost is well below the human time to reproduce equivalent quality.
- **Marketing:** organic first (Steam wishlists, short clips, TikTok/Shorts/X, Discord, creator outreach, playtests, Steam Playtest, word of mouth). Expected ~100 €, max ~200 €. No significant paid ads before strong player response. The product itself should generate marketable moments.
- **Trailer / promo assets:** 0 € initially, produced in-house. Paid production only if the in-house result clearly limits Steam conversion.
- **Audio:** 0 € planned. Free/licensed assets, AI-assisted where legally appropriate, team-made sounds. Not a major production budget.
- **Contractors:** 0 € initially. Possible future exceptions (music, key art, trailer, voice, localization, specialized technical work) require explicit justification.

## Spending gates
- **Gate 0, Idea:** ~0 €. No significant spending.
- **Gate 1, Greybox:** minimal. Objective: prove the game is fun.
- **Gate 2, Validated prototype:** selective spending becomes possible. Objective: turn a proven loop into a credible product.
- **Gate 3, Release candidate:** spending may increase for Steam assets, marketing, QA, audio, final polish, creator outreach.
- **Gate 4, Launch:** spending decisions based on wishlists, playtest results, retention, creator interest, store conversion.

## Cost tracking
Record every expense with: date, category, amount, currency, paid by, purpose, shared/personal, approved by both, reimbursable or not. Update this model whenever a new recurring or significant one-time expense appears.

## Financial rule
Never spend money merely because it can make development easier. The default question for any expense: **does this materially increase our probability of shipping a good game or achieving commercial success?** If unclear, defer it.
