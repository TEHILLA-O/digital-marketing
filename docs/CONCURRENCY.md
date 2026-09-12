# Concurrency

LedgerX treats money movement as a contention problem, not a best-effort increment.

## Dangerous scenario

Alice's current account has £100.

- Request A transfers £80
- Request B transfers £80

Both must not succeed. The ledger would otherwise invent £60 that does not exist.

## Chosen strategy

1. **PostgreSQL row locks on ledger accounts**  
   `PostTransferAsync` / `PostWithdrawalAsync` open a database transaction and `SELECT … FOR UPDATE` the sender (and house cash when needed) before reading the derived liability balance.

2. **Balance derived from immutable lines**  
   Available funds = `SUM(credits) - SUM(debits)` on the customer liability account. The bank-account `ProjectedBalance` is a read model only.

3. **Unique idempotency constraint**  
   `(SenderCustomerId, IdempotencyKey)` is unique on `transfers`. Journals have a unique `IdempotencyKey`. A retried client cannot post a second journal for the same key.

4. **Redis short lock**  
   Payments takes a per-source-account Redis lock (`ledgerx:lock:account:{id}`) as a first gate so obvious double-clicks fail fast. The database lock remains authoritative if Redis is slow or the lock expires.

5. **xmin concurrency tokens**  
   Bank accounts and transfers map PostgreSQL `xmin` so lost updates on status changes surface as `ConcurrencyConflictException` rather than silent overwrites.

## Why not serializable everywhere?

Serializable isolation would work, but row-level locks plus a unique idempotency key are easier to reason about in an interview and cheaper under load. The invariant that matters is: **no journal is posted unless the locked sender still has funds**.

## Tests

`LedgerX.IntegrationTests.LedgerConcurrencyTests` starts PostgreSQL with Testcontainers and races two £80 transfers against a £100 opening deposit. Exactly one succeeds; Alice remains at £20.
