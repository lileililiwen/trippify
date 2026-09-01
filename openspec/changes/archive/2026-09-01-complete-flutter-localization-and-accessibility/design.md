# Design: Complete Flutter localization and accessibility

## Localization

Create ARB resources for English and Simplified Chinese, generate strongly typed accessors, and replace hard-coded screen copy including errors, labels, tooltips, status messages, and empty states. Dates, currencies, plural counts, and provider statuses SHALL use locale-aware formatting. Missing translations fail CI or fall back predictably to English.

## Accessibility

Use semantic names/roles/states, headings, live regions for async status, visible focus, logical traversal order, minimum 24x24 targets, non-color status cues, and measured 4.5:1 text/3:1 UI contrast. Ensure focus is not obscured, forms provide labels and field errors, and authentication supports accessible password entry and error recovery.

## Verification

Add semantics/widget tests, keyboard traversal tests, locale snapshots, contrast measurements, reduced-motion tests, and manual screen-reader checks for supported platforms.
