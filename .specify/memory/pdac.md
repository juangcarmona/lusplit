# Product Definition as Code (PDaC) guidance for this Spec Kit workspace

<!-- Managed by `prodshape integration add speckit`. Do not edit; changes are overwritten by `prodshape integration update`. -->

This repository uses Product Definition as Code. The canonical product definition lives in
docs/product/model (actors, journeys, use cases, business rules, domain terms, requirements).
Spec Kit artifacts consume it and are never a second source of truth. The constitution
(.specify/memory/constitution.md) governs how software is built here; it never carries product
intent, and this file never modifies it.

## Citations bind

A citation binds consumer text to a canonical artifact: it records the artifact id, a sha256
content digest and an optional verification scenario anchor. Verification reports one status per
citation: current, stale, tampered or unresolved.

To cite: run `npx prodshape inspect <ID>` to read the current digest, then
`npx prodshape cite --id <ID> --digest <digest>` to emit the canonical payload. Wrap it in the
document's native comment (`<!-- ... -->` in Markdown) on its own line directly under the text
it grounds. Never write a citation record by hand, and never invent artifact ids or digests.

## Before specifying a feature

Start from cited product context, not from paraphrase. Run
`npx prodshape context <ID> [<ID>...]` for the product artifacts the feature implements; the
projection carries the relevant canonical excerpts with their citation records attached. Feed it
to the Spec Kit specify command and keep the citations in the generated spec.md. To find which
artifacts a feature depends on, compare its intent with the whole product definition first, then
widen the result with `npx prodshape impact <ID>`.

## While writing spec.md, plan.md and tasks.md

- Every requirement derived from canonical product text carries a citation to every PDaC artifact
  it derives from, not only the closest one, one citation per line under the text it grounds.
- When a plan decision depends on canonical product text, cite the artifact it depends on.
- A task that changes cited behaviour includes a follow-up task to refresh the affected citations.
- Every gated document (spec.md, plan.md, tasks.md) of a feature must end up bound or exempt,
  each with an explicit declaration: declare `pdac-scope: cited` on a line of its own and cite
  the canonical text the document depends on, or declare `pdac-scope: none` with a non-empty
  reason (`pdac-scope-reason: <why>` in frontmatter, or
  `<!-- pdac-scope: none reason="<why>" -->`) when a human judges the document has no
  product-semantic dependency. Citations alone never bind, and never declare an exemption just
  because citations are missing.

## Drift

If a feature's goals contradict the product definition, or need behaviour it does not describe,
that is product-definition drift. Record it in spec.md under a 'Product definition drift' note
naming the artifacts involved, with the marker
`<!-- pdac-drift ids="<ID>[, <ID>...]" summary="<one line>" -->` on its own line so
`npx prodshape drift --provider speckit` can list it. The decision is human: propose a Product
Change or adjust the feature. Never fix drift quietly, drop or weaken a citation to hide it, or
write around the conflict. Spec Kit never edits docs/product/model: the accepted definition
changes only through a Product Change under docs/product/changes/.

## The Product Grounding sections

The integration merges a managed "Product Grounding (PDaC)" section into this workspace's spec,
plan and tasks templates, so every generated document carries it. Fill it: replace its
placeholder with the citations the document depends on, or (a human decision only) with the
exemption declaration. Never delete the section without doing one of the two; a gated document
with neither is unclassified and fails verification.

## Before finishing a feature

Run `npx prodshape citations verify --provider speckit` and fix every stale, tampered or unresolved citation, every
unclassified document and every bound document without citations. Refresh a stale digest by
re-running `npx prodshape inspect <ID>`; never delete a citation or declare `pdac-scope: none`
to silence a diagnostic.
