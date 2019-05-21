# Apex.Runtime

Various runtime information helpers for .NET

## Memory

### Usage

```csharp
var obj = ...
var m = new Memory(graph: true);
var size = m.SizeOf(obj); // size of obj and all objects reachable from obj in bytes
```

For performance, pool Memory instances for re-use.
