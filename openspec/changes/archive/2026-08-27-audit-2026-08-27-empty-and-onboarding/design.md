# Design Decisions

## Why a `RegistrationConfirmationScreen`

The current code displays the literal string "Check your email to
confirm your account." in the form. That is invisible after the user
scrolls. A dedicated screen with a checklist (check inbox, check
spam, resend) and a "Resend verification" action gives the user
agency. It also matches the email-confirmation contract the backend
already exposes.

## Why a typed error enum

The current `catch (_) { ... }` everywhere is the source of the
"unavailable" drift. A typed enum — `enum AppError { notFound,
unauthorized, paymentDeclined, network, server, unknown }` — keeps
the user-facing copy in one place and the mapping centralized.

## Why `popUntil` after sign-in

`pushReplacementNamed` is the right choice for a fresh launch
(clears the back stack), but wrong for "I clicked something that
required sign-in, then signed in, and now I want to be back where I
was". `popUntil` returns to the previous route when there is one;
falls back to `/` when the stack is empty.

## Why pair empty states with CTAs

The audit (F22) found four empty states with helpful copy and no
follow-through. Pairing each with at least one action button costs
nothing and removes a dead-end UX.

## Rollback

Per-screen. The typed error enum and the registration confirmation
screen are additive; reverting the affected screens restores the
old behavior without a global flag.
