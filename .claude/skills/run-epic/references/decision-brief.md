# Decision brief

Written to `log.md` under the story when a decision is needed that the specs and research do not settle. The user reads it in the orchestrator's session through AskUserQuestion, so it must stand alone and be short.

```markdown
### Decision needed: S<id> <short question>

- **Story and state:** S<id>, parked after attempt <n>, fix round <n>. Dependents halted: <ids or none>.
- **What was tried:** <the coder's or reviewers' trail in two or three lines; commits and findings by file:line>.
- **Research:** <what the Explore agent found, with doc and code references>. Facts, not guesses.
- **The question:** <one decision, phrased so each option is a complete answer>.
- **Options:**
  1. <recommended> - <what it means for the code and the specs> - <cost>
  2. <alternative> - <consequence> - <cost>
- **Recommendation:** option <n>, because <the rule or doc it follows>.
- **Impact on other stories:** <plans that change if the answer differs, or none>.
- **Spec change needed:** <doc and section, or none>.
```

After the answer: record it under the brief as `Answer: <option> (<date>)`, update the story plan if the answer changes it (an amendment, logged), set the story back to `ready` and its dependents to `planned`.
