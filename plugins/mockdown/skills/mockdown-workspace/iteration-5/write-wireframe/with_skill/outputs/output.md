# User Settings Page — Wireframe

```text
Settings > Profile

┌──────────────────┬───────────────────────────────────────────────────────────┐
│                  │                                                           │
│  • Profile       │   Profile Settings                                        │
│    Security      │                                                           │
│    Notifications │   ┌──────────┐                                            │
│                  │   │\        /│                                            │
│                  │   │  \    /  │                                            │
│                  │   │  IMG  \  │                                            │
│                  │   │/        \│                                            │
│                  │   └──────────┘                                            │
│                  │   [ Upload Photo ]                                        │
│                  │                                                           │
│                  │   Full Name                                               │
│                  │   [Jane Doe                          ]                    │
│                  │                                                           │
│                  │   Email Address                                           │
│                  │   [jane.doe@example.com              ]                    │
│                  │                                                           │
│                  │                              [ Save Changes ]             │
│                  │                                                           │
└──────────────────┴───────────────────────────────────────────────────────────┘
```

## Components

| Component      | Pattern              | Details                                          |
|----------------|----------------------|--------------------------------------------------|
| Breadcrumb     | `Settings > Profile` | Hierarchical nav trail; "Profile" is current page |
| Split Panel    | `┌──┬──┐ … └──┴──┘`  | Two-column layout: sidebar + main content         |
| List (nav)     | `• Profile`, etc.    | Sidebar navigation; bullet marks the active item  |
| Text (heading) | `Profile Settings`   | Section heading in the main content area          |
| Placeholder    | `IMG` with crosshatch| Avatar image placeholder                          |
| Button         | `[ Upload Photo ]`   | Triggers avatar upload                            |
| Text (label)   | `Full Name`          | Label above name input                            |
| Input          | `[Jane Doe … ]`      | Text input pre-filled with sample name            |
| Text (label)   | `Email Address`      | Label above email input                           |
| Input          | `[jane.doe@… ]`      | Text input pre-filled with sample email           |
| Button         | `[ Save Changes ]`   | Primary action to persist profile edits           |

## Layout Notes

- **Breadcrumb** at the top provides context: the user is on *Settings > Profile*.
- **Split Panel** divides the page into a narrow sidebar (18-column content area) and a wide main area (59-column content area), totaling 80 columns.
- **Sidebar** lists three navigation sections. The bullet (`•`) on "Profile" marks it as the active/selected section. "Security" and "Notifications" are plain text (inactive links).
- **Main area** opens with a "Profile Settings" heading, followed by an avatar image placeholder with a crosshatch pattern and an `[ Upload Photo ]` button beneath it. Two labeled text inputs come next — "Full Name" pre-filled with `Jane Doe` and "Email Address" pre-filled with `jane.doe@example.com`. A right-aligned `[ Save Changes ]` button closes the form.
