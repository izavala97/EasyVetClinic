# Clinical and commercial workflows

## First release

This release keeps the existing React / ASP.NET Core architecture and introduces one connected workflow:

1. Register the patient and guardian together, or select existing contacts.
2. Schedule a clinician-specific visit or start a walk-in consultation.
3. Record clinical notes and prescription directions.
4. Add catalog products and services to the consultation's persisted billing account.
5. Explicitly administer or dispense supplied products.
6. Complete clinical care independently of payment.
7. Open the billing account for checkout, record payments, and print the receipt.

Reception can manage scheduling and checkout; clinical editing and finalization require a veterinarian or clinic administrator. The platform owner retains authorized support access. Suspended clinic members cannot operate these workflows.

## Catalogs and historical prices

Products and services are clinic-specific. Administrators create, edit, favorite, and archive items. Stock-tracked products support fractional quantities, unit labels, and low-stock thresholds. Quantities are expressed in the configured product unit; automatic bottle/tablet/milliliter conversions are not implemented.

Charge lines retain item descriptions, units, and prices. Repricing or archiving the catalog does not rewrite existing bills. Archived items remain available in history but cannot be added to new accounts.

Administrator price overrides require a reason stored on the charge line. This is a reason snapshot, not a complete actor/timestamp override audit trail.

Use inventory adjustments for subsequent stock receipts or corrections rather than editing stock through the catalog.

## Independent lifecycles

| Record | States | Meaning |
| --- | --- | --- |
| Consultation | `InProgress`, `Completed`, `Cancelled` | Clinical care and record finalization. |
| Appointment | `Scheduled`, `CheckedIn`, `InProgress`, `Completed`, `Cancelled`, `NoShow` | Scheduling and patient arrival; started care is managed through the linked consultation. |
| Billing account | `Draft`, `Open`, `Closed`, `Voided` | Charge preparation, collection, settlement, or authorized cancellation. |
| Payment status | `Unpaid`, `PartiallyPaid`, `Paid` | Derived from recorded payments and refunds; not a clinical status. |

Opening an account locks its current charges. Review the charge sheet before sending it to checkout. Closing clinical care does not automatically open or pay its account.

Payments record cash, card, or transfer; card entries are bookkeeping, not payment-processor transactions. A split payment is recorded as multiple payments on the same account. Receipts are operational receipts, not fiscal invoices.

## Physical inventory

- A draft charge does not reduce stock.
- A prescription is a clinical direction and does not sell or dispense medication.
- Explicit fulfillment records a movement for stock-tracked products.
- Fulfillment can occur while an account is Draft and unpaid. Supplied lines become immutable, while additional charges can still be added to that Draft account and supplied later.
- Payment does not reduce stock again.
- Voiding a bill or refunding payment does not restore stock automatically.
- A physical return requires an authorized inventory movement and a restock decision.
- Adjustments require an attributable reason.
- Concurrent fulfillment must not oversell available inventory.

Payment and stock-adjustment requests use persisted idempotency keys. If a response is lost, the client retains the unresolved operation and its original payload/key for reconciliation rather than silently generating a second payment or adjustment.

Products supplied externally can appear in a prescription without appearing in the account. Internal consumables can be maintained as stock items, but automatic service-consumable bundles are outside this release.

## Clinical records

Clinical saves include an expected version. Conflicting edits must be reloaded rather than overwriting another user's work. The consultation screen shows unsaved/saved state and guards navigation, reload, and clinic switching.

Finalized notes and prescription directions cannot be overwritten. An amendment stores its author, timestamp, reason, and note while preserving the original record. Cancellation retains the visit and its reason. Financial accounts are not silently cancelled when clinical care is cancelled.

Prescription item identifiers survive edits. Each new consultation snapshots its treating clinician's display name and professional credentials. Set credentials under Clinic settings before starting care. Updating the clinician profile later does not change an existing consultation.

Clinic settings and onboarding contain only clinic details, not a clinic-wide veterinarian identity. Professional titles and license numbers belong to individual clinic members. The obsolete clinic-wide veterinarian columns are removed; their values are not copied or retained as compatibility fields.

## Patient intake and scheduling

Only patient name, species, and a guardian relationship are required for initial intake. Unknown demographics and unassessed allergies can remain unspecified. Select the primary guardian explicitly when linking existing contacts.

Weight measurements and vaccinations have separate timelines. A backdated weight does not replace a more recent displayed measurement. Recording a vaccination does not automatically create a charge or consume vaccine stock.

Configure the clinic time zone under Clinic settings. Appointment entry and calendar display use that zone, independently of the browser. Ambiguous or nonexistent daylight-saving times are rejected. New appointment starts are stored as UTC instants. Different clinicians may work simultaneously; overlapping intervals for the same clinician are rejected.

## Development data and migrations

No database reset is required for this release. New migrations extend the existing SQLite and SQL Server schemas. Development startup applies migrations to the configured database.

Before any deployed database is upgraded, review the generated migration and take a backup. SQLite schema rebuild migrations are not guaranteed to be transactionally recoverable after interruption. SQL Server migration generation and model parity are separate from testing the migrations against a real SQL Server.

The normal local database is not used by the integration scripts. Temporary test data and processes are cleaned up by the scripts.

An upgrade regression seeds the pre-commerce schema and verifies existing product stock/prices and clinical history survive migration. Legacy nonzero tracked stock receives an opening-ledger entry without changing its balance.

## Verification

- `scripts/test-administration.ps1`: ownership, invitations, membership restrictions, tenant boundaries, and suspension regression.
- `scripts/test-clinical-workflow.ps1`: combined intake, guardians, weights, clinician attribution, timezone conversion, interval conflicts, appointment linkage, clinical versions, finalization, amendments, and cancellation.
- `scripts/test-commerce.ps1`: catalog, persisted charges, payment, fulfillment, inventory, and tenant/role invariants.
- `WebClient`: TypeScript/Vite build, lint, and timezone/navigation utility tests.

Enable the **Verify clinic workflows** checks as required checks before merging. Existing deployment workflows are not triggered by these local verification commands.

## Deliberately deferred

This is a first implementation, not the complete production-ready roadmap. Remaining increments include:

- Service bundles, automatic linked consumables, and unit conversions.
- Medication lots, expiry tracking, and supplier/purchasing workflows.
- Custom uncatalogued charges and a complete tax/discount policy.
- Autosave and fuller front-end end-to-end automation.
- Versioned signed documents and private attachment storage.
- Fiscal invoicing / CFDI and online payment processing.
- Automatic SaaS subscription collection.
- Hospitalization, boarding operations, offline synchronization, and AI features.

These features must not bypass the distinction between clinical care, physical stock movement, and financial settlement.
