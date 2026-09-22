# Watt-Wise — Product Specification

## Vision

Watt-Wise helps Lithuanian households find the cheapest electricity setup. A user uploads their real consumption history, and the product calculates which grid plan, which supplier plan, and which combination of the two would cost them the least — and how much they would save compared to what they pay today.

## Target market and users

- **Market:** Lithuania. Grid operator is ESO; suppliers include Ignitis, Enefit, Elektrum and others.
- **Users:** the general public. Anyone with an ESO account can use it.
- **Business model:** free at launch. Monetization is undecided (affiliate fees, premium tier, or ads are all possible); the design must not block any of them.
- **Scale for first release:** hundreds of users, hosted on a cheap single server or PaaS. Optimize for low cost and simplicity.

## Core user flow

1. User signs up / logs in.
2. User creates a **consumption object** (e.g. home, summer house). MVP supports one object per account; the data model and UI must be designed so that multiple objects can be added later.
3. For each object the user:
   - uploads the hourly consumption CSV exported from the ESO "Mano ESO" portal;
   - enters their **current grid plan** and **current supplier plan** with their pricing, so the product has a baseline to compare against.
4. The UI shows a rich, filterable chart of consumption over time.
5. User selects the analysis period (default: the last year from today; if data is insufficient, use whatever is available) and the cost basis (per month or per year; default month).
6. The product shows a **ranked list** of available grid plans, supplier plans, and grid+supplier combinations, each with its calculated monthly/yearly cost and the savings versus the user's current plans. "Best" means cheapest.
7. The user browses the catalog of current supplier and grid plans in a rich UI.

The UI is designed mobile-first: every screen works well on a phone and scales up to desktop. Users can choose light, dark or automatic (follow device) appearance.

## Functional requirements

### Consumption data
- Input format: ESO hourly CSV export only. The parser targets exactly what Mano ESO produces.
- Data covers whatever period the user exported; multiple uploads per object should be merged.

### Plan catalog
- Central catalog of current grid plans and supplier plans, maintained by the product (not by users).
- Source priority: official API or open data first; if unavailable, scrape provider websites.
- Supplier plan availability depends on the selected grid plan; only valid grid+supplier pairs are offered.

### Tariff types
- All supplier plan types offered on the Lithuanian market must be supported in MVP: fixed price (single rate), time-of-use (day/night, 2- and 4-zone), exchange/spot (Nord Pool LT hourly price + supplier margin), and hybrid / partially fixed plans.
- The plan model must be flexible enough to represent any pricing structure a supplier publishes, not just the types listed above.

### Spot price data
- The product fetches and stores historical hourly Nord Pool day-ahead prices for the LT bidding zone (from Nord Pool, ENTSO-E, or Litgrid open data) via a scheduled job.
- Price history must cover at least the range of consumption data users can upload.
- Spot-plan results are a backtest ("what you would have paid"), not a forecast; the UI must say so.

### Calculation
- Cost for each candidate plan/combination is computed from the user's actual hourly consumption over the selected period. For spot plans, each hour's consumption is multiplied by that hour's market price plus margin.
- Results are shown per month and/or per year, ranked by total cost, with the delta against the user's current plans.

### Accounts and data
- Login via username and password in MVP. OAuth / third-party sign-in may be added later; the account model must not block it.
- Data is kept until the user deletes it. Users can fully delete their account and data, and fully export their data (GDPR).

## Non-goals for MVP
- Prosumers (solar generation, net metering, ESO storage fees) — keep in mind in the data model, no UI.
- Multiple consumption objects per account — designed for, not exposed in MVP.
- OAuth / third-party sign-in — designed for, not exposed in MVP.
- Price-change alerts or notifications.

## Open questions
- Whether ESO grid plan pricing requires household parameters beyond the current plan (contracted power in kW, number of phases). To be resolved when the ESO tariff structure is modeled.
- Exact source (API/open data vs. scraping) for each provider, to be determined per provider during implementation.
