# Phone-Grapher Backend

## Database Schema

Core tables are modeled with EF Core Code-First in `PhoneGrapherDbContext`:

- `users`: customer, grapher, admin accounts with JWT-compatible identity fields.
- `refresh_tokens`: hashed refresh tokens per user.
- `grapher_profiles`: KYC/CCCD state, bio, location, rating aggregate, verification flags.
- `style_tags` and `grapher_style_tags`: searchable photography styles.
- `grapher_portfolio_items`: portfolio image URLs.
- `grapher_activity_areas`: city/district operating areas.
- `grapher_service_packages`: grapher pricing packages.
- `bookings`: customer, grapher, schedule, location, status, total, platform fee, payout.
- `payment_transactions`: VNPAY transaction reference, payment status, escrow status, release timestamps.
- `reviews`: one customer review per completed booking.
- `presets`: standalone preset/filter store.

Money fields use `numeric(18,2)`. Enums are stored as strings for operational readability. Key indexes cover email, CCCD, grapher location/rating, service price, booking schedule/status, payment transaction refs, and review uniqueness.

## REST API

Authentication:

- `POST /api/auth/register`: register customer, grapher, or admin-shaped account.
- `POST /api/auth/login`: login and return JWT access token plus refresh token.
- `GET /api/auth/me`: current authenticated profile.

Phone-Grapher profile:

- `GET /api/graphers`: search verified graphers by location, style, price, rating, verification.
- `PUT /api/graphers/me`: grapher updates bio, portfolio, style tags, and service packages.

Booking and payment:

- `GET /api/bookings`: list bookings visible to current user.
- `POST /api/bookings`: create booking and VNPAY payment URL.
- `POST /api/bookings/{id}/complete`: assigned grapher completes booking and releases escrow.
- `GET /api/payments/vnpay-return`: VNPAY browser return callback.
- `GET /api/payments/vnpay-ipn`: VNPAY server IPN callback.

Reviews:

- `POST /api/bookings/{bookingId}/reviews`: customer reviews a completed booking.

Admin:

- `POST /api/admin/graphers/{grapherProfileId}/kyc?approved=true`: approve or reject grapher KYC.
- `GET /api/admin/revenue`: gross revenue, platform revenue, payouts, completed bookings, pending KYC.

Frontend bootstrap:

- `GET /api/bootstrap`: returns photographers, styles, presets, booking statuses, and empty placeholders compatible with the current React bootstrap shape.

## Escrow Flow

1. Customer creates a booking through `POST /api/bookings`.
2. Backend validates grapher/package availability, calculates 15% platform fee and 85% grapher payout.
3. Backend creates `bookings` and `payment_transactions` in one EF transaction.
4. Backend returns a VNPAY payment URL for 100% of the booking amount.
5. VNPAY callback/IPN is signature-verified. On success, payment becomes `Succeeded`, escrow becomes `Held`, booking becomes `PendingConfirmation`.
6. Assigned grapher completes the booking. Backend marks booking `Completed` and escrow `Released`.
