\ Test script for Bug #21 and #22
include src/fvm.fs

: TEST-FILE ( c-addr u -- )
  CR ." Testing: " 2DUP TYPE CR
  SLURP-FILE ( c-addr2 u2 )
  LOAD-INPUT
  EVAL-MAIN
  ." Result: " .TERM CR
;

S" test_nested_ref.hvm" TEST-FILE
S" test_app.hvm" TEST-FILE
S" test_app_with_ref.hvm" TEST-FILE
S" test_era.hvm" TEST-FILE

bye
