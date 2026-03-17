# Color Palette Reference

How the color system works, when to use each palette, and design rules. All actual color values live in the
project's `color-palette.yml` file (see `color-palette-template.yml` for the default template).

---

## Two Color Systems

The palette provides two complementary systems. Choose based on diagram type.

### Semantic Colors (`semantic`)

Color by the **role** an element plays: start, decision, error, success, etc. Use for workflows, concepts,
tutorials, and any diagram where meaning matters more than component type.

### Component-Type Colors (`components`)

Color by **what** the component is: database, API, cache, queue, etc. Use for architecture diagrams where you
want to distinguish infrastructure types at a glance.

**You can mix both** in the same diagram — e.g., semantic colors for the flow logic and component-type colors
for the infrastructure boxes.

---

## Rules

- **Always pair a darker stroke with a lighter fill** for contrast
- **Do not invent new colors** — if a concept does not fit an existing category, use `primary` or `secondary`
  from the semantic palette
- **Arrows** use the stroke color of their source element
- **Structural lines** (dividers, tree trunks, timelines) use the `strokes.structural` color
- **Marker dots** use the `strokes.marker_dots` color for both fill and stroke

---

## Text Hierarchy

Use text color to create visual hierarchy without containers. A 28px title in the `title` color does not need
a rectangle around it.

| Level          | YAML key          | Use For                             |
|----------------|--------------------|-------------------------------------|
| Title          | `text.title`       | Section headings, major labels      |
| Subtitle       | `text.subtitle`    | Subheadings, secondary labels       |
| Body/Detail    | `text.body`        | Descriptions, annotations, metadata |
| On light fills | `text.on_light_fill` | Text inside light-colored shapes  |
| On dark fills  | `text.on_dark_fill`  | Text inside dark-colored shapes   |

---

## Evidence Artifacts

Technical diagrams include concrete evidence (code snippets, JSON examples, real data). These use a dark
background (`evidence.background`) with colored text:

- **Code snippets**: syntax-colored text appropriate to the language
- **JSON/data examples**: green text (`evidence.json_text`)

---

## Cloud-Specific Palettes (Reference Only)

When diagramming cloud infrastructure, override the component-type colors with the provider's official colors.
These are not in the YAML template since they are standard and not meant to be customized.

### AWS

| Service Category            | Fill      | Stroke    |
|-----------------------------|-----------|-----------|
| Compute (EC2, Lambda, ECS)  | `#ff9900` | `#cc7a00` |
| Storage (S3, EBS)           | `#3f8624` | `#2d6119` |
| Database (RDS, DynamoDB)    | `#3b48cc` | `#2d3899` |
| Networking (VPC, Route53)   | `#8c4fff` | `#6b3dcc` |
| Security (IAM, KMS)         | `#dd344c` | `#b12a3d` |
| Analytics (Kinesis, Athena) | `#8c4fff` | `#6b3dcc` |
| ML (SageMaker, Bedrock)     | `#01a88d` | `#017d69` |

### Azure

| Service Category | Fill      | Stroke    |
|------------------|-----------|-----------|
| Compute          | `#0078d4` | `#005a9e` |
| Storage          | `#50e6ff` | `#3cb5cc` |
| Database         | `#0078d4` | `#005a9e` |
| Networking       | `#773adc` | `#5a2ca8` |
| Security         | `#ff8c00` | `#cc7000` |
| AI/ML            | `#50e6ff` | `#3cb5cc` |

### GCP

| Service Category                | Fill      | Stroke    |
|---------------------------------|-----------|-----------|
| Compute (GCE, Cloud Run)        | `#4285f4` | `#3367d6` |
| Storage (GCS)                   | `#34a853` | `#2d8e47` |
| Database (Cloud SQL, Firestore) | `#ea4335` | `#c53929` |
| Networking                      | `#fbbc04` | `#d99e04` |
| AI/ML (Vertex AI)               | `#9334e6` | `#7627b8` |

### Kubernetes

| Component        | Fill      | Stroke             |
|------------------|-----------|---------------------|
| Pod              | `#326ce5` | `#2756b8`          |
| Service          | `#326ce5` | `#2756b8`          |
| Deployment       | `#326ce5` | `#2756b8`          |
| ConfigMap/Secret | `#7f8c8d` | `#626d6e`          |
| Ingress          | `#00d4aa` | `#00a888`          |
| Node             | `#303030` | `#1a1a1a`          |
| Namespace        | `#f0f0f0` | `#c0c0c0` (dashed) |
