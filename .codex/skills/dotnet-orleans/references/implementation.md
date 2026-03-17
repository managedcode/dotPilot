# Runtime Internals, Testing, and Deeper Reference

Use this reference when architecture decisions depend on Orleans internals, testing behavior, or the broader official resource set.

## Streaming

| Need | Official Source | What It Covers |
|---|---|---|
| Start with Orleans streams | [Streaming overview](https://learn.microsoft.com/dotnet/orleans/streaming/) | Stream model and key concepts |
| Build the smallest stream sample | [Streams quick start](https://learn.microsoft.com/dotnet/orleans/streaming/streams-quick-start) | End-to-end stream wiring |
| Decide when streams fit | [Why streams?](https://learn.microsoft.com/dotnet/orleans/streaming/streams-why) | Stream use cases and tradeoffs |
| Use broadcast channels | [Broadcast channels](https://learn.microsoft.com/dotnet/orleans/streaming/broadcast-channel) | Broadcast-oriented messaging |
| Learn the APIs | [Streams APIs](https://learn.microsoft.com/dotnet/orleans/streaming/streams-programming-apis) | Producer, consumer, and subscription APIs |
| Pick stream backends | [Stream providers](https://learn.microsoft.com/dotnet/orleans/streaming/stream-providers) | Available stream-provider shapes |

## Implementation Details

| Need | Official Source | What It Covers |
|---|---|---|
| Start from runtime internals | [Implementation overview](https://learn.microsoft.com/dotnet/orleans/implementation/) | Internal architecture entry point |
| Understand the grain directory internals | [Implementation grain directory](https://learn.microsoft.com/dotnet/orleans/implementation/grain-directory) | Internal directory behavior |
| Understand runtime lifecycles | [Orleans lifecycle](https://learn.microsoft.com/dotnet/orleans/implementation/orleans-lifecycle) | Internal lifecycle model |
| Check delivery semantics | [Messaging delivery guarantees](https://learn.microsoft.com/dotnet/orleans/implementation/messaging-delivery-guarantees) | Delivery guarantees and retry assumptions |
| Understand scheduler behavior | [Scheduler](https://learn.microsoft.com/dotnet/orleans/implementation/scheduler) | Runtime scheduling model |
| Review cluster membership behavior | [Cluster management](https://learn.microsoft.com/dotnet/orleans/implementation/cluster-management) | Membership and cluster coordination internals |
| Inspect stream implementation internals | [Streams implementation overview](https://learn.microsoft.com/dotnet/orleans/implementation/streams-implementation/) | Internal queueing and stream runtime model |
| Use Azure Queue stream internals | [Azure Queue streams implementation](https://learn.microsoft.com/dotnet/orleans/implementation/streams-implementation/azure-queue-streams) | Azure Queue specific stream implementation details |
| Understand load distribution | [Load balancing](https://learn.microsoft.com/dotnet/orleans/implementation/load-balancing) | Runtime balancing and redistribution |
| Test Orleans systems | [Unit testing](https://learn.microsoft.com/dotnet/orleans/implementation/testing) | Test-cluster patterns and testing guidance |

## Tutorials, Samples, and Resource Pages

| Need | Official Source | What It Covers |
|---|---|---|
| Browse official tutorials and samples | [Code samples overview](https://learn.microsoft.com/dotnet/orleans/tutorials-and-samples/) | Tutorials and sample entry points |
| Start from Hello World | [Hello World tutorial](https://learn.microsoft.com/dotnet/orleans/tutorials-and-samples/overview-helloworld) | Smallest tutorial walkthrough |
| Walk through Orleans basics | [Orleans basics tutorial](https://learn.microsoft.com/dotnet/orleans/tutorials-and-samples/tutorial-1) | Guided Orleans introduction |
| Inspect the adventure sample | [Adventure game sample](https://learn.microsoft.com/dotnet/orleans/tutorials-and-samples/adventure) | Larger sample walkthrough |
| Build custom storage | [Custom grain storage sample](https://learn.microsoft.com/dotnet/orleans/tutorials-and-samples/custom-grain-storage) | Custom storage provider extension point |
| Read design guidance | [Design principles](https://learn.microsoft.com/dotnet/orleans/resources/orleans-architecture-principles-and-approach) | Orleans architectural principles |
| Decide if Orleans fits | [Applicability](https://learn.microsoft.com/dotnet/orleans/resources/orleans-thinking-big-and-small) | When Orleans is and is not a fit |
| Review NuGet package map | [NuGet packages](https://learn.microsoft.com/dotnet/orleans/resources/nuget-packages) | Package inventory and purpose |
| Re-check best practices | [Best practices](https://learn.microsoft.com/dotnet/orleans/resources/best-practices) | Operational and design guidance |
| Look up common questions | [Frequently asked questions](https://learn.microsoft.com/dotnet/orleans/resources/frequently-asked-questions) | FAQ and clarifications |
| Find more external references | [External links](https://learn.microsoft.com/dotnet/orleans/resources/links) | Talks, repos, and related material |
| Review community/student material | [Student projects](https://learn.microsoft.com/dotnet/orleans/resources/student-projects) | Community and educational references |

## API Reference and Source Entry Points

| Need | Official Source | What It Covers |
|---|---|---|
| Core API docs | [Orleans.Core API reference](https://learn.microsoft.com/dotnet/api/orleans.core) | Main Orleans core API surface |
| Runtime API docs | [Orleans.Runtime API reference](https://learn.microsoft.com/dotnet/api/orleans.runtime) | Runtime-specific types |
| Streams API docs | [Orleans.Streams API reference](https://learn.microsoft.com/dotnet/api/orleans.streams) | Streams types and contracts |
| Browse the repo | [dotnet/orleans](https://github.com/dotnet/orleans) | Source, releases, issues, discussions |
| Browse official samples | [dotnet/samples Orleans folder](https://github.com/dotnet/samples/tree/main/orleans) | Current official source samples |
| Check the repo sample index | [Orleans samples README](https://github.com/dotnet/orleans/blob/main/samples/README.md) | Sample map and source entry points |

## Usage Guidance

- Start here when the problem depends on scheduler rules, runtime delivery guarantees, stream internals, or test-cluster behavior.
- Use [examples.md](examples.md) when you want example-first navigation instead of internals.
- Use [official-docs-index.md](official-docs-index.md) when you need the full documentation tree in one place.
