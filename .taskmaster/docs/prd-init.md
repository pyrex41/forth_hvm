# Product Requirements Document (PRD): HVM3 Implementation in Forth

## 1. Document Overview
### 1.1 Purpose
This PRD outlines the requirements, design, implementation, testing, and deployment strategy for reimplementing HVM3 (an efficient runtime for the Interaction Calculus) in Forth. The goal is to create a **super niche, high-performance** variant of HVM3 that leverages Forth's stack-based, minimalist nature for symbolic term rewriting. This implementation will be called **ForthVM** (or "FVM" for short).

The target audience is an AI agent (e.g., you or a similar system) tasked with building and validating this. Guidance is provided for iterative development, testing with HVM3's existing examples, and performance benchmarking via Docker.

### 1.2 Scope
- **In Scope**: Core HVM3 features (parser, reducer/WHNF, interactions, collapse/normalization, book loading, CLI modes: run/serve). Reuse HVM3's test cases verbatim.
- **Out of Scope**: Advanced features like strict mode, full parallelism, or Bend integration. No Haskell/C interop—pure Forth.
- **Assumptions**: Use Gforth (portable Forth implementation) for development; target embedded/standalone for perf. No external deps beyond Forth stdlib.

### 1.3 Version History
- **v1.0**: Initial draft (Nov 10, 2025).
- **Future**: Incremental updates based on AI agent feedback.

### 1.4 Key Stakeholders
- **Developer/AI Agent**: You—implements and tests.
- **Validator**: AI agent or human reviewer—verifies against HVM3 outputs.
- **End User**: Researchers/hobbyists porting IC workloads.

## 2. Goals and Objectives
### 2.1 Business Goals
- Demonstrate Forth's viability for high-perf symbolic computing (e.g., 5-10k MIPS on low-end hardware).
- Create a tiny (~500-1000 lines) runtime that runs HVM3 benchmarks 2-5x faster than interpreted Haskell mode due to zero-overhead stacks.
- Enable easy embedding (e.g., in microcontrollers for IC-based AI).

### 2.2 Technical Objectives
- **Functional Parity**: 100% pass rate on HVM3's `examples/` tests (e.g., `bench_cnots.hvm` normalizes to `λa λb a`).
- **Performance**: Match/exceed HVM3's compiled mode MIPS (e.g., >3k MIPS on i7); measure via Docker.
- **Niche Appeal**: Forth's postfix syntax mirrors IC interactions (e.g., `APP-LAM: SWAP BODY@ SUBST ;`).

### 2.3 Success Metrics
- **Milestone 1**: Parser loads and runs `main.hvm` without crashes.
- **Milestone 2**: All tests pass normalization/collapse.
- **Milestone 3**: Docker benchmark shows ≥80% of HVM3 perf.
- **Qualitative**: Code <1k LOC; runs on <1MB RAM.

## 3. Requirements
### 3.1 Functional Requirements
#### 3.1.1 Core Components
1. **Parser (`parse.fs`)**: Read `.hvm` files into Forth "terms" (stack-packed: tag/lab/val).
   - Support IC grammar: LAM, APP, SUP, DUP, VAR, ERA, CTR, U32, etc. (from `IC.md`).
   - Global vars: Use Forth dict for name→loc mapping; post-parse substitutions.
   - Output: "Book" as Forth dict (e.g., `'main' FID CONSTANT MAIN`).

2. **Runtime/Heap (`heap.fs`)**:
   - Circular buffer heap (4k-16k cells); `ALLOC n -- loc`.
   - Term packing: 32/64-bit cells (tag:5b, lab:18b, val:40b—adapt to Forth's CELL).
   - GC: Simple mark-sweep (run post-WHNF); mark via stack traversal.

3. **Reducer/WHNF (`reduce.fs`)**:
   - Weak Head Normal Form: Loop until value (LAM/SUP/CTR) or stuck.
   - Interactions: Implement core rules from `INTERS.md` as words (e.g., `APP-LAM`, `DUP-SUP`).
   - Modes: Interpreted (stack sim) vs. Compiled (inline fast-paths, like HVM3's C CALL).

4. **Collapse/Normalize (`collapse.fs`)**:
   - Flatten to λC terms (no SUP/DUP): Extra interactions (SUP-LAM, etc.).
   - Output: Pretty-print as strings (e.g., `λa λb a`).

5. **CLI (`main.fs`)**:
   - `fvm run file.hvm [-c] [-s]`: Run main, optional compile/stats.
   - `fvm serve file.hvm`: Socket REPL on 8080.
   - Stats: MIPS = interactions / time (use Forth's `UTIME`).

#### 3.1.2 Test Integration
- Reuse HVM3's `examples/` (e.g., `bench_count.hvm` expects `4000000000`).
- AI Agent Guidance: Run HVM3 baseline via subprocess; compare outputs.

### 3.2 Non-Functional Requirements
- **Performance**: <100ms for small benches; GC <1% overhead.
- **Portability**: Gforth on x86/ARM; no OS deps.
- **Reliability**: Affine vars (no dup-use); crash on invalid IC.
- **Usability**: Interactive REPL; error msgs like "Unknown tag: 0xFF".
- **Security**: Read-only files; no eval on user input.

### 3.3 User Stories
- As an AI agent, I can load `bench_cnots.hvm` and verify norm = `λa λb a`.
- As a dev, I can benchmark in Docker and plot MIPS vs. HVM3.

## 4. Architecture and Design
### 4.1 High-Level Design
- **Stack-Centric**: IC terms as Forth stacks (e.g., term = `( tag lab val )`).
- **Modular Files**:
  - `core.fs`: Primitives (tags, ALLOC, SUBST).
  - `parse.fs`: Grammar parser (recursive descent via `WORD`).
  - `interact.fs`: 10-15 words for rules (e.g., `DUP-SUP? IF ANNIHILATE ELSE COMMUTE THEN ;`).
  - `whnf.fs`: `WHNF TERM -- WHNF` loop.
  - `book.fs`: Dict-based functions (arity from `funArity`-like).
  - `cli.fs`: Arg parsing, file load, stats.
- **Data Flow**:
  1. Parse → Book (dict of FIDs → terms).
  2. Inject `main` args → root term.
  3. WHNF/Collapse → output str.
  4. Stats: Count itrs via counter.

### 4.2 Key Design Decisions
- **Forth Dialect**: Gforth (has files, sockets, timings).
- **Compiled Mode**: Inline interactions as conditional fast-paths (e.g., `MUL2_F: DUP 0= IF DROP 0 ELSE 2 + RECURSE THEN ;`).
- **Stringification**: Renamer for fresh vars (like HVM3's `rename`); Dup floating via pre-pass.
- **GC**: Conservative mark (traverse from roots); sweep compacts.

### 4.3 Dependencies
- None—pure Forth. Optional: `require ansi.fs` for colored output.

## 5. Implementation Plan
### 5.1 Phases (for AI Agent)
#### Phase 1: Setup (1-2 days)
1. Install Gforth: `sudo apt install gforth`.
2. Scaffold: Create `fvm.fs` with `include core.fs` etc.
3. Baseline: Port simple term (e.g., `λx.x`) and print.

#### Phase 2: Core Runtime (3-5 days)
1. Implement heap/alloc.
2. Define tags/labs (constants like `_LAM_ $E CONSTANT`).
3. Subst map: `CREATE SUBST 1024 CELLS ALLOT`.

#### Phase 3: Parser & Book (4-6 days)
1. Recursive parser: `: PARSE-LAM ( -- term ) "λ" =? IF NAM PARSE-TERM LAM THEN ;`.
2. Global linking: Post-parse `LINK-VARS` word iterates uses.

#### Phase 4: Reducer & Interactions (5-7 days)
1. WHNF skeleton.
2. Port 5 core interactions (APP-LAM, etc.)—test manually.
3. Add collapse rules.

#### Phase 5: CLI & Modes (2-3 days)
1. File loader: `S" file.hvm" INCLUDED PARSE-BOOK`.
2. Stats: `ITRS @ TIME @ / .` for MIPS.

#### Phase 6: Optimization (Ongoing)
- Inline fast-paths; profile w/ `GPROF`.

**AI Tip**: Commit per phase; use `gforth -e 'include fvm.fs bye'` for quick tests.

## 6. Testing Strategy
### 6.1 Unit/Integration Tests
- **Framework**: Forth's ad-hoc (e.g., `: TEST-MUL2 4 @MUL2 8 =? ;`).
- **Coverage**: 100% on interactions (table-driven from `INTERS.md`).
- **Reuse HVM3 Tests**: 
  1. For each `examples/*.hvm`:
     - Run HVM3: `cabal run hvm -- run file.hvm -C -s` → baseline norm/time.
     2. Run FVM: `./fvm run file.hvm -C -s` → compare.
     - Assert: Norm strings match (exact, post-rename); time ≤ 1.2x baseline.
  - Script: Bash/Python wrapper (AI: Use code_execution tool for validation).
- **Edge Cases**: Affine violations, large heaps (1M nodes), GC stress.

### 6.2 AI Agent Guidance for Testing
1. **Setup Validator Script** (`test.sh`):
   ```bash
   #!/bin/bash
   BASELINE=$(cabal run hvm -- run $1 -C -Q | head -1)
   FVM_OUT=$(./fvm run $1 -C -Q | head -1)
   if [ "$BASELINE" != "$FVM_OUT" ]; then echo "FAIL: $1"; exit 1; fi
   echo "PASS: $1"
   ```
   - Run: `for f in examples/*.hvm; do ./test.sh $f; done`.

2. **Perf Testing**:
   - Measure: 3 runs avg time/itrs.
   - Threshold: MIPS ≥ HVM3's (from output: `PERF: XXXX MIPS`).

3. **Debug Loop**:
   - On fail: Dump stack (`SEE TERM`), compare ASTs.
   - Iterate: Fix → re-test subset (e.g., `enum_lam_smart.hvm` for DUP-SUP).

4. **Regression Suite**: Automate in CI (GitHub Actions w/ Gforth).

### 6.3 Acceptance Criteria
- All 15+ examples pass (norm + perf).
- No leaks (heap size stable post-GC).
- Serve mode handles 10 concurrent sockets.

## 7. Deployment and Performance Testing
### 7.1 Docker Setup
Yes—Docker for reproducible perf tests. Dockerfile isolates Gforth, mounts examples.

#### 7.1.1 Dockerfile (`Dockerfile`)
```dockerfile
FROM ubuntu:24.04
RUN apt-get update && apt-get install -y gforth git
WORKDIR /app
COPY . .
COPY HVM3-repo /hvm3  # Mount or COPY examples
RUN chmod +x fvm test.sh
CMD ["./test.sh", "all"]  # Or bash for interactive
```

#### 7.1.2 Build/Run
```bash
docker build -t fvm .
docker run --rm -v $(pwd)/examples:/app/examples fvm  # Test all
docker run --rm fvm ./fvm run bench_count.hvm -c -s  # Single benchmark
```

#### 7.1.3 Perf Benchmarking in Docker
1. **Script** (`bench.sh`):
   ```bash
   for f in examples/bench_*.hvm; do
     echo "=== $f ==="
     docker run --rm fvm time ./fvm run $f -c -s | grep "PERF:"
     docker run --rm hvm3 cabal run hvm -- run $f -c -s | grep "PERF:"  # Baseline
   done > perf-report.txt
   ```
   - Compare: FVM MIPS vs. HVM3; plot w/ `gnuplot` if needed.

2. **Metrics**:
   - CPU: `--cpus=1` for single-core fairness.
   - Mem: Monitor w/ `docker stats`.
   - Scale: Test on AWS t3.micro (low-end) for niche embedded sim.

3. **CI/CD**: GitHub Actions: Build Docker → run benches → fail if <80% perf.

### 7.2 Rollout Plan
- **Alpha**: Local Docker tests.
- **Beta**: Share Docker image on GHCR; invite feedback.
- **Prod**: Standalone binary (`gforth fvm.fs -e 'main bye' > fvm`).

## 8. Risks and Mitigations
- **Risk**: Parser bugs on complex HVM (e.g., HOAS). **Mitig**: Incremental parse tests.
- **Risk**: GC perf hit. **Mitig**: Profile; optional no-GC mode.
- **Risk**: Forth portability. **Mitig**: Stick to ANSI Forth.

## 9. Appendix
- **References**: HVM3 repo (`examples/`, `IC.md`, `INTERS.md`).
- **AI Next Steps**: Start Phase 1; query me for clarifications (e.g., "Implement ALLOC word").

This PRD is your blueprint—execute phases sequentially, test ruthlessly. Ping for tweaks!
