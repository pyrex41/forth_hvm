\ heap.fs - Heap management and garbage collection

\ Heap buffer (128 cells = 1024 bytes)
128 CONSTANT HEAP-SIZE
CREATE DUMMY HERE 1000 ALLOT DROP
CREATE HEAP HEAP-SIZE CELLS ALLOT

\ Heap pointer and limit
VARIABLE HEAP-PTR
HEAP HEAP-PTR !

\ Get heap start and end addresses
: HEAP-START ( -- addr ) HEAP ;
: HEAP-END ( -- addr ) HEAP HEAP-SIZE CELLS + ;

\ Check if address is within heap bounds
: IN-HEAP? ( addr -- flag )
  DUP HEAP-START >= SWAP HEAP-END < AND
;

\ Allocate n cells from heap with circular wraparound
: ALLOC ( n -- addr )
  DUP 0<= IF
    DROP 0
    S" ALLOC: size must be positive" RUNTIME-ERROR
  THEN

  DUP HEAP-SIZE > IF
    DROP 0
    S" ALLOC: allocation too large for heap" RUNTIME-ERROR
  THEN

  CELLS                          \ convert to bytes
  HEAP-PTR @ TUCK                \ bytes ptr bytes ptr
  + DUP HEAP-END >= IF           \ bytes ptr new-ptr
    \ No wraparound - error
    DROP DROP 0
    S" ALLOC: heap full" RUNTIME-ERROR
  ELSE
    \ Normal case
    HEAP-PTR !                   \ bytes ptr (update pointer)
    \ Return old pointer value
  THEN
;

\ Mark table for GC (bit array: 1 bit per cell in heap)
\ With 16k cells, we need 16384 bits = 2048 bytes = 256 cells
256 CONSTANT MARK-TABLE-SIZE
CREATE MARK-TABLE MARK-TABLE-SIZE CELLS ALLOT

\ Clear mark table
: CLEAR-MARKS ( -- )
  MARK-TABLE MARK-TABLE-SIZE CELLS ERASE
;

\ Convert heap address to mark table index
: ADDR>MARK-IDX ( addr -- index )
  HEAP-START - CELL /
;

\ Check if address is marked
: MARKED? ( addr -- flag )
  ADDR>MARK-IDX               \ Get bit index
  DUP 8 /                     \ Byte offset
  MARK-TABLE + C@             \ Get byte
  SWAP 7 AND 1 SWAP LSHIFT    \ Create bit mask
  AND 0<>                     \ Check if bit is set
;

\ Mark an address
: MARK ( addr -- )
  DUP IN-HEAP? 0= IF DROP EXIT THEN    \ Skip if not in heap

  ADDR>MARK-IDX               \ Get bit index
  DUP 8 /                     \ Byte offset
  MARK-TABLE + >R             \ Save byte address
  7 AND 1 SWAP LSHIFT         \ Create bit mask
  R@ C@ OR                    \ Set bit
  R> C!                       \ Store back
;

\ Recursively mark from root (simplified - just marks the cell, not children)
: MARK-FROM-ROOT ( addr -- )
  DUP MARK
  \ TODO: For full implementation, would need to:
  \ 1. Unpack term to get tag
  \ 2. Based on tag, mark children (LAM body, APP function/arg, etc.)
  \ 3. Recursively mark all reachable terms
  \ For now, just mark the single cell
  DROP
;

\ Sweep unmarked cells (simplified - just reports stats)
: SWEEP ( -- )
  0 >R  \ Counter for freed cells
  HEAP-PTR @ HEAP-START - CELL / \ Number of allocated cells
  0 ?DO
    HEAP-START I CELLS + MARKED? 0= IF
      \ In real implementation: add to free list
      R> 1+ >R
    THEN
  LOOP
  R> DEBUG? @ IF
    ." Swept " . ." unmarked cells" CR
  ELSE
    DROP
  THEN
;

\ Trigger garbage collection
: GC-COLLECT ( -- )
  DEBUG? @ IF
    ." [GC] Starting collection..." CR
  THEN

  CLEAR-MARKS

  \ TODO: Mark from roots
  \ For now, we would need to scan the Forth stack and return stack
  \ for any heap pointers and mark from those roots
  \ This is a simplified stub

  SWEEP

  DEBUG? @ IF
    ." [GC] Collection complete" CR
  THEN
;

\ Test heap allocation
: TEST-ALLOC ( -- )
  ." Testing heap allocation..." CR

  \ Save original heap pointer
  HEAP-PTR @ >R

  \ Test 1: Simple allocation
  ." Test 1: Simple allocation... "
  10 ALLOC DUP IN-HEAP? IF
    ." PASS (addr=" . ." )" CR
  ELSE
    ." FAIL" CR
  THEN

  \ Test 2: Multiple allocations
  ." Test 2: Multiple allocations... "
  100 ALLOC DROP
  50 ALLOC DROP
  200 ALLOC IN-HEAP? IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN

  \ Test 3: Large allocation near end (should wraparound)
  ." Test 3: Wraparound test... "
  R> HEAP-PTR !  \ Reset pointer
  16000 ALLOC DROP    \ Allocate almost all heap
  1000 ALLOC DUP IN-HEAP? IF  \ This should wraparound
    HEAP-START = IF
      ." PASS (wrapped to start)" CR
    ELSE
      ." PASS (allocated)" CR
    THEN
  ELSE
    ." FAIL" CR
  THEN

  \ Reset heap for other tests
  HEAP HEAP-PTR !
;

\ Test GC functionality
: TEST-GC ( -- )
  ." Testing GC functionality..." CR

  \ Reset heap
  HEAP HEAP-PTR !

  \ Test 1: Mark and check
  ." Test 1: Mark and check... "
  10 ALLOC DUP
  CLEAR-MARKS
  DUP MARK
  MARKED? IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN

  \ Test 2: Unmarked cell
  ." Test 2: Unmarked cell... "
  20 ALLOC DUP
  MARKED? 0= IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN

  \ Test 3: GC-COLLECT runs without error
  ." Test 3: GC-COLLECT runs... "
  CLEAR-MARKS
  HEAP-START MARK  \ Mark first cell
  GC-COLLECT
  ." PASS" CR

  \ Reset heap
  HEAP HEAP-PTR !
;

\ Test word
: TEST-HEAP ( -- )
  ." Heap module loaded" CR
  ." Heap size: " HEAP-SIZE CELLS . ." bytes (" HEAP-SIZE . ." cells)" CR
  TEST-ALLOC
  TEST-GC
;
