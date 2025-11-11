\ ForthVM - HVM3 Implementation in Forth
\ Main entry point and module loader

\ Load all modules in order
include errors.fs
include core.fs
include heap.fs
include subst.fs
include parse.fs
include interact.fs
include reduce.fs
include collapse.fs
include book.fs
include cli.fs

\ Version info
: .VERSION ( -- )
  ." ForthVM v0.1.0 - HVM3 in Forth" CR
;

\ Simple test to verify all modules loaded
: TEST-ALL ( -- )
  TEST-ERRORS
  TEST-CORE
  TEST-HEAP
  TEST-SUBST
  TEST-PARSE
  TEST-REDUCE
  TEST-INTERACT
  TEST-COLLAPSE
  TEST-BOOK
  TEST-CLI
  ." All modules loaded successfully!" CR
;
