# BENCHMARKS

#### 2026-08-01 (v1.0 baseline)

- BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
- Intel Core i7-4790K CPU 4.00GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
- .NET SDK 10.0.302
  - [Host]   : .NET 10.0.10, X64 RyuJIT x86-64-v3
  - .NET 10  : .NET 10.0.10, X64 RyuJIT x86-64-v3

First baseline over the Phase 1 surface, mocked HTTP throughout (zero API cost). `DeserializeLocationsPage` is raw converter throughput over the full 200-record `/locations/latest` fixture (~93 KB); `ListCursorPageAsync` is the same page through the whole pipeline (mock HTTP, buffering, map) with the fresh-poll gate bypassed via a cursor; `ListGateHitAsync` is the gate cache-hit path — lookup plus the `FromCache` response clone, no HTTP — at ~398 ns / 1.2 KB it makes polling inside the 60 s floor effectively free. Vehicles `GetAsync` is the ungated bare-object endpoint on a small payload.

###### Locations

| Method                   |       Mean |    Error |   StdDev | Allocated |
| ------------------------ | ---------: | -------: | -------: | --------: |
| DeserializeLocationsPage | 296.747 us | 3.689 us | 3.270 us |   83.4 KB |
| ListCursorPageAsync      | 425.463 us | 3.852 us | 3.415 us |  366.4 KB |
| ListGateHitAsync         | 397.649 ns | 1.420 ns | 1.185 ns |    1.2 KB |

###### Vehicles

| Method   |     Mean |     Error |    StdDev | Allocated |
| -------- | -------: | --------: | --------: | --------: |
| GetAsync | 3.267 us | 15.078 ns | 14.104 ns |    4.6 KB |
