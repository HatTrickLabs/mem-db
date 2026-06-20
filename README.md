# HatTrick.MemDb

[![NuGet](https://img.shields.io/nuget/v/HatTrick.MemDb.svg)](https://www.nuget.org/packages/HatTrick.MemDb/)
[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)

An embedded database engine for .NET applications that need persistent, queryable storage without standing up a database server.

**[Full documentation](https://hattricklabs.com/docs/memdb/)** | **[hattricklabs.com](https://hattricklabs.com)**

---

## Installation

```bash
dotnet add package HatTrick.MemDb
```

The package is `HatTrick.MemDb`; the namespace is `HatTrick.Data`:

```csharp
using HatTrick.Data;
```

## Quick Start

```csharp
using System;
using HatTrick.Data;

public class Person
{
    public long Id { get; set; }
    public string FirstName { get; set; }
    public string MiddleName { get; set; }
    public string LastName { get; set; }
    public DateTime BirthDate { get; set; }
}

class Program
{
    static void Main()
    {
        // Configure — once at startup, before any Open calls.
        MemDb.ConfigureFor<Person>("people", @"C:\data\people")
             .Register();

        // Open — dispose flushes pending writes and releases file handles.
        using (var db = MemDb.Open<Person>("people"))
        {
            var person = new Person
            {
                FirstName = "Eric",
                LastName = "Cartman",
                BirthDate = new DateTime(2014, 5, 13)
            };

            // Insert — id auto-assigned and captured via callback.
            db.Insert(person, id => person.Id = id);

            // Update — by id.
            db.Update(p => p.MiddleName = "Theodore", person.Id);

            // Find — returns a deep clone, never a cache reference.
            var found = db.Find(person.Id);
            Console.WriteLine($"{found.FirstName} {found.MiddleName} {found.LastName}");
        }
    }
}
```

```
Eric Theodore Cartman
```

---

## Why MemDb

- **In-memory with file-backed persistence** — reads from RAM; writes appended to disk on a configurable flush interval and on dispose.
- **Native C# queries** — filtering, sorting, aggregation, and query-scoped writes via a fluent query builder. No SQL.
- **In-process thread safety** — all operations within a process are safe without additional locking.
- **Record-level encryption (AES-256)**, online snapshots, and archive/restore to any historical timestamp.

See the [full documentation](https://hattricklabs.com/docs/memdb/) for configuration, queries, indexes, encryption, snapshots, and tuning.

---

## License

Apache-2.0 — see [LICENSE](LICENSE).
