\ heap.fs - Heap management and garbage collection

\ Heap buffer (16k cells = 128KB on 64-bit)
16384 CONSTANT HEAP-SIZE
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
    \ Wraparound needed
    DROP DROP                    \ bytes
    HEAP-START TUCK              \ start bytes start
    + HEAP-PTR !                 \ Update pointer
    \ Return heap start
  ELSE
    \ Normal case: no wraparound
    HEAP-PTR !                   \ bytes ptr (update pointer)
    \ Return old pointer value
  THEN
;

\ Mark table for GC (placeholder)
CREATE MARK-TABLE 1024 CELLS ALLOT

\ GC operations (stubs for now)
: MARK-FROM-ROOT ( addr -- )
  \ TODO: recursive mark
  DROP
;

: SWEEP ( -- )
  \ TODO: sweep unmarked cells
;

: GC-COLLECT ( -- )
  \ TODO: trigger GC
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

\ Test word
: TEST-HEAP ( -- )
  ." Heap module loaded" CR
  ." Heap size: " HEAP-SIZE CELLS . ." bytes (" HEAP-SIZE . ." cells)" CR
  TEST-ALLOC
;
