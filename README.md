# Apex.Runtime

Various runtime information helpers for .NET

[![Build Status](https://numenfall.visualstudio.com/Libraries/_apis/build/status/dbolin.Apex.Runtime?branchName=master)](https://numenfall.visualstudio.com/Libraries/_build/latest?definitionId=10&branchName=master) [![Tests](https://img.shields.io/azure-devops/tests/numenfall/Libraries/10.svg?compact_message)](https://numenfall.visualstudio.com/Libraries/_build/latest?definitionId=10&branchName=master)
[![Code Coverage](https://img.shields.io/azure-devops/coverage/numenfall/Libraries/10/master.svg)](https://numenfall.visualstudio.com/Libraries/_build/latest?definitionId=10&branchName=master)

## Memory

### Usage

```csharp
var obj = ...
var m = new Memory(graph: true);
var size = m.SizeOf(obj); // size of obj and all objects reachable from obj in bytes
```

For performance, pool Memory instances for re-use.
