# Guide reviews and update feedback

Verified purchasers rate and review published guides, authors reply once per review, and readers report abuse or send private update feedback.

## Reviews

- `POST /api/v1/guides/{guideId}/reviews { rating, body }` submits a review. The guide must be published; the author cannot review their own guide (`400`), non-entitled users of paid guides are rejected (`403`), and one review per user per guide is enforced (`409`). Ratings are 1–5 and bodies are capped at 4000 characters.
- `PUT /api/v1/reviews/{reviewId}` edits the author's own rating and body.
- `DELETE /api/v1/reviews/{reviewId}` removes the review (and its reply via cascade); only the review author may do so.
- `GET /api/v1/guides/{guideId}/reviews` publicly lists visible reviews with author replies, newest first. Hidden or flagged reviews are excluded.

## Author replies

- `POST /api/v1/reviews/{reviewId}/reply { body }` lets the guide author post exactly one reply per review (`409` on duplicates). Bodies are capped at 4000 characters.

## Reports and moderation

- `POST /api/v1/reviews/{reviewId}/reports { reason }` lets any authenticated user except the review author report abuse; one report per user per review (`409`), reasons up to 500 characters.
- `PUT /api/v1/admin/reviews/{reviewId}/moderate { status }` (Administrator role) sets moderation status to `Visible`, `Flagged`, or `Hidden`. Hidden reviews disappear from public listings.

## Update feedback

- `POST /api/v1/guides/{guideId}/feedback { body }` lets any authenticated user send private correction feedback to a published guide; one submission per user per guide (`409`), bodies up to 4000 characters.

## Operations

The `Trippify.Reviews` meter emits `trippify.review.commands` with low-cardinality `operation` tags (`review-submitted`, `review-edited`, `review-deleted`, `reviews-listed`, `reply-created`, `report-created`, `feedback-submitted`, `review-moderated`). Alert on elevated 403 (entitlement bypass attempts) and 409 (duplicate submissions). Never record review bodies, reasons, or feedback text in telemetry.

Apply the forward-only EF Core migration before the matching API version. Unique indexes `(GuideId, UserId)` on reviews and feedback, `(ReviewId)` on replies, and `(ReviewId, ReporterUserId)` on reports enforce one-row-per-actor constraints at the database level.
