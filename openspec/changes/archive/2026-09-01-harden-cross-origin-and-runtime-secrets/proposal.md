# Proposal: Harden cross-origin and runtime secrets

## Problem

The API permits every origin while also allowing credentials, and the Docker example contains a weak default signed-URL secret. This creates avoidable cross-origin attack surface and encourages insecure deployments.

## Scope

Restrict credentialed CORS to explicitly configured origins, reject unsafe production configuration, and replace weak example secrets with required secret injection. Add startup and HTTP tests. This excludes authentication redesign and secret-manager vendor selection.

## Acceptance

Configured origins succeed; unconfigured origins fail preflight and credentialed requests; production rejects missing/weak runtime secrets; development remains usable with explicit local defaults.
