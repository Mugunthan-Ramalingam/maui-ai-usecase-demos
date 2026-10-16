# .NET MAUI Use Case Application - PR Review

## Review Mindset

Review this sample as a customer-facing showcase for Syncfusion controls.

Ask yourself:

> If a customer opens this sample for the first time, will they clearly understand the value, capabilities, and implementation of the showcased control?

Focus only on information that can be verified from:
- Source code
- XAML
- Project structure
- Configuration files
- README/documentation
- PR changes

Do NOT speculate about:
- Visual polish
- UI aesthetics
- Responsiveness
- Runtime behavior
- Performance characteristics

Unless evidence is available in the PR.

---

## 1. Use Case Scope

### Verify
- Sample remains focused on the intended Syncfusion control(s).
- Features directly support the use case.
- Scenario is realistic and easy to understand.
- Data is local/mock and self-contained.

### Flag
- Login/authentication
- User accounts
- Cloud sync
- Backend services
- Settings/onboarding pages
- Unrelated navigation flows
- Feature creep

---

## 2. MVVM & Architecture

### Verify
- Proper MVVM implementation.
- Clear View / ViewModel separation.
- Business logic resides in ViewModels.
- Commands are used appropriately.
- ViewModels are scoped to the sample.

### Flag
- Business logic in code-behind.
- Excessive event handling.
- App-wide shared state.
- Unnecessary DI frameworks.
- Over-engineered solutions.

---

## 3. Control Showcase Effectiveness

### Verify
- Syncfusion control is the primary focus.
- Control capabilities are demonstrated meaningfully.
- Sample helps customers understand usage patterns.
- Control integration is easy to follow from the code.

### Flag
- Control used only decoratively.
- Scenario hides the control's value.
- Important control features not demonstrated.
- Complexity outweighs learning value.

---

## 4. AI Functionality (AI Samples Only)

### Verify
- End-to-end AI flow is implemented.
- Request/response handling exists.
- Error handling is implemented.
- Loading/progress state handling exists.

### Flag
- Stub implementations.
- Placeholder AI logic.
- TODO-based workflows.
- Partially completed features.

---

## 5. Security Review

### Verify
- No secrets committed.
- No API keys committed.
- No access tokens committed.
- Placeholder pattern used where required.

Expected format:

// TODO: Add your API key before running
const string ApiKey = "YOUR_API_KEY_HERE";

### Flag (Critical)
- Hardcoded API keys.
- Credentials in source code.
- Secrets in configuration files.
- Sensitive information in repository history.

---

## 6. Repository Hygiene

### Verify
- No unused files.
- No dead code.
- No commented-out implementations.
- No temporary test assets.
- No template leftovers.

### Flag
- Duplicate code.
- Obsolete classes.
- Unused resources.
- Experimental files committed.

---

## 7. Documentation

### Verify README contains:
- Sample overview.
- Controls demonstrated.
- Setup instructions.
- AI setup steps (if applicable).
- Run instructions.

### Flag
- Missing README.
- Missing setup instructions.
- Incomplete or outdated documentation.

---

## 8. Definition of Done

Verify evidence exists that:

- Sample is complete.
- All advertised features are implemented.
- No unfinished TODO functionality.
- No secrets exist in repository.
- Documentation is complete.

If evidence is unavailable, state:

> Unable to verify from PR.

Do not assume.

---

# Review Output

## Verdict
✅ Approve

⚠️ Approve with Changes

❌ Reject

---

## Critical Issues
- Item

## Major Issues
- Item

## Minor Issues
- Item

---

## Summary

### Scope
✅ Pass / ⚠️ Needs Improvement / ❌ Fail

### MVVM & Architecture
✅ Pass / ⚠️ Needs Improvement / ❌ Fail

### Control Showcase
✅ Pass / ⚠️ Needs Improvement / ❌ Fail

### AI Functionality
✅ Pass / ⚠️ Needs Improvement / ❌ Fail / N/A

### Security
✅ Pass / ⚠️ Needs Improvement / ❌ Fail

### Documentation
✅ Pass / ⚠️ Needs Improvement / ❌ Fail

---

## Final Recommendation

Provide only actionable findings.

Avoid generic comments.

If something cannot be verified from the PR, explicitly state:

**Unable to verify from PR.**
