# Food Crafting – Fork Feature Backlog

Fork-only document (does not exist upstream, so it never conflicts on merge).
Status legend: `[ ]` planned · `[~]` built, needs check · `[x]` done · `[?]` idea / needs research

Branch: `fork/food-crafting` · Code: `src/StatisticsAnalysisTool/Crafting/FoodCrafting/` · Tests: `src/StatisticsAnalysisTool.UnitTests/Crafting/FoodCraftingTests.cs`

> **Build status (2026-11-22):** .NET 10 SDK 10.0.404 installed. Solution builds with 0 errors (no new warnings);
> all 223 unit tests pass, including the 26 food crafting tests. Not yet tried in the running app.

## Decisions
- Only **food** is in scope. Non-food crafting differences are ignored.
- Destiny board input = cooking mastery level + one level per food category (0–100).
- Materials are **bought and food is sold in the same city** (default: Brecilien, selectable).
- Planner only proposes **profitable** foods.
- All fork code lives in new files (`Crafting/FoodCrafting/`); upstream files get only minimal, marked (`[FORK]`) hook lines.
- Prices: buy ingredients from the cheapest sell order, sell food as a sell order (lowest current sell order), tax + setup fee included.

## Game data facts (verified from items.json / craftingmodifiers.json)
- Every food recipe has a base `@craftingfocus` per craft (e.g. T6 Potato Salad .0 = 504, .1 = 704).
- Fish sauce has no focus cost.
- Fish sauce recipes: .1 = 15 Chopped Fish + 1 Seaweed · .2 = 45 + 3 · .3 = 135 + 9
  (the reference sheet uses 125 + 15 for .3 – outdated).
- Enchanted food uses 90 sauce per craft (10-item recipes) or 27 (1-item recipes like Clam Soup).
- Food categories (`shopsubcategory2`): soups, salads, pies, omelettes, stews, sandwiches, roasts, grilledfish, other.
- City crafting bonus is 18% everywhere; **Caerleon has +15% for food**. Brecilien with focus = 43.5% RRR (matches the sheet).
- City cluster ids: Thetford 0000, Lymhurst 1000, Bridgewatch 2000, Martlock 3004, Fort Sterling 4000, Caerleon 3003, Brecilien 5000.

## A. Food Crafting sub-tab
- [~] A1. New "Food Crafting" sub-tab (Crafting → Food Crafting), food items only, all tiers & enchantments.
- [~] A2. Reuses existing `CraftingCalculator`, `CraftingLocationData`, `CraftingStationFeeService`, recipe resolver helpers.

## B. Destiny board & focus
- [~] B1. Inputs for cooking mastery + each food category level, saved in `FoodCrafting.json` (user data folder).
- [~] B2. Focus per craft / per item shown for every food.
- [~] B3. Profit per focus point.
- [ ] B4. **Verify formula in-game.** Assumed: cost = base × 0.5^(FCE / 10000),
      FCE = mastery × 30 + own spec × 250 + other specs × 30 (editable under "Focus formula").
      Use the built-in "Compare" check: pick a food, enter the in-game focus cost → shows implied FCE.
      Reference sheet 504 → 28 implies ≈ 41,700 FCE.

## C. Fish sauce
- [~] C1. Per sauce level: Cheapest / Buy / Craft.
- [~] C2. Crafted sauce cost from Chopped Fish + Seaweed prices, with city return rate (no focus).
- [~] C3. Table: market price vs crafted cost vs used price.
- [~] C4. Uses the tool's return model (cost × (1 − RRR)), not the sheet's 6-iteration "magic number".

## D. Daily focus planner (main goal)
- [~] D1. Inputs: focus budget (10,000), city, daily-volume divisor (10), volume days (7),
      min profit per focus, max price age (24 h), enchantment filter.
- [~] D2. Prices + 7-day average daily volume from AODP for every food (batched, own API client).
- [~] D3. Greedy fill: best profit/focus first, capped at (daily volume ÷ divisor), until focus budget used.
- [~] D4. Plan table + summary tiles (focus used/left, investment, revenue, profit, profit/focus, items).
- [~] D5. Shopping list (expected and no-return quantities), crafted-sauce ingredients listed separately.
- [~] D6. Sales tax + setup fee + station fee included.
- [~] D7. "Recalculate" re-plans with the downloaded prices after changing settings (no new download).

## Open questions / things to check in-game
- [ ] Q1. Focus formula constants (B4).
- [ ] Q2. Does crafting fish sauce get the focus return bonus? Currently: no (sauce has no focus cost).
- [ ] Q3. Daily production bonus is applied to food only (not to fish sauce). Correct?
- [ ] Q4. Does the game round the focus cost (to whole numbers)? Currently kept as decimal.
- [ ] Q5. Station fee is entered manually (silver per 100 nutrition). OK, or read it from the game?

## E. Later / ideas
- [?] E1. Subtract ingredients already owned from the shopping list.
- [?] E2. Read destiny board levels directly from game packets (unknown if the game sends them).
- [?] E3. Use live in-game sales history from the Black Market tab instead of AODP volume.
- [?] E4. Option: sell food instantly to buy orders (no setup fee) and/or buy ingredients via buy orders.
- [?] E5. Exclude / pin specific foods in the planner.
- [?] E6. Multiple destiny board profiles (one per character).
- [?] E7. Copy the plan / shopping list to clipboard.
- [?] E8. "Open in Item Crafting" for one food to see the detailed breakdown.
- [?] E9. Account for re-crafting the returned resources more precisely (variance, rounding per craft).

## Merge-risk log
| Change | Upstream file | Risk |
|---|---|---|
| Food Crafting `TabItem` block (5 lines, marked `[FORK]`) | `UserControls/CraftingControl.xaml` | low–medium (only if upstream edits the lines around the Loss Explorer tab) |
| Nothing else | – | none |
