# Proposal: Align Flutter API methods and error states

## Problem

The Flutter client calls `PATCH` for trip updates, but its request dispatcher does not implement `PATCH`. Many screens also collapse typed server errors into generic text, while a large set of meaningful widget tests is skipped.

## Scope

Align HTTP method support, cancellation/retry behavior, typed error presentation, controller disposal, and focused tests. This excludes a full networking-library replacement.

## Acceptance

Trip edits reach the API; unauthorized, conflict, validation, unavailable, and offline states are distinguishable; retries do not duplicate mutations; covered tests run instead of remaining skipped.
