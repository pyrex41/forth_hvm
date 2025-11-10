\ heap.fs - Heap management and garbage collection

\ Heap buffer (16k cells)
CREATE HEAP 16384 CELLS ALLOT

\ Heap pointer
VARIABLE HEAP-PTR
HEAP HEAP-PTR !

\ Allocate n cells from heap
: ALLOC ( n -- addr )
  CELLS HEAP-PTR @ SWAP
  OVER + HEAP-PTR !
  \ TODO: add circular wraparound
  \ TODO: check bounds
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

\ Test word
: TEST-HEAP ( -- )
  ." Heap module loaded" CR
  ." Heap size: " HEAP 16384 CELLS + HEAP - . ." bytes" CR
;
