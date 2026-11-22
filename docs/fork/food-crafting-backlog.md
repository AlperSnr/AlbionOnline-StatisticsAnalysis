# Food Crafting – Fork Feature Backlog

Fork-only document (does not exist upstream, so it never conflicts on merge).
Status legend: `[ ]` planned · `[~]` in progress · `[x]` done · `[?]` idea / needs research

## Decisions
- Only **food** is in scope. Non-food crafting differences are ignored.
- Destiny board input = cooking mastery level + one level per food category (0–100).
- Materials are **bought and food is sold in the same city** (default: Brecilien, selectable).
- Planner only proposes **profitable** foods.
- All fork code lives in new files (`Crafting/Food/`); upstream files get only minimal, marked (`// [FORK]`) hook lines.

## Game data facts (verified from items.json)
- Every food recipe has a base `@craftingfocus` per craft (e.g. T6 Potato Salad .0 = 504, .1 = 704).
- Fish sauce has no focus cost.
- Fish sauce recipes: .1 = 15 Chopped Fish + 1 Seaweed · .2 = 45 + 3 · .3 = 135 + 9
  (the reference sheet uses 125 + 15 for .3 – outdated).
- Enchanted food uses 90 sauce per craft (10-item recipes) or 27 (1-item recipes like Clam Soup).
- Food categories (`shopsubcategory2`): soups, salads, pies, omelettes, stews, sandwiches, roasts, grilledfish, other.
- Daily sold volume is available via AODP `stats/history` (`ApiController.GetHistoryItemPricesFromJsonAsync`).

## A. Food Crafting sub-tab
- [ ] A1. New sub-tab next to "Item Crafting", food items only (all tiers & enchantments).
- [ ] A2. Reuse existing `CraftingCalculator` math (no copy, no edits).

## B. Destiny board & focus
- [ ] B1. Inputs for cooking mastery + each food category level, saved in a fork-only settings file.
- [ ] B2. Focus per craft / per item / total for the batch.
- [ ] B3. Profit per focus point.
- [ ] B4. Verify formula against in-game values (assumed: cost = base × 0.5^(FCE / 10000);
      reference sheet 504 → 28 implies ≈ 41,700 FCE). **Need: one in-game focus value + my levels.**

## C. Fish sauce
- [ ] C1. Per sauce level: Buy vs Craft.
- [ ] C2. Crafted sauce cost from Chopped Fish + Seaweed prices, with return rate applied.
- [ ] C3. Show market sauce price vs crafted cost.
- [ ] C4. Use tool's return model (cost × (1 − RRR)) instead of the sheet's 6-iteration "magic number".

## D. Daily focus planner (main goal)
- [ ] D1. Inputs: focus budget (default 10,000), city, daily-volume divisor (default 10),
      min profit per focus, max price age, tier/enchant filter.
- [ ] D2. Fetch prices + daily volume for every food, compute profit per focus.
- [ ] D3. Greedy fill: best profit/focus first, capped at (daily volume ÷ divisor), until focus budget used.
- [ ] D4. Result list: food, crafts, items, focus used, profit, ROI.
- [ ] D5. Combined shopping list incl. sauce ingredients when crafting sauce.
- [ ] D6. Include sales tax + setup fee (the sheet ignores them).

## E. Later / ideas
- [?] E1. Subtract ingredients already owned from the shopping list.
- [?] E2. Read destiny board levels directly from game packets (unknown if the game sends them).
- [?] E3. Use live in-game sales history from the Black Market tab instead of AODP volume.

## Merge-risk log
| Change | Upstream file | Risk |
|---|---|---|
| New tab hook | `UserControls/CraftingControl.xaml` (~3 lines) | low–medium |
| View model hook | `Crafting/CraftingBindings.cs` or `ViewModels/MainWindowViewModel.cs` (~2 lines) | low |
