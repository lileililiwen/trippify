# Proposal: Complete Flutter localization and accessibility

## Problem

The app declares English and Chinese locales but hard-codes user-facing English strings. Accessibility semantics exist selectively, without evidence for full keyboard, focus, contrast, target-size, reduced-motion, or authentication checks.

## Scope

Introduce generated localization resources and audit core flows against WCAG 2.2 AA, including forms, navigation, dialogs, lists, loading/error/empty states, uploads, checkout, and role workspaces. This excludes native platform accessibility redesign outside Flutter.

## Acceptance

English and Chinese render complete copy; all core workflows are keyboard and screen-reader usable; focus and contrast pass measured gates; reduced motion and accessible authentication behavior are supported.
