# Real-World Diagram Examples

Complete, tested Mermaid syntax examples for every diagram type supported by beautiful-mermaid.

## Flowchart Examples

### CI/CD Pipeline

```mermaid
graph TD
  subgraph ci [CI Pipeline]
    A[Push Code] --> B{Tests Pass?}
    B -->|Yes| C[Build Image]
    B -->|No| D[Fix & Retry]
    D -.-> A
  end
  C --> E([Deploy Staging])
  E --> F{QA Approved?}
  F -->|Yes| G((Production))
  F -->|No| D
```

### Microservices Architecture

```mermaid
graph LR
  subgraph clients [Client Layer]
    A([Web App]) --> B[API Gateway]
    C([Mobile App]) --> B
  end
  subgraph services [Service Layer]
    B --> D[Auth Service]
    B --> E[User Service]
    B --> F[Order Service]
  end
  subgraph data [Data Layer]
    D --> G[(Auth DB)]
    E --> H[(User DB)]
    F --> I[(Order DB)]
    F --> J([Message Queue])
  end
```

### Decision Tree

```mermaid
graph TD
  A{Is it raining?} -->|Yes| B{Have umbrella?}
  A -->|No| C([Go outside])
  B -->|Yes| D([Go with umbrella])
  B -->|No| E{Is it heavy?}
  E -->|Yes| F([Stay inside])
  E -->|No| G([Run for it])
```

### All 12 Shapes

```mermaid
graph LR
  A[Rectangle] --> B(Rounded)
  B --> C{Diamond}
  C --> D([Stadium])
  D --> E((Circle))
  E --> F[[Subroutine]]
  F --> G(((Double Circle)))
  G --> H{{Hexagon}}
  H --> I[(Database)]
  I --> J>Flag]
  J --> K[/Trapezoid\]
  K --> L[\Inverse Trap/]
```

### All Edge Styles

```mermaid
graph TD
  A[Source] -->|solid arrow| B[Target 1]
  A -.->|dotted arrow| C[Target 2]
  A ==>|thick arrow| D[Target 3]
  E[Node] ---|no arrow solid| F[Node]
  E -.- G[Node]
  E === H[Node]
  I[Client] <-->|bidirectional| J[Server]
  J <-.->|heartbeat| K[Monitor]
  K <==>|data| L[Storage]
```

### Subgraph with Direction Override

```mermaid
graph TD
  subgraph pipeline [Processing Pipeline]
    direction LR
    A[Input] --> B[Parse] --> C[Transform] --> D[Output]
  end
  E[Source] --> A
  D --> F[Sink]
```

### Parallel Edges with &

```mermaid
graph TD
  A[Input] & B[Config] --> C[Processor]
  C --> D[Output] & E[Log]
```

## State Diagram Examples

### Connection Lifecycle

```mermaid
stateDiagram-v2
  [*] --> Closed
  Closed --> Connecting : connect
  Connecting --> Connected : success
  Connecting --> Closed : timeout
  Connected --> Disconnecting : close
  Connected --> Reconnecting : error
  Reconnecting --> Connected : success
  Reconnecting --> Closed : max_retries
  Disconnecting --> Closed : done
  Closed --> [*]
```

### Composite States

```mermaid
stateDiagram-v2
  [*] --> Idle
  Idle --> Processing : submit
  state Processing {
    parse --> validate
    validate --> execute
  }
  Processing --> Complete : done
  Processing --> Error : fail
  Error --> Idle : retry
  Complete --> [*]
```

## Sequence Diagram Examples

### OAuth 2.0 Flow

```mermaid
sequenceDiagram
  actor U as User
  participant App as Client App
  participant Auth as Auth Server
  participant API as Resource API
  U->>App: Click Login
  App->>Auth: Authorization request
  Auth->>U: Login page
  U->>Auth: Credentials
  Auth-->>App: Authorization code
  App->>Auth: Exchange code for token
  Auth-->>App: Access token
  App->>API: Request + token
  API-->>App: Protected resource
  App-->>U: Display data
```

### Database Transaction with Error Handling

```mermaid
sequenceDiagram
  participant C as Client
  participant S as Server
  participant DB as Database
  C->>S: POST /transfer
  S->>DB: BEGIN
  S->>DB: Debit account A
  alt Success
    S->>DB: Credit account B
    S->>DB: INSERT audit_log
    S->>DB: COMMIT
    S-->>C: 200 OK
  else Insufficient funds
    S->>DB: ROLLBACK
    S-->>C: 400 Bad Request
  end
```

### Microservice Orchestration with Parallel Calls

```mermaid
sequenceDiagram
  participant G as Gateway
  participant A as Auth
  participant U as Users
  participant O as Orders
  participant N as Notify
  G->>A: Validate token
  A-->>G: Valid
  par Fetch data
    G->>U: Get user
    U-->>G: User data
  and
    G->>O: Get orders
    O-->>G: Order list
  end
  G->>N: Send notification
  N-->>G: Queued
  Note over G: Aggregate response
```

### All Block Types

```mermaid
sequenceDiagram
  participant C as Client
  participant S as Server
  participant DB as Database

  C->>S: Connect
  loop Every 30s
    C->>S: Heartbeat
    S-->>C: Ack
  end

  C->>S: Login
  alt Valid credentials
    S-->>C: 200 OK
  else Invalid
    S-->>C: 401 Unauthorized
  end

  opt Cache miss
    S->>DB: Query
    DB-->>S: Results
  end

  par Async tasks
    S->>DB: Write log
  and
    S->>C: Push notification
  end

  critical Transaction
    S->>DB: UPDATE accounts
    S->>DB: INSERT log
  end
```

### Notes Positioning

```mermaid
sequenceDiagram
  participant A as Alice
  participant B as Bob
  Note left of A: Alice prepares
  A->>B: Hello
  Note right of B: Bob thinks
  B-->>A: Reply
  Note over A,B: Conversation complete
```

## Class Diagram Examples

### Observer Pattern

```mermaid
classDiagram
  class Subject {
    <<interface>>
    +attach(Observer) void
    +detach(Observer) void
    +notify() void
  }
  class Observer {
    <<interface>>
    +update() void
  }
  class EventEmitter {
    -List~Observer~ observers
    +attach(Observer) void
    +detach(Observer) void
    +notify() void
  }
  class Logger {
    +update() void
  }
  class Alerter {
    +update() void
  }
  Subject <|.. EventEmitter
  Observer <|.. Logger
  Observer <|.. Alerter
  EventEmitter --> Observer
```

### All 6 Relationship Types

```mermaid
classDiagram
  A <|-- B : inheritance
  C *-- D : composition
  E o-- F : aggregation
  G --> H : association
  I ..> J : dependency
  K ..|> L : realization
```

### MVC Architecture

```mermaid
classDiagram
  class Model {
    -data Map
    +getData() Map
    +setData(key, val) void
    +notify() void
  }
  class View {
    -model Model
    +render() void
    +update() void
  }
  class Controller {
    -model Model
    -view View
    +handleInput(event) void
    +updateModel(data) void
  }
  Controller --> Model : updates
  Controller --> View : refreshes
  View --> Model : reads
  Model ..> View : notifies
```

### Visibility and Annotations

```mermaid
classDiagram
  class User {
    +String name
    -String password
    #int internalId
    ~String packageField
    +login() bool
    -hashPassword() String
    #validate() void
  }
  class Serializable {
    <<interface>>
    +serialize() String
    +deserialize(data) void
  }
  class Status {
    <<enumeration>>
    ACTIVE
    INACTIVE
    PENDING
  }
```

## ER Diagram Examples

### E-Commerce Schema

```mermaid
erDiagram
  CUSTOMER {
    int id PK
    string name
    string email UK
  }
  ORDER {
    int id PK
    date created
    int customer_id FK
  }
  PRODUCT {
    int id PK
    string name
    float price
  }
  LINE_ITEM {
    int id PK
    int order_id FK
    int product_id FK
    int quantity
  }
  CUSTOMER ||--o{ ORDER : places
  ORDER ||--|{ LINE_ITEM : contains
  PRODUCT ||--o{ LINE_ITEM : includes
```

### Blog Platform

```mermaid
erDiagram
  USER {
    int id PK
    string username UK
    string email UK
    date joined
  }
  POST {
    int id PK
    string title
    text content
    int author_id FK
    date published
  }
  COMMENT {
    int id PK
    text body
    int post_id FK
    int user_id FK
    date created
  }
  TAG {
    int id PK
    string name UK
  }
  USER ||--o{ POST : writes
  USER ||--o{ COMMENT : authors
  POST ||--o{ COMMENT : has
  POST }|--o{ TAG : tagged-with
```

### All Cardinality Types

```mermaid
erDiagram
  A ||--|| B : one-to-one
  C ||--o{ D : one-to-many
  E |o--|{ F : opt-to-many
  G }|--o{ H : many-to-many
```

### Mixed Identifying and Non-Identifying

```mermaid
erDiagram
  ORDER ||--|{ LINE_ITEM : contains
  ORDER ||..o{ SHIPMENT : ships-via
  PRODUCT ||--o{ LINE_ITEM : includes
  PRODUCT ||..o{ REVIEW : receives
```
