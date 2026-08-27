# Design Decisions

## Why `Form` + `TextFormField`

The current code uses bare `TextField` + a single status `Text` for
errors. `Form` + `TextFormField` + `validator` is the idiomatic M3
pattern: it gives us `autovalidateMode`, an `InputDecoration.errorText`
slot next to the field, and a `FormState.validate()` call that returns
a boolean we can short-circuit on.

## Why an RFC-5322-light regex

Full RFC-5322 is famously unreadable. A practical compromise is
`^[^\s@]+@[^\s@]+\.[^\s@]+$` — it catches the common 99% of malformed
emails (`foo`, `foo@`, `@bar.com`, `foo@bar`) without trying to
match the full RFC grammar. The server remains the authoritative
validator.

## Why a `Semantics(value:)` rating row

TalkBack/VoiceOver announce a row of five buttons as "Button, Button,
Button, Button, Button". A `Semantics(value: 3, label: 'Rating: 3 of
5')` collapses that into a single semantic node. The buttons remain
individually focusable so a motor-impaired user can still pick a
rating.

## Why a contrast golden

The `flutter test` golden pipeline can render the four home shapes at
both brightnesses and measure the actual `colorScheme.onSurfaceMuted`
text against the surface. The test fails if the ratio drops below
4.5:1, so a token regression cannot ship silently.

## Rollback

Per-screen. If a single form regresses, revert that form to
`TextField` + status text. No data migration.
