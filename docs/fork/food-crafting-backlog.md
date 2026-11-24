# Food Crafting – Fork Feature Backlog

Fork-only document (does not exist upstream, so it never conflicts on merge).
Status legend: `[ ]` planned · `[~]` built, needs check · `[x]` done · `[?]` idea / needs research

Branch: `fork/food-crafting` · Code: `src/StatisticsAnalysisTool/Crafting/FoodCrafting/` · Tests: `src/StatisticsAnalysisTool.UnitTests/Crafting/FoodCraftingTests.cs`

> **Build status (2026-11-22):** .NET 10 SDK 10.0.404 installed. Solution builds with 0 errors (no new warnings);
> all 258 unit tests pass. Used for real batches by the user (sell orders, plan lock, safety margin confirmed).

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
- Cooking specializations (destiny board, verified in-game): Soups, Salads, Pies, Omelettes, Stews, Sandwiches, Roasts,
  **Ingredients**, **Meat**. There is no Grilled Fish spec. Grilled fish / other foods only get the shared FCE part.
- **Focus formula (verified in-game):** focus = base × 0.5^(FCE / 10,000);
  FCE = mastery × 30 + every spec level × 30 (all foods) + own spec level × 250 extra (own spec = 280 / level).
  Checked again with T5 Cabbage Soup: 43,140 FCE → 25.3 focus per craft. Setting renamed to
  `FceExtraPerOwnSpecializationLevel` so an old saved 280 is not double-counted.
  The game shows focus rounded (34) but really uses decimals (33.8 × 5 crafts = 169).
- **Focus is per crafted item, not per craft (fixed 2026-11-24).** `@craftingfocus` and the in-game number are per item;
  one craft of a 10-item recipe costs 10× (Cabbage Soup: 25.3 per soup → 253 per craft). Before the fix the plan
  showed ~10× too little focus (user: plan 670, real batch ~7k). `FoodFocusCalculator.GetFocusPerCraft`.
- **Fish sauce:** no focus toggle and **0% return rate** when crafted (verified in-game).
- **Station fee:** T1 and T2 recipes cost no station fee (verified in-game) → fish sauce (T1) is free to craft,
  T1/T2 foods are calculated without a fee.
- City crafting bonus is 18% everywhere; **Caerleon has +15% for food**. Brecilien with focus = 43.5% RRR (matches the sheet).
- City cluster ids: Thetford 0000, Lymhurst 1000, Bridgewatch 2000, Martlock 3004, Fort Sterling 4000, Caerleon 3003, Brecilien 5000.

## A. Food Crafting sub-tab
- [~] A1. New "Food Crafting" sub-tab (Crafting → Food Crafting), food items only, all tiers & enchantments.
- [~] A2. Reuses existing `CraftingCalculator`, `CraftingLocationData`, `CraftingStationFeeService`, recipe resolver helpers.

## B. Destiny board & focus
- [~] B1. Inputs for cooking mastery + each food category level, saved in `FoodCrafting.json` (user data folder).
- [~] B2. Focus per craft / per item shown for every food.
- [~] B3. Profit per focus point.
- [x] B4. Formula verified in-game (own spec counts 250 + 30). Constants stay editable under "Focus formula";
      the "Compare" check (pick a food, enter in-game focus) remains for future patches.
- [x] B5. Specialization inputs: Soups, Salads, Pies, Omelettes, Stews, Sandwiches, Roasts, Ingredients, Meat.

## C. Fish sauce
- [~] C1. Per sauce level: Cheapest / Buy / Craft.
- [x] C2. Crafted sauce cost from Chopped Fish + Seaweed prices, 0% return rate (verified in-game).
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
- [~] D8. "Missing / outdated prices" list: every item (food or ingredient) with no price or a price older than
      the max price age, how old it is, and which foods it keeps out of the plan (most blocking first).
      For fish sauce it lists the sauce (Buy), its missing ingredients (Craft) or both (Cheapest).
      "Copy list" copies it, so you can refresh those items in-game with the Albion Data Client.
- [~] D9. Max investment (silver) limit for the plan; 0 = no limit. Leftover silver goes to cheaper foods.
- [x] D10. Real cost from in-game sell orders. When an item's market page (Buy tab) is opened while the tool runs,
      the game's sell orders (price + amount) are stored per item and city (`FoodOrderBook.cs`, saved in
      `FoodCraftingOrderBooks.json`, pages within 5 min are merged, books older than 7 days dropped).
      Ingredient prices then come from the order book: the plan is re-calculated up to 4 times with the average price
      for the planned quantity (buying a lot pushes the price up, e.g. Avalonian Energy). Shopping list shows
      listed amount (red when short), real average/max price, real cost and order age. Settings: on/off, max order age (120 min).
- [x] D11. Lock plan: the lock button keeps the planned foods + crafts fixed (saved, survives restart).
      Prices, profit and the shopping list still update; locked foods are kept even if filters change.
- [x] D12. Shopping safety margin: returns are random, so 'Buy (safe)' = expected + N standard deviations
      (binomial per unit, default 2 = about 98% enough), never more than the no-return quantity.
      Order-book real cost uses the safe quantity.
- [~] D13. "Fish sauce to craft" table: per sauce level the sauces to craft (expected + safety margin, whole crafts),
      crafts, Chopped Fish and Seaweed for that level, plus a total line. The shopping list buys exactly these amounts.

## Open questions / things to check in-game
- [x] Q1. Focus formula constants → verified (own spec 250 + 30).
- [x] Q2. Fish sauce crafting → no focus, 0% return rate.
- [~] Q3. Daily production bonus applies to food only (not fish sauce). User: "probably not" for sauce → kept food-only.
- [x] Q4. Focus is not rounded (33.8 per craft, 169 for 5) → kept as decimal.
- [x] Q5. Station fee entered manually → OK.

## E. Later / ideas
- [~] E1. Subtract owned items: bank chests of the city from Storage History (read-only, no upstream change;
      guild chests skipped) + hand-entered "Owned (extra)" per item. Shopping list shows To buy / Need (safe) / In bank;
      owned fish sauce lowers the sauce crafts. Real cost from sell orders uses the quantity still to buy.
- [?] E2. Read destiny board levels directly from game packets (unknown if the game sends them).
- [?] E3. Use live in-game sales history from the Black Market tab instead of AODP volume.
- [?] E4. Option: sell food instantly to buy orders (no setup fee) and/or buy ingredients via buy orders.
- [?] E5. Exclude / pin specific foods in the planner.
- [?] E6. Multiple destiny board profiles (one per character).
- [?] E7. Copy the plan / shopping list to clipboard.
- [?] E8. "Open in Item Crafting" for one food to see the detailed breakdown.
- [x] E9. Re-crafting returned resources more precisely → done as D12 (safety margin for random returns)
      and D13 (fish sauce rounded to whole crafts per level).

## Known issues (upstream)
- [ ] K1. Storage History keeps an item stack's **first seen quantity**. `VaultController.AddDiscoveredItem`
      (`StorageHistory/VaultController.cs`) returns early when the ObjectId is already known, so a stack that grows
      later (e.g. crafted fish sauce stacking from 1 to 41, same ObjectId) still shows the old amount. Moving it out
      and back keeps the ObjectId, so that does not help. Discovered items are only cleared on zone change
      (`ClusterController` → `ResetDiscoveredItems`).
      **Workaround (used for now):** change zone, come back, open the chest again → correct amount (verified in-game).
      **Fix idea:** update the existing entry's quantity instead of returning (few lines, upstream file, mark `[FORK]`),
      or report / send it to Triky313 so it is fixed upstream.

## Merge-risk log
| Change | Upstream file | Risk |
|---|---|---|
| Food Crafting `TabItem` block (5 lines, marked `[FORK]`) | `UserControls/CraftingControl.xaml` | low–medium (only if upstream edits the lines around the Loss Explorer tab) |
| 1 line calling `FoodOrderBookStore.CaptureFromGame` (marked `[FORK]`) | `Network/Handler/AuctionGetOffersResponseHandler.cs` | low (only if upstream rewrites this small handler) |
