# User Settings Page - Wireframe

```
+------------------------------------------------------------------+
|  Settings                                                        |
+------------------------------------------------------------------+
|              |                                                    |
|  NAVIGATION  |  MAIN CONTENT                                     |
|              |                                                    |
|  +--------+  |  Profile                                           |
|  |        |  |  ─────────────────────────────────────────         |
|  | > Profile |  |                                                    |
|  |        |  |  +------------+                                    |
|  | Security| |  |            |                                    |
|  |        |  |  |   Avatar   |                                    |
|  | Notifi- | |  |  (image)   |                                    |
|  | cations | |  |            |                                    |
|  |        |  |  +------------+                                    |
|  +--------+  |  [ Upload Photo ]                                  |
|              |                                                    |
|              |  Name                                              |
|              |  +------------------------------------------+      |
|              |  |                                          |      |
|              |  +------------------------------------------+      |
|              |                                                    |
|              |  Email                                             |
|              |  +------------------------------------------+      |
|              |  |                                          |      |
|              |  +------------------------------------------+      |
|              |                                                    |
|              |                          +------------------+      |
|              |                          |   Save Changes   |      |
|              |                          +------------------+      |
|              |                                                    |
+------------------------------------------------------------------+
```

## Layout Description

### Sidebar Navigation (left, ~200px wide)
- Vertical list of navigation items
- Three sections: **Profile**, **Security**, **Notifications**
- Active item (Profile) is visually highlighted with a left border accent or background color
- Fixed position; does not scroll with main content

### Main Content Area (right, remaining width)

#### Section Header
- Title: "Profile"
- Horizontal divider beneath the title

#### Avatar
- Square or circular placeholder (~100x100px)
- Displays a generic user silhouette icon when no image is uploaded
- "Upload Photo" text link or button below the placeholder

#### Form Fields
- **Name** - single-line text input, full width of the content area
- **Email** - single-line text input, full width of the content area
- Each field has a label above the input

#### Actions
- **Save Changes** - primary action button, right-aligned at the bottom of the form
