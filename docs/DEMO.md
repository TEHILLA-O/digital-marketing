# Demo walkthrough

LedgerX is an **educational simulation**. Nothing here is a real bank, Faster Payments instruction, or regulated activity.

Default development password (override with `LEDGERX_DEMO_PASSWORD` / `LedgerX:Seed:DemoPassword`):

```text
LedgerX-Demo-2026!
```

## Accounts

| Email | Role | Opening position |
| --- | --- | --- |
| demo.customer@ledgerx.local | Customer (Alice Thompson) | Current £5,000 · Savings £2,500 |
| bob.customer@ledgerx.local | Customer (Bob Nguyen) | Current £1,000 |
| carol.customer@ledgerx.local | Customer (Carol Okoye) | Current £750 |
| admin@ledgerx.local | Administrator | Operations UI |
| auditor@ledgerx.local | Auditor | Read-only ledger and audit |
| finance@ledgerx.local | FinanceOperator | Ledger + failed payments |
| support@ledgerx.local | SupportAgent | Freeze / customer search |

## Flow

1. Open http://localhost:5100 and sign in as Alice (`demo.customer@ledgerx.local`).
2. Dashboard shows Alice Current at **£5,000**.
3. Sign out and sign in as Bob if you want to confirm **£1,000**, then return to Alice.
4. Open **Transfer**, choose Alice Current → Bob Nguyen, amount **£500**.
5. Submit. The payment is `Created → Validated → Processing`.
6. Payments calls the Ledger API. A journal is posted:

   ```text
   Dr  LIAB:CUSTOMER:{alice-current}   500 GBP
   Cr  LIAB:CUSTOMER:{bob-current}     500 GBP
   ```

7. The transfer becomes **Completed** and stores the journal id.
8. Alice Current is **£4,500**.
9. Bob Current is **£1,500**.
10. Sign in as `admin@ledgerx.local` → **Ledger explorer**.
11. Confirm `TotalDebits == TotalCredits`.
12. Open Kafka UI at http://localhost:8088 and inspect `ledgerx.payments.events` / `ledgerx.ledger.events`.
13. Open **Audit log** for `ACCOUNT_FROZEN` after you freeze an account from Operations.

## API check

```http
POST http://localhost:5101/api/auth/login
Content-Type: application/json

{ "email": "demo.customer@ledgerx.local", "password": "LedgerX-Demo-2026!" }
```

```http
POST http://localhost:5103/api/transfers
Authorization: Bearer <token>
Idempotency-Key: 11111111-1111-1111-1111-111111111111
Content-Type: application/json

{
  "sourceAccountId": "bbbbbbbb-0001-0001-0001-000000000001",
  "destinationAccountId": "bbbbbbbb-0001-0001-0001-000000000003",
  "amount": 500,
  "currency": "GBP",
  "description": "Demo transfer"
}
```

Repeating the same `Idempotency-Key` must return the original transfer and must not move money again.
