# ForthVM - HVM3 in Forth

A Forth implementation of HVM3 (Interaction Calculus runtime) for high-performance symbolic computing.

## Status

**Phase 1: Setup & Scaffolding - COMPLETE ✅**

- [x] Updated task list with 14 refined tasks
- [x] Cloned HVM3 repository for baseline comparison
- [x] Installed Gforth 0.7.3
- [x] Installed GHC 9.12.2 and built HVM3
- [x] Verified HVM3 baseline (bench_cnots: λa λb a @ 12.7 MIPS)
- [x] Created project structure with 9 modular Forth files
- [x] All modules load successfully

## Project Structure

```
forth_hvm/
├── src/              # Forth source files
│   ├── fvm.fs        # Main entry point
│   ├── errors.fs     # Error handling & debug utils
│   ├── core.fs       # IC term tags & primitives
│   ├── heap.fs       # Heap allocation & GC
│   ├── parse.fs      # Parser for HVM files
│   ├── reduce.fs     # WHNF reduction loop
│   ├── interact.fs   # Interaction rules (INTERS.md)
│   ├── collapse.fs   # Normalization & pretty-print
│   ├── book.fs       # Book loading & linking
│   └── cli.fs        # Command-line interface
├── tests/            # Test scripts
├── hvm3/             # HVM3 baseline for comparison
├── fvm               # Launcher script
└── README.md         # This file
```

## Quick Start

```bash
# Test that modules load
cd src && gforth fvm.fs -e 'TEST-ALL bye'

# Run ForthVM (currently stub)
./fvm
```

## HVM3 Baseline

Tested with `bench_cnots.hvm`:
- Output: `λa λb a`
- Performance: 12.7 MIPS (117M interactions in 9.2s)
- Target: ≥50% of HVM3 performance initially, ≥80% stretch goal

## Next Steps (Task 2: Error Handling)

1. Implement proper error reporting with context
2. Add DEBUG? flag and TRACE utilities
3. Add ASSERT for runtime validation
4. Test error handling with intentional failures

## Task Roadmap

- ✅ Task 0: Setup HVM3 Baseline Environment
- ✅ Task 1: Setup Development Environment
- 🔄 Task 2: Implement Error Handling Infrastructure (NEXT)
- ⏳ Task 3: Implement Core Primitives and Heap Management
- ⏳ Task 4: Implement Substitution Map
- ⏳ Task 5: Build Parser for IC Grammar (Minimal Subset First)
- ⏳ Task 6: Implement Book Loading and Global Linking
- ⏳ Task 7: Implement Reducer and WHNF Loop
- ⏳ Task 8: Implement Core Interaction Rules
- ⏳ Task 9: Extend Interaction Rules for Full Coverage
- ⏳ Task 10: Implement Collapse and Normalization
- ⏳ Task 11: Build CLI and Run Mode
- ⏳ Task 12: Create Test Harness and Validation Scripts
- ⏳ Task 13: Integration Testing and Benchmarking

## References

- HVM3 Repository: `./hvm3/`
- IC Grammar: `./hvm3/IC.md`
- Interaction Rules: `./hvm3/INTERS.md`
- Task Details: `./.taskmaster/tasks/tasks.json`
- PRD: `./.taskmaster/docs/prd-init.md`

## Development

All stub functions have TODOs marking implementation points. Each task in `.taskmaster/tasks/tasks.json` has detailed subtasks and test strategies.

Ready to rock! 🚀
