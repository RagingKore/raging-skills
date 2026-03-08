# Domain-Driven Design: Books, Articles & Resources

A curated reading list organized by topic and expertise level.

---

## Foundational Books

### The Essential Two

**Domain-Driven Design: Tackling Complexity in the Heart of Software** (2003)
*Eric Evans* - The "Blue Book"
- The original DDD text that started it all
- Deep coverage of strategic and tactical patterns
- Dense but essential reading
- Best for: Understanding the *why* behind DDD
- ISBN: 978-0321125217

**Implementing Domain-Driven Design** (2013)
*Vaughn Vernon* - The "Red Book"
- Practical implementation guide
- Covers bounded contexts, aggregates, domain events in depth
- Includes code examples (Java)
- Best for: Translating DDD theory into code
- ISBN: 978-0321834577

### Modern Essentials

**Learning Domain-Driven Design** (2021)
*Vlad Khononov*
- Most accessible modern introduction
- Excellent coverage of strategic patterns
- Connects DDD to microservices and event-driven architecture
- Best for: Teams new to DDD, modern architectural contexts
- ISBN: 978-1098100131

**Domain-Driven Design Distilled** (2016)
*Vaughn Vernon*
- Compact introduction to core DDD concepts
- Quick read (~170 pages)
- Good for getting buy-in from stakeholders
- Best for: Busy developers, managers, quick overview
- ISBN: 978-0134434421

---

## Event Sourcing & CQRS

**Versioning in an Event Sourced System** (2016)
*Greg Young* - Free eBook
- Definitive guide to event versioning
- Handling schema evolution over time
- Upcasting, weak vs strong schemas
- Link: https://leanpub.com/esversioning

**Event Sourcing** (2024)
*Martin Dilger*
- Modern comprehensive guide
- Practical patterns and anti-patterns
- ISBN: 978-1484296776

**CQRS Documents** (2010)
*Greg Young* - Free PDF
- Original CQRS documentation
- Task-based UIs, event sourcing fundamentals
- Link: https://cqrs.files.wordpress.com/2010/11/cqrs_documents.pdf

**Exploring CQRS and Event Sourcing** (2012)
*Microsoft patterns & practices*
- Detailed reference implementation (CQRS Journey)
- Conference management system example
- Link: https://docs.microsoft.com/en-us/previous-versions/msp-n-p/jj554200(v=pandp.10)

---

## Functional Domain Modeling

**Domain Modeling Made Functional** (2018)
*Scott Wlaschin*
- Functional DDD with F#
- Type-driven design, making illegal states unrepresentable
- Excellent coverage of Decider-like patterns
- Highly influential even for OOP developers
- Best for: Functional programmers, anyone interested in type-safe modeling
- ISBN: 978-1680502541

**Functional and Reactive Domain Modeling** (2016)
*Debasish Ghosh*
- DDD with Scala and functional patterns
- Algebraic data types, monads for domain logic
- ISBN: 978-1617292248

### Key Articles on Decider Pattern

**The Decider Pattern**
*Jérémie Chassaing*
- Original formulation of the Decider pattern
- Link: https://thinkbeforecoding.com/post/2021/12/17/functional-event-sourcing-decider

**Decider: Event Sourcing Made Simple**
*Oskar Dudycz*
- Practical implementation guide
- C# examples
- Link: https://event-driven.io/en/how_to_effectively_compose_your_business_logic/

---

## Actor Model & Distributed Systems

**Reactive Messaging Patterns with the Actor Model** (2017)
*Vaughn Vernon*
- Comprehensive actor patterns
- Akka-based examples applicable to any actor framework
- Integrates well with DDD concepts
- ISBN: 978-0133846836

**Akka in Action** (2nd Edition, 2023)
*Francisco Lopez-Sancho Abraham*
- Practical Akka/Pekko guide
- Event sourcing with persistent actors
- Cluster sharding for distributed aggregates
- ISBN: 978-1617299216

**Microsoft Orleans Documentation**
- Virtual actor model
- Grain persistence patterns
- Link: https://learn.microsoft.com/en-us/dotnet/orleans/

**Programming Microsoft Azure Service Fabric** (2018)
*Haishi Bai*
- Reliable Actors deep dive
- Distributed state management
- ISBN: 978-1509301881

---

## Microservices & DDD

**Building Microservices** (2nd Edition, 2021)
*Sam Newman*
- Microservices architecture fundamentals
- Chapter on splitting the monolith using DDD
- ISBN: 978-1492034025

**Monolith to Microservices** (2019)
*Sam Newman*
- Practical decomposition strategies
- Uses bounded contexts as decomposition boundaries
- ISBN: 978-1492047841

**Strategic Monoliths and Microservices** (2021)
*Vaughn Vernon, Tomasz Jaskuła*
- When to use monoliths vs microservices
- DDD-based decision framework
- ISBN: 978-0137355464

---

## Event Storming & Collaborative Modeling

**Introducing EventStorming** (2021)
*Alberto Brandolini*
- Definitive guide from the creator
- Big Picture and Design-Level workshops
- Best for: Anyone facilitating domain discovery
- ISBN: 978-1727742541

**The EventStorming Handbook**
*Paul Rayner*
- Practical facilitation guide
- Remote EventStorming techniques
- Link: https://leanpub.com/eventstorming_handbook

**Domain Storytelling** (2022)
*Stefan Hofer, Henning Schwentner*
- Alternative collaborative modeling technique
- Complements EventStorming well
- ISBN: 978-0137458912

---

## Architecture & Patterns

**Patterns of Enterprise Application Architecture** (2002)
*Martin Fowler*
- Foundational patterns (Repository, Unit of Work, etc.)
- Essential background for DDD implementation
- ISBN: 978-0321127426

**Enterprise Integration Patterns** (2003)
*Gregor Hohpe, Bobby Woolf*
- Messaging patterns essential for event-driven DDD
- Saga, Process Manager patterns
- ISBN: 978-0321200686
- Web companion: https://www.enterpriseintegrationpatterns.com/

**Clean Architecture** (2017)
*Robert C. Martin*
- Dependency inversion, architectural boundaries
- Complements DDD's layered architecture
- ISBN: 978-0134494166

**Software Architecture: The Hard Parts** (2021)
*Neal Ford, Mark Richards, Pramod Sadalage, Zhamak Dehghani*
- Modern architectural trade-offs
- Data decomposition patterns
- ISBN: 978-1492086895

---

## Online Resources

### Blogs & Websites

**Vaughn Vernon's Blog**
https://kalele.io/
- Regular DDD insights from the author

**Event-Driven.io (Oskar Dudycz)**
https://event-driven.io/
- Excellent practical articles
- Event sourcing in .NET
- Marten library author

**CodeOpinion (Derek Comartin)**
https://codeopinion.com/
- Video and written content
- CQRS, Event Sourcing, DDD patterns
- YouTube: https://www.youtube.com/c/CodeOpinion

**ThinkBeforeCoding (Jérémie Chassaing)**
https://thinkbeforecoding.com/
- Functional DDD, Decider pattern origin
- F# examples

**Scott Wlaschin (F# for Fun and Profit)**
https://fsharpforfunandprofit.com/
- Functional domain modeling
- Type-driven design

**Martin Fowler's Bliki**
https://martinfowler.com/
- Foundational pattern definitions
- Architecture articles

### Video Content

**Explore DDD Conference**
https://www.youtube.com/@ExploreDDD
- Annual conference recordings
- Strategic and tactical DDD talks

**Domain-Driven Design Europe**
https://www.youtube.com/@daborin
- European DDD conference
- Advanced topics, case studies

**Virtual DDD Community**
https://virtualddd.com/
- Monthly meetups
- Collaborative sessions

**NDC Conferences**
https://www.youtube.com/@NDC
- Many DDD-related talks
- Search for speakers: Udi Dahan, Greg Young, Jimmy Bogard

### GitHub Repositories

**EventStore Samples**
https://github.com/EventStore/samples
- Official KurrentDB examples
- Multiple languages

**Marten (PostgreSQL Document DB + Event Store)**
https://github.com/JasperFx/marten
- .NET event sourcing
- Excellent documentation

**Eventuous**
https://github.com/Eventuous/eventuous
- Modern .NET event sourcing framework
- Clean Decider implementation

**Equinox**
https://github.com/jet/equinox
- F# event sourcing
- Functional patterns

**Event Sourcing in Python (esdbclient)**
https://github.com/pyeventsourcing/esdbclient
- Python KurrentDB client
- Part of larger eventsourcing ecosystem

---

## Classic Articles

### Must-Read Online Articles

**Effective Aggregate Design (3-part series)**
*Vaughn Vernon*
- Part 1: Modeling a Single Aggregate
- Part 2: Making Aggregates Work Together
- Part 3: Gaining Insight Through Discovery
- Link: https://www.dddcommunity.org/library/vernon_2011/

**How to Define Bounded Contexts**
*Martin Fowler*
- Practical bounded context identification
- Link: https://martinfowler.com/bliki/BoundedContext.html

**Domain Events**
*Martin Fowler*
- Original domain events description
- Link: https://martinfowler.com/eBooks/patterns-of-enterprise-application-architecture.html

**The Aggregate Pattern**
*Martin Fowler*
- Concise aggregate definition
- Link: https://martinfowler.com/bliki/DDD_Aggregate.html

**StranglerFigApplication**
*Martin Fowler*
- Incremental migration strategy
- Essential for legacy modernization
- Link: https://martinfowler.com/bliki/StranglerFigApplication.html

**CQRS**
*Martin Fowler*
- Clear CQRS explanation
- When to use (and when not to)
- Link: https://martinfowler.com/bliki/CQRS.html

---

## Recommended Reading Order

### For Beginners
1. **Domain-Driven Design Distilled** - Quick overview
2. **Learning Domain-Driven Design** - Modern, accessible
3. **Effective Aggregate Design** articles - Practical modeling
4. **Domain-Driven Design** (Blue Book) - Depth and context

### For Functional Programmers
1. **Domain Modeling Made Functional** - F# approach
2. **The Decider Pattern** article - Core pattern
3. **Functional and Reactive Domain Modeling** - Advanced
4. **Equinox repository** - Real-world F# examples

### For Event Sourcing Focus
1. **CQRS Documents** (Greg Young) - Foundations
2. **Versioning in an Event Sourced System** - Essential for production
3. **Event-Driven.io blog** - Practical .NET examples
4. **KurrentDB documentation** - Implementation details

### For Distributed Systems / Actors
1. **Reactive Messaging Patterns** - Actor patterns
2. **Akka in Action** or **Orleans documentation** - Framework specifics
3. **Building Microservices** - Context mapping at scale
4. **Enterprise Integration Patterns** - Messaging foundations

### For Collaborative Modeling
1. **Introducing EventStorming** - Workshop facilitation
2. **Domain Storytelling** - Alternative technique
3. **The EventStorming Handbook** - Practical tips
4. **Virtual DDD meetups** - See it in practice

---

## Community & Learning

### Communities
- **DDD Community**: https://www.dddcommunity.org/
- **Virtual DDD**: https://virtualddd.com/
- **DDD Slack**: Various regional Slack workspaces
- **Reddit r/DomainDrivenDesign**: https://www.reddit.com/r/DomainDrivenDesign/

### Courses
- **Pluralsight**: Multiple DDD courses by Julie Lerman, Vladimir Khorikov
- **Dometrain**: Modern .NET courses including DDD
- **NDC Workshops**: Hands-on DDD workshops

### Conferences
- **Explore DDD** (USA)
- **DDD Europe** (Amsterdam)
- **KanDDDinsky** (Berlin)
- **µCon** (London) - Microservices with DDD focus

---

## Quick Reference: When to Read What

| Situation | Recommended Reading |
|-----------|---------------------|
| New to DDD | Learning DDD → DDD Distilled |
| Struggling with aggregates | Effective Aggregate Design articles |
| Implementing event sourcing | CQRS Documents → Versioning in ES |
| Functional approach | Domain Modeling Made Functional |
| Microservices decomposition | Learning DDD → Building Microservices |
| Facilitating workshops | Introducing EventStorming |
| Actor-based systems | Reactive Messaging Patterns |
| Legacy modernization | Monolith to Microservices |
| Advanced patterns | Blue Book → Red Book |
