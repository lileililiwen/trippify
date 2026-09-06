# Delivery documentation

## REMOVED Requirements

### Requirement: Roadmap SHALL be the authoritative delivery record

The delivery roadmap and handoff MUST accurately reflect shipped and pending
work so agents do not start already-shipped changes or omit shipped work.

#### Scenario: Shipped change absent from roadmap

- **Given** a change is committed, archived, and has a source-of-truth spec
- **When** a contributor or agent reads the roadmap
- **Then** the shipped change is listed in the completed work
- **And** the roadmap does not present a shipped change as the next pending item

#### Scenario: Handoff count matches audit backlog

- **Given** the September 2026 audit backlog defines the active changes
- **When** the handoff states how many audit changes shipped
- **Then** the stated count matches the actual number of audit changes
