# Tasks

## 1. Session coordination
- [x] 1.1 Add a shared session controller that invokes logout and clears all locally persisted credentials and cached private state in `finally`.
- [x] 1.2 Replace dummy-login and duplicate sign-out handlers with the shared flow.
- [x] 1.3 Handle expired/revoked sessions consistently and reset navigation to the anonymous home/sign-in route.

## 2. Flutter behavior and accessibility
- [x] 2.1 Prevent back navigation into authenticated screens after sign-out.
- [x] 2.2 Expose an accessible sign-out control and actionable offline/server-failure feedback without retaining credentials.
- [x] 2.3 Verify session listeners are registered once and disposed correctly.

## 3. Verification
- [x] 3.1 Test successful, offline, server-error, already-expired, and repeated sign-out flows with secure storage assertions.
- [x] 3.2 Test anonymous/authenticated route transitions, role-specific shells, back navigation, and no dummy login request.
- [x] 3.3 Run Flutter analyze/tests and update authentication UX documentation.
