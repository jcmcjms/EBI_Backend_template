# Performance and Scalability Rules

Target low latency, predictable throughput, efficient resource usage, and safe horizontal scaling.

Mandatory:
- asynchronous I/O
- bounded collections
- pagination for potentially large result sets
- cancellation propagation
- projection of required data only
- no accidental N+1 queries
- no unbounded fan-out
- no per-request `new HttpClient()`
- no blocking waits

Optimize in this order:
1. unnecessary I/O
2. database queries
3. network round trips
4. payload/serialization
5. allocations
6. CPU micro-optimizations

Parallelize only independent work, with bounded concurrency and cancellation. Never share a `DbContext` across concurrent operations.

Caching requires an explicit key, TTL, invalidation strategy, authorization/tenant dimensions where applicable, and multi-instance behavior.

Long-running work belongs in explicit background processing.

Performance claims require evidence: benchmark, profiler, query plan, metrics, trace, or load test.

~300 lines is a review threshold for production classes. Split by responsibility, not by line count.
